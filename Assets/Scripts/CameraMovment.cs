using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraMovement : MonoBehaviour
{
    [Header("Vista")]
    public float mouseSensitivity = 0.2f;
    [Range(0.01f, 0.3f)]
    public float nearClipPlane = 0.05f;

    [Header("Referencias")]
    public Transform player;

    private float xRotation = 0f;
    private Camera playerCamera;

    void Awake()
    {
        playerCamera = GetComponent<Camera>();

        ApplyNearClipPlane();
    }

    void OnValidate()
    {
        nearClipPlane = Mathf.Clamp(nearClipPlane, 0.01f, 0.3f);
        playerCamera = GetComponent<Camera>();

        if (playerCamera != null)
            ApplyNearClipPlane();
    }

    private void ApplyNearClipPlane()
    {
        if (playerCamera == null)
            return;

        // Un plano cercano pequeno evita que sus esquinas atraviesen una pared
        // cuando la camara se aproxima o la observa desde un angulo oblicuo.
        playerCamera.nearClipPlane = Mathf.Clamp(
            nearClipPlane,
            0.01f,
            0.3f
        );
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Vector2 mouse = Mouse.current.delta.ReadValue();

        float mouseX = mouse.x * mouseSensitivity;
        float mouseY = mouse.y * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        player.Rotate(Vector3.up * mouseX);
    }
}
