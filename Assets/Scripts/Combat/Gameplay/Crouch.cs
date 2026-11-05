using UnityEngine;
using UnityEngine.InputSystem;

public class Crouch : MonoBehaviour
{
    [Header("Referencias")]
    public CapsuleCollider capsule;
    public Transform playerCamera;

    [Header("Agacharse")]
    public float crouchHeight = 1.2f;
    public float cameraOffset = 0.5f;
    public float transitionSpeed = 10f;
    // [TECLAS] La tecla ya no es un campo (crouchKey): sale de Controles.

    private float originalHeight;
    private Vector3 originalCenter;
    private Vector3 originalCameraPosition;
    private bool crouching;

    // [MULTIJUGADOR] En red, el servidor confirma si el jugador está agachado
    // y todas las PCs achican la cápsula con este mismo código.
    private NetworkPlayerCrouch network;

    private void Awake()
    {
        // [MULTIJUGADOR]
        network = GetComponent<NetworkPlayerCrouch>();

        if (capsule == null)
            capsule = GetComponent<CapsuleCollider>();

        if (capsule != null)
        {
            originalHeight = capsule.height;
            originalCenter = capsule.center;
        }

        if (playerCamera != null)
            originalCameraPosition = playerCamera.localPosition;
    }

    private void Update()
    {
        if (Keyboard.current == null || capsule == null)
            return;

        // [MULTIJUGADOR] Solo el dueño lee su teclado y pide el cambio. Las
        // copias de los demás jugadores no leen el teclado de esta PC.
        if (network == null || network.IsOwner)
        {
            if (Keyboard.current[Controles.Tecla(Accion.Agacharse)].isPressed)
            {
                crouching = true;
            }
            else if (CanStandUp())
            {
                crouching = false;
            }

            if (network != null)
                network.RequestCrouch(crouching);
        }

        // [MULTIJUGADOR] Todas las PCs usan lo que confirmó el servidor.
        if (network != null)
            crouching = network.IsCrouched;

        float targetHeight = crouching
            ? crouchHeight
            : originalHeight;

        capsule.height = Mathf.Lerp(
            capsule.height,
            targetHeight,
            transitionSpeed * Time.deltaTime
        );

        float heightDifference = originalHeight - capsule.height;

        capsule.center = new Vector3(
            originalCenter.x,
            originalCenter.y - heightDifference / 2f,
            originalCenter.z
        );

        if (playerCamera != null)
        {
            Vector3 targetPosition = originalCameraPosition;

            if (crouching)
                targetPosition.y -= cameraOffset;

            playerCamera.localPosition = Vector3.Lerp(
                playerCamera.localPosition,
                targetPosition,
                transitionSpeed * Time.deltaTime
            );
        }
    }

    // [MULTIJUGADOR] public: el servidor también lo usa para validar.
    public bool CanStandUp()
    {
        // [COMO HOY] Antes se probaba una cápsula que iba desde el centro hasta
        // bastante más arriba de la cabeza, y a veces no dejaba pararse. Ahora
        // solo se mira el lugar que ocuparía la cabeza al pararse.
        float radius = capsule.radius;
        float height = originalHeight;

        Vector3 center = transform.TransformPoint(originalCenter);
        Vector3 head = center + Vector3.up * (height / 2f - radius);

        Collider[] hits = Physics.OverlapSphere(
            head,
            radius * 0.95f,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            if (hit == capsule || hit.transform.IsChildOf(transform))
                continue;

            return false;
        }

        return true;
    }
}
