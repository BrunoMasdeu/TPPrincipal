using UnityEngine;

/// <summary>
/// Ficha de un arma: qué arma es, en qué ranura va, qué tipo de ataque hace
/// y su ícono para el HUD. La usan la tienda, el inventario, el HUD, el
/// sonido y las animaciones.
///
/// Los números de disparo (daño, cadencia, cargador, recarga...) no están
/// acá: están en el GunSystem o KnifeSystem de cada arma, en el Inspector,
/// como lo armó Felipe.
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
    [SerializeField] private Sprite hudSprite;

    public WeaponId Id => id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? id.ToString()
        : displayName;
    public WeaponSlot Slot => slot;
    public WeaponAttackType AttackType => attackType;
    public Sprite HudSprite => hudSprite;
}
