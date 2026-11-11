using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Unity.Netcode;

public class AchievementNotificationHUD : NetworkBehaviour
{
    [Header("UI Elements")]
    public GameObject notificationPanel; // El panel base que se mostrará/ocultará
    public Image achievementIcon;        // El icono de la insignia (la gorra, calavera, etc.)
    public TextMeshProUGUI achievementTitle; // Texto del nombre del logro

    [Header("Settings")]
    public float displayDuration = 3f;   // Tiempo en pantalla (3 segundos)

    [Header("Icon Assets (Asigna tus PNG transparentes)")]
    public Sprite iconTutorial;
    public Sprite iconZombies;
    public Sprite iconTDM;
    public Sprite iconCTF;
    public Sprite iconEasterEgg;

    // [ZOMBIES] Iconos para avisar lo que agarraste (vida, balas, velocidad).
    [Header("Avisos de objetos (modo zombies)")]
    public Sprite iconVida;
    public Sprite iconBalas;
    public Sprite iconVelocidad;

    public enum TipoAviso { Vida, Balas, Velocidad }

    // [ZOMBIES] Texto fijo "Logro desbloqueado" del panel: se oculta en los avisos de objetos.
    private TMP_Text encabezadoLogro;

    private Coroutine currentCoroutine;

    public override void OnNetworkSpawn()
    {
        // MUY IMPORTANTE: Solo el jugador local (el dueño de esta pantalla) debe suscribirse.
        if (IsOwner)
        {
            notificationPanel.SetActive(false);
            AchievementManager.OnAchievementUnlocked += ShowNotification;
        }
        else
        {
            // Apagar este Canvas para los demás jugadores (clones) en multijugador
            if (notificationPanel != null) notificationPanel.SetActive(false);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            AchievementManager.OnAchievementUnlocked -= ShowNotification;
        }
    }

    // Se ejecuta automáticamente cuando AchievementManager dispara el evento
    private void ShowNotification(string achievementId)
    {
        // 1. Configurar los datos visuales según el ID
        switch (achievementId)
        {
            case "tutorial_done":
                achievementTitle.text = "COMPLETAR EL TUTORIAL";
                achievementIcon.sprite = iconTutorial;
                break;
            case "win_zombies":
                achievementTitle.text = "PRIMERA VICTORIA EN ZOMBIES";
                achievementIcon.sprite = iconZombies;
                break;
            case "win_ctf":
                achievementTitle.text = "PRIMERA VICTORIA EN CTF";
                achievementIcon.sprite = iconCTF;
                break;
            case "win_tdm":
                achievementTitle.text = "PRIMERA VICTORIA EN TDM";
                achievementIcon.sprite = iconTDM;
                break;
            case "easter_egg":
                achievementTitle.text = "ENCONTRAR EL EASTER EGG";
                achievementIcon.sprite = iconEasterEgg;
                break;
        }

        achievementIcon.enabled = true; // [ZOMBIES] un aviso sin icono lo pudo haber ocultado
        MostrarEncabezadoLogro(true);   // [ZOMBIES] los avisos de objetos lo ocultan

        // 2. Reiniciar la corrutina si ya había un logro mostrándose
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        currentCoroutine = StartCoroutine(DisplayRoutine());
    }

    // [ZOMBIES] Muestra un aviso cualquiera en el mismo panel de los logros.
    // Si el icono es null, se muestra solo el texto.
    public void MostrarAviso(string texto, Sprite icono)
    {
        if (!IsOwner || notificationPanel == null || achievementTitle == null)
            return;

        achievementTitle.text = texto;
        MostrarEncabezadoLogro(false);
        if (achievementIcon != null)
        {
            achievementIcon.sprite = icono;
            achievementIcon.enabled = icono != null;
        }

        if (currentCoroutine != null)
            StopCoroutine(currentCoroutine);

        currentCoroutine = StartCoroutine(DisplayRoutine());
    }

    // [ZOMBIES] Busca (una sola vez) el texto "Logro desbloqueado" dentro del panel
    // y lo muestra u oculta.
    private void MostrarEncabezadoLogro(bool mostrar)
    {
        if (encabezadoLogro == null && notificationPanel != null)
        {
            foreach (TMP_Text texto in notificationPanel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (texto != achievementTitle &&
                    texto.text.ToLowerInvariant().Contains("logro"))
                {
                    encabezadoLogro = texto;
                    break;
                }
            }
        }

        if (encabezadoLogro != null)
            encabezadoLogro.gameObject.SetActive(mostrar);
    }

    // [ZOMBIES] Atajo para los objetos agarrables: busca el aviso del jugador
    // que agarro el objeto y lo muestra. Solo hace algo en el modo zombies.
    public static void AvisarEnZombies(Component parteDelJugador, string texto, TipoAviso tipo)
    {
        if (!WaveManager.IsActive || parteDelJugador == null)
            return;

        NetworkObject jugador = parteDelJugador.GetComponentInParent<NetworkObject>();
        AchievementNotificationHUD aviso = jugador != null
            ? jugador.GetComponentInChildren<AchievementNotificationHUD>(true)
            : null;
        if (aviso == null)
            return;

        Sprite icono = tipo == TipoAviso.Vida ? aviso.iconVida
            : tipo == TipoAviso.Balas ? aviso.iconBalas
            : aviso.iconVelocidad;
        aviso.MostrarAviso(texto, icono);
    }

    private IEnumerator DisplayRoutine()
    {
        // Mostrar el panel
        notificationPanel.SetActive(true);

        // Esperar 3 segundos
        yield return new WaitForSeconds(displayDuration);

        // Ocultar el panel
        notificationPanel.SetActive(false);
    }
}