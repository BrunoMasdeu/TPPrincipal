using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class ZombieDrop : MonoBehaviour
{
    [Header("Objetos que puede soltar")]
    [Tooltip("Munición, botiquín y potenciador de velocidad. Se elige uno al azar.")]
    public GameObject[] dropPrefabs;

    [Header("Configuración")]
    [Tooltip("Altura sobre el suelo a la que aparece el objeto.")]
    public float alturaSueloObjeto = 0.5f;

    [Tooltip("Segundos que el objeto queda en el suelo antes de desaparecer si nadie lo agarra. 0 = no desaparece.")]
    [Min(0f)]
    public float duracionDrop = 10f;

    private bool alreadyDropped;

    public void Drop()
    {
        if (alreadyDropped)
            return;
        alreadyDropped = true;

        if (dropPrefabs == null || dropPrefabs.Length == 0)
            return;

        GameObject prefab = dropPrefabs[Random.Range(0, dropPrefabs.Length)];
        if (prefab == null)
        {
            Debug.LogWarning("ZombieDrop: hay un espacio vacío en la lista de drops.");
            return;
        }

        Vector3 position = transform.position + Vector3.up * alturaSueloObjeto;
        GameObject drop = Instantiate(prefab, position, Quaternion.identity);

        if (drop.TryGetComponent(out NetworkObject networkObject))
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsServer)
            {
                networkObject.Spawn();

                if (duracionDrop > 0f)
                    drop.AddComponent<DespawnTimer>().seconds = duracionDrop;
            }
            else
            {
                Debug.LogWarning("ZombieDrop: no hay servidor activo, el objeto no se puede spawnear.");
                Destroy(drop);
            }
        }
    }
}