using UnityEngine;

/// <summary>
/// Configuracion inmutable de un tipo de arma. La municion actual pertenece
/// al jugador y nunca se guarda en este asset compartido.
/// </summary>
[CreateAssetMenu(
    fileName = "WeaponDefinition",
    menuName = "Game/Weapons/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [Header("Identidad")]
    [SerializeField] private WeaponId id = WeaponId.None;
    [SerializeField] private string displayName;
    [SerializeField] private WeaponSlot slot = WeaponSlot.None;
    [SerializeField] private WeaponAttackType attackType = WeaponAttackType.Hitscan;

    [Header("Ataque")]
    [SerializeField, Min(1)] private int damage = 25;
    [SerializeField, Min(0.1f)] private float range = 100f;
    [SerializeField, Min(0f)] private float secondsBetweenAttacks = 0.5f;
    [SerializeField, Min(0f)] private float spread = 0.01f;
    [SerializeField] private bool automatic;

    [Header("Municion")]
    [SerializeField, Min(0)] private int magazineSize = 12;
    [SerializeField, Min(0)] private int initialReserveAmmunition = 30;
    [SerializeField, Min(0f)] private float reloadSeconds = 1.3f;

    public WeaponId Id => id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? id.ToString()
        : displayName;
    public WeaponSlot Slot => slot;
    public WeaponAttackType AttackType => attackType;
    public int Damage => damage;
    public float Range => range;
    public float SecondsBetweenAttacks => secondsBetweenAttacks;
    public float Spread => spread;
    public bool Automatic => automatic;
    public int MagazineSize => magazineSize;
    public int InitialReserveAmmunition => initialReserveAmmunition;
    public float ReloadSeconds => reloadSeconds;
    public bool UsesAmmunition =>
        attackType == WeaponAttackType.Hitscan && magazineSize > 0;
    public bool CanAttack => attackType != WeaponAttackType.Utility;

    private void OnValidate()
    {
        damage = Mathf.Max(1, damage);
        range = Mathf.Max(0.1f, range);
        secondsBetweenAttacks = Mathf.Max(0f, secondsBetweenAttacks);
        spread = Mathf.Max(0f, spread);

        if (attackType == WeaponAttackType.Hitscan)
            return;

        automatic = false;
        magazineSize = 0;
        initialReserveAmmunition = 0;
        reloadSeconds = 0f;

        if (attackType == WeaponAttackType.Utility)
        {
            spread = 0f;
            secondsBetweenAttacks = 0f;
        }
    }
}
