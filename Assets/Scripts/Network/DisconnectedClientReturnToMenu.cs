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

    /// <summary>
    /// [COG-599] Los motivos que escribe el juego (te expulsaron, sala llena)
    /// ya están en castellano y se muestran tal cual. Los del transporte vienen
    /// en inglés y con datos internos, así que se reemplazan por un aviso claro.
    /// </summary>
    private static string MensajeDeDesconexion(string motivo)
    {
        const string AvisoHost = "El host cerró la partida o se perdió la conexión.";

        if (string.IsNullOrWhiteSpace(motivo) || motivo.StartsWith("[") ||
            motivo.Contains("host shutting down") ||
            motivo.Contains("Connection was closed") ||
            motivo.Contains("Disconnected due to"))
        {
            return AvisoHost;
        }

        return motivo;
    }

    private IEnumerator ReturnToMenu(NetworkManager networkManager)
    {
        Debug.LogWarning("[Netcode] Se perdió la conexión con el host. Volviendo al menú.");

        // [COG-599] El menú muestra por qué volviste (por ejemplo, el host se fue).
        ConnectionManager.MensajePendiente = MensajeDeDesconexion(
            networkManager != null ? networkManager.DisconnectReason : string.Empty
        );

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
