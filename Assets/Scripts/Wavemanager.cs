using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class WaveManager : MonoBehaviour
{
    [System.Serializable]
    public class Wave
    {
        public int zombieCount;
        public int specialZombieCount;
        public float spawnDelay;
        public float zombieHealth;
    }

    [Header("Referencias de la escena")]
    public GameObject zombiePrefab;          // zombie normal
    public GameObject specialZombiePrefab;   // zombie especial (Zombie1)
    public Transform[] spawnPoints;
    public Transform playerBody;
    public GameObject[] walkPoints;   // opcional

    [Header("Oleadas")]
    public Wave[] waves = new Wave[]
    {
        new Wave { zombieCount = 10, specialZombieCount = 1, spawnDelay = 2.0f, zombieHealth = 150f },
        new Wave { zombieCount = 20, specialZombieCount = 3, spawnDelay = 1.0f, zombieHealth = 250f },
        new Wave { zombieCount = 30, specialZombieCount = 5, spawnDelay = 0.5f, zombieHealth = 350f },
    };

    [Header("Pruebas (sin armas)")]
    [Tooltip("Si es mayor que 0, cada zombie muere solo a los N segundos de aparecer. Poner 0 para desactivar.")]
    public float testAutoKillSeconds = 3f;

    [Header("Configuración")]
    public bool autoStart = true;
    public float timeBetweenWaves = 10f;
    public float firstWaveDelay = 5f;

    private int currentWave = 0;
    private int zombiesAlive = 0;
    private int zombiesRemaining = 0;
    private bool started = false;
    private string hudMessage = "";

    public static bool IsLoaded =>
        FindAnyObjectByType<WaveManager>(FindObjectsInactive.Include) != null;

    private void Start()
    {
        if (autoStart) StartWaves();
    }

    public void StartWaves()
    {
        if (started) return;
        started = true;
        StartCoroutine(WaveRoutine());
    }

    private IEnumerator WaveRoutine()
    {
        yield return new WaitForSeconds(firstWaveDelay);

        while (playerBody == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerBody = p.transform;
            else yield return new WaitForSeconds(0.5f);
        }

        for (int i = 0; i < waves.Length; i++)
        {
            currentWave = i + 1;
            Wave wave = waves[i];

            zombiesRemaining = wave.zombieCount;

            hudMessage = "Oleada " + currentWave;
            yield return new WaitForSeconds(2f);

            List<bool> order = BuildSpawnOrder(wave);
            foreach (bool special in order)
            {
                if (!SpawnZombie(special ? specialZombiePrefab : zombiePrefab, wave.zombieHealth))
                    zombiesRemaining--;
                yield return new WaitForSeconds(wave.spawnDelay);
            }

            while (zombiesRemaining > 0)
                yield return null;

            if (i < waves.Length - 1)
            {
                hudMessage = "Oleada " + currentWave + " completada";
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        hudMessage = "¡Sobreviviste a todas las oleadas!";
    }

    private List<bool> BuildSpawnOrder(Wave wave)
    {
        int special = Mathf.Clamp(wave.specialZombieCount, 0, wave.zombieCount);
        if (specialZombiePrefab == null && special > 0)
        {
            Debug.LogWarning("WaveManager: falta Special Zombie Prefab, se usará el zombie normal.");
            special = 0;
        }

        List<bool> order = new List<bool>();
        for (int i = 0; i < wave.zombieCount; i++)
            order.Add(i < special);

        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            bool tmp = order[i];
            order[i] = order[j];
            order[j] = tmp;
        }
        return order;
    }

    private bool SpawnZombie(GameObject prefab, float health)
    {
        if (prefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("WaveManager: falta el prefab o los spawnPoints.");
            return false;
        }

        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (sp == null)
        {
            Debug.LogError("WaveManager: hay un spawnPoint vacío en la lista.");
            return false;
        }

        Vector3 pos = sp.position;
        if (NavMesh.SamplePosition(sp.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            pos = hit.position;

        GameObject obj = Instantiate(prefab, pos, sp.rotation);
        Zombie zombie = obj.GetComponent<Zombie>();
        if (zombie == null)
        {
            Debug.LogError("WaveManager: el prefab '" + prefab.name + "' no tiene el script Zombie en el objeto raíz.");
            Destroy(obj);
            return false;
        }

        zombiesAlive++;
        string tipo = prefab == specialZombiePrefab ? "ESPECIAL" : "NORMAL";
        Debug.Log($"[Oleada {currentWave}] zombie {tipo} ({prefab.name}) spawneado por WaveManager. Vivos: {zombiesAlive}");
        zombie.Initialize(playerBody, walkPoints, health, OnZombieDied);

        if (testAutoKillSeconds > 0f)
            StartCoroutine(AutoKill(zombie, testAutoKillSeconds));

        return true;
    }

    private IEnumerator AutoKill(Zombie zombie, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (zombie != null)
            zombie.zombieHitDamage(float.MaxValue);
    }

    private void OnZombieDied()
    {
        zombiesAlive--;
        zombiesRemaining--;
    }

    private void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.red;
        GUI.Label(new Rect(20, 20, 600, 40), hudMessage, style);
        GUI.Label(new Rect(20, 60, 600, 40), "Zombies restantes: " + zombiesRemaining, style);
    }
}