using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vista puramente visual del HUD del jugador. No conoce Netcode ni decide
/// reglas de partida: recibe valores ya validados desde el presentador.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class PlayerHudView : MonoBehaviour
{
    [Header("Estado del jugador")]
    [SerializeField] private Image healthIcon;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text ammunitionText;

    [Header("Ranuras del equipamiento")]
    [SerializeField] private Image meleeHighlight;
    [SerializeField] private Image secondaryHighlight;
    [SerializeField] private Image primaryHighlight;
    [SerializeField] private Image utilityHighlight;

    [Header("Presentacion")]
    [SerializeField, Range(0f, 1f)] private float inactiveSlotAlpha = 0f;
    [SerializeField, Range(0f, 1f)] private float activeSlotAlpha = 0.65f;
    [SerializeField] private Color normalHealthColor = Color.white;
    [SerializeField] private Color lowHealthColor = new(1f, 0.25f, 0.25f, 1f);

    private Canvas hudCanvas;
    private int lastAmmunition;
    private int lastReserve;
    private bool lastUsesAmmunition;

    public Canvas HudCanvas => hudCanvas != null
        ? hudCanvas
        : hudCanvas = GetComponent<Canvas>();

    public bool HasRequiredReferences =>
        HudCanvas != null &&
        healthIcon != null &&
        healthText != null &&
        ammunitionText != null &&
        meleeHighlight != null &&
        secondaryHighlight != null &&
        primaryHighlight != null &&
        utilityHighlight != null;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Permite que el prefab visual de Juan se conecte por nombres estables.
    /// Las referencias quedan serializadas al ejecutar la herramienta de
    /// unificacion, pero este fallback evita que una copia antigua quede rota.
    /// </summary>
    public void ResolveReferences()
    {
        hudCanvas = GetComponent<Canvas>();

        Transform panelHealth = FindDescendant(transform, "PanelHealth");
        Transform health = FindDescendant(transform, "Salud");
        Transform ammunition = FindDescendant(transform, "Cargador");

        healthIcon ??= health != null ? health.GetComponent<Image>() : null;
        healthText ??= panelHealth != null
            ? panelHealth.GetComponentInChildren<TMP_Text>(true)
            : null;
        ammunitionText ??= ammunition != null
            ? ammunition.GetComponent<TMP_Text>()
            : null;

        meleeHighlight ??= FindImage("Cuchillo");
        secondaryHighlight ??= FindImage("ArmaSecundaria");
        primaryHighlight ??= FindImage("ArmaPrincipal");
        utilityHighlight ??= FindImage("Gancho");
    }

    public void SetVisible(bool visible)
    {
        if (HudCanvas != null)
            HudCanvas.enabled = visible;
    }

    public void SetHealth(int current, int maximum)
    {
        int safeMaximum = Mathf.Max(1, maximum);
        int safeCurrent = Mathf.Clamp(current, 0, safeMaximum);
        float ratio = safeCurrent / (float)safeMaximum;

        if (healthText != null)
            healthText.text = safeCurrent.ToString();

        if (healthIcon != null)
            healthIcon.color = Color.Lerp(lowHealthColor, normalHealthColor, ratio);
    }

    public void SetLifeState(PlayerLifeState state)
    {
        if (healthText == null || state == PlayerLifeState.Alive)
            return;

        healthText.text = state == PlayerLifeState.Dead
            ? "0"
            : "...";
    }

    public void SetAmmunition(int current, int reserve, bool usesAmmunition)
    {
        lastAmmunition = Mathf.Max(0, current);
        lastReserve = Mathf.Max(0, reserve);
        lastUsesAmmunition = usesAmmunition;
        SetReloading(false);
    }

    public void SetReloading(bool reloading)
    {
        if (ammunitionText == null)
            return;

        if (!lastUsesAmmunition)
        {
            ammunitionText.text = "--";
            return;
        }

        ammunitionText.text = reloading
            ? "RECARGANDO"
            : $"{lastAmmunition}/{lastReserve}";
    }

    public void SetSelectedSlot(WeaponSlot selectedSlot)
    {
        SetHighlight(meleeHighlight, selectedSlot == WeaponSlot.Melee);
        SetHighlight(secondaryHighlight, selectedSlot == WeaponSlot.Secondary);
        SetHighlight(primaryHighlight, selectedSlot == WeaponSlot.Primary);
        SetHighlight(utilityHighlight, selectedSlot == WeaponSlot.Utility);
    }

    private Image FindImage(string objectName)
    {
        Transform target = FindDescendant(transform, objectName);
        return target != null ? target.GetComponent<Image>() : null;
    }

    private void SetHighlight(Image image, bool active)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = active ? activeSlotAlpha : inactiveSlotAlpha;
        image.color = color;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        foreach (Transform child in root)
        {
            if (child.name == objectName)
                return child;

            Transform nested = FindDescendant(child, objectName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
