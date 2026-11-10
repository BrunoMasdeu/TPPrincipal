using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Puente entre las armas de Felipe (GunSystem y KnifeSystem) y la red.
///
/// Las armas funcionan como siempre en la PC del jugador: leen el mouse,
/// cuentan las balas, recargan y hacen su raycast. Este componente agrega lo
/// que el multijugador necesita:
///   1. Las armas solo funcionan en tu propio jugador, vivo y con la partida
///      en curso (en las copias de los demás jugadores quedan apagadas).
///   2. Cuando un arma toca a otro jugador, el servidor le resta la vida.
///   3. Todos escuchan los disparos y las recargas y ven la animación.
///   4. Al reaparecer, todas las armas vuelven llenas.
/// También guarda el dinero del jugador (tienda y recompensa por baja).
/// </summary>
[DisallowMultipleComponent]
public class NetworkPlayerCombat : NetworkBehaviour
{
    [Header("Economia provisional")]
    [SerializeField, Min(0)] private int moneyLostOnDeath = 100;

    // Momento (reloj del servidor) en que termina la recarga. Lo leen todos
    // para el sonido de recarga y el HUD. 0 = no está recargando.
    private readonly NetworkVariable<double> reloadEndsAt = new(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Dinero: solo lo ve el dueño y solo lo cambia el servidor.
    private readonly NetworkVariable<int> money = new(
        0,
        NetworkVariableReadPermission.Owner,
        NetworkVariableWritePermission.Server);

    private GunSystem[] guns;
    private KnifeSystem knife;
    private Inventory weaponInventory;
    private NetworkPlayerHealth health;
    private NetworkPlayerInventory inventory;

    private bool localInputEnabled = true;
    private bool deathPenaltyApplied;
    private string lastShotSummary = "Sin disparos";

    // Solo en el dueño: últimos valores vistos, para avisar al HUD y a los demás.
    private int lastBullets = -1;
    private int lastReserve = -1;
    private bool lastReloading;

    public event Action<int> AmmunitionChanged;
    public event Action<int> ReserveAmmunitionChanged;
    public event Action ReloadChanged;
    public event Action<int> MoneyChanged;
    /// <summary>Solo el atacante recibe daño efectivo confirmado y si fue letal.</summary>
    public event Action<int, bool> HitConfirmed;

    /// <summary>
    /// Se dispara en TODAS las copias del jugador cuando alguien ataca.
    /// Lo usan la animación y el sonido de las armas.
    /// </summary>
    public event Action<WeaponId, WeaponAttackType> AttackPresented;

    /// <summary>El GunSystem del arma en la mano (null con cuchillo o gancho).</summary>
    public GunSystem CurrentGun =>
        weaponInventory != null && weaponInventory.CurrentWeapon != null
            ? weaponInventory.CurrentWeapon.GetComponent<GunSystem>()
            : null;

    public int Ammunition => CurrentGun != null ? CurrentGun.BulletsLeft : 0;
    public int ReserveAmmunition => CurrentGun != null ? CurrentGun.ExtraBulletsLeft : 0;
    public int MagazineSize => CurrentGun != null ? CurrentGun.magazineSize : 0;
    public bool SelectedWeaponUsesAmmunition => CurrentGun != null;
    public int Money => money.Value;
    public bool IsCombatActive => IsSpawned && CanUseWeaponsNow();
    public string LastShotSummary => lastShotSummary;
    public WeaponDefinition SelectedWeapon =>
        inventory != null ? inventory.SelectedWeaponDefinition : null;
    public float ReloadRemaining => reloadEndsAt.Value > 0d && NetworkManager != null
        ? Mathf.Max(0f, (float)(reloadEndsAt.Value - NetworkManager.ServerTime.Time))
        : 0f;

    private void Awake()
    {
        guns = GetComponentsInChildren<GunSystem>(true);
        knife = GetComponentInChildren<KnifeSystem>(true);
        weaponInventory = GetComponent<Inventory>();
        health = GetComponent<NetworkPlayerHealth>();
        inventory = GetComponent<NetworkPlayerInventory>();

        // Las armas arrancan apagadas: solo se encienden en el dueño (Update).
        SetWeaponsEnabled(false);
    }

    public override void OnNetworkSpawn()
    {
        // OnValueChanged avisa en cada copia cuando llega un valor nuevo.
        reloadEndsAt.OnValueChanged += OnReloadEndsAtChanged;
        money.OnValueChanged += OnMoneyChanged;
    }

    public override void OnNetworkDespawn()
    {
        reloadEndsAt.OnValueChanged -= OnReloadEndsAtChanged;
        money.OnValueChanged -= OnMoneyChanged;

        AmmunitionChanged = null;
        ReserveAmmunitionChanged = null;
        ReloadChanged = null;
        AttackPresented = null;
        HitConfirmed = null;
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        SetWeaponsEnabled(CanUseWeaponsNow());
        WatchCurrentGun();
    }

    /// <summary>Lo llama PlayerNetworkSetup (pausa, muerte, pantallas finales).</summary>
    public void SetLocalInputEnabled(bool enabled)
    {
        localInputEnabled = enabled;
    }

    private bool CanUseWeaponsNow() =>
        localInputEnabled &&
        !WaveManager.IsInputCaptured &&
        HasInventoryInThisMode() &&
        (IsTutorialTraining() || WaveManager.IsActive || IsMatchPlaying());

    private void SetWeaponsEnabled(bool enabled)
    {
        foreach (GunSystem gun in guns)
        {
            if (gun != null && gun.enabled != enabled)
                gun.enabled = enabled;
        }

        if (knife != null && knife.enabled != enabled)
            knife.enabled = enabled;
    }

    /// <summary>Avisa al HUD de las balas y a los demás de la recarga.</summary>
    private void WatchCurrentGun()
    {
        int bullets = Ammunition;
        int reserve = ReserveAmmunition;

        if (bullets != lastBullets)
        {
            lastBullets = bullets;
            AmmunitionChanged?.Invoke(bullets);
        }

        if (reserve != lastReserve)
        {
            lastReserve = reserve;
            ReserveAmmunitionChanged?.Invoke(reserve);
        }

        GunSystem gun = CurrentGun;
        bool reloading = gun != null && gun.IsReloading;
        if (reloading != lastReloading)
        {
            lastReloading = reloading;
            ReloadRpc(reloading ? gun.reloadTime : 0f);
        }
    }

    // =====================================================================
    // Llamadas desde GunSystem y KnifeSystem (en la PC del dueño)
    // =====================================================================

    /// <summary>El arma atacó: que todos lo escuchen y lo vean.</summary>
    public void ReportShot()
    {
        WeaponDefinition weapon = SelectedWeapon;
        if (IsSpawned && IsOwner && weapon != null)
            ShotRpc(weapon.Id, weapon.AttackType);
    }

    /// <summary>
    /// El raycast del arma tocó este collider. Si es otro jugador, se le pide
    /// al servidor que le reste la vida.
    /// </summary>
    public void ReportHit(Collider hitCollider, int damage)
    {
        if (!IsSpawned || !IsOwner || hitCollider == null)
            return;

        NetworkPlayerHealth victim = hitCollider.GetComponentInParent<NetworkPlayerHealth>();
        if (victim == null || victim == health)
            return;

        HitPlayerRpc(victim.NetworkObject, damage);
    }

    // =====================================================================
    // Servidor
    // =====================================================================

    // SendTo.Server: el dueño pide y el servidor decide. InvokePermission
    // Everyone deja que lo llame un cliente; por eso se revisa el remitente.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ShotRpc(
        WeaponId weaponId,
        WeaponAttackType attackType,
        RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId == OwnerClientId && CanAttackOnServer())
            PresentAttackRpc(weaponId, attackType);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void HitPlayerRpc(
        NetworkObjectReference victimReference,
        int damage,
        RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId ||
            !CanAttackOnServer() ||
            !victimReference.TryGet(out NetworkObject victimObject) ||
            !victimObject.TryGetComponent(out NetworkPlayerHealth victim))
            return;

        string result;
        if (TryApplyDamageToPlayerOnServer(victim, damage,
                out int applied, out ulong deathEventId))
        {
            result = deathEventId > 0UL
                ? $"Muerte confirmada: jugador {victim.OwnerClientId}"
                : $"Impacto confirmado: jugador {victim.OwnerClientId}, -{applied} HP";
        }
        else
        {
            result = "Sin daño (aliado, jugador muerto o partida detenida)";
        }

        ShotFeedbackRpc(result);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ReloadRpc(float seconds, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        reloadEndsAt.Value = seconds > 0f
            ? NetworkManager.ServerTime.Time + seconds
            : 0d;
    }

    private bool CanAttackOnServer() =>
        health != null &&
        health.LifeState == PlayerLifeState.Alive &&
        (IsTutorialTraining() || WaveManager.IsActive || IsMatchPlaying());

    /// <summary>
    /// Único lugar donde el servidor daña a otro jugador, con cualquier arma
    /// (también las granadas). Si lo mata, paga la recompensa.
    /// </summary>
    public bool TryApplyDamageToPlayerOnServer(
        NetworkPlayerHealth victim,
        int damage,
        out int appliedDamage,
        out ulong deathEventId)
    {
        appliedDamage = 0;
        deathEventId = 0UL;

        if (!IsServer || !IsSpawned || victim == null || victim == health)
            return false;

        if (!victim.ApplyDamageFromPlayer(
                OwnerClientId,
                damage,
                out appliedDamage,
                out deathEventId))
            return false;

        if (deathEventId > 0UL)
        {
            victim.GetComponent<NetworkPlayerCombat>()?.ApplyDeathMoneyPenalty();
            money.Value += 200;
        }

        // Punto común para armas, cuchillo y granadas. Nunca se confirma un rechazo.
        ConfirmHitRpc(appliedDamage, victim.LifeState == PlayerLifeState.Dead);
        return true;
    }

    public bool TryBuyWeaponOnServer(WeaponId weaponId, int price)
    {
        if (!IsServer || !IsSpawned ||
            price < 0 || money.Value < price ||
            health == null || health.LifeState != PlayerLifeState.Alive ||
            inventory == null ||
            inventory.HasWeaponInLoadout(weaponId) ||
            !inventory.TryAddPurchasedWeaponOnServer(weaponId))
            return false;

        money.Value -= price;
        return true;
    }

    public void ApplyDeathMoneyPenalty()
    {
        if (!IsServer || deathPenaltyApplied)
            return;

        deathPenaltyApplied = true;
        money.Value = Mathf.Max(0, money.Value - moneyLostOnDeath);
    }

    public void AddMoneyOnServer(int amount)
    {
        if (!IsServer || !IsSpawned || amount == 0)
            return;

        money.Value = Mathf.Max(0, money.Value + amount);
    }

    public void ResetMoneyOnServer()
    {
        if (!IsServer || !IsSpawned)
            return;

        money.Value = 0;
    }

    /// <summary>Lo llama NetworkPlayerHealth al morir: corta el sonido de recarga.</summary>
    public void CancelReloadForDeath()
    {
        if (IsServer)
            reloadEndsAt.Value = 0d;
    }

    public void RefillAllWeaponsOnServer()
    {
        if (!IsServer || !IsSpawned)
            return;

        reloadEndsAt.Value = 0d;
        RefillOwnerRpc();
    }

    /// <summary>Lo llama NetworkPlayerHealth al reaparecer.</summary>
    public void ResetForRespawn()
    {
        if (!IsServer)
            return;

        deathPenaltyApplied = false;
        reloadEndsAt.Value = 0d;
        RefillOwnerRpc();

        GetComponent<NetworkPlayerShop>()?.BeginPurchaseWindowOnServer();
    }

    // =====================================================================
    // Avisos a los clientes
    // =====================================================================

    // SendTo.Owner: las balas viven en las armas de la PC del dueño.
    [Rpc(SendTo.Owner)]
    private void RefillOwnerRpc()
    {
        foreach (GunSystem gun in guns)
        {
            if (gun != null)
                gun.Refill();
        }
    }

    // SendTo.ClientsAndHost: todos ven y escuchan el ataque.
    [Rpc(SendTo.ClientsAndHost)]
    private void PresentAttackRpc(WeaponId weaponId, WeaponAttackType attackType)
    {
        AttackPresented?.Invoke(weaponId, attackType);
    }

    // SendTo.Owner: solo quien atacó necesita el resultado (panel de debug).
    [Rpc(SendTo.Owner)]
    private void ShotFeedbackRpc(string result)
    {
        lastShotSummary = result;
    }

    [Rpc(SendTo.Owner)]
    private void ConfirmHitRpc(int appliedDamage, bool lethal)
    {
        if (appliedDamage > 0)
            HitConfirmed?.Invoke(appliedDamage, lethal);
    }

    private void OnReloadEndsAtChanged(double previous, double current) =>
        ReloadChanged?.Invoke();

    private void OnMoneyChanged(int previous, int current)
    {
        if (IsOwner)
            MoneyChanged?.Invoke(current);
    }

    // =====================================================================
    // Consultas de la partida
    // =====================================================================

    private static bool HasInventoryInThisMode() =>
        WaveManager.IsActive ||
        (NetworkLobbySession.Instance != null &&
        CombatValidationRules.SupportsInventoryPreview(
            NetworkLobbySession.Instance.SelectedGameModeId,
            IsTutorialTraining()));

    private static bool IsTutorialTraining() =>
        NetworkLobbySession.Instance != null &&
        NetworkLobbySession.Instance.SelectedGameModeId == GameModeId.None &&
        TutorialManager.IsTutorialLoaded;

    private static bool IsMatchPlaying() =>
        NetworkMatchManager.Instance != null &&
        NetworkMatchManager.Instance.Phase == MatchPhase.Playing;
}
