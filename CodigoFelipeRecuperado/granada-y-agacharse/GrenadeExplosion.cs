using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrenadeExplosion : MonoBehaviour
{
    [Header("Explosion")]
    public float fuseTime = 3f;
    public float explosionRadius = 5f;
    public int damage = 100;
    public LayerMask damageLayers = ~0;

    private bool exploded;

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

            hit.SendMessageUpwards(
                "TakeDamage",
                damage,
                SendMessageOptions.DontRequireReceiver
            );
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
