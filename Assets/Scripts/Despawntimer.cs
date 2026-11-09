using Unity.Netcode;
using UnityEngine;

public class DespawnTimer : MonoBehaviour
{
    public float seconds = 10f;

    private NetworkObject networkObject;
    private float timer;

    private void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < seconds)
            return;

        enabled = false;

        NetworkManager manager = NetworkManager.Singleton;
        if (networkObject != null && networkObject.IsSpawned &&
            manager != null && manager.IsServer)
        {
            networkObject.Despawn(true); 
        }
        else
        {
            Destroy(gameObject);
        }
    }
}