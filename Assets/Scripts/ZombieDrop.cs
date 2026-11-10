using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class ZombieDrop : MonoBehaviour
{
    [Header("Objetos que puede soltar")]
    [Tooltip("Munición, botiquín y potenciador de velocidad.")]
    public GameObject[] dropPrefabs;

    [Header("Configuración")]
    [Range(0f, 1f)] public float dropChance = 1f;
    [Tooltip("Altura sobre el suelo a la que aparece el objeto.")]
    [Min(0f)] public float spawnHeight = 0.5f;

    private bool alreadyDropped;

    public void Drop()
    {
        if (alreadyDropped)
            return;
        alreadyDropped = true;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer)
            return;

        WaveManager waveManager = WaveManager.ActiveInstance;
        GameObject[] candidates = waveManager != null &&
            waveManager.dropPrefabs != null && waveManager.dropPrefabs.Length > 0
            ? waveManager.dropPrefabs
            : dropPrefabs;
        float chance = waveManager != null ? waveManager.dropChance : dropChance;
        if (candidates == null || candidates.Length == 0 ||
            Random.value > Mathf.Clamp01(chance))
            return;

        GameObject prefab = candidates[Random.Range(0, candidates.Length)];
        if (prefab == null)
        {
            Debug.LogWarning("ZombieDrop: hay un espacio vacío en la lista de drops.");
            return;
        }

        Vector3 position = transform.position + Vector3.up * spawnHeight;
        GameObject drop = Instantiate(prefab, position, Quaternion.identity);

        if (drop.TryGetComponent(out NetworkObject networkObject))
        {
            networkObject.Spawn();
        }
    }
}
