using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GrenadeExplosion : MonoBehaviour
{
    [Header("Explosion")]
    public float fuseTime = 3f;
    public float explosionRadius = 5f;
    public int damage = 100;
    public LayerMask damageLayers = ~0;

    private bool exploded;

    // [MULTIJUGADOR] Quién la lanzó. Lo completa Grenade.ThrowGrenade. Sirve
    // para que el servidor le dé la baja y el dinero.
    [HideInInspector] public NetworkPlayerCombat attacker;

    public void StartExplosion()
    {
        StartCoroutine(ExplodeAfterDelay());
    }

    private IEnumerator ExplodeAfterDelay()
    {
        yield return new WaitForSeconds(fuseTime);
        Explode();
    }

    private void Explode()
    {
        if (exploded)
            return;

        exploded = true;

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            explosionRadius,
            damageLayers,
            QueryTriggerInteraction.Ignore
        );

        HashSet<Transform> damagedTargets = new HashSet<Transform>();

        foreach (Collider hit in hits)
        {
            Transform target = hit.transform.root;

            if (target == transform.root)
                continue;

            if (!damagedTargets.Add(target))
                continue;

            // [COMO HOY] No daña a quien está detrás de una pared.
            if (!HasLineOfSight(hit))
                continue;

            // [MULTIJUGADOR] A los jugadores les resta vida el servidor, a
            // través del combate de quien lanzó la granada.
            NetworkPlayerHealth player = hit.GetComponentInParent<NetworkPlayerHealth>();
            if (player != null)
            {
                if (attacker != null)
                    attacker.TryApplyDamageToPlayerOnServer(player, damage, out _, out _);
                continue;
            }

            hit.SendMessageUpwards(
                "TakeDamage",
                damage,
                SendMessageOptions.DontRequireReceiver
            );
        }

        // [MULTIJUGADOR] Una granada de red se quita con Despawn (desaparece en
        // todas las PCs). Sin red, Destroy como antes.
        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
            networkObject.Despawn();
        else
            Destroy(gameObject);
    }

    // [COMO HOY] Rayo desde la granada hasta el objetivo: si lo primero que
    // toca es otra cosa (una pared), el objetivo está cubierto.
    private bool HasLineOfSight(Collider target)
    {
        Vector3 toTarget = target.bounds.center - transform.position;

        if (Physics.Raycast(
            transform.position,
            toTarget.normalized,
            out RaycastHit firstHit,
            toTarget.magnitude,
            damageLayers,
            QueryTriggerInteraction.Ignore))
        {
            return firstHit.collider.transform.root == target.transform.root;
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
