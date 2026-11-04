using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class WaveManager : MonoBehaviour
{
    [System.Serializable]
    public class Wave
    {
        public int zombieCount;     // cuántos zombies tiene la oleada
        public float spawnDelay;    // segundos entre cada zombie (menor = más rápido)
        public float zombieHealth;  // vida de cada zombie en esta oleada
    }

    [Header("Referencias de la escena")]
    public GameObject zombiePrefab;
    public Transform[] spawnPoints;
    public Transform playerBody;
    public GameObject[] walkPoints;   // opcional

    [Header("Oleadas")]
    public Wave[] waves = new Wave[]
    {
        new Wave { zombieCount = 10, spawnDelay = 2.0f, zombieHealth = 150f },
        new Wave { zombieCount = 20, spawnDelay = 1.0f, zombieHealth = 250f },
        new Wave { zombieCount = 30, spawnDelay = 0.5f, zombieHealth = 350f },
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
    private bool started = false;
    private string hudMessage = "";

    // Lo usa NetworkPlayerCombat para saber si esta escena es la de zombies
    public static bool IsLoaded =>
        FindAnyObjectByType<WaveManager>(FindObjectsInactive.Include) != null;

    private void Start()
    {
        if (autoStart) StartWaves();
    }

    // Puedes llamarlo desde un trigger (por ejemplo tu zombieSpawn actual)
    public void StartWaves()
    {
        if (started) return;
        started = true;
        StartCoroutine(WaveRoutine());
    }

    private IEnumerator WaveRoutine()
    {
        yield return new WaitForSeconds(firstWaveDelay);

        // El jugador se crea en runtime (Netcode), así que se busca por tag
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

            hudMessage = "Oleada " + currentWave;
            yield return new WaitForSeconds(2f);

            for (int z = 0; z < wave.zombieCount; z++)
            {
                SpawnZombie(wave.zombieHealth);
                yield return new WaitForSeconds(wave.spawnDelay);
            }

            // Esperar a que mueran todos los zombies de la oleada
            while (zombiesAlive > 0)
                yield return null;

            if (i < waves.Length - 1)
            {
                hudMessage = "Oleada " + currentWave + " completada";
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        hudMessage = "¡Sobreviviste a todas las oleadas!";
    }

    private void SpawnZombie(float health)
    {
        // Spawn aleatorio
        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];

        // Asegurar que aparezca sobre el NavMesh
        Vector3 pos = sp.position;
        if (NavMesh.SamplePosition(sp.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            pos = hit.position;

        GameObject obj = Instantiate(zombiePrefab, pos, sp.rotation);
        Zombie zombie = obj.GetComponent<Zombie>();

        zombiesAlive++;
        Debug.Log($"[Oleada {currentWave}] zombie spawneado por WaveManager. Vivos: {zombiesAlive}");
        zombie.Initialize(playerBody, walkPoints, health, OnZombieDied);

        if (testAutoKillSeconds > 0f)
            StartCoroutine(AutoKill(zombie, testAutoKillSeconds));
    }

    // Solo para probar las oleadas: mata al zombie pasado un tiempo
    private IEnumerator AutoKill(Zombie zombie, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (zombie != null)
            zombie.zombieHitDamage(float.MaxValue);
    }

    private void OnZombieDied()
    {
        zombiesAlive--;
    }

    private void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.red;
        GUI.Label(new Rect(20, 20, 600, 40), hudMessage, style);
        GUI.Label(new Rect(20, 60, 600, 40), "Zombies restantes: " + zombiesAlive, style);
    }
}