using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controla la lógica de movimiento, patrullaje en el NavMesh, salto local, evasión física de obstáculos y 
/// salud de los bots tutoriales.
/// 
/// Relación con otros componentes:
/// - <see cref="BotPatrolArea"/>: Define los límites del suelo navegable y la malla NavMesh donde el bot se desplaza.
/// - <see cref="NavMeshAgent"/>: Calcula rutas e informa sobre el siguiente punto en la malla de navegación.
/// - <see cref="CapsuleCollider"/> y <see cref="Rigidbody"/>: Permite hacer comprobaciones de colisión manuales mediante CapsuleCast/OverlapCapsule.
/// - <see cref="Animator"/>: Actualiza los parámetros de animación visual (velocidad y estado en aire/suelo).
/// </summary>
[RequireComponent(typeof(CapsuleCollider), typeof(NavMeshAgent))]
public class movimientoBot : MonoBehaviour
{
    /// <summary>
    /// Evento estático invocado cuando un bot es destruido al quedarse sin vida.
    /// Preservado para integración con GunSystem y TutorialManager.
    /// </summary>
    public static event Action<string> OnTargetDestroyed;

    [Min(0.1f)] public float velocidad = 3f;

    /// <summary>
    /// Variable conservada por compatibilidad con la serialización de escenas anteriores.
    /// </summary>
    [HideInInspector] public float distanciaMovimiento = 1f;

    public int health = 100;

    [Header("Patrullaje")]
    public BotPatrolArea patrolArea;

    /// <summary>
    /// Rango de duración de pausa (en segundos) cuando el bot alcanza su destino o encuentra un bloqueo.
    /// </summary>
    public Vector2 pauseDuration = new Vector2(0.4f, 1.3f);

    [Min(1f)] public float turnSpeed = 240f;

    /// <summary>
    /// Máscara de capas utilizada para las comprobaciones de colisiones físicas durante el movimiento y los saltos.
    /// </summary>
    public LayerMask obstacleMask = ~0;

[Header("Saltos en el lugar")]
public bool puedeSaltar = true;
[Min(0.1f)] public float alturaDelSalto = 0.8f;
[Min(0.2f)] public float duracionDelSalto = 0.8f;

[Min(0.5f)] public float saltoCadaSegundo = 5f;

public Vector2 jumpInterval
{
    get => new Vector2(saltoCadaSegundo, saltoCadaSegundo);
    set => saltoCadaSegundo = Mathf.Max(0.5f, Mathf.Min(value.x, value.y));
}
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

    /// <summary>
    /// Indica si el bot se encuentra actualmente realizando una trayectoria de salto.
    /// </summary>
    public bool IsJumping => jumping;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        capsule = GetComponent<CapsuleCollider>();
        animator = GetComponent<Animator>();
        path = new NavMeshPath();

        // Cálculo de dimensiones del CapsuleCollider considerando la escala global (lossyScale)
        Vector3 scale = transform.lossyScale;
        radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        height = Mathf.Max(radius * 2f, capsule.height * Mathf.Abs(scale.y));
        capsuleCenter = Vector3.Scale(capsule.center, scale);

        // Coordinación de NavMeshAgent, Rigidbody y Animator:
        // El movimiento del personaje es controlado explícitamente mediante código utilizando la información del NavMeshAgent.
        // Para evitar conflictos entre físicas, animación y navegación:
        // 1. Rigidbody se configura como kinemático y sin gravedad.
        // 2. Animator no aplica Root Motion para que la animación no interfiera con el desplazamiento en transform.position.
        // 3. NavMeshAgent deshabilita la actualización automática de posición y rotación (updatePosition/updateRotation = false).
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

        // Validación inicial del punto de aparición dentro del perímetro del área de patrullaje.
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

        // Si está ejecutando un salto vertical, se procesa de forma prioritaria.
        if (jumping)
        {
            UpdateJump();
            return;
        }

        // Evaluación de inicio de salto: se verifica disponibilidad del tiempo y el espacio superior necesario.
        if (puedeSaltar && Time.time >= nextJump)
            {
            if (CanMove(transform.position, transform.position + Vector3.up * alturaDelSalto))
                {
                    ScheduleJump();
                    jumping = true;
                    jumpTime = 0f;
                    jumpOrigin = transform.position;
                    agent.isStopped = true;
                    Animate(0f);
                    return;
                }
            nextJump = Time.time + 0.5f;
            }

        // En período de espera/pausa en el lugar.
        if (Time.time < waitUntil)
        {
            Animate(0f);
            return;
        }

        // Si no hay ruta en proceso de cálculo y se alcanzó el destino actual o no posee ruta, elige un nuevo destino.
        if (!agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance))
        {
            if (agent.hasPath) Pause();
            else ChooseDestination();
            Animate(0f);
            return;
        }

        // Avance hacia el siguiente punto calculado por NavMeshAgent.
        Vector3 candidate = agent.nextPosition + Vector3.up * Skin;
        Vector3 previous = transform.position;

        // Comprobación de límites y colisiones antes de desplazar el Transform:
        // Valida que la posición candidata permanezca dentro del perímetro y no colisione con obstáculos físicos dinámicos.
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
            // Los obstáculos móviles pueden invalidar las rutas horneadas. Se detiene la marcha antes del contacto y se intenta reevaluar.
            agent.nextPosition = previous - Vector3.up * Skin;
            Pause();
            Animate(0f);
        }

        // Detección de atascamiento/recuperación si no logra avanzar al menos 0.15 metros en un intervalo de 2 segundos.
        if (Time.time - progressTime >= 2f)
        {
            if (Vector3.Distance(progressPosition, transform.position) < 0.15f) Pause();
            progressPosition = transform.position;
            progressTime = Time.time;
        }
    }

    /// <summary>
    /// Intenta seleccionar un punto de destino aleatorio dentro del piso, calculando y validando la ruta.
    /// Realiza hasta 24 intentos para encontrar un camino completamente dentro del perímetro del piso.
    /// </summary>
    private void ChooseDestination()
    {
        for (int attempt = 0; attempt < 24; attempt++)
        {
            // Selección y validación del punto destino en la malla de navegación y perímetro
            if (!NavMesh.SamplePosition(patrolArea.RandomPoint(), out NavMeshHit hit, 0.6f, agent.areaMask) ||
                !patrolArea.Contains(hit.position, radius) ||
                Vector3.Distance(transform.position, hit.position) < 1.5f ||
                !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                continue;

            // Verificación de que cada esquina del camino permanezca dentro de los márgenes
            bool inside = true;
            foreach (Vector3 corner in path.corners)
                if (!patrolArea.Contains(corner, radius)) { inside = false; break; }
            if (!inside) continue;

            // Al tratarse de un suelo convexo, los segmentos entre esquinas dentro de los márgenes también se mantienen dentro.
            agent.isStopped = false;
            if (agent.SetPath(path)) return;
        }
        Pause();
    }

    /// <summary>
    /// Detiene el movimiento del agente y programa un tiempo de pausa aleatorio.
    /// </summary>
    private void Pause()
    {
        agent.ResetPath();
        agent.isStopped = true;
        waitUntil = Time.time + RandomBetween(pauseDuration, 0.1f);
    }

    /// <summary>
    /// Programa el tiempo del próximo intento de salto.
    /// </summary>
    private void ScheduleJump() => nextJump = Time.time + saltoCadaSegundo;

    /// <summary>
    /// Devuelve un valor aleatorio entre los componentes de un Vector2 respetando un límite mínimo.
    /// </summary>
    private static float RandomBetween(Vector2 range, float minimum)
    {
        float low = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
        return UnityEngine.Random.Range(low, Mathf.Max(low, Mathf.Max(range.x, range.y)));
    }

    /// <summary>
    /// Actualiza la posición durante el desarrollo de un salto parabólico vertical.
    /// Maneja interrupciones por obstrucción superior y reanudación del estado.
    /// </summary>
    private void UpdateJump()
    {
        jumpTime += Time.deltaTime;
        float t = Mathf.Clamp01(jumpTime / Mathf.Max(0.2f, duracionDelSalto));

        // Trayectoria parabólica: 4 * h * t * (1 - t)
        Vector3 candidate = jumpOrigin + Vector3.up * (4f * alturaDelSalto * t * (1f - t));

        if (CanMove(transform.position, candidate)) transform.position = candidate;
        else if (t < 0.5f) jumpTime = Mathf.Max(0.2f, duracionDelSalto) - jumpTime; // Invierte dirección si encuentra techo al subir

        // Mientras el bot está en el aire, se mantiene la huella del NavMeshAgent en el origen para que otros bots lo esquiven.
        agent.nextPosition = jumpOrigin - Vector3.up * Skin;
        Animate(0f);

        // Aterrizaje completado
        if (t >= 1f && Vector3.Distance(transform.position, jumpOrigin) < 0.01f)
        {
            jumping = false;
            agent.isStopped = false;
            progressPosition = transform.position;
            progressTime = Time.time;
        }
    }

    /// <summary>
    /// Realiza un barrido de cápsula (CapsuleCastAll) y comprobación de superposición (OverlapCapsule)
    /// para asegurar que el movimiento entre dos puntos no atraviese colisionadores u obstáculos dinámicos.
    /// </summary>
    /// <param name="from">Posición de origen.</param>
    /// <param name="to">Posición de destino propuesta.</param>
    /// <returns>Verdadero si el camino está libre de obstáculos; falso en caso contrario.</returns>
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

    /// <summary>
    /// Determina si un colisionador dado representa un obstáculo para el bot (excluye al propio suelo, su propio collider y componentes hijos).
    /// </summary>
    private bool IsObstacle(Collider other) => other != null && other != patrolArea.floor &&
        other != capsule && !other.transform.IsChildOf(transform);

    /// <summary>
    /// Actualiza los parámetros del Animator para reflejar el estado de movimiento y salto en el modelo visual.
    /// </summary>
    /// <param name="speed">Velocidad actual de desplazamiento horizontal.</param>
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

    /// <summary>
    /// Recibe daño de armas u otras fuentes, reduciendo la salud. 
    /// Al morir invoca el evento <see cref="OnTargetDestroyed"/> y destruye el objeto.
    /// </summary>
    /// <param name="damage">Cantidad de daño a infligir.</param>
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