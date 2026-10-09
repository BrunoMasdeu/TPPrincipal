using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TutorialManager : MonoBehaviour
{
    public enum TaskType { KeyPress, MouseClick, GameEvent }
    public enum MouseButtonType { LeftClick, RightClick }

    [System.Serializable]
    public class TutorialTask
    {
        public string instructionText;
        public TaskType taskType;

        [Header("Si es KeyPress")]
        public Key requiredKey;

        [Header("Si es MouseClick")]
        public MouseButtonType mouseButton;

        [Header("Si es GameEvent")]
        public string eventID; // Ej: "Wallrun", "DianaDestruida", "BotAsesinado"
        public int requiredCount = 1; // Para exigir múltiples acciones (ej: matar 3 bots)

        [HideInInspector] public int currentCount = 0;
        [HideInInspector] public bool isCompleted = false;
        [HideInInspector] public TutorialTaskUI uiInstance;
    }

    [System.Serializable]
    public class TutorialStep
    {
        public string stepTitle;
        public List<TutorialTask> tasks;
        public UnityEvent onStepCompleted; // Permite arrastrar la puerta en el Inspector para abrirla
    }
    [Header("Menú de Pausa")]
    [SerializeField] private GameObject pauseMenuPanel;
    private bool isPaused = false;

    [Header("Referencias UI")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private TextMeshProUGUI stepTitleText;
    [SerializeField] private Transform taskListContainer;
    [SerializeField] private GameObject taskItemPrefab;
    [SerializeField] private GameObject finalMenuPanel; // Menú de "Tutorial Completado"

    [Header("Configuración de Pasos")]
    [SerializeField] private List<TutorialStep> tutorialSteps;
    [SerializeField] private float delayBetweenSteps = 1.5f;

    private int currentStepIndex = 0;
    private bool isTransitioning = false;

    private Move playerMoveScript;
    private GunSystem playerGunScript;
    private GameObject playerHUD;
    private MonoBehaviour playerCameraScript;

    // --- SUSCRIPCIÓN A EVENTOS EXTERNOS (DESACOPLAMIENTO) ---
    private void OnEnable()
    {
        WallRun.OnPlayerWallrun += HandleWallrun;      // Escucha al script WallRun
        Grappling.OnPlayerGrapple += HandleGrapple;    // Escucha al script Grappling
        Diana.OnTargetDestroyed += HandleTargetDestroyed;
        movimientoBot.OnTargetDestroyed += HandleTargetDestroyed;
    }

    private void OnDisable()
    {
        WallRun.OnPlayerWallrun -= HandleWallrun;
        Grappling.OnPlayerGrapple -= HandleGrapple;
        Diana.OnTargetDestroyed -= HandleTargetDestroyed;
        movimientoBot.OnTargetDestroyed -= HandleTargetDestroyed;
    }
    // ---------------------------------------------------------

    private void Start()
    {
        playerMoveScript = FindAnyObjectByType<Move>();
        playerCameraScript = FindAnyObjectByType<CameraMovement>();
        playerGunScript = FindAnyObjectByType<GunSystem>();
        playerHUD = GameObject.Find("PlayerUI");
        if (tutorialSteps.Count > 0)
        {
            LoadStep(currentStepIndex);
        }
    }

    private void Update()
    {
        // 1. Detectar la tecla Escape para pausar/reanudar
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }

        // 2. Si el juego está pausado, o si estamos transicionando, o si ya terminó, no hacer nada más
        if (isPaused || isTransitioning || currentStepIndex >= tutorialSteps.Count)
            return;

        CheckInputs();
    }

    private void LoadStep(int index)
    {
        // 1. Limpiar las tareas visuales del paso anterior
        foreach (Transform child in taskListContainer)
        {
            Destroy(child.gameObject);
        }

        // 2. Configurar el título del nuevo paso
        TutorialStep step = tutorialSteps[index];
        stepTitleText.text = step.stepTitle;

        // 3. Instanciar los prefabs de UI (los checkboxes) para el nuevo paso
        foreach (TutorialTask task in step.tasks)
        {
            task.isCompleted = false;
            task.currentCount = 0;

            GameObject obj = Instantiate(taskItemPrefab, taskListContainer);
            task.uiInstance = obj.GetComponent<TutorialTaskUI>();

            // Añade "(0/X)" al texto si se requiere más de un evento
            string textSuffix = task.requiredCount > 1 ? $" (0/{task.requiredCount})" : "";
            task.uiInstance.SetupTask(task.instructionText + textSuffix);
        }

        tutorialPanel.SetActive(true);
    }

    private void CheckInputs()
    {
        TutorialStep currentStep = tutorialSteps[currentStepIndex];
        bool allTasksCompleted = true;

        foreach (TutorialTask task in currentStep.tasks)
        {
            if (!task.isCompleted)
            {
                bool actionDetected = false;

                // Evaluación para teclado (New Input System)
                if (task.taskType == TaskType.KeyPress && Keyboard.current != null)
                {
                    if (Keyboard.current[task.requiredKey].wasPressedThisFrame)
                    {
                        actionDetected = true;
                    }
                }
                // Evaluación para mouse (New Input System)
                else if (task.taskType == TaskType.MouseClick && Mouse.current != null)
                {
                    if (task.mouseButton == MouseButtonType.LeftClick && Mouse.current.leftButton.wasPressedThisFrame)
                        actionDetected = true;
                    else if (task.mouseButton == MouseButtonType.RightClick && Mouse.current.rightButton.wasPressedThisFrame)
                        actionDetected = true;
                }

                // Marcar completado o frenar avance
                if (actionDetected) CompleteTask(task);
                else if (task.taskType != TaskType.GameEvent) allTasksCompleted = false;
            }
        }

        // Faltaba asegurar que los GameEvents no pasaran solos de largo si no estaban completados
        foreach (TutorialTask task in currentStep.tasks)
        {
            if (task.taskType == TaskType.GameEvent && !task.isCompleted)
            {
                allTasksCompleted = false;
            }
        }

        if (allTasksCompleted) StartCoroutine(TransitionToNextStep());
    }

    // --- MANEJADORES DE EVENTOS (Traducción de Gameplay a Tutorial) ---
    private void HandleWallrun() => RegisterGameEvent("Wallrun");

    private void HandleGrapple() => RegisterGameEvent("UsoGancho");

    private void HandleTargetDestroyed(string targetTag)
    {
        if (targetTag == "Target") RegisterGameEvent("DianaDestruida");
        else if (targetTag == "Enemy") RegisterGameEvent("BotAsesinado");
    }
    // ------------------------------------------------------------------

    private void RegisterGameEvent(string eventID)
    {
        if (isTransitioning || currentStepIndex >= tutorialSteps.Count) return;

        TutorialStep currentStep = tutorialSteps[currentStepIndex];
        bool allTasksCompleted = true;

        foreach (TutorialTask task in currentStep.tasks)
        {
            if (!task.isCompleted)
            {
                // Si la tarea actual coincide con el evento que acaba de ocurrir
                if (task.taskType == TaskType.GameEvent && task.eventID == eventID)
                {
                    task.currentCount++;

                    // Actualiza el texto UI "(1/3)"...
                    if (task.requiredCount > 1)
                    {
                        task.uiInstance.SetupTask($"{task.instructionText} ({task.currentCount}/{task.requiredCount})");
                    }

                    // Verifica si ya se alcanzó la meta
                    if (task.currentCount >= task.requiredCount)
                    {
                        CompleteTask(task);
                    }
                }

                if (!task.isCompleted) allTasksCompleted = false;
            }
        }

        if (allTasksCompleted)
        {
            StartCoroutine(TransitionToNextStep());
        }
    }

    private void CompleteTask(TutorialTask task)
    {
        task.isCompleted = true;
        task.uiInstance.MarkAsCompleted(); // Tilda visualmente el checkbox en pantalla
    }

    private IEnumerator TransitionToNextStep()
    {
        isTransitioning = true;

        // Ejecuta el UnityEvent configurado en el Inspector (Ej: Puerta.Abrir())
        tutorialSteps[currentStepIndex].onStepCompleted?.Invoke();

        // Pausa para que el jugador vea que completó todo
        yield return new WaitForSeconds(delayBetweenSteps);

        currentStepIndex++;

        if (currentStepIndex < tutorialSteps.Count)
        {
            // Carga el siguiente bloque de instrucciones
            LoadStep(currentStepIndex);
            isTransitioning = false;
        }
        else
        {
            Time.timeScale = 0f;
            // Finaliza el tutorial completo
            tutorialPanel.SetActive(false);
            playerHUD.SetActive(false);
            if (finalMenuPanel != null) finalMenuPanel.SetActive(true);
            if (playerMoveScript != null) playerMoveScript.SetKeyboardInputEnabled(false);
            if (playerGunScript != null) playerMoveScript.enabled = false;
            if (playerCameraScript != null) playerCameraScript.enabled = false;
            // Libera el mouse para poder hacer click en el menú final
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        // --- LIMPIEZA DE RED ---
        if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
            Destroy(Unity.Netcode.NetworkManager.Singleton.gameObject);
        }

        // Si tienes tu propio ConnectionManager, llama a su método de desconexión aquí

        SceneManager.LoadScene("MenuScene");
    }
    public void PermanecerEnMapa()
    {
        finalMenuPanel.SetActive(false);
        Time.timeScale = 1f; // Devuelve el tiempo a la normalidad
        Cursor.lockState = CursorLockMode.Locked; // Bloquea el cursor al centro
        Cursor.visible = false; // Oculta el cursor
        if (playerMoveScript != null) playerMoveScript.SetKeyboardInputEnabled(true);
        if (playerGunScript != null) playerMoveScript.enabled = true;
        if (playerCameraScript != null) playerCameraScript.enabled = true;
        playerHUD.SetActive(true);
    }
    public void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            // Pausar
            pauseMenuPanel.SetActive(true);
            Time.timeScale = 0f; // Congela el tiempo y las físicas
            Cursor.lockState = CursorLockMode.None; // Libera el cursor
            Cursor.visible = true; // Hace visible el cursor
            if (playerMoveScript != null) playerMoveScript.SetKeyboardInputEnabled(false);
            if (playerGunScript != null) playerMoveScript.enabled = false;
            if (playerCameraScript != null) playerCameraScript.enabled = false;
            playerHUD.SetActive(false);
        }
        else
        {
            // Reanudar
            pauseMenuPanel.SetActive(false);
            Time.timeScale = 1f; // Devuelve el tiempo a la normalidad
            Cursor.lockState = CursorLockMode.Locked; // Bloquea el cursor al centro
            Cursor.visible = false; // Oculta el cursor
            if (playerMoveScript != null) playerMoveScript.SetKeyboardInputEnabled(true);
            if (playerGunScript != null) playerMoveScript.enabled = true;
            if (playerCameraScript != null) playerCameraScript.enabled = true;
            playerHUD.SetActive(true);
        }
    }
}

