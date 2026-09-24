using Unity.Netcode;
using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        PlayerNetworkSetup networkPlayer = other.GetComponentInParent<PlayerNetworkSetup>();
        if (networkPlayer != null)
        {
            networkPlayer.GuardarCheckpoint(transform.position, transform.rotation);
        }
    }
}