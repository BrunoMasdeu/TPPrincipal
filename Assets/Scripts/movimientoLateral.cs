using System;
using UnityEngine;
using UnityEngine.AI;

// Preserve the damage/event API used by GunSystem and TutorialManager.
[RequireComponent(typeof(CapsuleCollider), typeof(NavMeshAgent))]
public class movimientoLateral : MonoBehaviour
{
    public static event Action<string> OnTargetDestroyed;
    [Min(0.1f)] public float velocidad = 3f;
    [HideInInspector] public float distanciaMovimiento = 1f; // Old scene serialization.
    public int health = 100;

    [Header("Patrullaje")]
    public BotPatrolArea patrolArea;
    public Vector2 pauseDuration = new Vector2(0.4f, 1.3f);
    [Min(1f)] public float turnSpeed = 240f;
    public LayerMask obstacleMask = ~0;

    [Header("Saltos en el lugar")]
    public bool canJump;
    [Min(0.1f)] public float jumpHeight = 0.8f;
    [Min(0.2f)] public float jumpDuration = 0.8f;
    public Vector2 jumpInterval = new Vector2(4f, 8f);

    private const float Skin = 0.04f;
    private NavMeshAgent agent;
    private CapsuleCollider capsule;
    private Animator animator;
    private NavMeshPath path;
    private float radius;
    private float height;
    private Vector3 capsuleCenter;
    private float waitUntil;
    private float nextJump;
    private float jumpTime;
    private Vector3 jumpOrigin;
    private bool jumping;
    private bool dead;
    private bool initialized;
    private Vector3 progressPosition;
    private float progressTime;

    public bool IsJumping => jumping;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        capsule = GetComponent<CapsuleCollider>();
        animator = GetComponent<Animator>();
        path = new NavMeshPath();
        Vector3 scale = transform.lossyScale;
        radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        height = Mathf.Max(radius * 2f, capsule.height * Mathf.Abs(scale.y));
        capsuleCenter = Vector3.Scale(capsule.center, scale);

        // Navigation owns translation; physics and root motion must not compete with it.
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
        if (animator != null) animator.applyRootMotion = false;
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.autoTraverseOffMeshLink = false;
        agent.areaMask = 1;
        agent.radius = radius + Skin;
        agent.height = height;
        agent.speed = velocidad;
        agent.acceleration = 12f;
        agent.stoppingDistance = 0.25f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        if (patrolArea == null || !patrolArea.IsReady ||
            !NavMesh.SamplePosition(transform.position, out NavMeshHit spawn, 2f, agent.areaMask) ||
            !patrolArea.Contains(spawn.position, radius) || !agent.Warp(spawn.position))
        {
            Debug.LogError($"{name}: falta un área de patrullaje navegable o el bot está fuera del piso.", this);
            agent.enabled = false;
            enabled = false;
            return;
        }

        transform.position = spawn.position + Vector3.up * Skin;
        agent.nextPosition = spawn.position;
        initialized = true;
        waitUntil = Time.time + UnityEngine.Random.Range(0f, 0.6f);
        ScheduleJump();
        progressPosition = transform.position;
        progressTime = Time.time;
        Animate(0f);
    }

    private void Update()
    {
        if (!initialized || dead || Time.deltaTime <= 0f) return;
        if (!patrolArea.IsReady || !agent.isOnNavMesh) return;
        if (jumping)
        {
            UpdateJump();
            return;
        }

        if (canJump && Time.time >= nextJump)
        {
            ScheduleJump();
            if (CanMove(transform.position, transform.position + Vector3.up * jumpHeight))
            {
                jumping = true;
                jumpTime = 0f;
                jumpOrigin = transform.position;
                agent.isStopped = true;
                Animate(0f);
                return;
            }
        }
        if (Time.time < waitUntil)
        {
            Animate(0f);
            return;
        }
        if (!agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance))
        {
            if (agent.hasPath) Pause();
            else ChooseDestination();
            Animate(0f);
            return;
        }

        Vector3 candidate = agent.nextPosition + Vector3.up * Skin;
        Vector3 previous = transform.position;
        if (patrolArea.Contains(candidate, radius) && CanMove(previous, candidate))
        {
            transform.position = candidate;
            Vector3 heading = candidate - previous;
            heading.y = 0f;
            if (heading.sqrMagnitude > 0.00001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(heading), turnSpeed * Time.deltaTime);
            Animate(heading.magnitude / Time.deltaTime);
        }
        else
        {
            // Moving obstacles can invalidate baked paths. Stop before contact and retry.
            agent.nextPosition = previous - Vector3.up * Skin;
            Pause();
            Animate(0f);
        }
        if (Time.time - progressTime >= 2f)
        {
            if (Vector3.Distance(progressPosition, transform.position) < 0.15f) Pause();
            progressPosition = transform.position;
            progressTime = Time.time;
        }
    }

    private void ChooseDestination()
    {
        for (int attempt = 0; attempt < 24; attempt++)
        {
            if (!NavMesh.SamplePosition(patrolArea.RandomPoint(), out NavMeshHit hit, 0.6f, agent.areaMask) ||
                !patrolArea.Contains(hit.position, radius) ||
                Vector3.Distance(transform.position, hit.position) < 1.5f ||
                !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                continue;
            bool inside = true;
            foreach (Vector3 corner in path.corners)
                if (!patrolArea.Contains(corner, radius)) { inside = false; break; }
            if (!inside) continue;
            // Convex floor: segments between in-bounds corners also remain inside.
            agent.isStopped = false;
            if (agent.SetPath(path)) return;
        }
        Pause();
    }

    private void Pause()
    {
        agent.ResetPath();
        agent.isStopped = true;
        waitUntil = Time.time + RandomBetween(pauseDuration, 0.1f);
    }

    private void ScheduleJump() => nextJump = Time.time + RandomBetween(jumpInterval, 0.5f);

    private static float RandomBetween(Vector2 range, float minimum)
    {
        float low = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
        return UnityEngine.Random.Range(low, Mathf.Max(low, Mathf.Max(range.x, range.y)));
    }

    private void UpdateJump()
    {
        jumpTime += Time.deltaTime;
        float t = Mathf.Clamp01(jumpTime / Mathf.Max(0.2f, jumpDuration));
        Vector3 candidate = jumpOrigin + Vector3.up * (4f * jumpHeight * t * (1f - t));
        if (CanMove(transform.position, candidate)) transform.position = candidate;
        else if (t < 0.5f) jumpTime = Mathf.Max(0.2f, jumpDuration) - jumpTime;
        // Other bots continue to avoid the landing footprint while this bot is airborne.
        agent.nextPosition = jumpOrigin - Vector3.up * Skin;
        Animate(0f);
        if (t >= 1f && Vector3.Distance(transform.position, jumpOrigin) < 0.01f)
        {
            jumping = false;
            agent.isStopped = false;
            progressPosition = transform.position;
            progressTime = Time.time;
        }
    }

    private bool CanMove(Vector3 from, Vector3 to)
    {
        Vector3 center = from + transform.rotation * capsuleCenter;
        float halfSegment = height * 0.5f - radius;
        Vector3 bottom = center - Vector3.up * halfSegment;
        Vector3 top = center + Vector3.up * halfSegment;
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance > 0.00001f)
        {
            foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, radius,
                delta / distance, distance + Skin, obstacleMask, QueryTriggerInteraction.Ignore))
                if (IsObstacle(hit.collider)) return false;
        }
        foreach (Collider other in Physics.OverlapCapsule(bottom + delta, top + delta,
            radius, obstacleMask, QueryTriggerInteraction.Ignore))
            if (IsObstacle(other)) return false;
        return true;
    }

    private bool IsObstacle(Collider other) => other != null && other != patrolArea.floor &&
        other != capsule && !other.transform.IsChildOf(transform);

    private void Animate(float speed)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetFloat("InputVertical", speed / Mathf.Max(velocidad, 0.1f));
        animator.SetFloat("InputMagnitude", speed / Mathf.Max(velocidad, 0.1f));
        animator.SetBool("IsGrounded", !jumping);
        animator.SetFloat("GroundDistance", jumping ? Mathf.Max(0f, transform.position.y - jumpOrigin.y) : 0f);
    }

    private void OnDisable()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
    }

    public void TakeDamage(int damage)
    {
        if (dead) return;
        health -= damage;
        if (health > 0) return;
        dead = true;
        OnTargetDestroyed?.Invoke(gameObject.tag);
        Destroy(gameObject);
    }
}
