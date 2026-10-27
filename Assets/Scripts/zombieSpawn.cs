using UnityEngine;

public class zombieSpawn : MonoBehaviour
{
    public GameObject zombiePrefab;
    public Transform zombieSpawnPosition;
    //public GameObject zonaDePeligro;
    private float repeatCycle = 1f;

    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.tag == "Player")
        {
            InvokeRepeating("EnemySpawner", 1f, repeatCycle);
            Destroy(gameObject, 6f);
            gameObject.GetComponent<BoxCollider>().enabled = false;
        }
    }
    void EnemySpawner()
    {
        Instantiate(zombiePrefab, zombieSpawnPosition.position, zombieSpawnPosition.rotation);
    }
}
