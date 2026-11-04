using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Recupera al invitado si el host cierra la sesión durante la partida.</summary>
public sealed class DisconnectedClientReturnToMenu : MonoBehaviour
{
    private static bool returning;

    public static void Begin(NetworkManager networkManager)
    {
        if (returning)
            return;

        returning = true;
        GameObject recoveryObject = new("DisconnectedClientReturnToMenu");
        DontDestroyOnLoad(recoveryObject);
        DisconnectedClientReturnToMenu recovery =
            recoveryObject.AddComponent<DisconnectedClientReturnToMenu>();
        recovery.StartCoroutine(recovery.ReturnToMenu(networkManager));
    }

    private IEnumerator ReturnToMenu(NetworkManager networkManager)
    {
        Debug.LogWarning("[Netcode] Se perdió la conexión con el host. Volviendo al menú.");

        // Dejar que Netcode termine de procesar el callback de desconexión.
        yield return null;

        if (networkManager != null && networkManager.IsListening)
            networkManager.Shutdown();

        float shutdownDeadline = Time.realtimeSinceStartup + 5f;
        while (networkManager != null && networkManager.ShutdownInProgress &&
               Time.realtimeSinceStartup < shutdownDeadline)
            yield return null;

        if (networkManager != null)
        {
            Destroy(networkManager.gameObject);
            yield return null;
        }

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        yield return SceneManager.LoadSceneAsync("MenuScene", LoadSceneMode.Single);

        returning = false;
        Destroy(gameObject);
    }
}
