using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Move : MonoBehaviour
{
    [Header("Animaciones")]
    [Tooltip("Asigna aquí el Animator (si está vacío, se buscará automáticamente en hijos)")]
    [SerializeField] private Animator anim;

    [Header("Movimiento")]
    public float speed = 6f;
    public float runSpeed = 10f;
    public float acceleration = 20f;

    [Header("Salto")]
    public float jumpForce = 7f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.25f;
    public LayerMask groundMask;

    [Header("Referencias")]
    public Transform orientation;

    [HideInInspector]
    public bool wallrunning;

    private Rigidbody rb;
    private Vector2 input;
    private bool jumpPressed;
    private bool running;
    private bool keyboardInputEnabled = true;
    private float speedMultiplier = 1f;

    private readonly List<Vector3> wallContactNormals = new List<Vector3>();

    private const float MaxWallNormalUpDot = 0.5f;
    private const float SameWallNormalDot = 0.99f;

    // Hashes de parámetros de Animator (más eficiente que usar strings en cada frame)
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
    private static readonly int JumpParam = Animator.StringToHash("Jump");
    private static readonly int RunningParam = Animator.StringToHash("Running");
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        if (anim == null)
            anim = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        bool grounded = IsGrounded();
        if (anim != null)
        {
            anim.SetBool(IsGroundedParam, grounded);
            anim.SetBool(RunningParam, running);
        }

        if (!keyboardInputEnabled)
            return;

        if (Keyboard.current == null)
        {
            ClearMovementInput();
            return;
        }

        // Movimiento
        Vector2 movementInput = Vector2.zero;

        if (Keyboard.current.wKey.isPressed)
            movementInput.y += 1;

        if (Keyboard.current.sKey.isPressed)
            movementInput.y -= 1;

        if (Keyboard.current.aKey.isPressed)
            movementInput.x -= 1;

        if (Keyboard.current.dKey.isPressed)
            movementInput.x += 1;

        // Correr con Shift SOLO en el suelo
        SetMovementInput(
            movementInput,
            Keyboard.current.leftShiftKey.isPressed
        );

        // Salto
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            RequestJump();
    }

    public void SetKeyboardInputEnabled(bool enabled)
    {
        keyboardInputEnabled = enabled;

        if (!enabled)
            ClearMovementInput();
    }

    public void SetMovementInput(Vector2 movementInput, bool wantsToRun = false)
    {
        input = Vector2.ClampMagnitude(movementInput, 1f);

        if (IsGrounded())
        {
            running = wantsToRun;
            return;
        }

        // Mantener la carrera si el salto comenzó corriendo, pero no permitir
        // que Shift active la carrera por primera vez mientras está en el aire.
        if (!wantsToRun)
            running = false;
    }

    public void ClearMovementInput()
    {
        input = Vector2.zero;
        running = false;
    }

    public void RequestJump()
    {
        jumpPressed = true;
    }

    private void FixedUpdate()
    {
        if (!wallrunning)
        {
            MovePlayer();
        }

        if (jumpPressed)
        {
            Jump();
            jumpPressed = false;
        }

        if (anim != null)
        {
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            anim.SetFloat(SpeedParam, horizontalVelocity.magnitude);
        }
        // Los callbacks de colision vuelven a llenar la lista despues del
        // siguiente paso de fisica. De esta forma nunca usamos contactos viejos.
        wallContactNormals.Clear();
    }

    private void MovePlayer()
    {
        Vector3 moveDirection =
            orientation.right * input.x +
            orientation.forward * input.y;

        moveDirection.y = 0f;

        // Velocidad normal o de correr
        float currentSpeed = (running ? runSpeed : speed) * speedMultiplier;

        Vector3 targetVelocity = ProjectVelocityAlongWalls(
            moveDirection * currentSpeed,
            wallContactNormals
        );

        Vector3 currentVelocity = rb.linearVelocity;

        Vector3 horizontalVelocity =
            new Vector3(currentVelocity.x, 0f, currentVelocity.z);

        Vector3 velocityChange =
            targetVelocity - horizontalVelocity;

        velocityChange = Vector3.ClampMagnitude(
            velocityChange,
            acceleration * Time.fixedDeltaTime
        );

        rb.AddForce(
            velocityChange,
            ForceMode.VelocityChange
        );
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Clamp(multiplier, 0.1f, 5f);
    }

    public float GetSpeedMultiplier()
    {
        return speedMultiplier;
    }

    public static Vector3 ProjectVelocityAlongWalls(
        Vector3 velocity,
        IList<Vector3> wallNormals
    )
    {
        Vector3 projectedVelocity = velocity;

        // Repetir la proyeccion permite resolver correctamente el contacto con
        // mas de una pared (por ejemplo, una esquina) sin atravesar ninguna.
        for (int pass = 0; pass < wallNormals.Count; pass++)
        {
            bool velocityChanged = false;

            for (int i = 0; i < wallNormals.Count; i++)
            {
                Vector3 horizontalNormal = Vector3.ProjectOnPlane(
                    wallNormals[i],
                    Vector3.up
                );

                if (horizontalNormal.sqrMagnitude <= Mathf.Epsilon)
                    continue;

                horizontalNormal.Normalize();

                float speedIntoWall = Vector3.Dot(
                    projectedVelocity,
                    horizontalNormal
                );

                if (speedIntoWall >= 0f)
                    continue;

                projectedVelocity -= horizontalNormal * speedIntoWall;
                velocityChanged = true;
            }

            if (!velocityChanged)
                break;
        }

        return projectedVelocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        RegisterWallContacts(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        RegisterWallContacts(collision);
    }

    private void RegisterWallContacts(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal.normalized;

            // Ignorar suelo, pendientes transitables y techos. Solo las
            // superficies suficientemente verticales deben modificar el avance.
            if (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > MaxWallNormalUpDot)
                continue;

            Vector3 horizontalNormal = Vector3.ProjectOnPlane(
                normal,
                Vector3.up
            ).normalized;

            bool alreadyRegistered = false;

            for (int j = 0; j < wallContactNormals.Count; j++)
            {
                if (Vector3.Dot(wallContactNormals[j], horizontalNormal) >
                    SameWallNormalDot)
                {
                    alreadyRegistered = true;
                    break;
                }
            }

            if (!alreadyRegistered)
                wallContactNormals.Add(horizontalNormal);
        }
    }

    private void Jump()
    {
        if (!IsGrounded())
        {
            // Debug.Log("NO ESTOY EN EL SUELO");
            return;
        }

        // Debug.Log("SALTANDO");

        rb.linearVelocity = new Vector3(
            rb.linearVelocity.x,
            0f,
            rb.linearVelocity.z
        );

        rb.AddForce(
            Vector3.up * jumpForce,
            ForceMode.Impulse
        );

        if (anim != null)
        {
            anim.SetTrigger(JumpParam);
        }
    }

    public bool IsGrounded()
    {
        if (groundCheck == null)
        {
            Debug.LogError("Falta asignar Ground Check en Move");
            return false;
        }

        return Physics.CheckSphere(
            groundCheck.position,
            groundDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundDistance
        );
    }
}
