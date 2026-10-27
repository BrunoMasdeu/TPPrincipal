
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
public class HealthPickup : NetworkBehaviour
{
    [Header("Configuracion")]
    [SerializeField] private int cantidadVida = 25;
    [SerializeField] private float serverValidationDistance = 2.5f;

    [Header("Visual")]
    [SerializeField] private Transform visual;
    [SerializeField] private float rotationSpeed = 90f;

    private bool collected;

    private void Update()
    {
        if (visual != null)
            visual.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsSpawned || collected)
            return;

        NetworkObject playerObject =
            other.GetComponentInParent<NetworkObject>();

        if (playerObject == null || !playerObject.IsPlayerObject)
            return;

        if (IsServer)
        {
            TryCollectOnServer(
                playerObject,
                playerObject.OwnerClientId
            );
        }
        else if (playerObject.IsOwner)
        {
            RequestCollectionRpc(playerObject);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestCollectionRpc(
        NetworkObjectReference playerReference,
        RpcParams rpcParams = default)
    {
        if (!playerReference.TryGet(out NetworkObject playerObject))
            return;

        TryCollectOnServer(
            playerObject,
            rpcParams.Receive.SenderClientId
        );
    }

    private void TryCollectOnServer(
        NetworkObject playerObject,
        ulong requestingClientId)
    {
        if (!IsServer || collected || playerObject == null)
            return;

        if (!playerObject.IsPlayerObject ||
            playerObject.OwnerClientId != requestingClientId)
        {
            return;
        }

        if (Vector3.Distance(
                transform.position,
                playerObject.transform.position) >
            serverValidationDistance)
        {
            return;
        }

        NetworkPlayerHealth health =
            playerObject.GetComponent<NetworkPlayerHealth>();

        if (health == null || !health.Heal(cantidadVida))
            return;

        collected = true;
        NetworkObject.Despawn(true);
    }
}

