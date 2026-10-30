/// <summary>Identificador estable que puede replicarse por red.</summary>
public enum WeaponId : byte
{
    None = 0,
    Knife = 1,
    Pistol = 2,
    Revolver = 3,
    AK = 4,
    M4 = 5,
    Grapple = 6
}

/// <summary>Ranura que ocupa un arma dentro del equipamiento.</summary>
public enum WeaponSlot : byte
{
    None = 0,
    Melee = 1,
    Secondary = 2,
    Primary = 3,
    Utility = 4
}

/// <summary>Forma en que el servidor resuelve el ataque.</summary>
public enum WeaponAttackType : byte
{
    Hitscan = 0,
    Melee = 1,
    Utility = 2
}
