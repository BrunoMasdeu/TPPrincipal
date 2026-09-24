using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
public class SpeedBoostPickup : NetworkBehaviour
{
    [SerializeField] private float speedMultiplier = 1.5f;
    [SerializeField] private float durationSeconds = 8f;
    [SerializeField] private float serverValidationDistance = 2.5f;
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
            TryCollectOnServer(playerObject, playerObject.OwnerClientId);
        }
        else if (playerObject.IsOwner)
        {
            RequestCollectionRpc(playerObject);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestCollectionRpc(
        NetworkObjectReference playerReference,
        RpcParams rpcParams = default
    )
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
        ulong requestingClientId
    )
    {
        if (!IsServer || collected || playerObject == null)
            return;

        if (!playerObject.IsPlayerObject ||
            playerObject.OwnerClientId != requestingClientId)
        {
            return;
        }

        if (Vector3.Distance(transform.position, playerObject.transform.position) >
            serverValidationDistance)
        {
            return;
        }

        SpeedBoostController boostController =
            playerObject.GetComponent<SpeedBoostController>();

        if (boostController == null ||
            !boostController.TryApplyBoostOnServer(
                speedMultiplier,
                durationSeconds
            ))
        {
            return;
        }

        // El servidor procesa las solicitudes de forma secuencial. Marcarlo
        // antes de despawnear garantiza un unico ganador si llegan a la vez.
        collected = true;
        NetworkObject.Despawn(true);
    }
}
