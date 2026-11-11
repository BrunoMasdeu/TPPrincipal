using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Grenade : MonoBehaviour
{
    [Header("Lanzamiento")]
    public GameObject grenadePrefab;
    public Transform throwPoint;
    public float throwForce = 12f;
    public float upwardForce = 4f;

    [Header("Municion")]
    public int grenades = 3;

    // [MULTIJUGADOR] En red las granadas las crea el servidor.
    private NetworkPlayerGrenades network;

    // [MULTIJUGADOR]
    private void Awake()
    {
        network = GetComponent<NetworkPlayerGrenades>();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // [MULTIJUGADOR] Con la tienda o la consola de depuración abiertas no
        // se lanzan granadas.
        if (MatchDebugConsole.IsInputCaptured || NetworkPlayerShop.IsInputCaptured)
            return;

        // [TECLAS] La tecla sale de Controles (se cambia en Opciones).
        int availableGrenades = network != null ? network.GrenadeCount : grenades;
        if (Keyboard.current[Controles.Tecla(Accion.Granada)].wasPressedThisFrame &&
            availableGrenades > 0)
        {
            // [MULTIJUGADOR] La dirección se toma acá, en la PC del jugador.
            // En red se le pide al servidor que ejecute ThrowGrenade.
            Vector3 direction = Camera.main.transform.forward;

            if (network != null)
                network.RequestThrow(direction);
            else
            {
                ThrowGrenade(direction);
                grenades--;
            }
        }
    }

 // [MULTIJUGADOR] Recibe la dirección (antes usaba Camera.main adentro) y es
 // public para que el servidor la pueda llamar.
 public void ThrowGrenade(Vector3 direction)
{
    if (grenadePrefab == null || throwPoint == null)
    {
        Debug.LogError("Asigna Grenade Prefab y Throw Point en el Inspector.");
        return;
    }

    GameObject grenade = Instantiate(
        grenadePrefab,
        throwPoint.position,
        throwPoint.rotation
    );

    // [MULTIJUGADOR] Spawn hace que la granada aparezca en todas las PCs.
    if (network != null)
        grenade.GetComponent<NetworkObject>().Spawn(true);

    Rigidbody rb = grenade.GetComponent<Rigidbody>();

    if (rb == null)
    {
        Debug.LogError("La granada necesita un Rigidbody.");
        Destroy(grenade);
        return;
    }

    rb.isKinematic = false;
    rb.useGravity = true;
    rb.linearVelocity = Vector3.zero;
    rb.angularVelocity = Vector3.zero;

    // [COMO HOY] Ahora usa upwardForce (antes no se usaba): la granada sale
    // un poco hacia arriba, como en el juego actual.
    Vector3 force = direction * throwForce + Vector3.up * upwardForce;

    rb.AddForce(force, ForceMode.Impulse);

    Collider grenadeCollider = grenade.GetComponent<Collider>();

    if (grenadeCollider != null)
    {
        Collider[] playerColliders = GetComponentsInChildren<Collider>();

        foreach (Collider playerCollider in playerColliders)
        {
            Physics.IgnoreCollision(grenadeCollider, playerCollider);
        }
    }

    GrenadeExplosion explosion = grenade.GetComponent<GrenadeExplosion>();

    if (explosion == null)
    {
        Debug.LogError("Falta GrenadeExplosion en el prefab.");
        Destroy(grenade);
        return;
    }

    // [MULTIJUGADOR] Quién la lanzó, para dar la baja y el dinero.
    explosion.attacker = GetComponent<NetworkPlayerCombat>();

    explosion.StartExplosion();
}
}
