using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Proyectil de granada simulado, explotado y destruido por el servidor.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public sealed class NetworkGrenadeProjectile : NetworkBehaviour
{
    [Header("Explosion")]
    [SerializeField, Min(0.05f)] private float fuseTime = 3f;
    [SerializeField, Min(0.1f)] private float explosionRadius = 5f;
    [SerializeField, Min(0)] private int damage = 100;
    [SerializeField] private LayerMask damageLayers = ~0;
    [SerializeField] private LayerMask obstructionLayers = ~0;

    private readonly HashSet<NetworkPlayerHealth> damagedPlayers = new();

    private Rigidbody body;
    private Collider grenadeCollider;
    private ulong attackerClientId;
    private double explodeAt;
    private bool initialized;
    private bool exploded;

    public float FuseTime => fuseTime;
    public float ExplosionRadius => explosionRadius;
    public int Damage => damage;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        grenadeCollider = GetComponent<Collider>();
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer || !initialized || exploded ||
            NetworkManager.ServerTime.Time < explodeAt)
            return;

        ExplodeOnServer();
    }

    public void InitializeOnServer(
        ulong ownerClientId,
        Vector3 initialVelocity,
        Collider[] ownerColliders)
    {
        if (!IsServer || !IsSpawned || initialized)
            return;

        attackerClientId = ownerClientId;
        initialized = true;
        explodeAt = NetworkManager.ServerTime.Time + fuseTime;

        if (body != null)
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = initialVelocity;
            body.angularVelocity = Vector3.zero;
        }

        if (grenadeCollider == null || ownerColliders == null)
            return;

        foreach (Collider ownerCollider in ownerColliders)
        {
            if (ownerCollider != null)
                Physics.IgnoreCollision(grenadeCollider, ownerCollider);
        }
    }

    private void ExplodeOnServer()
    {
        if (!IsServer || exploded)
            return;

        exploded = true;
        damagedPlayers.Clear();

        NetworkPlayerCombat attackerCombat = ResolveAttackerCombat();
        if (attackerCombat != null)
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position,
                explosionRadius,
                damageLayers,
                QueryTriggerInteraction.Ignore);

            foreach (Collider hit in hits)
            {
                NetworkPlayerHealth victim =
                    hit.GetComponentInParent<NetworkPlayerHealth>();

                if (victim == null ||
                    victim.OwnerClientId == attackerClientId ||
                    !damagedPlayers.Add(victim) ||
                    !HasLineOfSight(victim))
                    continue;

                attackerCombat.TryApplyDamageToPlayerOnServer(
                    victim,
                    damage,
                    out _,
                    out _);
            }
        }

        NetworkObject.Despawn(true);
    }

    private NetworkPlayerCombat ResolveAttackerCombat()
    {
        if (NetworkManager == null ||
            !NetworkManager.ConnectedClients.TryGetValue(
                attackerClientId,
                out NetworkClient client) ||
            client.PlayerObject == null)
            return null;

        return client.PlayerObject.GetComponent<NetworkPlayerCombat>();
    }

    private bool HasLineOfSight(NetworkPlayerHealth victim)
    {
        Collider victimCollider = victim.GetComponent<Collider>();
        Vector3 target = victimCollider != null
            ? victimCollider.bounds.center
            : victim.transform.position;
        Vector3 offset = target - transform.position;
        float distance = offset.magnitude;

        if (distance <= Mathf.Epsilon)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(
            transform.position,
            offset / distance,
            distance,
            obstructionLayers,
            QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (left, right) =>
            left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == grenadeCollider ||
                hit.collider.transform.IsChildOf(transform))
                continue;

            return hit.collider.GetComponentInParent<NetworkPlayerHealth>() ==
                victim;
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
