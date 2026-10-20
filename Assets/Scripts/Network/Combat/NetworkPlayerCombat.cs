using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Combate autoritativo para TDM/CTF y práctica local explícita en el tutorial.
/// El cliente solo solicita atacar o recargar.
/// </summary>
[DisallowMultipleComponent]
public class NetworkPlayerCombat : NetworkBehaviour
{
    private enum ShotOutcome : byte
    {
        Miss,
        Blocked,
        FriendlyBlocked,
        Hit,
        Kill,
        NotPlaying,
        Dead,
        NoAmmo,
        Reloading,
        Cooldown,
        InvalidAim,
        InvalidWeapon,
        TrainingHit,
        ZombieHit,
        ZombieKill
    }

    private sealed class WeaponRuntimeState
    {
        public int MagazineAmmunition;
        public int ReserveAmmunition;
        public double ReloadEndsAt;
        public double NextAttackAt;
    }

    [Header("Referencias")]
    [SerializeField] private Camera aimCamera;

    [Header("Economia provisional")]
    [SerializeField, Min(0)] private int moneyLostOnDeath = 100;
    [SerializeField, Min(0)] private int moneyPerZombieKill = 100;

    private readonly NetworkVariable<int> ammunition = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> reserveAmmunition = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<double> reloadEndsAt = new(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> money = new(
        0,
        NetworkVariableReadPermission.Owner,
        NetworkVariableWritePermission.Server);

    private readonly Dictionary<WeaponId, WeaponRuntimeState> weaponStates =
        new();
    private readonly List<WeaponDefinition> definitionBuffer = new();

    private NetworkPlayerHealth health;
    private NetworkPlayerInventory inventory;
    private bool combatMode;
    private bool deathPenaltyApplied;
    private ulong nextLocalShotId;
    private ulong lastServerShotId;
    private double nextLocalAttackRequestAt;
    private string lastShotSummary = "Sin disparos";
    private bool localInputEnabled = true;

    public event Action<int> AmmunitionChanged;
    public event Action<int> ReserveAmmunitionChanged;
    public event Action ReloadChanged;
    public event Action<int> MoneyChanged;
    public event Action<WeaponId, WeaponAttackType> AttackPresented;

    public int Ammunition => ammunition.Value;
    public int ReserveAmmunition => reserveAmmunition.Value;
    public int Money => money.Value;
    public bool LocalInputEnabled => localInputEnabled;
    public bool IsCombatActive => combatMode;
    public string LastShotSummary => lastShotSummary;
    public WeaponDefinition SelectedWeapon =>
        inventory != null ? inventory.SelectedWeaponDefinition : null;
    public int MagazineSize => SelectedWeapon != null
        ? SelectedWeapon.MagazineSize
        : 0;
    public bool SelectedWeaponUsesAmmunition =>
        SelectedWeapon != null && SelectedWeapon.UsesAmmunition;
    public float ReloadRemaining => reloadEndsAt.Value > 0d &&
        NetworkManager != null
            ? Mathf.Max(
                0f,
                (float)(reloadEndsAt.Value - NetworkManager.ServerTime.Time))
            : 0f;

    private void Awake()
    {
        health = GetComponent<NetworkPlayerHealth>();
        inventory = GetComponent<NetworkPlayerInventory>();
    }

    public override void OnNetworkSpawn()
    {
        ammunition.OnValueChanged += OnAmmunitionChanged;
        reserveAmmunition.OnValueChanged += OnReserveAmmunitionChanged;
        reloadEndsAt.OnValueChanged += OnReloadChanged;
        money.OnValueChanged += OnMoneyChanged;

        if (inventory != null)
            inventory.SelectedWeaponChanged += OnSelectedWeaponChanged;
        else
            Debug.LogError(
                $"[Combat] '{name}' necesita NetworkPlayerInventory.",
                this);

        Debug.Log(
            $"[Combat] NetworkPlayerCombat presente: cliente={OwnerClientId}, " +
            $"owner={IsOwner}, servidor={IsServer}, " +
            $"modo={NetworkLobbySession.Instance?.SelectedGameModeId.ToString() ?? "sin sesion"}."
        );

        TryActivateCombatMode();

        if (IsOwner)
        {
            Debug.Log(
                $"[Dinero][Local] Cliente {OwnerClientId}: " +
                $"Saldo inicial: ${money.Value}"
            );
        }
    }

    public override void OnNetworkDespawn()
    {
        ammunition.OnValueChanged -= OnAmmunitionChanged;
        reserveAmmunition.OnValueChanged -= OnReserveAmmunitionChanged;
        reloadEndsAt.OnValueChanged -= OnReloadChanged;
        money.OnValueChanged -= OnMoneyChanged;

        if (inventory != null)
            inventory.SelectedWeaponChanged -= OnSelectedWeaponChanged;

        AmmunitionChanged = null;
        ReserveAmmunitionChanged = null;
        ReloadChanged = null;
        AttackPresented = null;
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (!combatMode)
            TryActivateCombatMode();

        if (!combatMode)
            return;

        if (IsServer)
            CompleteReloadWhenReady();

        if (!IsOwner || !localInputEnabled || MatchDebugConsole.IsInputCaptured)
            return;

        WeaponDefinition weapon = SelectedWeapon;
        if (weapon == null)
            return;

        Mouse mouse = Mouse.current;
        if (weapon.CanAttack && mouse != null)
        {
            bool wantsToAttack = weapon.Automatic
                ? mouse.leftButton.isPressed
                : mouse.leftButton.wasPressedThisFrame;

            if (wantsToAttack &&
                Time.unscaledTimeAsDouble >= nextLocalAttackRequestAt)
            {
                nextLocalAttackRequestAt = Time.unscaledTimeAsDouble +
                    Math.Max(0.02d, weapon.SecondsBetweenAttacks);
                RequestLocalAttack();
            }
        }

        if (weapon.UsesAmmunition && Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            RequestReloadRpc();
        }
    }

    private void TryActivateCombatMode()
    {
        if (combatMode || !IsSpawned ||
            NetworkLobbySession.Instance == null ||
            !CombatValidationRules.SupportsInventoryPreview(
                NetworkLobbySession.Instance.SelectedGameModeId,
                IsTutorialTraining()))
            return;

        combatMode = true;

        if (IsServer)
        {
            ResetAllWeaponStates();
            PublishSelectedWeaponState();
        }

        Debug.Log(
            $"[Combat] Combate de red ACTIVADO: cliente={OwnerClientId}, " +
            $"owner={IsOwner}, modo={NetworkLobbySession.Instance.SelectedGameModeId}."
        );
    }

    private void RequestLocalAttack()
    {
        if (aimCamera == null)
        {
            lastShotSummary = "Sin camara de mira configurada";
            return;
        }

        Transform aim = aimCamera.transform;
        RequestAttackRpc(++nextLocalShotId, aim.position, aim.forward);
    }

    /// <summary>
    /// Bloquea ataques y recargas locales sin detener el NetworkBehaviour ni
    /// sus callbacks de replicacion.
    /// </summary>
    public void SetLocalInputEnabled(bool enabled)
    {
        localInputEnabled = enabled;
        if (!enabled)
            nextLocalAttackRequestAt = 0d;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestAttackRpc(
        ulong shotId,
        Vector3 aimOrigin,
        Vector3 aimDirection,
        RpcParams rpcParams = default)
    {
        if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId ||
            shotId <= lastServerShotId)
            return;

        lastServerShotId = shotId;

        if (!TryGetSelectedWeaponState(
            out WeaponDefinition weapon,
            out WeaponRuntimeState state))
        {
            ShotFeedbackRpc(ShotOutcome.InvalidWeapon, 0UL, 0, 0UL);
            return;
        }

        if (!weapon.CanAttack)
        {
            ShotFeedbackRpc(ShotOutcome.InvalidWeapon, 0UL, 0, 0UL);
            return;
        }

        double now = NetworkManager.ServerTime.Time;
        bool trainingPreview = IsTutorialTraining();
        MatchPhase phase = NetworkMatchManager.Instance != null
            ? NetworkMatchManager.Instance.Phase
            : MatchPhase.WaitingForPlayers;
        MatchPhase effectivePhase = trainingPreview
            ? MatchPhase.Playing
            : phase;

        if (effectivePhase != MatchPhase.Playing)
        {
            ShotFeedbackRpc(ShotOutcome.NotPlaying, 0UL, 0, 0UL);
            return;
        }

        if (health == null || health.LifeState != PlayerLifeState.Alive)
        {
            ShotFeedbackRpc(ShotOutcome.Dead, 0UL, 0, 0UL);
            return;
        }

        if (state.ReloadEndsAt > 0d)
        {
            ShotFeedbackRpc(ShotOutcome.Reloading, 0UL, 0, 0UL);
            return;
        }

        if (weapon.UsesAmmunition && state.MagazineAmmunition <= 0)
        {
            ShotFeedbackRpc(ShotOutcome.NoAmmo, 0UL, 0, 0UL);
            return;
        }

        if (now < state.NextAttackAt)
        {
            ShotFeedbackRpc(ShotOutcome.Cooldown, 0UL, 0, 0UL);
            return;
        }

        if (!IsFinite(aimOrigin) || !IsFinite(aimDirection) ||
            Vector3.Distance(aimOrigin, transform.position) > 4f ||
            aimDirection.sqrMagnitude < 0.9f ||
            aimDirection.sqrMagnitude > 1.1f)
        {
            ShotFeedbackRpc(ShotOutcome.InvalidAim, 0UL, 0, 0UL);
            return;
        }

        if (!CombatValidationRules.CanAttack(
            effectivePhase,
            health.LifeState,
            weapon.UsesAmmunition,
            state.MagazineAmmunition,
            state.ReloadEndsAt,
            state.NextAttackAt,
            now))
            return;

        if (weapon.UsesAmmunition)
            state.MagazineAmmunition--;

        state.NextAttackAt = now + weapon.SecondsBetweenAttacks;
        PublishSelectedWeaponState();
        PresentAttackRpc(weapon.Id, weapon.AttackType);

        Vector3 direction = weapon.AttackType == WeaponAttackType.Hitscan
            ? ApplySpread(aimDirection.normalized, weapon.Spread)
            : aimDirection.normalized;

        Debug.Log(
            $"[Combat] Servidor acepto {weapon.DisplayName} de " +
            $"{OwnerClientId}; cargador={state.MagazineAmmunition}, " +
            $"reserva={state.ReserveAmmunition}."
        );
        ResolveAttack(weapon, aimOrigin, direction, trainingPreview);
    }

    private void ResolveAttack(
        WeaponDefinition weapon,
        Vector3 origin,
        Vector3 direction,
        bool trainingPreview)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            weapon.Range,
            ~0,
            QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (left, right) =>
            left.distance.CompareTo(right.distance));

        NetworkLobbySession lobby = NetworkLobbySession.Instance;

        LobbyPlayerData attacker = default;
        if (!trainingPreview && (lobby == null ||
            !lobby.TryGetPlayerData(
                OwnerClientId,
                out attacker)))
        {
            ShotFeedbackRpc(ShotOutcome.Blocked, 0UL, 0, 0UL);
            return;
        }

        foreach (RaycastHit hit in hits)
        {
            NetworkPlayerHealth victim =
                hit.collider.GetComponentInParent<NetworkPlayerHealth>();

            if (victim == health)
                continue;

            if (trainingPreview)
            {
                movimientoBot bot =
                    hit.collider.GetComponentInParent<movimientoBot>();

                if (bot != null)
                {
                    bot.TakeDamage(weapon.Damage);
                    ShotFeedbackRpc(
                        ShotOutcome.TrainingHit,
                        0UL,
                        weapon.Damage,
                        0UL);
                }
                else if (hit.collider.GetComponentInParent<Diana>() is
                    Diana target)
                {
                    target.Derribar();
                    ShotFeedbackRpc(
                        ShotOutcome.TrainingHit,
                        0UL,
                        weapon.Damage,
                        0UL);
                }
                else
                {
                    ShotFeedbackRpc(ShotOutcome.Blocked, 0UL, 0, 0UL);
                }

                return;
            }
            if (victim == null && IsCtfMatch())
            {
                Zombie zombie = hit.collider.GetComponentInParent<Zombie>();
                if (zombie != null)
                {
                    bool killed = zombie.zombieHitDamage(weapon.Damage);
                    if (killed)
                        AwardMoneyOnServer(moneyPerZombieKill, "eliminacion de zombie");
                    ShotFeedbackRpc(
                    killed ? ShotOutcome.ZombieKill : ShotOutcome.ZombieHit,
                    0UL,
                    weapon.Damage,
                    0UL);
                    return;
                }
            }

            if (victim == null)
            {
                ShotFeedbackRpc(ShotOutcome.Blocked, 0UL, 0, 0UL);
                return;
            }

            if (!lobby.TryGetPlayerData(
                    victim.OwnerClientId,
                    out LobbyPlayerData victimData))
            {
                ShotFeedbackRpc(ShotOutcome.Blocked, 0UL, 0, 0UL);
                return;
            }

            if (!CombatValidationRules.IsEnemy(
                    attacker.TeamId,
                    victimData.TeamId))
            {
                ShotFeedbackRpc(
                    ShotOutcome.FriendlyBlocked,
                    victim.OwnerClientId,
                    0,
                    0UL);
                return;
            }

            if (TryApplyDamageToPlayerOnServer(
                victim,
                weapon.Damage,
                out int applied,
                out ulong deathId))
            {
                Debug.Log(
                    $"[Combat] Impacto confirmado con {weapon.DisplayName}: " +
                    $"{OwnerClientId} -> {victim.OwnerClientId}, " +
                    $"dano={applied}, vida restante={victim.CurrentHealth}."
                );

                ShotFeedbackRpc(
                    deathId > 0UL ? ShotOutcome.Kill : ShotOutcome.Hit,
                    victim.OwnerClientId,
                    applied,
                    deathId);
            }
            else
            {
                ShotFeedbackRpc(ShotOutcome.Blocked, 0UL, 0, 0UL);
            }

            return;
        }

        ShotFeedbackRpc(ShotOutcome.Miss, 0UL, 0, 0UL);
    }

    /// <summary>
    /// Punto comun y exclusivo del servidor para aplicar dano causado por
    /// cualquier arma del jugador, incluidas las granadas.
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

        ApplyCombatRewards(victim, appliedDamage, deathEventId);
        return true;
    }

    private void ApplyCombatRewards(
        NetworkPlayerHealth victim,
        int appliedDamage,
        ulong deathEventId)
    {
        if (!IsServer || deathEventId == 0UL)
            return;

        NetworkPlayerCombat victimCombat =
            victim.GetComponent<NetworkPlayerCombat>();
        victimCombat?.ApplyDeathMoneyPenalty();

        money.Value += 200;

        Debug.Log(
            $"[Dinero][Servidor] Cliente {OwnerClientId}: " +
            $"+200 por eliminacion | Total: ${money.Value}"
        );
    }
    private void AwardMoneyOnServer(int amount, string reason)
    {
        if (!IsServer || amount <= 0)
            return;

        money.Value += amount;

        Debug.Log(
            $"[Dinero][Servidor] Cliente {OwnerClientId}: " +
            $"+{amount} por {reason} | Total: ${money.Value}"
        );
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestReloadRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId ||
            !TryGetSelectedWeaponState(
                out WeaponDefinition weapon,
                out WeaponRuntimeState state) ||
            !weapon.UsesAmmunition)
            return;

        bool trainingPreview = IsTutorialTraining();
        MatchPhase phase = NetworkMatchManager.Instance != null
            ? NetworkMatchManager.Instance.Phase
            : MatchPhase.WaitingForPlayers;
        MatchPhase effectivePhase = trainingPreview
            ? MatchPhase.Playing
            : phase;

        if (health == null || !CombatValidationRules.CanReload(
            effectivePhase,
            health.LifeState,
            state.MagazineAmmunition,
            weapon.MagazineSize,
            state.ReloadEndsAt,
            state.ReserveAmmunition))
            return;

        state.ReloadEndsAt =
            NetworkManager.ServerTime.Time + weapon.ReloadSeconds;
        PublishSelectedWeaponState();
    }

    private static bool IsTutorialTraining() =>
        NetworkLobbySession.Instance != null &&
        NetworkLobbySession.Instance.SelectedGameModeId == GameModeId.None &&
        TutorialManager.IsTutorialLoaded;
    private static bool IsCtfMatch() =>
        CtfMatchManager.Instance != null &&
        CtfMatchManager.Instance.IsCtf;

    private void CompleteReloadWhenReady()
    {
        if (!TryGetSelectedWeaponState(
                out WeaponDefinition weapon,
                out WeaponRuntimeState state) ||
            state.ReloadEndsAt <= 0d ||
            NetworkManager.ServerTime.Time < state.ReloadEndsAt)
            return;

        state.ReloadEndsAt = 0d;

        if (health != null && health.LifeState == PlayerLifeState.Alive)
        {
            int transferred = CombatValidationRules.CalculateReloadTransfer(
                state.MagazineAmmunition,
                weapon.MagazineSize,
                state.ReserveAmmunition);
            state.MagazineAmmunition += transferred;
            state.ReserveAmmunition -= transferred;
        }

        PublishSelectedWeaponState();
    }

    private void OnSelectedWeaponChanged(WeaponId current)
    {
        nextLocalAttackRequestAt = 0d;

        if (!IsServer || !combatMode)
            return;

        // Cambiar de arma interrumpe cualquier recarga pendiente. Se limpian
        // todos los estados para que ninguna recarga vieja termine a espaldas
        // del jugador al volver a seleccionar esa arma.
        foreach (WeaponRuntimeState state in weaponStates.Values)
        {
            state.ReloadEndsAt = 0d;
        }

        PublishSelectedWeaponState();
    }

    private void ResetAllWeaponStates()
    {
        weaponStates.Clear();
        inventory?.CopyDefinitionsTo(definitionBuffer);

        foreach (WeaponDefinition definition in definitionBuffer)
        {
            if (definition == null || definition.Id == WeaponId.None)
                continue;

            weaponStates[definition.Id] = new WeaponRuntimeState
            {
                MagazineAmmunition = definition.UsesAmmunition
                    ? definition.MagazineSize
                    : 0,
                ReserveAmmunition = definition.UsesAmmunition
                    ? definition.InitialReserveAmmunition
                    : 0,
                ReloadEndsAt = 0d,
                NextAttackAt = 0d
            };
        }
    }

    private bool TryGetSelectedWeaponState(
        out WeaponDefinition weapon,
        out WeaponRuntimeState state)
    {
        weapon = SelectedWeapon;
        state = null;
        return weapon != null &&
            weaponStates.TryGetValue(weapon.Id, out state);
    }

    public bool TryBuyWeaponOnServer(WeaponId weaponId, int price)
    {
        if (!IsServer ||
            !IsSpawned ||
            !combatMode ||
            price < 0 ||
            money.Value < price ||
            health == null ||
            health.LifeState != PlayerLifeState.Alive ||
            inventory == null ||
            inventory.HasWeaponInLoadout(weaponId) ||
            !inventory.TryGetDefinition(
                weaponId,
                out WeaponDefinition definition))
        {
            return false;
        }

        bool createdState = false;

        if (!weaponStates.ContainsKey(weaponId))
        {
            weaponStates.Add(weaponId, new WeaponRuntimeState
            {
                MagazineAmmunition = definition.UsesAmmunition
                    ? definition.MagazineSize
                    : 0,
                ReserveAmmunition = definition.UsesAmmunition
                    ? definition.InitialReserveAmmunition
                    : 0,
                ReloadEndsAt = 0d,
                NextAttackAt = 0d
            });

            createdState = true;
        }

        if (!inventory.TryAddPurchasedWeaponOnServer(weaponId))
        {
            if (createdState)
                weaponStates.Remove(weaponId);

            return false;
        }

        money.Value -= price;
        PublishSelectedWeaponState();

        Debug.Log(
            $"[Tienda][Servidor] Cliente {OwnerClientId}: " +
            $"compro {definition.DisplayName} por ${price}. " +
            $"Saldo: ${money.Value}");

        return true;
    }

    private void PublishSelectedWeaponState()
    {
        if (!IsServer || !TryGetSelectedWeaponState(
                out WeaponDefinition weapon,
                out WeaponRuntimeState state))
        {
            if (IsServer)
            {
                ammunition.Value = 0;
                reserveAmmunition.Value = 0;
                reloadEndsAt.Value = 0d;
            }
            return;
        }

        ammunition.Value = weapon.UsesAmmunition
            ? state.MagazineAmmunition
            : 0;
        reserveAmmunition.Value = weapon.UsesAmmunition
            ? state.ReserveAmmunition
            : 0;
        reloadEndsAt.Value = state.ReloadEndsAt;
    }

    public void ApplyDeathMoneyPenalty()
    {
        if (!IsServer || deathPenaltyApplied)
            return;

        deathPenaltyApplied = true;
        int previous = money.Value;
        money.Value = Mathf.Max(0, money.Value - moneyLostOnDeath);

        Debug.Log(
            $"[Dinero][Servidor] Cliente {OwnerClientId} murio: " +
            $"-{moneyLostOnDeath} | ${previous} -> ${money.Value}"
        );
    }

    public void CancelReloadForDeath()
    {
        if (!IsServer || !combatMode)
            return;

        foreach (WeaponRuntimeState state in weaponStates.Values)
            state.ReloadEndsAt = 0d;

        PublishSelectedWeaponState();
    }

    public void ResetForRespawn()
    {
        if (!IsServer || !combatMode)
            return;

        deathPenaltyApplied = false;
        ResetAllWeaponStates();
        PublishSelectedWeaponState();

        GetComponent<NetworkPlayerShop>()?
            .BeginPurchaseWindowOnServer();
    }

    private void OnAmmunitionChanged(int previous, int current) =>
        AmmunitionChanged?.Invoke(current);

    private void OnReserveAmmunitionChanged(int previous, int current) =>
        ReserveAmmunitionChanged?.Invoke(current);

    private void OnReloadChanged(double previous, double current) =>
        ReloadChanged?.Invoke();

    private void OnMoneyChanged(int previous, int current)
    {
        if (!IsOwner)
            return;

        MoneyChanged?.Invoke(current);

        Debug.Log(
            $"[Dinero][Local] Cliente {OwnerClientId}: " +
            $"${previous} -> ${current}"
        );
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PresentAttackRpc(
        WeaponId weaponId,
        WeaponAttackType attackType)
    {
        AttackPresented?.Invoke(weaponId, attackType);
    }

    [Rpc(SendTo.Owner)]
    private void ShotFeedbackRpc(
        ShotOutcome outcome,
        ulong victimClientId,
        int appliedDamage,
        ulong deathEventId)
    {
        lastShotSummary = outcome switch
        {
            ShotOutcome.Hit =>
                $"Impacto confirmado: jugador {victimClientId}, -{appliedDamage} HP",
            ShotOutcome.Kill =>
                $"Muerte confirmada: jugador {victimClientId}, evento #{deathEventId}",
            ShotOutcome.ZombieHit =>
                $"Zombie impactado: -{appliedDamage} HP",
            ShotOutcome.ZombieKill =>
                $"Zombie eliminado: +${moneyPerZombieKill}",
            ShotOutcome.FriendlyBlocked => "Aliado delante: sin dano",
            ShotOutcome.Blocked => "Impacto en obstaculo / objetivo invulnerable",
            ShotOutcome.Miss => "Ataque sin impacto",
            ShotOutcome.NoAmmo => "Sin balas: R para recargar",
            ShotOutcome.Reloading => "Recargando",
            ShotOutcome.Cooldown => "Cadencia: espera el proximo ataque",
            ShotOutcome.Dead => "No podes atacar mientras estas muerto",
            ShotOutcome.NotPlaying => "La partida aun no esta en Playing",
            ShotOutcome.InvalidWeapon => "Arma sin configurar en el inventario",
            ShotOutcome.TrainingHit =>
                $"Blanco de entrenamiento: -{appliedDamage} HP",
            _ => "Mira invalida: ataque rechazado"
        };
    }

    private static Vector3 ApplySpread(
        Vector3 direction,
        float spread)
    {
        Vector3 right = Vector3.Cross(Vector3.up, direction);
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.right;
        right.Normalize();

        Vector3 up = Vector3.Cross(direction, right).normalized;
        return (direction +
            right * UnityEngine.Random.Range(-spread, spread) +
            up * UnityEngine.Random.Range(-spread, spread)).normalized;
    }

    private static bool IsFinite(Vector3 vector) =>
        IsFinite(vector.x) && IsFinite(vector.y) && IsFinite(vector.z);

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
