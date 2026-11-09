using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
public class AmmoPickup : NetworkBehaviour
{
    [SerializeField] private float serverValidationDistance = 2.5f;
    [SerializeField] private Transform visual;
    [SerializeField] private float rotationSpeed = 100f;

    private bool collectRequested;

    private void Update()
    {
        if (IsSpawned && visual != null)
            visual.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsSpawned || collectRequested)
            return;

        NetworkObject playerObject = other.GetComponentInParent<NetworkObject>();

        if (playerObject == null || !playerObject.IsPlayerObject || !playerObject.IsOwner)
            return;

        if (!RefillAllWeapons(playerObject))
            return;

        collectRequested = true;

        if (IsServer)
            DespawnOnServer();
        else
            RequestDespawnRpc(playerObject);
    }

    private static bool RefillAllWeapons(NetworkObject playerObject)
    {
        bool anyAmmoWasSpent = false;

        GunSystem[] guns = playerObject.GetComponentsInChildren<GunSystem>(true);
        foreach (GunSystem gun in guns)
        {
            if (gun == null)
                continue;

            int before = gun.BulletsLeft + gun.ExtraBulletsLeft;
            gun.Refill();
            int after = gun.BulletsLeft + gun.ExtraBulletsLeft;

            if (after > before)
                anyAmmoWasSpent = true;
        }

        return anyAmmoWasSpent;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestDespawnRpc(
        NetworkObjectReference playerReference,
        RpcParams rpcParams = default)
    {
        if (!IsSpawned || !playerReference.TryGet(out NetworkObject playerObject))
            return;

        if (!playerObject.IsPlayerObject ||
            playerObject.OwnerClientId != rpcParams.Receive.SenderClientId)
            return;

        if (Vector3.Distance(transform.position, playerObject.transform.position) >
            serverValidationDistance)
            return;

        DespawnOnServer();
    }

    private void DespawnOnServer()
    {
        if (!IsServer || !IsSpawned)
            return;

        NetworkObject.Despawn(true);
    }
}