using UnityEngine;
using UnityEngine.AI;

public class BOT : MonoBehaviour
{

    public NavMeshAgent botAgente;
    public Animator anim;

    public GameObject[] puntosRecorrido;
    public float velocidadDelBot;
    public float radioPuntoDesplazamiento = 2f;
    int posicionActualDelBot = 0;

    public float tiempoDeEspera = 3f;
    float temporizadorEspera;
    bool esperando;

    public float velocidadDeGiro = 120f;

    private void Awake()
    {
        botAgente = GetComponent<NavMeshAgent>();
        botAgente.updateRotation = false;

        if (velocidadDelBot > 0) botAgente.speed = velocidadDelBot;
    }

    private void Start()
    {
        if (puntosRecorrido == null || puntosRecorrido.Length == 0) return;
 
        IrAlPuntoActual();
    }

    private void Update()
    {
        if (puntosRecorrido == null || puntosRecorrido.Length == 0) return;
 
        if (esperando)
        {
            temporizadorEspera -= Time.deltaTime;
 
            if (temporizadorEspera <= 0f)
            {
                ElegirSiguientePunto();
                IrAlPuntoActual();
            }
            return;
        }
 
        float distancia = Vector3.Distance(puntosRecorrido[posicionActualDelBot].transform.position, transform.position);
 
        if (distancia < radioPuntoDesplazamiento)
        {
            EmpezarEspera();
            return;
        }
 
        GirarSuavemente();
    }

    private void IrAlPuntoActual()
    {
        esperando = false;
        botAgente.isStopped = false;
        botAgente.SetDestination(puntosRecorrido[posicionActualDelBot].transform.position);
        if (anim != null) anim.SetBool("isMoving", true);
    }

    private void EmpezarEspera()
    {
        esperando = true;
        temporizadorEspera = tiempoDeEspera;
        botAgente.isStopped = true;
        botAgente.ResetPath();
        if (anim != null) anim.SetBool("isMoving", false);
    }
 
    private void ElegirSiguientePunto()
    {
        if (puntosRecorrido.Length == 1) return;
 
        int nuevo;
        do
        {
        nuevo = Random.Range(0, puntosRecorrido.Length);
        } 
        while (nuevo == posicionActualDelBot);
 
        posicionActualDelBot = nuevo;
    }

    private void GirarSuavemente()
    {
        Vector3 direccion = botAgente.desiredVelocity;
        direccion.y = 0f;

        if (direccion.sqrMagnitude < 0.01f) return;

        Quaternion rotacionObjetivo = Quaternion.LookRotation(direccion);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            rotacionObjetivo,
            velocidadDeGiro * Time.deltaTime
        );
    }
}