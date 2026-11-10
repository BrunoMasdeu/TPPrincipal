using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Apuntado vertical sobre la pose de armas y agachado, replicado por el dueño.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(Animator))]
public sealed class PlayerAimAnimation : NetworkBehaviour
{
    [SerializeField] private Transform view;
    [Tooltip("Limita la pose del torso y el ángulo replicado. Los brazos en primera persona siguen todo el giro de la cámara.")]
    [SerializeField, Range(0f, 90f)] private float maxPitch = 85f;
    [SerializeField, Range(0f, 0.5f)] private float torsoContribution = 0.25f;
    [SerializeField, Min(0f)] private float remoteSmoothTime = 0.06f;
    [Tooltip("Posición del agarre respecto de la pose animada, en los ejes de la cámara local.")]
    [SerializeField] private Vector3 firstPersonArmOffset = new(0f, 0.16f, 0.50f);

    // Décimas de grado. El giro horizontal ya viaja en el NetworkTransform.
    private readonly NetworkVariable<short> aimPitch = new(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private const float SendInterval = 0.05f;
    private const float ArmClearance = 0.12f;
    private Animator animator;
    private Camera firstPersonCamera;
    private NetworkPlayerHealth health;
    private PlayerCrouchAnimation crouch;
    private Transform chest, leftShoulder, rightShoulder, neck, head;
    private Transform[] armBones;
    private float presentedPitch;
    private float pitchVelocity;
    private float nextSendTime;
    private Vector3 leftShoulderLocalPosition;
    private Vector3 rightShoulderLocalPosition;
    private bool shouldersAdjusted;
    private Vector3 neutralArmPivot;
    private float animatedArmPitch;
    private float crouchLean;
    private FirstPersonArmMeshes firstPersonMeshes;
    private bool renderingFirstPerson;
    private Pose leftWorldShoulder;
    private Pose rightWorldShoulder;
    private Vector3 worldArmPivot;
    private Vector3 localArmPivot;
    private Quaternion localArmRotation;

    public float PresentedPitch => presentedPitch;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        health = GetComponent<NetworkPlayerHealth>();
        crouch = GetComponent<PlayerCrouchAnimation>();
        if (view == null)
        {
            CameraMovement cameraMovement = GetComponentInChildren<CameraMovement>(true);
            if (cameraMovement != null)
                view = cameraMovement.transform;
        }
        firstPersonCamera = view != null ? view.GetComponent<Camera>() : null;
        if (animator.avatar == null || !animator.isHuman)
            return;
        chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Spine);
        leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
        rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
        if (leftShoulder == null) leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        if (rightShoulder == null) rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        head = animator.GetBoneTransform(HumanBodyBones.Head);
        armBones = new[]
        {
            animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
            animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
            animator.GetBoneTransform(HumanBodyBones.LeftHand),
            animator.GetBoneTransform(HumanBodyBones.RightUpperArm),
            animator.GetBoneTransform(HumanBodyBones.RightLowerArm),
            animator.GetBoneTransform(HumanBodyBones.RightHand)
        };
    }

    public override void OnNetworkSpawn()
    {
        presentedPitch = ReadTargetPitch();
        pitchVelocity = 0f;
        nextSendTime = 0f;
    }

    public override void OnNetworkDespawn()
    {
        RestoreShoulderOffsets();
        presentedPitch = 0f;
        pitchVelocity = 0f;
        nextSendTime = 0f;
    }

    // Los clips Humanoid no necesariamente reescriben la posición local de las clavículas.
    private void Update() => RestoreShoulderOffsets();

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        RestoreShoulderOffsets();
    }

    public override void OnDestroy()
    {
        RestoreFirstPersonView();
        firstPersonMeshes?.Release();
        base.OnDestroy();
    }

    private void RestoreShoulderOffsets()
    {
        RestoreFirstPersonView();
        if (!shouldersAdjusted)
            return;
        if (leftShoulder != null) leftShoulder.localPosition = leftShoulderLocalPosition;
        if (rightShoulder != null) rightShoulder.localPosition = rightShoulderLocalPosition;
        shouldersAdjusted = false;
    }

    private float ReadTargetPitch()
    {
        if (IsSpawned && !IsOwner)
            return Mathf.Clamp(aimPitch.Value * 0.1f, -maxPitch, maxPitch);
        if (view == null)
            return 0f;
        Vector3 direction = transform.InverseTransformDirection(view.forward);
        float angle = -Mathf.Atan2(direction.y,
            new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
        return Mathf.Clamp(angle, -maxPitch, maxPitch);
    }

    private void LateUpdate()
    {
        bool alive = health == null || health.LifeState == PlayerLifeState.Alive;
        float target = alive ? ReadTargetPitch() : 0f;
        if (IsSpawned && IsOwner && Time.unscaledTime >= nextSendTime)
        {
            short encoded = (short)Mathf.RoundToInt(target * 10f);
            if (aimPitch.Value != encoded)
                aimPitch.Value = encoded;
            nextSendTime = Time.unscaledTime + SendInterval;
        }

        if (!alive)
        {
            presentedPitch = 0f;
            pitchVelocity = 0f;
            return;
        }
        presentedPitch = IsSpawned && !IsOwner && remoteSmoothTime > 0f
            ? Mathf.SmoothDampAngle(presentedPitch, target, ref pitchVelocity,
                remoteSmoothTime, Mathf.Infinity, Time.deltaTime)
            : target;
        if (chest == null || leftShoulder == null || rightShoulder == null || head == null ||
            !animator.enabled || !animator.isInitialized)
            return;

        // Compensa la inclinación añadida por el agachado para mantener la mira.
        crouchLean = crouch != null && crouch.isActiveAndEnabled ? crouch.AppliedTorsoLean : 0f;
        animatedArmPitch = presentedPitch - crouchLean;
        float pitch = animatedArmPitch;
        Vector3 axis = transform.right;
        neutralArmPivot = (leftShoulder.position + rightShoulder.position) * 0.5f;
        leftShoulderLocalPosition = leftShoulder.localPosition;
        rightShoulderLocalPosition = rightShoulder.localPosition;
        shouldersAdjusted = true;
        chest.rotation = Quaternion.AngleAxis(pitch * torsoContribution, axis) * chest.rotation;
        Quaternion armsRotation = Quaternion.AngleAxis(pitch * (1f - torsoContribution), axis);
        Vector3 pivot = (leftShoulder.position + rightShoulder.position) * 0.5f;
        // Un pivote común conserva la distancia entre las manos y el agarre del rifle.
        RotateShoulder(leftShoulder, pivot, pivot, armsRotation);
        RotateShoulder(rightShoulder, pivot, pivot, armsRotation);

        float headPitch = pitch * (1f - torsoContribution);
        if (neck != null)
        {
            neck.rotation = Quaternion.AngleAxis(headPitch * 0.35f, axis) * neck.rotation;
            headPitch *= 0.65f;
        }
        head.rotation = Quaternion.AngleAxis(headPitch, axis) * head.rotation;
        if ((!IsSpawned || IsOwner) && firstPersonCamera != null && firstPersonCamera.isActiveAndEnabled)
            PrepareFirstPersonArms();
    }

    private void PrepareFirstPersonArms()
    {
        firstPersonMeshes ??= new FirstPersonArmMeshes(transform, armBones[0], armBones[3]);
        if (!firstPersonMeshes.HasArms)
            return;
        Quaternion viewRotation = view.rotation * Quaternion.Inverse(transform.rotation);
        localArmRotation = viewRotation * Quaternion.AngleAxis(-crouchLean - animatedArmPitch, transform.right);
        worldArmPivot = (leftShoulder.position + rightShoulder.position) * 0.5f;
        localArmPivot = view.position + viewRotation * (neutralArmPivot - view.position) +
            view.rotation * firstPersonArmOffset;
        localArmPivot -= ArmClearanceOffset();
        // La malla de vista se prepara antes de que Unity evalúe el skinning de este frame.
        firstPersonMeshes.UpdatePose(worldArmPivot, localArmPivot, localArmRotation);
    }

    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != firstPersonCamera || (IsSpawned && !IsOwner) || !shouldersAdjusted ||
            renderingFirstPerson || firstPersonMeshes == null || !firstPersonMeshes.HasArms)
            return;

        // Las armas son mallas rígidas. Sus objetos se mueven solo para el render local.
        leftWorldShoulder = new Pose(leftShoulder.position, leftShoulder.rotation);
        rightWorldShoulder = new Pose(rightShoulder.position, rightShoulder.rotation);
        renderingFirstPerson = true;
        RotateShoulder(leftShoulder, worldArmPivot, localArmPivot, localArmRotation);
        RotateShoulder(rightShoulder, worldArmPivot, localArmPivot, localArmRotation);
        firstPersonMeshes.Show();
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == firstPersonCamera)
            RestoreFirstPersonView();
    }

    private void RestoreFirstPersonView()
    {
        if (!renderingFirstPerson)
            return;
        renderingFirstPerson = false;
        firstPersonMeshes.Restore();
        if (leftShoulder != null)
            leftShoulder.SetPositionAndRotation(leftWorldShoulder.position, leftWorldShoulder.rotation);
        if (rightShoulder != null)
            rightShoulder.SetPositionAndRotation(rightWorldShoulder.position, rightWorldShoulder.rotation);
    }

    private Vector3 ArmClearanceOffset()
    {
        // Deja espacio para las mangas incluso cuando un disparo o recarga levanta los brazos.
        Vector3 up = view.up;
        float excess = 0f;
        foreach (Transform bone in armBones)
        {
            if (bone != null)
            {
                Vector3 target = localArmPivot + localArmRotation * (bone.position - worldArmPivot);
                excess = Mathf.Max(excess, Vector3.Dot(target - view.position, up) + ArmClearance);
            }
        }
        return up * excess;
    }

    private static void RotateShoulder(Transform shoulder, Vector3 pivot, Vector3 targetPivot, Quaternion rotation)
    {
        shoulder.SetPositionAndRotation(targetPivot + rotation * (shoulder.position - pivot), rotation * shoulder.rotation);
    }
}
