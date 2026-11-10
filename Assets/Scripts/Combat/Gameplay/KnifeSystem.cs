using UnityEngine;
using UnityEngine.InputSystem;

public class KnifeSystem : MonoBehaviour
{
    public Camera fpsCam;
    public float range = 2f;
    public int damage = 50;
    public float attackCooldown = 0.7f;

    private float nextAttackTime;

    // [MULTIJUGADOR] Igual que en GunSystem: avisa a la red del ataque y de
    // a quién le pegó. Está en la raíz del jugador (este script va en el arma).
    private NetworkPlayerCombat network;

    void Start()
    {
        // [MULTIJUGADOR]
        network = GetComponentInParent<NetworkPlayerCombat>();
    }

    void Update()
    {
        if (Mouse.current == null)
            return;

        // [MULTIJUGADOR] Con la tienda o la consola de depuración abiertas no
        // se ataca.
        if (MatchDebugConsole.IsInputCaptured || NetworkPlayerShop.IsInputCaptured)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (Time.time < nextAttackTime)
                return;

            nextAttackTime = Time.time + attackCooldown;
            Attack();
        }
    }

    void Attack()
    {
        if (fpsCam == null)
        {
            Debug.LogError("KnifeSystem: falta asignar Fps Cam");
            return;
        }

        Ray ray = new Ray(
            fpsCam.transform.position,
            fpsCam.transform.forward
        );

        Debug.DrawRay(ray.origin, ray.direction * range, Color.red, 2f);

        // [ANIMACIONES] Primero el raycast: el sonido cambia según si el
        // cuchillo le pega a alguien o corta el aire.
        bool tocoAlgo = Physics.Raycast(ray, out RaycastHit hit, range);

        // [MULTIJUGADOR] Todos escuchan el ataque y ven la animación.
        if (network != null)
            network.ReportShot(tocoAlgo && EsBlanco(hit.collider));

        if (tocoAlgo)
        {
            // [COMO HOY] Como en el GunSystem: el cuchillo también les pega a
            // los bots y a las dianas del tutorial.
            // [ZOMBIES] Los zombies tienen su propia vida (Zombie.cs). Se revisa
            // primero para que tenga prioridad aunque el prefab esté con el
            // tag "Enemy".
            Zombie zombie = hit.collider.GetComponentInParent<Zombie>();

            if (zombie != null)
            {
                zombie.zombieHitDamage(damage);
            }
            else if (hit.collider.CompareTag("Enemy"))
            {
                movimientoBot bot = hit.collider.GetComponentInParent<movimientoBot>();
                if (bot != null)
                    bot.TakeDamage(damage);
            }
            else if (hit.collider.CompareTag("Target"))
            {
                Diana target = hit.collider.GetComponentInParent<Diana>();
                if (target != null)
                    target.Derribar();
            }
            // [MULTIJUGADOR] Si tocó a otro jugador, la vida la resta el servidor.
            else if (network != null)
            {
                network.ReportHit(hit.collider, damage);
            }

            ShootingAi enemy =
                hit.collider.GetComponentInParent<ShootingAi>();

            if (enemy != null)
                enemy.TakeDamage(damage);
        }
    }

    // [ANIMACIONES] Lo que el cuchillo puede lastimar (no una pared ni el
    // propio jugador). Sirve para elegir el sonido del ataque.
    private bool EsBlanco(Collider tocado)
    {
        NetworkPlayerHealth jugador = tocado.GetComponentInParent<NetworkPlayerHealth>();
        if (jugador != null)
            return network == null || jugador.gameObject != network.gameObject;

        return tocado.GetComponentInParent<Zombie>() != null
            || tocado.GetComponentInParent<ShootingAi>() != null
            || tocado.CompareTag("Enemy")
            || tocado.CompareTag("Target");
    }
}
