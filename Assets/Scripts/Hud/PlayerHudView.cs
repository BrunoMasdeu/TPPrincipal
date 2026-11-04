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
    [SerializeField] private TMP_Text grenadeText;

    [Header("Ranuras del equipamiento")]
    [SerializeField] private Image meleeIcon;
    [SerializeField] private Image secondaryIcon;
    [SerializeField] private Image primaryIcon;
    [SerializeField] private Image utilityIcon;

    [Header("Presentacion")]
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
        grenadeText != null &&
        meleeIcon != null &&
        secondaryIcon != null &&
        primaryIcon != null &&
        utilityIcon != null;

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
        Transform grenades = FindDescendant(transform, "Granadas");

        healthIcon ??= health != null ? health.GetComponent<Image>() : null;
        healthText ??= panelHealth != null
            ? panelHealth.GetComponentInChildren<TMP_Text>(true)
            : null;
        ammunitionText ??= ammunition != null
            ? ammunition.GetComponent<TMP_Text>()
            : null;
        grenadeText ??= grenades != null
            ? grenades.GetComponentInChildren<TMP_Text>(true)
            : null;

        meleeIcon ??= FindImage("CuchilloImage");
        utilityIcon ??= FindImage("GanchoImage");
        secondaryIcon ??= FindImage("ArmaSecundaria");
        primaryIcon ??= FindImage("ArmaPrincipal");
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

    public void SetGrenades(int count)
    {
        if (grenadeText != null)
            grenadeText.text = $"G x{Mathf.Max(0, count)}";
    }

    public void SetSelectedSlot(WeaponSlot selectedSlot)
    {
        SetHighlight(meleeIcon, selectedSlot == WeaponSlot.Melee);
        SetHighlight(secondaryIcon, selectedSlot == WeaponSlot.Secondary);
        SetHighlight(primaryIcon, selectedSlot == WeaponSlot.Primary);
        SetHighlight(utilityIcon, selectedSlot == WeaponSlot.Utility);
    }

    public void SetWeaponIcons(Sprite secondarySprite, Sprite primarySprite)
    {
        SetWeaponIcon(secondaryIcon, secondarySprite);
        SetWeaponIcon(primaryIcon, primarySprite);
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

        Color color = active ? Color.white : Color.gray;
        image.color = color;
    }

    private static void SetWeaponIcon(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        Color c = image.color;
        c.a = 1f;
        image.color = c;
        image.enabled = sprite != null;
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
