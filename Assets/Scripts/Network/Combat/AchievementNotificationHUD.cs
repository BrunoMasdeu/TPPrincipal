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

        // 2. Reiniciar la corrutina si ya había un logro mostrándose
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        currentCoroutine = StartCoroutine(DisplayRoutine());
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