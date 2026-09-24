using UnityEngine;
using UnityEngine.InputSystem;

public class Grappling : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public Transform gunTip;
    public LineRenderer lr;
    public Transform hookHead;

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
    }

    void FixedUpdate()
    {
        if (!grappling)
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
        if (grappling)
            UpdateGrappleVisuals();
    }

    void StartGrapple()
    {
        if (grapplingCdTimer > 0)
            return;

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
            ShowGrappleVisuals();
        }
    }

    void StopGrapple()
    {
        if (!grappling)
            return;

        grappling = false;
        grapplingCdTimer = grapplingCd;

        HideGrappleVisuals();
    }

    void OnDisable()
    {
        HideGrappleVisuals();
    }

    private void ShowGrappleVisuals()
    {
        if (lr != null)
        {
            lr.positionCount = 2;
            lr.enabled = true;
        }

        if (hookHead != null)
            hookHead.gameObject.SetActive(true);

        UpdateGrappleVisuals();
    }

    private void UpdateGrappleVisuals()
    {
        if (lr != null && gunTip != null)
        {
            lr.SetPosition(0, gunTip.position);
            lr.SetPosition(1, grapplePoint);
        }

        if (hookHead != null)
        {
            hookHead.position = grapplePoint + grappleNormal * hookSurfaceOffset;
            hookHead.rotation = Quaternion.FromToRotation(Vector3.forward, -grappleNormal)
                * Quaternion.Euler(hookHeadRotationOffset);
        }
    }

    private void HideGrappleVisuals()
    {
        if (lr != null)
            lr.enabled = false;

        if (hookHead != null)
            hookHead.gameObject.SetActive(false);
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
