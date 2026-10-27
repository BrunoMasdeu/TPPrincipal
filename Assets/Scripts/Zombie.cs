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

    public float timeBtwAttack;
    bool previouslyAttack;

    public Animator anim;

    public float visionRadius;
    public float attackingRadius;
    public bool playerInvisionRadius;
    public bool playerInattackingRadius;

    // Aviso al WaveManager cuando el zombie muere
    private Action onDeath;

    private void Awake()
    {
        presentHealth = zombieHealth;
        zombieAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
    }

    // Lo llama el WaveManager justo después de instanciar el zombie
    public void Initialize(Transform player, GameObject[] points, float health, Action deathCallback)
    {
        playerBody = player;
        if (LookPoint == null) LookPoint = player;
        walkPoints = points;

        zombieHealth = health;
        presentHealth = health;

        // Estilo Black Ops: siempre persiguen al jugador
        visionRadius = 1000f;

        onDeath = deathCallback;
    }

    private void Update()
    {
        if (isDead) return;

        playerInvisionRadius = Physics.CheckSphere(transform.position, visionRadius, PlayerLayer);
        playerInattackingRadius = Physics.CheckSphere(transform.position, attackingRadius, PlayerLayer);

        if(!playerInvisionRadius && !playerInattackingRadius) Guard();
        if(playerInvisionRadius && !playerInattackingRadius) Pursueplayer();
        if(playerInvisionRadius && playerInattackingRadius) AttackPlayer();
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
    private void Pursueplayer()
    {
        if(zombieAgent.SetDestination(playerBody.position))
        {
            anim.SetBool("Walking", false);
            anim.SetBool("Running", true);
            anim.SetBool("Attacking", false);
            anim.SetBool("Died", false);
        }
        else
        {
            anim.SetBool("Walking", false);
            anim.SetBool("Running", false);
            anim.SetBool("Attacking", false);
            anim.SetBool("Died", true);
        }
    }
    private void AttackPlayer()
    {
        zombieAgent.SetDestination(transform.position);
        transform.LookAt(LookPoint);
        if(!previouslyAttack)
        {
            RaycastHit hitInfo;
            if(Physics.Raycast(AttackingRaycastArea.transform.position, AttackingRaycastArea.transform.forward, out hitInfo, attackingRadius))
            {
                Debug.Log("Attacking" + hitInfo.transform.name);

                Move playerBody = hitInfo.transform.GetComponent<Move>();

                if(playerBody != null)
                {
                    playerBody.playerHitDamage(giveDamage);
                }

                anim.SetBool("Walking", false);
                anim.SetBool("Running", false);
                anim.SetBool("Attacking", true);
                anim.SetBool("Died", false);
            }
            previouslyAttack = true;
            Invoke(nameof(ActiveAttacking), timeBtwAttack);
        }
    }
    private void ActiveAttacking()
    {
        previouslyAttack = false;
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

        onDeath?.Invoke();   // avisa al WaveManager
        onDeath = null;

        UnityEngine.Object.Destroy(gameObject, 1.5f);
    }
}