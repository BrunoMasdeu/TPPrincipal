using UnityEngine;
using UnityEngine.InputSystem;

public class EasterEggDoor : MonoBehaviour
{
    [Header("Configuración de Escena")]
    [SerializeField] private string sceneToLoad;

    [Header("Referencias de UI")]
    [SerializeField] private GameObject interactionPromptUI; // El texto en pantalla (ej: "Presiona E para interactuar")
    private bool playerIsNearby = false;
    private bool transitionInProgress;
    private TutorialManager tutorialManager;

    private void Start()
    {
        tutorialManager = FindFirstObjectByType<TutorialManager>();
        if (interactionPromptUI != null)
            interactionPromptUI.SetActive(false);
    }

    private void Update()
    {
        bool canInteract = playerIsNearby &&
            !transitionInProgress &&
            tutorialManager != null && tutorialManager.IsCompleted;

        if (interactionPromptUI != null &&
            interactionPromptUI.activeSelf != canInteract)
            interactionPromptUI.SetActive(canInteract);

        if (canInteract && Keyboard.current != null &&
            Keyboard.current[Key.E].wasPressedThisFrame)
            TryEnterRace();
    }

    // Detecta cuando el jugador entra al área de la puerta
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerIsNearby = true;
    }

    // Detecta cuando el jugador se aleja de la puerta
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerIsNearby = false;
    }

    public bool TryEnterRace()
    {
        if (transitionInProgress || !playerIsNearby ||
            tutorialManager == null || !tutorialManager.IsCompleted)
            return false;

        NetworkLobbySession session = NetworkLobbySession.Instance;
        if (session == null)
        {
            Debug.LogWarning(
                "[EasterEggDoor] No hay una sesión local activa. Iniciá el tutorial desde el menú.");
            return false;
        }

        if (!session.TryStartLocalRaceFromTutorial(
                sceneToLoad, out string validationError))
        {
            Debug.LogWarning($"[EasterEggDoor] {validationError}");
            return false;
        }

        transitionInProgress = true;
        Time.timeScale = 1f;
        if (interactionPromptUI != null)
            interactionPromptUI.SetActive(false);
        return true;
    }
}
