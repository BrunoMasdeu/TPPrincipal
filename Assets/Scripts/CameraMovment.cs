using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraMovement : MonoBehaviour
{
    public const string ClaveSensibilidad = "SensibilidadMouse";

    [Header("Vista")]
    [Range(0.01f, 0.3f)]
    public float nearClipPlane = 0.05f;

    [Header("Referencias")]
    public Transform player;

    private float xRotation = 0f;
    private Camera playerCamera;
    private Move playerMove;

    void Awake()
    {
        playerCamera = GetComponent<Camera>();
        playerMove = GetComponentInParent<Move>();

        ApplyNearClipPlane();

        if (playerMove == null)
            Debug.LogError("No se encontró el componente Move en el jugador.");
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
        if (MatchDebugConsole.IsInputCaptured)
            return;

        if (NetworkPlayerShop.IsInputCaptured)
            return;

        if (Cursor.lockState != CursorLockMode.Locked ||
            Mouse.current == null ||
            playerMove == null ||
            player == null)
            return;

        Vector2 mouse = Mouse.current.delta.ReadValue();

        float sensibilidad = PlayerPrefs.GetFloat(ClaveSensibilidad, playerMove.sensibilidad);
        float mouseX = mouse.x * sensibilidad;
        float mouseY = mouse.y * sensibilidad;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        player.Rotate(Vector3.up * mouseX);
    }
}