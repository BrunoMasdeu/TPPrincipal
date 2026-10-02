using UnityEngine;
using UnityEngine.InputSystem;

public class Grappling : MonoBehaviour
{
    private enum HookState
    {
        Idle,
        Flying,
        Attached,
        Returning
    }

    [Header("References")]
    public Camera cam;
    public LineRenderer lr;

    [Header("Animated Hook")]
    public GameObject hookProjectilePrefab;
    public Transform cableOrigin;
    public GameObject hookStored;

    [Header("Hook Movement")]
    [Min(0.1f)] public float hookProjectileSpeed = 15f;
    [Min(0.1f)] public float hookReturnSpeed = 20f;
    [Min(0.001f)] public float hookArrivalDistance = 0.03f;
    [Min(0f)] public float hookRotationSpeed = 15f;

    [Header("Hook Visual Alignment")]
    public Vector3 hookVisualPositionOffset;
    public Vector3 hookVisualRotationOffset;
    [Min(0.000001f)] public float hookRuntimeScale = 0.0001f;

    private GameObject activeHook;
    private Transform hookVisualOffset;
    private GameObject hookVisual;
    private Animator hookAnimator;
    private HookState hookState = HookState.Idle;
    private bool hasStarted;

    [Header("Visuals")]
    [SerializeField] private float hookSurfaceOffset = 0.04f;
    [SerializeField] private Vector3 hookHeadRotationOffset;

    [Header("Grappling")]
    public LayerMask whatIsGrappleable;
    public float maxGrappleDistance = 20f;
    public float grappleForce = 20f;
    public float stopDistance = 1f;

    [Header("Cooldown")]
    public float grapplingCd = 1f;
    private float grapplingCdTimer;

    private Rigidbody rb;
    private Vector3 grapplePoint;
    private Vector3 grappleNormal;
    private Vector3 velocityBeforeGrapple;
    private bool grappling;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Evita que el Rigidbody haga girar el personaje
        rb.constraints |= RigidbodyConstraints.FreezeRotation;

        HideGrappleVisuals();
        hasStarted = true;
        CreatePersistentHook();

        Debug.Log(
            $"[Grappling] Start en '{name}'. " +
            $"Prefab={(hookProjectilePrefab != null ? hookProjectilePrefab.name : "NULL")}, " +
            $"CableOrigin={(cableOrigin != null ? cableOrigin.name : "NULL")}, " +
            $"HookRuntime={(activeHook != null ? activeHook.name : "NULL")}."
        );
    }

    void OnEnable()
    {
        if (hasStarted)
            CreatePersistentHook();
    }

    void Update()
    {
        // Click derecho
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            StartGrapple();
        }

        // Soltar click derecho
        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            StopGrapple();
        }

        if (grapplingCdTimer > 0)
            grapplingCdTimer -= Time.deltaTime;

        ApplyHookVisualAlignment();

        if (hookState == HookState.Flying)
            UpdateHookFlight();
        else if (hookState == HookState.Returning)
            UpdateHookReturn();
    }

    void FixedUpdate()
    {
        if (!grappling || hookState != HookState.Attached)
            return;

        Vector3 toPoint = grapplePoint - transform.position;
        float distance = toPoint.magnitude;

        // Llegamos al final del gancho
        if (distance <= stopDistance)
        {
            // Recuperamos la velocidad que tenía antes de usar el gancho
            rb.linearVelocity = velocityBeforeGrapple;

            StopGrapple();
            return;
        }

        Vector3 direction = toPoint.normalized;

        // Movimiento hacia el punto del gancho
        rb.linearVelocity = direction * grappleForce;
    }

    void LateUpdate()
    {
        if (hookState != HookState.Idle)
            UpdateGrappleVisuals();
    }

    void StartGrapple()
    {
        if (grapplingCdTimer > 0 || hookState != HookState.Idle)
            return;

        if (activeHook == null)
            CreatePersistentHook();

        if (activeHook == null || cableOrigin == null)
        {
            Debug.LogWarning(
                $"[Grappling] Disparo cancelado en '{name}': falta configurar el gancho animado."
            );
            return;
        }

        Debug.Log(
            $"[Grappling] Disparo en '{name}'. Estado={hookState}, " +
            $"HookRuntime={(activeHook != null ? activeHook.name : "NULL")}, " +
            $"CableOrigin={(cableOrigin != null ? cableOrigin.name : "NULL")}."
        );

        RaycastHit hit;

        if (Physics.Raycast(
            cam.transform.position,
            cam.transform.forward,
            out hit,
            maxGrappleDistance,
            whatIsGrappleable))
        {
            grapplePoint = hit.point;
            grappleNormal = hit.normal;

            // Guardamos la velocidad ANTES de activar el gancho
            velocityBeforeGrapple = rb.linearVelocity;

            grappling = true;

            activeHook.transform.SetParent(null, true);
            hookState = HookState.Flying;
            SetLineVisible(true);
            UpdateGrappleVisuals();
        }
    }

    void StopGrapple()
    {
        if (!grappling)
            return;

        grappling = false;
        grapplingCdTimer = grapplingCd;

        if (activeHook != null && cableOrigin != null)
        {
            ResetHookAnimator();
            hookState = HookState.Returning;
            SetLineVisible(true);
            return;
        }

        hookState = HookState.Idle;
        HideGrappleVisuals();
    }

    void OnDisable()
    {
        HideGrappleVisuals();

        if (activeHook != null)
        {
            if (cableOrigin != null)
            {
                activeHook.transform.SetParent(cableOrigin, false);
                activeHook.transform.localScale = Vector3.one;
                activeHook.transform.SetPositionAndRotation(
                    GetHookDockPosition(),
                    GetHookDockRotation());
            }

            activeHook.SetActive(false);
        }

        if (hookStored != null)
            hookStored.SetActive(true);

        hookState = HookState.Idle;
        grappling = false;
    }

    private void UpdateGrappleVisuals()
    {
        if (lr != null && cableOrigin != null)
        {
            lr.SetPosition(0, cableOrigin.position);
            lr.SetPosition(1, activeHook != null ? activeHook.transform.position : grapplePoint);
        }
    }

    private void HideGrappleVisuals()
    {
        if (lr != null)
            lr.enabled = false;
    }

    private void CreatePersistentHook()
    {
        if (hookProjectilePrefab == null || cableOrigin == null)
        {
            Debug.LogWarning(
                $"[Grappling] No se pudo crear el gancho en '{name}': " +
                $"Prefab={(hookProjectilePrefab != null ? hookProjectilePrefab.name : "NULL")}, " +
                $"CableOrigin={(cableOrigin != null ? cableOrigin.name : "NULL")}."
            );

            if (hookStored != null)
                hookStored.SetActive(true);
            return;
        }

        if (activeHook == null)
        {
            activeHook = new GameObject("GrappleHook_Runtime");
            activeHook.transform.SetParent(cableOrigin, false);
            activeHook.transform.localPosition = Vector3.zero;
            activeHook.transform.localRotation = Quaternion.identity;
            activeHook.transform.localScale = Vector3.one;

            GameObject visualOffsetObject = new GameObject("GrappleHook_VisualOffset");
            hookVisualOffset = visualOffsetObject.transform;
            hookVisualOffset.SetParent(activeHook.transform, false);
            ApplyHookVisualAlignment();

            hookVisual = Instantiate(hookProjectilePrefab, hookVisualOffset);
            hookVisual.name = "GrappleHook_Visual";
            hookAnimator = hookVisual.GetComponentInChildren<Animator>();
        }

        activeHook.SetActive(true);
        activeHook.transform.SetParent(cableOrigin, false);
        activeHook.transform.localScale = Vector3.one;
        activeHook.transform.SetPositionAndRotation(
            GetHookDockPosition(),
            GetHookDockRotation());

        if (hookStored != null)
            hookStored.SetActive(false);

        ResetHookAnimator();
        hookState = HookState.Idle;

        Debug.Log(
            $"[Grappling] Gancho persistente listo en '{name}'. " +
            $"Escala mundial contenedor=({activeHook.transform.lossyScale.x:F6}, " +
            $"{activeHook.transform.lossyScale.y:F6}, {activeHook.transform.lossyScale.z:F6}), " +
            $"Escala offset={(hookVisualOffset != null ? hookVisualOffset.localScale.ToString() : "NULL")}, " +
            $"Escala visual={(hookVisual != null ? hookVisual.transform.localScale.ToString() : "NULL")}, " +
            $"Controller={(hookAnimator != null && hookAnimator.runtimeAnimatorController != null ? hookAnimator.runtimeAnimatorController.name : "NULL")}."
        );
    }

    private void UpdateHookFlight()
    {
        if (activeHook == null)
        {
            FinishHookCycle();
            return;
        }

        Vector3 target = grapplePoint + grappleNormal * hookSurfaceOffset;
        MoveHookTowards(target, hookProjectileSpeed);

        if (Vector3.Distance(activeHook.transform.position, target) <= hookArrivalDistance)
        {
            activeHook.transform.position = target;
            activeHook.transform.rotation = Quaternion.FromToRotation(Vector3.forward, -grappleNormal)
                * Quaternion.Euler(hookHeadRotationOffset);

            if (hookAnimator != null)
                hookAnimator.SetTrigger("Grip");

            hookState = HookState.Attached;
        }
    }

    private void UpdateHookReturn()
    {
        if (activeHook == null || cableOrigin == null)
        {
            FinishHookCycle();
            return;
        }

        Vector3 dockPosition = GetHookDockPosition();
        Quaternion dockRotation = GetHookDockRotation();

        MoveHookTowards(dockPosition, hookReturnSpeed);
        activeHook.transform.rotation = Quaternion.Slerp(
            activeHook.transform.rotation,
            dockRotation,
            hookRotationSpeed * Time.deltaTime);

        if (Vector3.Distance(activeHook.transform.position, dockPosition) <= hookArrivalDistance)
            DockHook();
    }

    private void MoveHookTowards(Vector3 target, float speed)
    {
        Vector3 direction = target - activeHook.transform.position;
        activeHook.transform.position = Vector3.MoveTowards(
            activeHook.transform.position,
            target,
            speed * Time.deltaTime);

        if (direction.sqrMagnitude > 0.000001f && hookRotationSpeed > 0f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up)
                * Quaternion.Euler(hookHeadRotationOffset);
            activeHook.transform.rotation = Quaternion.Slerp(
                activeHook.transform.rotation,
                targetRotation,
                hookRotationSpeed * Time.deltaTime);
        }
    }

    private void DockHook()
    {
        activeHook.transform.SetParent(cableOrigin, false);
        activeHook.transform.localScale = Vector3.one;
        activeHook.transform.SetPositionAndRotation(
            GetHookDockPosition(),
            GetHookDockRotation());
        ResetHookAnimator();
        FinishHookCycle();
    }

    private void ApplyHookVisualAlignment()
    {
        if (hookVisualOffset == null)
            return;

        hookVisualOffset.localPosition = hookVisualPositionOffset;
        hookVisualOffset.localRotation = Quaternion.Euler(hookVisualRotationOffset);
        hookVisualOffset.localScale = Vector3.one * hookRuntimeScale;
    }

    private Vector3 GetHookDockPosition()
    {
        if (hookStored != null)
            return hookStored.transform.position;

        return cableOrigin != null ? cableOrigin.position : transform.position;
    }

    private Quaternion GetHookDockRotation()
    {
        if (hookStored != null)
            return hookStored.transform.rotation;

        return cableOrigin != null ? cableOrigin.rotation : transform.rotation;
    }

    private void FinishHookCycle()
    {
        grappling = false;
        hookState = HookState.Idle;
        SetLineVisible(false);
    }

    private void ResetHookAnimator()
    {
        if (hookAnimator == null)
            return;

        hookAnimator.ResetTrigger("Grip");
        hookAnimator.Rebind();
        hookAnimator.Update(0f);
    }

    private void SetLineVisible(bool visible)
    {
        if (lr == null)
            return;

        if (visible)
            lr.positionCount = 2;

        lr.enabled = visible;
     }

    public void SetRopeSpeed(float launchSpeed, float returnSpeed)
    {
        hookProjectileSpeed = Mathf.Max(0.1f, launchSpeed);
        hookReturnSpeed = Mathf.Max(0.1f, returnSpeed);
    }

    public void SetRopeSpeed(float speed)
    {
        SetRopeSpeed(speed, speed);
    }

    public bool IsGrappling()
    {
        return grappling;
    }

    public Vector3 GetGrapplePoint()
    {
        return grapplePoint;
    }
}
