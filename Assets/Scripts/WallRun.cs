using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class WallRun : MonoBehaviour
{
    [Header("Wallrunning")]
    public LayerMask whatIsWall;
    public LayerMask whatIsGround;

    public float wallRunForce = 20f;
    public float wallJumpUpForce = 7f;
    public float wallJumpSideForce = 7f;
    public float wallClimbSpeed = 5f;
    public float maxWallRunTime = 2f;

    private float wallRunTimer;

    [Header("Input")]
    public Key upwardsRunKey = Key.LeftShift;
    public Key downwardsRunKey = Key.LeftCtrl;

    private bool upwardsRunning;
    private bool downwardsRunning;

    [Header("Detection")]
    public float wallCheckDistance = 0.7f;
    public float minJumpHeight = 1f;

    private RaycastHit leftWallHit;
    private RaycastHit rightWallHit;

    private bool wallLeft;
    private bool wallRight;

    [Header("Exiting")]
    public float exitWallTime = 0.2f;

    private bool exitingWall;
    private float exitWallTimer;

    [Header("Gravity")]
    public bool useGravity = true;
    public float gravityCounterForce = 5f;

    [Header("References")]
    public Transform orientation;

    private Move move;
    private Rigidbody rb;

    private float horizontalInput;
    private float verticalInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        move = GetComponent<Move>();
    }

    private void Update()
    {
        CheckForWall();
        GetInput();
        StateMachine();
    }

    private void FixedUpdate()
    {
        if (move.wallrunning)
        {
            WallRunningMovement();
        }
    }

    private void GetInput()
    {
        horizontalInput = 0f;
        verticalInput = 0f;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.aKey.isPressed)
            horizontalInput -= 1f;

        if (Keyboard.current.dKey.isPressed)
            horizontalInput += 1f;

        if (Keyboard.current.wKey.isPressed)
            verticalInput += 1f;

        if (Keyboard.current.sKey.isPressed)
            verticalInput -= 1f;

        upwardsRunning = Keyboard.current[upwardsRunKey].isPressed;
        downwardsRunning = Keyboard.current[downwardsRunKey].isPressed;
    }

    private void CheckForWall()
    {
        wallRight = Physics.Raycast(
            transform.position,
            orientation.right,
            out rightWallHit,
            wallCheckDistance,
            whatIsWall
        );

        wallLeft = Physics.Raycast(
            transform.position,
            -orientation.right,
            out leftWallHit,
            wallCheckDistance,
            whatIsWall
        );
    }

    private bool AboveGround()
    {
        return !Physics.Raycast(
            transform.position,
            Vector3.down,
            minJumpHeight,
            whatIsGround
        );
    }

    private void StateMachine()
    {
        if (wallLeft || wallRight)
        {
            if (verticalInput > 0 &&
                AboveGround() &&
                !exitingWall)
            {
                if (!move.wallrunning)
                    StartWallRun();

                if (wallRunTimer > 0)
                    wallRunTimer -= Time.deltaTime;

                if (wallRunTimer <= 0)
                {
                    exitingWall = true;
                    exitWallTimer = exitWallTime;
                }

                if (Keyboard.current != null &&
                    Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    WallJump();
                }
            }
            else if (exitingWall)
            {
                if (move.wallrunning)
                    StopWallRun();

                if (exitWallTimer > 0)
                    exitWallTimer -= Time.deltaTime;

                if (exitWallTimer <= 0)
                    exitingWall = false;
            }
            else
            {
                if (move.wallrunning)
                    StopWallRun();
            }
        }
        else
        {
            if (move.wallrunning)
                StopWallRun();
        }
    }

    private void StartWallRun()
    {
        move.wallrunning = true;

        wallRunTimer = maxWallRunTime;

        rb.linearVelocity = new Vector3(
            rb.linearVelocity.x,
            0f,
            rb.linearVelocity.z
        );

        rb.useGravity = useGravity;
    }

    private void WallRunningMovement()
    {
        Vector3 wallNormal;

        if (wallRight)
            wallNormal = rightWallHit.normal;
        else
            wallNormal = leftWallHit.normal;

        Vector3 wallForward = Vector3.Cross(
            wallNormal,
            transform.up
        );

        if (
            (orientation.forward - wallForward).magnitude >
            (orientation.forward - -wallForward).magnitude
        )
        {
            wallForward = -wallForward;
        }

        // Movimiento hacia adelante
        rb.AddForce(
            wallForward * wallRunForce,
            ForceMode.Force
        );

        // Subir
        if (upwardsRunning)
        {
            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x,
                wallClimbSpeed,
                rb.linearVelocity.z
            );
        }

        // Bajar
        if (downwardsRunning)
        {
            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x,
                -wallClimbSpeed,
                rb.linearVelocity.z
            );
        }

        // Mantenerse pegado a la pared
        if (
            !(wallLeft && horizontalInput > 0) &&
            !(wallRight && horizontalInput < 0)
        )
        {
            rb.AddForce(
                -wallNormal * 100f,
                ForceMode.Force
            );
        }

        // Reducir efecto de gravedad
        if (useGravity)
        {
            rb.AddForce(
                transform.up * gravityCounterForce,
                ForceMode.Force
            );
        }
    }

    private void StopWallRun()
    {
        move.wallrunning = false;

        rb.useGravity = true;
    }

    private void WallJump()
    {
        exitingWall = true;
        exitWallTimer = exitWallTime;

        Vector3 wallNormal;

        if (wallRight)
            wallNormal = rightWallHit.normal;
        else
            wallNormal = leftWallHit.normal;

        Vector3 forceToApply =
            transform.up * wallJumpUpForce +
            wallNormal * wallJumpSideForce;

        rb.linearVelocity = new Vector3(
            rb.linearVelocity.x,
            0f,
            rb.linearVelocity.z
        );

        rb.AddForce(
            forceToApply,
            ForceMode.Impulse
        );
    }
}
