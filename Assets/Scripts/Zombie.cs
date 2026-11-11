using System;
using UnityEngine;
using UnityEngine.AI;

public class Zombie : MonoBehaviour
{
    public float giveDamage = 5f;
    private float zombieHealth = 100f;
    private float presentHealth;
    public UnityEngine.AI.NavMeshAgent zombieAgent;
    public Transform LookPoint;
    public Camera AttackingRaycastArea;
    public Transform playerBody;
    public LayerMask PlayerLayer;
    private bool isDead;

    public GameObject[] walkPoints;
    int currentZombiePosition = 0;
    public float zombieSpeed;
    float walkingpointRadius = 2;

    [Header("Modo patrulla")]
    [Tooltip("Si está activo, el zombie solo camina entre los walkPoints: no persigue ni ataca al jugador.")]
    public bool onlyPatrol = true;
    [Tooltip("Velocidad al patrullar (usa el NavMeshAgent).")]
    public float patrolSpeed = 1.5f;
    private bool patrolHasDestination;
    private bool patrolAnimationSet;

    public float timeBtwAttack;
    bool previouslyAttack;

    public Animator anim;

    public float visionRadius;
    public float attackingRadius;
    public bool playerInvisionRadius;
    public bool playerInattackingRadius;

    private Action onDeath;

    private void Awake()
    {
        presentHealth = zombieHealth;
        zombieAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
    }

    public void Initialize(Transform player, GameObject[] points, float health, Action deathCallback)
    {
        playerBody = player;
        if (LookPoint == null) LookPoint = player;
        walkPoints = points;

        zombieHealth = health;
        presentHealth = health;

        visionRadius = 10000f;

        onDeath = deathCallback;
    }

    private void Update()
    {
        if (isDead) return;

        if (onlyPatrol)
        {
            Patrol();
            return;
        }

        playerInvisionRadius = Physics.CheckSphere(transform.position, visionRadius, PlayerLayer);
        playerInattackingRadius = Physics.CheckSphere(transform.position, attackingRadius, PlayerLayer);

        if(!playerInvisionRadius && !playerInattackingRadius) Guard();
        if(playerInvisionRadius && !playerInattackingRadius) Pursueplayer();
        if(playerInvisionRadius && playerInattackingRadius) AttackPlayer();
    }
    private void Patrol()
    {
        if (walkPoints == null || walkPoints.Length == 0 ||
            zombieAgent == null || !zombieAgent.isOnNavMesh)
            return;

        if (!patrolAnimationSet)
        {
            patrolAnimationSet = true;
            zombieAgent.speed = patrolSpeed;
            anim.SetBool("Walking", true);
            anim.SetBool("Running", false);
            anim.SetBool("Attacking", false);
            anim.SetBool("Died", false);
        }

        bool arrived = !zombieAgent.pathPending &&
                       zombieAgent.remainingDistance <= walkingpointRadius;

        if (!patrolHasDestination || arrived)
        {
            if (walkPoints.Length > 1)
            {
                currentZombiePosition =
                    (currentZombiePosition + UnityEngine.Random.Range(1, walkPoints.Length))
                    % walkPoints.Length;
            }

            GameObject target = walkPoints[currentZombiePosition];
            if (target != null)
            {
                zombieAgent.SetDestination(target.transform.position);
                patrolHasDestination = true;
            }
        }
    }

    private void Guard()
    {
        if (walkPoints == null || walkPoints.Length == 0) return;

        if(Vector3.Distance(walkPoints[currentZombiePosition].transform.position, transform.position) < walkingpointRadius)
        {
            currentZombiePosition = UnityEngine.Random.Range(0, walkPoints.Length);
            if(currentZombiePosition >= walkPoints.Length)
            {
                currentZombiePosition = 0;
            }
        }
        transform.position = Vector3.MoveTowards(transform.position, walkPoints[currentZombiePosition].transform.position, Time.deltaTime * zombieSpeed);
        transform.LookAt(walkPoints[currentZombiePosition].transform.position);
    }
    // [ZOMBIES] Cada cuantos segundos el zombie recalcula el camino hacia el jugador.
    private const float tiempoEntreCaminos = 0.25f;
    private float proximoCamino;

    private void Pursueplayer()
    {
        if (!zombieAgent.isOnNavMesh) return;

        // [ZOMBIES] Si venia de atacar, el agente estaba frenado: se lo vuelve a soltar.
        zombieAgent.isStopped = false;

        // [ZOMBIES] Antes se pedia un camino nuevo en TODOS los frames. Cada pedido
        // cancela al anterior, y si el calculo tarda mas de un frame el zombie nunca
        // recibe el camino y se queda corriendo en el lugar. Ahora se pide 4 veces
        // por segundo, que alcanza para seguir al jugador.
        if (Time.time >= proximoCamino)
        {
            proximoCamino = Time.time + tiempoEntreCaminos;
            zombieAgent.SetDestination(playerBody.position);
        }

        // [ZOMBIES] Antes, si no habia camino (por ejemplo con el jugador en el aire),
        // se activaba la animacion "Died", y el Animator no tiene salida de ese estado:
        // el zombie quedaba vivo pero con la pose de muerto. Ahora, mientras persigue,
        // usa siempre la animacion de correr.
        anim.SetBool("Walking", false);
        anim.SetBool("Running", true);
        anim.SetBool("Attacking", false);
        anim.SetBool("Died", false);
    }
    private void AttackPlayer()
    {
        // [ZOMBIES] Frenar en seco al llegar. Antes se le daba como destino su propia
        // posicion en cada frame y el zombie seguia deslizandose hacia el jugador.
        if (zombieAgent.isOnNavMesh)
        {
            zombieAgent.isStopped = true;
            zombieAgent.velocity = Vector3.zero;
        }

        transform.LookAt(LookPoint);

        // [ZOMBIES] La animacion de ataque se pone siempre que el jugador esta en rango.
        // Antes solo se ponia si el rayo de abajo tocaba algo; si no, el zombie quedaba
        // quieto con la animacion de correr.
        anim.SetBool("Walking", false);
        anim.SetBool("Running", false);
        anim.SetBool("Attacking", true);
        anim.SetBool("Died", false);

        if(!previouslyAttack)
        {
            RaycastHit hitInfo;
            // [ZOMBIES] El rayo sale de la cabeza y el radio de ataque se mide desde los
            // pies, por eso se le da medio metro extra: asi no falla justo en el borde.
            if(Physics.Raycast(AttackingRaycastArea.transform.position, AttackingRaycastArea.transform.forward, out hitInfo, attackingRadius + 0.5f))
            {
                Debug.Log("Attacking" + hitInfo.transform.name);

                Move playerBody = hitInfo.transform.GetComponent<Move>();

                if(playerBody != null)
                {
                    playerBody.playerHitDamage(giveDamage, transform.position);
                }
            }
            previouslyAttack = true;
            Invoke(nameof(ActiveAttacking), timeBtwAttack);
        }
    }
    private void ActiveAttacking()
    {
        previouslyAttack = false;
    }
    // [ZOMBIES] Igual que el de abajo, pero avisa al jugador que disparó para que
    // muestre la marca de impacto en la mira (misma lógica que los bots del tutorial).
    public bool zombieHitDamage(float takeDamage, NetworkPlayerCombat atacante)
    {
        if (isDead || takeDamage <= 0f) return false;

        int danioAplicado = Mathf.RoundToInt(Mathf.Min(takeDamage, presentHealth));
        bool murio = zombieHitDamage(takeDamage);

        if (atacante != null)
            atacante.ConfirmZombieHit(danioAplicado, murio);

        return murio;
    }

    public bool zombieHitDamage(float takeDamage)
    {
        if (isDead) return false;

        presentHealth -= takeDamage;

        if(presentHealth <= 0)
        {
            isDead = true;

            anim.SetBool("Walking", false);
            anim.SetBool("Running", false);
            anim.SetBool("Attacking", false);
            anim.SetBool("Died", true);

            zombieDie();
            return true;
        }
        return false;
    }

    private void zombieDie()
    {
        zombieAgent.SetDestination(transform.position);
        zombieSpeed = 0f;
        attackingRadius = 0f;
        visionRadius = 0f;
        playerInattackingRadius = false;
        playerInvisionRadius = false;

        // [ZOMBIES] Primero se avisa al WaveManager (si es el último de la oleada
        // suelta la caja y cancela el drop) y después se suelta el objeto.
        onDeath?.Invoke();
        onDeath = null;
        GetComponent<ZombieDrop>()?.Drop();

        UnityEngine.Object.Destroy(gameObject, 1.5f);
    }
}
