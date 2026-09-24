using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 6f;         // Velocidad al caminar
    public float SprintSpeed = 12f;  // Velocidad máxima al correr
    public float aceleracion = 40f;  // Qué tan rápido arranca
    public float desaceleracion = 60f; // Qué tan rápido clava los frenos

    [Header("Salto")]
    public float jumpForce = 3.5f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    public LayerMask groundMask;

    [Header("Sistemas")]
    public Grappling grappling;
    public WallRunning wallRunning;

    private Rigidbody rb;
    private Animator anim; // <-- 1. Agregamos la referencia al Animator

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        // 2. Busca automáticamente el Animator en el modelo 3D hijo
        anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // ACA AGREGAMOS EL MENSAJE PARA VER SI DETECTA EL PISO
        Debug.Log("Tocando el piso: " + IsGrounded());

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (IsGrounded())
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
        }
    }

    void FixedUpdate()
    {
        // Si está usando el grappling, no modificar el movimiento
        if (grappling != null && grappling.IsGrappling())
            return;

        Vector2 input = Vector2.zero;

        // WASD
        if (Keyboard.current.wKey.isPressed) input.y += 1;
        if (Keyboard.current.sKey.isPressed) input.y -= 1;
        if (Keyboard.current.dKey.isPressed) input.x += 1;
        if (Keyboard.current.aKey.isPressed) input.x -= 1;

        // Evita que diagonal sea más rápido (QA TST-S2-004)
        input = Vector2.ClampMagnitude(input, 1f);

        // 1. Dirección hacia la que queremos ir
        Vector3 moveDirection = transform.right * input.x + transform.forward * input.y;

        // 2. Definir velocidad máxima (Caminar vs Correr)
        float targetSpeed = speed;
        if (Keyboard.current.leftShiftKey.isPressed)
        {
            targetSpeed = SprintSpeed;
        }

        // 3. Vector de velocidad objetivo final (Hacia dónde y a qué velocidad)
        Vector3 targetVelocity = moveDirection * targetSpeed;

        // 4. Leer la velocidad horizontal ACTUAL del Rigidbody
        Vector3 currentVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

        // 5. Elegir la marcha: ¿Estamos acelerando o soltamos las teclas para frenar?
        float factorDeCambio = (input.magnitude > 0.1f) ? aceleracion : desaceleracion;

        // 6. La magia: Acercar la velocidad actual a la objetivo suavemente
        Vector3 nuevaVelocidad = Vector3.MoveTowards(currentVelocity, targetVelocity, factorDeCambio * Time.fixedDeltaTime);

        // 7. Aplicar al Rigidbody (respetando la gravedad/salto en el eje Y)
        rb.linearVelocity = new Vector3(nuevaVelocidad.x, rb.linearVelocity.y, nuevaVelocidad.z);
    }


    bool IsGrounded()
    {
        return Physics.OverlapSphere(
            groundCheck.position,
            groundDistance,
            groundMask
        ).Length > 0;
    }
   
}