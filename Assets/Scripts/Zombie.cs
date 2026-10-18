using UnityEngine;
using UnityEngine.AI;

public class Zombie : MonoBehaviour
{
    public float giveDamage = 5f;
    
    public UnityEngine.AI.NavMeshAgent zombieAgent;
    public Transform LookPoint;
    public Camera AttackingRaycastArea;
    public Transform playerBody;
    public LayerMask PlayerLayer;

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

    private void Awake()
    {
        zombieAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
    }

    private void Update()
{
    playerInvisionRadius = Physics.CheckSphere(transform.position, visionRadius, PlayerLayer);
    playerInattackingRadius = Physics.CheckSphere(transform.position, attackingRadius, PlayerLayer);

    if(!playerInvisionRadius && !playerInattackingRadius) Guard();
    if(playerInvisionRadius && !playerInattackingRadius) Pursueplayer();
    if(playerInvisionRadius && playerInattackingRadius) AttackPlayer();
}
private void Guard()
{
    if(Vector3.Distance(walkPoints[currentZombiePosition].transform.position, transform.position) < walkingpointRadius)
    {
        currentZombiePosition = Random.Range(0, walkPoints.Length);
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
    }
    else
    {
        anim.SetBool("Walking", false);
        anim.SetBool("Running", false);
        anim.SetBool("Attacking", false);
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
        
    }
    previouslyAttack = true;
    Invoke(nameof(ActiveAttacking), timeBtwAttack);
    }
  }
  private void ActiveAttacking()
  {
    previouslyAttack = false;
  }
}
