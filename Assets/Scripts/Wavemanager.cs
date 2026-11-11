using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using TMPro;

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

    // [ZOMBIES] Fuentes de la interfaz del modo zombies.
    [Header("Interfaz")]
    public Font fuenteTitulos;
    public Font fuenteTexto;

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
            anuncioDesde = Time.time; // [ZOMBIES] anuncio grande "RONDA N"
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


    // =====================================================================
    // [ZOMBIES] Interfaz con estética de terror
    // Títulos con fuenteTitulos (Frightmare), textos y números con fuenteTexto
    // (Special Elite). Frightmare no tiene signos ($ ! ¡ ? :), por eso todo lo
    // que lleva signos va con la fuente de texto.
    // =====================================================================

    private static readonly Color RojoSangre = new Color(0.62f, 0.03f, 0.03f);
    private static readonly Color BlancoHueso = new Color(0.91f, 0.87f, 0.78f);
    private static readonly Color Dorado = new Color(0.95f, 0.75f, 0.25f);
    private static readonly Color Celeste = new Color(0.35f, 0.95f, 0.95f); // potenciador de velocidad

    // Segundos que se ve el anuncio grande "RONDA N" en el centro.
    private const float DuracionAnuncio = 3f;
    private float anuncioDesde = -100f;

    private GUIStyle estiloTitulo;
    private GUIStyle estiloTexto;
    private GUIStyle estiloBoton;
    private Texture2D fondoBoton;
    private Texture2D fondoBotonEncima;
    private float escalaEstilos = -1f;

    private void OnGUI()
    {
        PrepararEstilos();
        float e = Screen.height / 1080f; // todo escala con la resolución

        if (choosingDifficulty)
        {
            float bw = 340f * e, bh = 64f * e, gap = 16f * e;
            float bx = (Screen.width - bw) / 2f;
            float by = (Screen.height - (bh * 3 + gap * 2)) / 2f;

            Escribir(new Rect(0, by - 130f * e, Screen.width, 110f * e), "ELIGE LA DIFICULTAD",
                estiloTitulo, 64f * e, RojoSangre, TextAnchor.MiddleCenter);

            if (GUI.Button(new Rect(bx, by, bw, bh), "Fácil", estiloBoton))
                SelectDifficulty("Fácil", easyDamageMultiplier);
            if (GUI.Button(new Rect(bx, by + bh + gap, bw, bh), "Normal", estiloBoton))
                SelectDifficulty("Normal", normalDamageMultiplier);
            if (GUI.Button(new Rect(bx, by + (bh + gap) * 2, bw, bh), "Difícil", estiloBoton))
                SelectDifficulty("Difícil", hardDamageMultiplier);
            return;
        }

        if (!gameOver && !gameWon)
        {
            // Arriba al centro: ronda y zombies que faltan.
            if (currentWave > 0)
            {
                Escribir(new Rect(0, 10f * e, Screen.width, 80f * e), "RONDA " + currentWave,
                    estiloTitulo, 60f * e, RojoSangre, TextAnchor.MiddleCenter);
                Escribir(new Rect(0, 82f * e, Screen.width, 40f * e), "ZOMBIES: " + zombiesRemaining,
                    estiloTexto, 28f * e, BlancoHueso, TextAnchor.MiddleCenter);
            }

            // Arriba a la izquierda, uno debajo del otro: dinero, dificultad y FPS.
            // Cada renglón baja según el tamaño del anterior, así nunca se pisan.
            NetworkPlayerCombat combat = GetPlayerCombat();
            float renglon = 16f * e;
            renglon = Renglon(renglon, "$" + (combat != null ? combat.Money : 0), 40f * e, Dorado);
            renglon = Renglon(renglon, "Dificultad: " + difficultyName, 22f * e, BlancoHueso);

            // Los FPS los sigue calculando el contador del HUD (respeta "Mostrar FPS"
            // del menú); acá solo se muestran en su lugar y con la fuente del modo.
            if (fpsTexto != null && fpsTexto.enabled)
                renglon = Renglon(renglon, fpsTexto.text, 20f * e, new Color(BlancoHueso.r, BlancoHueso.g, BlancoHueso.b, 0.75f));

            // Potenciador de velocidad activo: lo mismo que mostraba su cartel
            // (multiplicador y segundos que quedan), en la misma columna.
            SpeedBoostController boost = combat != null ? combat.GetComponent<SpeedBoostController>() : null;
            if (boost != null && boost.RemainingSeconds > 0f)
            {
                renglon += 6f * e;
                renglon = Renglon(renglon, $"VELOCIDAD x{boost.ActiveMultiplier:0.0}", 26f * e, Celeste);
                Renglon(renglon, $"{boost.RemainingSeconds:0.0} s", 22f * e, Celeste);
            }

            // Centro: anuncio grande al empezar la ronda (se desvanece) o ronda completada.
            float pasado = Time.time - anuncioDesde;
            if (pasado < DuracionAnuncio)
            {
                float alfa = Mathf.Clamp01((DuracionAnuncio - pasado) / 1f) * Mathf.Clamp01(pasado / 0.3f);
                Color color = RojoSangre;
                color.a = alfa;
                Escribir(new Rect(0, Screen.height * 0.28f, Screen.width, 160f * e), "RONDA " + currentWave,
                    estiloTitulo, 140f * e, color, TextAnchor.MiddleCenter);
            }
            else if (hudMessage.EndsWith("completada"))
            {
                Escribir(new Rect(0, Screen.height * 0.3f, Screen.width, 100f * e),
                    "RONDA " + currentWave + " COMPLETADA",
                    estiloTitulo, 72f * e, RojoSangre, TextAnchor.MiddleCenter);
            }
            return;
        }

        // Fin de la partida: título grande y botones.
        float w = 340f * e, h = 64f * e, spacing = 16f * e;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - (h * 2 + spacing)) / 2f;

        if (gameWon)
        {
            Escribir(new Rect(0, y - 200f * e, Screen.width, 130f * e), "SOBREVIVISTE",
                estiloTitulo, 110f * e, RojoSangre, TextAnchor.MiddleCenter);
            Escribir(new Rect(0, y - 80f * e, Screen.width, 50f * e), "a todas las oleadas",
                estiloTexto, 30f * e, BlancoHueso, TextAnchor.MiddleCenter);
        }
        else
        {
            Escribir(new Rect(0, y - 180f * e, Screen.width, 140f * e), "HAS MUERTO",
                estiloTitulo, 120f * e, RojoSangre, TextAnchor.MiddleCenter);
        }

        if (GUI.Button(new Rect(x, y, w, h), "Reiniciar la partida", estiloBoton))
            RestartGame();

        if (GUI.Button(new Rect(x, y + h + spacing, w, h), "Volver al menú", estiloBoton))
            GoToMenu();
    }

    // [ZOMBIES] HUD del jugador (vida, balas, armas, FPS) con la fuente del modo zombies,
    // y los FPS movidos debajo de la dificultad. Solo pasa en esta escena: en las
    // partidas por equipos el HUD queda como siempre.
    private const float MargenIzquierdo = 30f;
    private const float PosicionAviso = 130f; // distancia desde arriba, debajo de "ZOMBIES: N"
    private const float EscalaAviso = 0.7f;   // tamaño del aviso (1 = el original)
    private bool hudAdaptado;
    private TMP_Text fpsTexto;

    private void LateUpdate()
    {
        if (!hudAdaptado)
            AdaptarHudJugador();
    }

    // Escribe un renglón arriba a la izquierda y devuelve dónde empieza el siguiente.
    private float Renglon(float y, string texto, float tamanio, Color color)
    {
        float e = Screen.height / 1080f;
        Escribir(new Rect(MargenIzquierdo * e, y, 800f * e, tamanio * 1.4f), texto,
            estiloTexto, tamanio, color, TextAnchor.UpperLeft);
        return y + tamanio * 1.35f;
    }

    private void AdaptarHudJugador()
    {
        NetworkPlayerCombat combat = GetPlayerCombat();
        NetworkPlayerHudPresenter hud = combat != null ? combat.GetComponent<NetworkPlayerHudPresenter>() : null;
        if (hud == null || hud.View == null)
            return; // el jugador todavía no apareció: se vuelve a intentar el próximo cuadro

        hudAdaptado = true;
        TMP_FontAsset fuente = FuenteTextoTMP;

        // Los avisos (logros y objetos agarrados) van arriba al centro, debajo de
        // "ZOMBIES: N", y más chicos, para no tapar el dinero ni la dificultad.
        AchievementNotificationHUD aviso = combat.GetComponentInChildren<AchievementNotificationHUD>(true);
        if (aviso != null && aviso.notificationPanel != null)
        {
            RectTransform panel = aviso.notificationPanel.GetComponent<RectTransform>();
            if (panel != null)
            {
                panel.anchorMin = new Vector2(0.5f, 1f);
                panel.anchorMax = new Vector2(0.5f, 1f);
                panel.pivot = new Vector2(0.5f, 1f);
                panel.anchoredPosition = new Vector2(0f, -PosicionAviso);
                panel.localScale = Vector3.one * EscalaAviso;
            }
        }

        foreach (TMP_Text texto in hud.View.GetComponentsInChildren<TMP_Text>(true))
        {
            if (fuente != null)
                texto.font = fuente;

            // El texto original de FPS queda invisible: se dibuja abajo de la dificultad.
            if (texto.gameObject.name == "FPS")
            {
                fpsTexto = texto;
                texto.color = new Color(0f, 0f, 0f, 0f);
            }
        }
    }

    // Texto con borde negro (se dibuja 8 veces corrido en negro y después el color).
    private static void Escribir(Rect rect, string texto, GUIStyle estilo, float tamanio,
        Color color, TextAnchor alineacion)
    {
        estilo.fontSize = Mathf.Max(8, Mathf.RoundToInt(tamanio));
        estilo.alignment = alineacion;

        float borde = Mathf.Max(1f, tamanio / 24f);
        Color negro = new Color(0f, 0f, 0f, color.a * 0.9f);
        PonerColor(estilo, negro);
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Rect corrido = rect;
                corrido.x += dx * borde;
                corrido.y += dy * borde;
                GUI.Label(corrido, texto, estilo);
            }
        }

        PonerColor(estilo, color);
        GUI.Label(rect, texto, estilo);
    }

    // El mismo color en todos los estados: si no, al pasar el mouse por encima
    // Unity usa el color de "hover" (blanco) y el texto cambia y pierde el borde.
    private static void PonerColor(GUIStyle estilo, Color color)
    {
        estilo.normal.textColor = color;
        estilo.hover.textColor = color;
        estilo.active.textColor = color;
        estilo.focused.textColor = color;
    }

    private void PrepararEstilos()
    {
        float e = Screen.height / 1080f;
        if (estiloTitulo != null && Mathf.Approximately(escalaEstilos, e))
            return;
        escalaEstilos = e;

        estiloTitulo = new GUIStyle(GUI.skin.label) { font = fuenteTitulos, wordWrap = false, clipping = TextClipping.Overflow };
        estiloTexto = new GUIStyle(GUI.skin.label) { font = fuenteTexto, wordWrap = false, clipping = TextClipping.Overflow };

        if (fondoBoton == null)
        {
            fondoBoton = Color1x1(new Color(0.12f, 0.02f, 0.02f, 0.9f));
            fondoBotonEncima = Color1x1(new Color(0.45f, 0.04f, 0.04f, 0.95f));
        }

        estiloBoton = new GUIStyle(GUI.skin.button)
        {
            font = fuenteTexto,
            fontSize = Mathf.RoundToInt(28f * e),
            alignment = TextAnchor.MiddleCenter
        };
        estiloBoton.normal.background = fondoBoton;
        estiloBoton.hover.background = fondoBotonEncima;
        estiloBoton.active.background = fondoBotonEncima;
        estiloBoton.normal.textColor = BlancoHueso;
        estiloBoton.hover.textColor = Color.white;
        estiloBoton.active.textColor = Dorado;
    }

    private static Texture2D Color1x1(Color color)
    {
        Texture2D textura = new Texture2D(1, 1);
        textura.SetPixel(0, 0, color);
        textura.Apply();
        return textura;
    }

    // Fuentes para los textos TextMeshPro del modo zombies (cartel de la caja,
    // ruleta, avisos). Se crean una sola vez a partir de los archivos de fuente.
    private static TMP_FontAsset fuenteTextoTMP;
    public static TMP_FontAsset FuenteTextoTMP
    {
        get
        {
            if (fuenteTextoTMP == null && instance != null && instance.fuenteTexto != null)
                fuenteTextoTMP = TMP_FontAsset.CreateFontAsset(instance.fuenteTexto);
            return fuenteTextoTMP;
        }
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
