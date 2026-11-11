using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Encuadra el arma y los brazos únicamente en la cámara del jugador local.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)] // Después del Animator y del seguimiento de ojos de PlayerSkin.
[RequireComponent(typeof(Animator), typeof(NetworkObject), typeof(PlayerSkin))]
public sealed class FirstPersonView : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [Tooltip("Desplazamiento local desde la pose animada. X: derecha, Y: arriba, Z: adelante.")]
    [SerializeField] private Vector3 armOffset = new(0.16f, 0.11f, 0.50f);
    [Tooltip("Cuánto se acerca el agarre a la cámara, en unidades de escena. 0 usa la distancia original.")]
    [SerializeField, Range(0f, 0.8f)] private float viewmodelProximity = 0.25f;
    [Tooltip("Capas del escenario que pueden bloquear el arma y los brazos.")]
    [SerializeField] private LayerMask wallLayers = (1 << 0) | (1 << 3) | (1 << 7) | (1 << 8) | (1 << 13);

    private const float ArmClearance = 0.12f;
    private const float NearClipMargin = 0.08f;
    private const float MinimumNearClip = 0.01f;
    private const float MinimumViewmodelDepth = 0.04f;
    private const float WallMargin = 0.015f;

    private readonly List<Renderer> handRenderers = new();
    private readonly List<Renderer> armRenderers = new();
    private readonly List<(Renderer Renderer, bool WasHidden)> hiddenHandRenderers = new();
    private readonly Collider[] wallHits = new Collider[24];

    private NetworkObject player;
    private NetworkPlayerHealth health;
    private PlayerSkin playerSkin;
    private Animator animator;
    private PlayerSkin.Skin boundSkin;
    private FirstPersonArmMeshes meshes;
    private Transform leftShoulder;
    private Transform rightShoulder;
    private Transform rightHand;
    private Transform[] armBones;
    private Vector3 worldPivot;
    private Vector3 viewPivot;
    private Quaternion viewTilt;
    private float wallScale = 1f;
    private float viewNearClip;
    private float worldNearClip;
    private Pose leftShoulderPose;
    private Pose rightShoulderPose;
    private Vector3 leftShoulderScale;
    private Vector3 rightShoulderScale;
    private bool readyToRender;
    private bool rendering;
    private bool showArms;

    private void Awake()
    {
        player = GetComponent<NetworkObject>();
        health = GetComponent<NetworkPlayerHealth>();
        playerSkin = GetComponent<PlayerSkin>();
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        ReleaseView();
    }

    private void LateUpdate()
    {
        RestoreWorldView();
        readyToRender = false;
        if (!player.IsSpawned || !player.IsOwner)
        {
            if (boundSkin != null)
                ReleaseView();
            return;
        }

        if (playerCamera == null || !playerCamera.isActiveAndEnabled ||
            (health != null && health.LifeState != PlayerLifeState.Alive) ||
            !animator.isInitialized || !animator.isHuman)
            return;

        PlayerSkin.Skin skin = playerSkin.SkinActual;
        if (skin != boundSkin)
            BindView(skin);
        if (meshes == null || !meshes.HasArms)
            return;

        Transform view = playerCamera.transform;
        worldPivot = (leftShoulder.position + rightShoulder.position) * 0.5f;
        Quaternion neutralRotation = Quaternion.Inverse(transform.rotation);
        viewTilt = view.rotation * neutralRotation;
        Vector3 pivotInView = neutralRotation * (worldPivot - view.position) + armOffset;
        Matrix4x4 worldToView = Matrix4x4.TRS(pivotInView, neutralRotation, Vector3.one) *
            Matrix4x4.Translate(-worldPivot);

        var armBounds = new Bounds(worldToView.MultiplyPoint3x4(armBones[0].position), Vector3.zero);
        meshes.EncapsulateArms(worldToView, ref armBounds);
        Vector3 grip = worldToView.MultiplyPoint3x4(rightHand.position);
        var heldBounds = new Bounds(grip, Vector3.zero);
        handRenderers.Clear();
        CollectHandRenderers(leftShoulder, worldToView, ref heldBounds);
        CollectHandRenderers(rightShoulder, worldToView, ref heldBounds);

        float verticalSlope = Mathf.Tan(playerCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Vector3 framing = FrameViewmodel(worldToView, armBounds, heldBounds, grip, verticalSlope);
        armBounds.center += framing;
        heldBounds.center += framing;
        // Cualquier punto del brazo por debajo del borde inferior puede quedar detrás del plano cercano.
        float nearestArmDepth = Mathf.Max(armBounds.min.z, -armBounds.max.y / verticalSlope);
        float nearestDepth = Mathf.Min(heldBounds.min.z, nearestArmDepth);

        Bounds bounds = armBounds;
        bounds.Encapsulate(heldBounds);
        // Incluir la cámara evita saltar al otro lado de un muro delgado.
        bounds.Encapsulate(Vector3.zero);
        bounds.SetMinMax(new Vector3(bounds.min.x, bounds.min.y, 0f), bounds.max);
        float fittingScale = FitAgainstWalls(view, bounds);
        // La escala uniforme alrededor de la cámara conserva la posición en pantalla.
        // La retracción es inmediata, y la vuelta a la profundidad normal es gradual.
        wallScale = Mathf.Min(fittingScale, wallScale + 4f * Time.deltaTime);
        float nearestVisibleDepth = nearestDepth * wallScale - 0.003f;
        viewNearClip = Mathf.Min(playerCamera.nearClipPlane, Mathf.Max(MinimumNearClip, nearestVisibleDepth));
        showArms = nearestVisibleDepth >= viewNearClip;
        if (showArms)
        {
            viewPivot = view.position + view.rotation * ((pivotInView + framing) * wallScale);
            meshes.UpdatePose(worldPivot, viewPivot, viewTilt, wallScale);
        }
        readyToRender = true;
    }

    private void BindView(PlayerSkin.Skin skin)
    {
        ReleaseView();
        boundSkin = skin;
        if (skin?.modelo == null)
            return;

        Transform leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (leftArm == null || rightArm == null)
            return;

        leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
        if (leftShoulder == null)
            leftShoulder = leftArm;
        rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
        if (rightShoulder == null)
            rightShoulder = rightArm;
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (rightHand == null)
            rightHand = rightArm;
        armBones = new[]
        {
            leftArm,
            animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
            animator.GetBoneTransform(HumanBodyBones.LeftHand),
            rightArm,
            animator.GetBoneTransform(HumanBodyBones.RightLowerArm),
            rightHand
        };
        meshes = new FirstPersonArmMeshes(transform, skin.modelo.transform, leftArm, rightArm);
    }

    private Vector3 FrameViewmodel(Matrix4x4 worldToView, Bounds arms, Bounds held, Vector3 grip,
        float verticalSlope)
    {
        float nearestDepth = Mathf.Min(arms.min.z, held.min.z);
        float forward = Mathf.Max(0f, playerCamera.nearClipPlane + NearClipMargin - nearestDepth);
        Vector3 framing = Vector3.forward * forward;
        float down = meshes.GetSleeveLowering(Matrix4x4.Translate(framing) * worldToView, verticalSlope);
        foreach (Transform bone in armBones)
        {
            if (bone != null)
                down = Mathf.Max(down, worldToView.MultiplyPoint3x4(bone.position).y + ArmClearance);
        }
        framing.y -= down;

        float available = Mathf.Max(0f, held.min.z + forward - MinimumViewmodelDepth);
        float approach = Mathf.Min(viewmodelProximity, available);
        if (approach <= 0f)
            return framing;

        // Mueve el conjunto hacia la cámara a lo largo del rayo del agarre.
        // La mano queda en el mismo punto de la pantalla y el modelo crece por perspectiva.
        Vector3 framedGrip = grip + framing;
        framing -= framedGrip * (approach / framedGrip.z);

        float extraDown = meshes.GetSleeveLowering(Matrix4x4.Translate(framing) * worldToView, verticalSlope);
        if (arms.min.z + framing.z < MinimumViewmodelDepth)
        {
            // Si la manga se acerca al plano cercano, su parte más próxima queda fuera del cuadro.
            extraDown = Mathf.Max(extraDown,
                arms.max.y + framing.y + MinimumViewmodelDepth * verticalSlope + 0.005f);
        }
        framing.y -= extraDown;
        return framing;
    }

    private void CollectHandRenderers(Transform shoulder, Matrix4x4 worldToView, ref Bounds bounds)
    {
        shoulder.GetComponentsInChildren(false, armRenderers);
        foreach (Renderer renderer in armRenderers)
        {
            if (!renderer.enabled || renderer.forceRenderingOff)
                continue;
            handRenderers.Add(renderer);
            if (renderer is MeshRenderer or SkinnedMeshRenderer)
                EncapsulateBounds(renderer.localBounds, worldToView * renderer.localToWorldMatrix, ref bounds);
        }
    }

    internal static void EncapsulateBounds(Bounds source, Matrix4x4 transform, ref Bounds target)
    {
        Vector3 center = transform.MultiplyPoint3x4(source.center);
        Vector3 x = transform.MultiplyVector(Vector3.right * source.extents.x);
        Vector3 y = transform.MultiplyVector(Vector3.up * source.extents.y);
        Vector3 z = transform.MultiplyVector(Vector3.forward * source.extents.z);
        Vector3 extents = new(
            Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
            Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
            Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
        target.Encapsulate(center - extents);
        target.Encapsulate(center + extents);
    }

    private float FitAgainstWalls(Transform view, Bounds bounds)
    {
        if (!TouchesWall(view, bounds, 1f))
            return 1f;

        float clear = 0f;
        float blocked = 1f;
        for (int i = 0; i < 6; i++)
        {
            float scale = (clear + blocked) * 0.5f;
            if (TouchesWall(view, bounds, scale))
                blocked = scale;
            else
                clear = scale;
        }
        return clear;
    }

    private bool TouchesWall(Transform view, Bounds bounds, float scale)
    {
        Vector3 center = view.position + view.rotation * (bounds.center * scale);
        Vector3 halfSize = bounds.extents * scale + Vector3.one * WallMargin;
        int count = Physics.OverlapBoxNonAlloc(center, halfSize, wallHits, view.rotation,
            wallLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = wallHits[i];
            if (hit.transform.IsChildOf(transform))
                continue;
            if (hit.attachedRigidbody != null && hit.attachedRigidbody.TryGetComponent<PlayerNetworkSetup>(out _))
                continue;
            return true;
        }
        return count == wallHits.Length;
    }

    private void PoseShoulder(Transform shoulder)
    {
        Vector3 position = viewPivot + viewTilt * (shoulder.position - worldPivot) * wallScale;
        shoulder.SetPositionAndRotation(position, viewTilt * shoulder.rotation);
        shoulder.localScale *= wallScale;
    }

    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != playerCamera || !readyToRender || rendering || !player.IsOwner)
            return;

        // Las armas y los accesorios de mano siguen el rig solo durante este render.
        leftShoulderPose = new Pose(leftShoulder.localPosition, leftShoulder.localRotation);
        rightShoulderPose = new Pose(rightShoulder.localPosition, rightShoulder.localRotation);
        leftShoulderScale = leftShoulder.localScale;
        rightShoulderScale = rightShoulder.localScale;
        worldNearClip = camera.nearClipPlane;
        rendering = true;
        if (showArms)
        {
            // Usa el mismo búfer de profundidad que el mapa, también con el arma retraída.
            camera.nearClipPlane = viewNearClip;
            PoseShoulder(leftShoulder);
            PoseShoulder(rightShoulder);
        }
        else
        {
            // Si el espacio libre no alcanza para el plano cercano, ocultamos la vista local completa.
            foreach (Renderer renderer in handRenderers)
            {
                if (renderer == null)
                    continue;
                hiddenHandRenderers.Add((renderer, renderer.forceRenderingOff));
                renderer.forceRenderingOff = true;
            }
        }
        meshes.Show(showArms);
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == playerCamera)
            RestoreWorldView();
    }

    private void RestoreWorldView()
    {
        if (!rendering)
            return;

        rendering = false;
        if (playerCamera != null)
            playerCamera.nearClipPlane = worldNearClip;
        meshes.Restore();
        foreach (var hidden in hiddenHandRenderers)
            if (hidden.Renderer != null)
                hidden.Renderer.forceRenderingOff = hidden.WasHidden;
        hiddenHandRenderers.Clear();
        if (leftShoulder != null)
        {
            leftShoulder.SetLocalPositionAndRotation(leftShoulderPose.position, leftShoulderPose.rotation);
            leftShoulder.localScale = leftShoulderScale;
        }
        if (rightShoulder != null)
        {
            rightShoulder.SetLocalPositionAndRotation(rightShoulderPose.position, rightShoulderPose.rotation);
            rightShoulder.localScale = rightShoulderScale;
        }
    }

    private void ReleaseView()
    {
        RestoreWorldView();
        readyToRender = false;
        meshes?.Release();
        meshes = null;
        boundSkin = null;
        wallScale = 1f;
    }
}
