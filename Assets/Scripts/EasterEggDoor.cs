using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class EasterEggDoor : MonoBehaviour
{
    [Header("Configuración de Escena")]
    [SerializeField] private string sceneToLoad;

    [Header("Referencias de UI")]
    [SerializeField] private GameObject interactionPromptUI; // El texto en pantalla (ej: "Presiona E para interactuar")
    private bool playerIsNearby = false;

    private void Start()
    {
        // Asegurarnos de que el aviso esté apagado al iniciar
        if (interactionPromptUI != null)
            interactionPromptUI.SetActive(false);
    }

    private void Update()
    {
        // Si el jugador está cerca y presiona la tecla asignada
        if (playerIsNearby && Keyboard.current != null)
        {
            if (Keyboard.current[Key.E].wasPressedThisFrame)
            {
                LoadTargetScene();
            }
        }
    }

    // Detecta cuando el jugador entra al área de la puerta
    private void OnTriggerEnter(Collider other)
    {
        // Puedes verificar si el objeto que entra es el jugador mediante un Tag
        if (other.CompareTag("Player"))
        {
            playerIsNearby = true;

            if (interactionPromptUI != null)
                interactionPromptUI.SetActive(true);
        }
    }

    // Detecta cuando el jugador se aleja de la puerta
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsNearby = false;

            if (interactionPromptUI != null)
                interactionPromptUI.SetActive(false);
        }
    }
    //===============>> HACER <<====================//
    private void LoadTargetScene()
    {
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            // Restablece el tiempo por si acaso estuvo pausado antes
            Time.timeScale = 1f;

            // Carga la nueva escena
            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            Debug.LogWarning($"[InteractiveDoor] No se ha definido una escena para cargar en '{gameObject.name}'.");
        }
    }
    //==========================================//
}
