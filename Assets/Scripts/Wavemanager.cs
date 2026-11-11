using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

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
    public GameObject zombiePrefab;
    public GameObject specialZombiePrefab;
    public Transform[] spawnPoints;
    public Transform playerBody;
    public GameObject[] walkPoints;

    [Header("Oleadas")]
    public Wave[] waves = new Wave[]
    {
        new Wave { zombieCount = 10, specialZombieCount = 1, spawnDelay = 2.0f, zombieHealth = 150f },
        new Wave { zombieCount = 20, specialZombieCount = 3, spawnDelay = 1.0f, zombieHealth = 250f },
        new Wave { zombieCount = 30, specialZombieCount = 5, spawnDelay = 0.5f, zombieHealth = 350f },
    };

    [Header("Dificultad (multiplicador de daño)")]
    public float easyDamageMultiplier = 1f;
    public float normalDamageMultiplier = 2f;
    public float hardDamageMultiplier = 3f;

    [Header("Economía")]
    public int moneyPerKill = 100;

    [Header("Drops")]
    public GameObject[] dropPrefabs = System.Array.Empty<GameObject>();
    [Range(0f, 1f)] public float dropChance = 1f;

    // [ZOMBIES] Modelo de la caja de armas que suelta el último zombie de cada oleada.
    // Se le agrega el script CajaArmas al crearla. Si queda vacío, no se suelta caja.
    [Header("Caja de armas")]
    public GameObject cajaArmasModelo;

    // [ZOMBIES] Sonido cuando los zombies te matan.
    [Header("Sonidos")]
    public AudioClip sonidoMuerteJugador;
    [Range(0f, 1f)] public float volumenMuerteJugador = 1f;
    // Precio de la caja según la oleada que la soltó (1ra, 2da...). Si hay más
    // oleadas que precios, se usa el último.
    public int[] preciosCaja = { 800, 1500 };
    // Las cajas que no se compran se acumulan.
    private readonly List<GameObject> cajas = new List<GameObject>();

    // [ZOMBIES] Música, ambiente y sonidos de la caja (componente en este mismo objeto).
    private SonidosOleadas sonidosOleadas;

    private NetworkPlayerCombat playerCombat;

    private bool choosingDifficulty = false;
    private float damageMultiplier = 1f;
    private string difficultyName = "";
    public static bool IsActive => instance != null;
    public static WaveManager ActiveInstance => instance;

    [Header("Pruebas (sin armas)")]
    [Tooltip("Si es mayor que 0, cada zombie muere solo a los N segundos de aparecer. Poner 0 para desactivar.")]
    public float testAutoKillSeconds = 0f;

    [Header("Configuración")]
    public bool autoStart = true;
    public float timeBetweenWaves = 10f;
    public float firstWaveDelay = 5f;

    [Header("Game Over")]
    [Tooltip("Nombre exacto de la escena del menú (debe estar en Build Settings).")]
    public string menuSceneName = "MenuScene";

    private int currentWave = 0;
    private int zombiesAlive = 0;
    private int zombiesRemaining = 0;
    private bool started = false;
    private bool gameOver = false;
    private bool gameWon = false;
    private string hudMessage = "";
    private static WaveManager instance;
    private static int queuedMenuDifficulty = -1;

    public static void QueueMenuDifficulty(int difficulty)
    {
        queuedMenuDifficulty = Mathf.Clamp(difficulty, 0, 2);
    }

    public static void ClearQueuedMenuDifficulty()
    {
        queuedMenuDifficulty = -1;
    }

    private Move playerMove;
    private readonly List<Zombie> spawnedZombies = new List<Zombie>();

    // Lo usa NetworkPlayerCombat para saber si esta escena es la de zombies
    public static bool IsLoaded =>
        FindAnyObjectByType<WaveManager>(FindObjectsInactive.Include) != null;

    public static bool IsInputCaptured =>
        instance != null &&
        (instance.choosingDifficulty || instance.gameOver || instance.gameWon);

    private void OnEnable()
    {
        instance = this;
        Move.PlayerDied += OnPlayerDied;
    }

    private void OnDisable()
    {
        if (instance == this) instance = null;
        Move.PlayerDied -= OnPlayerDied;
    }

    private void Start()
    {
        sonidosOleadas = GetComponent<SonidosOleadas>(); // [ZOMBIES]
        if (autoStart) StartCoroutine(ShowMenuNextFrame());
    }

    private IEnumerator ShowMenuNextFrame()
    {
        yield return null;
        int difficulty = queuedMenuDifficulty;
        ClearQueuedMenuDifficulty();

        switch (difficulty)
        {
            case 0:
                SelectDifficulty("Fácil", easyDamageMultiplier);
                break;
            case 1:
                SelectDifficulty("Medio", normalDamageMultiplier);
                break;
            case 2:
                SelectDifficulty("Difícil", hardDamageMultiplier);
                break;
            default:
                ShowDifficultyMenu();
                break;
        }
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

            zombiesRemaining = wave.zombieCount;

            hudMessage = "Oleada " + currentWave;
            if (sonidosOleadas != null) sonidosOleadas.EmpiezaOleada(currentWave); // [ZOMBIES]
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

            if (sonidosOleadas != null) sonidosOleadas.TerminaOleada(); // [ZOMBIES] fade out

            if (i < waves.Length - 1)
            {
                hudMessage = "Oleada " + currentWave + " completada";
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        hudMessage = "¡Sobreviviste a todas las oleadas!";
        gameWon = true;
        started = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
        spawnedZombies.Add(zombie);
        string tipo = prefab == specialZombiePrefab ? "ESPECIAL" : "NORMAL";
        Debug.Log($"[Oleada {currentWave}] zombie {tipo} ({prefab.name}) spawneado por WaveManager. Vivos: {zombiesAlive}");
        zombie.Initialize(playerBody, walkPoints, health, () => OnZombieDied(zombie)); // [ZOMBIES] pasa qué zombie murió
        zombie.giveDamage *= damageMultiplier;

        if (testAutoKillSeconds > 0f)
            StartCoroutine(AutoKill(zombie, testAutoKillSeconds));

        return true;
    }

    // [ZOMBIES] Crea la caja de armas en el piso, donde murió el zombie.
    private void SoltarCajaArmas(Vector3 posicion)
    {
        if (cajaArmasModelo == null)
        {
            Debug.LogWarning("WaveManager: falta asignar 'Caja Armas Modelo' en Regla, no se suelta caja.");
            return;
        }

        if (NavMesh.SamplePosition(posicion, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            posicion = hit.position;

        GameObject caja = Instantiate(cajaArmasModelo, posicion, Quaternion.identity);
        caja.name = "CajaArmas (oleada " + currentWave + ")";
        CajaArmas compra = caja.AddComponent<CajaArmas>();
        if (sonidosOleadas != null) sonidosOleadas.ConfigurarCaja(compra); // [ZOMBIES] antes de su Start
        if (preciosCaja != null && preciosCaja.Length > 0)
            compra.precio = preciosCaja[Mathf.Clamp(currentWave - 1, 0, preciosCaja.Length - 1)];

        cajas.RemoveAll(c => c == null);
        cajas.Add(caja);
    }

    private IEnumerator AutoKill(Zombie zombie, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (zombie != null)
            zombie.zombieHitDamage(float.MaxValue);
    }

    private void OnZombieDied(Zombie zombie)
    {
        zombiesAlive--;
        zombiesRemaining--;

        // [ZOMBIES] El último zombie de la oleada suelta la caja de armas,
        // menos en la última oleada (ahí ya ganaste).
        // Ese zombie suelta solo la caja, no un objeto además.
        if (zombiesRemaining == 0 && currentWave < waves.Length && zombie != null &&
            cajaArmasModelo != null)
        {
            zombie.GetComponent<ZombieDrop>()?.CancelarDrop();
            SoltarCajaArmas(zombie.transform.position);
        }

        NetworkPlayerCombat combat = GetPlayerCombat();
        if (combat != null)
            combat.AddMoneyOnServer(moneyPerKill);
    }

    private void OnPlayerDied(Move player)
    {
        if (gameOver) return;
        gameOver = true;
        playerMove = player;

        if (sonidosOleadas != null) sonidosOleadas.FinPartida(); // [ZOMBIES]

        // [ZOMBIES] Sonido de que te comen.
        if (sonidoMuerteJugador != null)
        {
            Vector3 donde = Camera.main != null ? Camera.main.transform.position : player.transform.position;
            AudioSource.PlayClipAtPoint(sonidoMuerteJugador, donde, volumenMuerteJugador);
        }

        StopAllCoroutines();
        started = false;

        // Elimina todos los zombies
        foreach (Zombie z in spawnedZombies)
        {
            if (z != null) Destroy(z.gameObject);
        }
        spawnedZombies.Clear();
        zombiesAlive = 0;
        zombiesRemaining = 0;

        // [ZOMBIES] Si quedaron cajas sin comprar (o una abriéndose), se sacan.
        foreach (GameObject caja in cajas)
            if (caja != null) Destroy(caja);
        cajas.Clear();
        RuletaArmas.Cerrar();

        hudMessage = "Has muerto";

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestartGame()
    {
        gameOver = false;
        gameWon = false;
        currentWave = 0;
        zombiesAlive = 0;
        zombiesRemaining = 0;
        hudMessage = "";

        if (playerMove == null && playerBody != null)
            playerMove = playerBody.GetComponentInParent<Move>();

        if (playerMove != null)
            playerMove.Respawn();

        NetworkPlayerCombat combat = GetPlayerCombat();
        if (combat != null)
        {
            combat.ResetMoneyOnServer();
            combat.RefillAllWeaponsOnServer();
        }
        NetworkPlayerInventory loadout = playerBody != null
            ? playerBody.GetComponentInParent<NetworkPlayerInventory>()
            : null;
        if (loadout != null)
            loadout.ResetLoadoutOnServer();

        ShowDifficultyMenu();
    }

    private void GoToMenu()
    {
        ScenePauseMenu pauseMenu = FindAnyObjectByType<ScenePauseMenu>();
        if (pauseMenu != null)
        {
            pauseMenu.ReturnToMainMenu();
            return;
        }

        StartCoroutine(ReturnToMenuWithoutPauseMenu());
    }

    private IEnumerator ReturnToMenuWithoutPauseMenu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            if (networkManager.IsListening)
                networkManager.Shutdown();
            Destroy(networkManager.gameObject);
            yield return null;
        }

        SceneManager.LoadScene(menuSceneName);
    }


    private void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.red;
        GUI.Label(new Rect(20, 20, 600, 40), hudMessage, style);

        if (choosingDifficulty)
        {
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = Color.white;

            GUIStyle diffButton = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };

            float bw = 300f, bh = 60f, gap = 15f;
            float bx = (Screen.width - bw) / 2f;
            float by = (Screen.height - (bh * 3 + gap * 2)) / 2f;

            GUI.Label(new Rect(bx - 100, by - 60, bw + 200, 50), "Elige la dificultad", titleStyle);

            if (GUI.Button(new Rect(bx, by, bw, bh), "Fácil", diffButton))
                SelectDifficulty("Fácil", easyDamageMultiplier);
            if (GUI.Button(new Rect(bx, by + bh + gap, bw, bh), "Normal", diffButton))
                SelectDifficulty("Normal", normalDamageMultiplier);
            if (GUI.Button(new Rect(bx, by + (bh + gap) * 2, bw, bh), "Difícil", diffButton))
                SelectDifficulty("Difícil", hardDamageMultiplier);

            return;
        }

        if (!gameOver && !gameWon)
        {
            GUI.Label(new Rect(20, 60, 600, 40), "Zombies restantes: " + zombiesRemaining, style);
            GUI.Label(new Rect(20, 100, 600, 40), "Dificultad: " + difficultyName, style);

            NetworkPlayerCombat combat = GetPlayerCombat();

            GUIStyle moneyStyle = new GUIStyle(style);
            moneyStyle.normal.textColor = Color.green;
            GUI.Label(new Rect(20, 140, 600, 40),
                "Dinero: $" + (combat != null ? combat.Money : 0), moneyStyle);
            return;
        }

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };

        float w = 300f, h = 60f, spacing = 15f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - (h * 2 + spacing)) / 2f;

        if (GUI.Button(new Rect(x, y, w, h), "Reiniciar la partida", buttonStyle))
            RestartGame();

        if (GUI.Button(new Rect(x, y + h + spacing, w, h), "Volver al menú", buttonStyle))
            GoToMenu();
    }
    private void ShowDifficultyMenu()
    {
        choosingDifficulty = true;
        hudMessage = "";

        if (playerMove == null)
            playerMove = FindAnyObjectByType<Move>();

        if (playerMove != null)
            playerMove.SetKeyboardInputEnabled(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SelectDifficulty(string name, float multiplier)
    {
        difficultyName = name;
        damageMultiplier = multiplier;
        choosingDifficulty = false;

        if (playerMove != null)
            playerMove.SetKeyboardInputEnabled(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        StartWaves();
    }
    private NetworkPlayerCombat GetPlayerCombat()
    {
        if (playerCombat == null && playerBody != null)
            playerCombat = playerBody.GetComponentInParent<NetworkPlayerCombat>();

        return playerCombat;
    }
}
