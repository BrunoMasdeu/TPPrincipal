using UnityEngine;
using UnityEngine.InputSystem;

public class WallRunning : MonoBehaviour
{
    [Header("Wall Run")]
    public float wallRunSpeed = 7f;
    public float wallCheckDistance = 0.8f;
    public float wallRunGravity = 1f;

    [Header("Detección")]
    public LayerMask wallMask;

    [Header("Estado")]
    public bool wallrunning = false;

    private Rigidbody rb;

    private bool wallLeft;
    private bool wallRight;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        CheckWalls();

        // Activar Wall Run manteniendo SHIFT
        if (Keyboard.current != null &&
            Keyboard.current.leftShiftKey.isPressed &&
            !IsGrounded() &&
            (wallLeft || wallRight))
        {
            wallrunning = true;
        }
        else
        {
            wallrunning = false;
        }

        // Saltar desde la pared
        if (wallrunning &&
            Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            WallJump();
        }
    }

    void FixedUpdate()
    {
        if (!wallrunning)
            return;

        // Dirección hacia adelante
        Vector3 forwardMovement =
            transform.forward * wallRunSpeed;

        // Reducir la gravedad mientras corre por la pared
        Vector3 velocity = rb.linearVelocity;

        velocity.x = forwardMovement.x;
        velocity.z = forwardMovement.z;

        // Caída controlada
        velocity.y = -wallRunGravity;

        rb.linearVelocity = velocity;
    }

    void CheckWalls()
    {
        wallLeft = Physics.Raycast(
            transform.position,
            -transform.right,
            wallCheckDistance,
            wallMask
        );

        wallRight = Physics.Raycast(
            transform.position,
            transform.right,
            wallCheckDistance,
            wallMask
        );
    }

    void WallJump()
    {
        wallrunning = false;

        Vector3 jumpDirection = Vector3.up;

        if (wallLeft)
        {
            jumpDirection += transform.right;
        }
        else if (wallRight)
        {
            jumpDirection -= transform.right;
        }

        rb.linearVelocity = Vector3.zero;

        rb.AddForce(
            jumpDirection.normalized * 7f,
            ForceMode.Impulse
        );
    }

    bool IsGrounded()
    {
        return Physics.Raycast(
            transform.position,
            Vector3.down,
            1.1f,
            wallMask
        );
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawRay(
            transform.position,
            -transform.right * wallCheckDistance
        );

        Gizmos.DrawRay(
            transform.position,
            transform.right * wallCheckDistance
        );
    }
}
