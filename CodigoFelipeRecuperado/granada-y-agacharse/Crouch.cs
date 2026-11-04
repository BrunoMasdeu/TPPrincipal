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
    public Key crouchKey = Key.LeftCtrl;

    private float originalHeight;
    private Vector3 originalCenter;
    private Vector3 originalCameraPosition;
    private bool crouching;

    private void Awake()
    {
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

        if (Keyboard.current[crouchKey].isPressed)
        {
            crouching = true;
        }
        else if (CanStandUp())
        {
            crouching = false;
        }

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

    private bool CanStandUp()
    {
        float radius = capsule.radius;
        float height = originalHeight;

        Vector3 center = transform.TransformPoint(originalCenter);
        Vector3 bottom = center + Vector3.up * radius;
        Vector3 top = center + Vector3.up * (height - radius);

        Collider[] hits = Physics.OverlapCapsule(
            bottom,
            top,
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
