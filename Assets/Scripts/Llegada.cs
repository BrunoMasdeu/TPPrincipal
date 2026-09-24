using Unity.Netcode;
using UnityEngine;

public class Llegada : MonoBehaviour
{
    [Header("Conexión con el sistema")]
    public GameManager gameManager;

    private void OnTriggerEnter(Collider other)
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsServer || gameManager == null)
            return;

        NetworkObject playerNetworkObject =
            other.GetComponentInParent<NetworkObject>();

        if (playerNetworkObject == null || !playerNetworkObject.IsPlayerObject)
            return;

        gameManager.RegistrarLlegada(playerNetworkObject.OwnerClientId);
    }
}
