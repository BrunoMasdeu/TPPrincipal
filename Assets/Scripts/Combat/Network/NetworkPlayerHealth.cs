using System;
using Unity.Netcode;
using UnityEngine;

internal struct PlayerLifeSnapshot : INetworkSerializeByMemcpy
{
    public PlayerLifeState State;
    public Vector3 DeathPosition;
    public Quaternion DeathRotation;
}

/// <summary>Vida y respawn autoritativos del servidor para TDM/CTF.</summary>
public class NetworkPlayerHealth : NetworkBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField, Min(0f)] private float respawnDelaySeconds = 5f;

    private readonly NetworkVariable<int> currentHealth = new(
        100, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<PlayerLifeSnapshot> life = new(
        default, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<double> respawnAt = new(
        0d, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<ulong> lastDeathEventId = new(
        0UL, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private PlayerNetworkSetup playerSetup;
    private NetworkPlayerCombat combat;
    private NetworkPlayerCrouch crouch;
    private NetworkPlayerGrenades grenades;
    private NetworkPlayerInventory inventory;
    private bool combatMode;
    private ulong respawnSequence;

    public event Action<int> HealthChanged;
    public event Action<PlayerLifeState> LifeStateChanged;
    public event Action<Vector3> DamageReceived;

    public int CurrentHealth => currentHealth.Value;
    public int MaxHealth => maxHealth;
    public bool IsCombatActive => combatMode;
    public PlayerLifeState LifeState => life.Value.State;
    public ulong LastDeathEventId => lastDeathEventId.Value;
    public float RespawnRemaining => respawnAt.Value > 0d && NetworkManager != null
        ? Mathf.Max(0f, (float)(respawnAt.Value - NetworkManager.ServerTime.Time))
        : 0f;

    private void Awake()
    {
        playerSetup = GetComponent<PlayerNetworkSetup>();
        combat = GetComponent<NetworkPlayerCombat>();
        crouch = GetComponent<NetworkPlayerCrouch>();
        grenades = GetComponent<NetworkPlayerGrenades>();
        inventory = GetComponent<NetworkPlayerInventory>();
    }

    public override void OnNetworkSpawn()
    {
        currentHealth.OnValueChanged += OnHealthChanged;
        Debug.Log(
            $"[Combat] NetworkPlayerHealth presente: cliente={OwnerClientId}, " +
            $"owner={IsOwner}, servidor={IsServer}, " +
            $"modo={NetworkLobbySession.Instance?.SelectedGameModeId.ToString() ?? "sin sesión"}."
        );
        TryActivateCombatMode();
    }

    private void TryActivateCombatMode()
    {
        if (combatMode || !IsSpawned ||
            NetworkLobbySession.Instance == null ||
            !CombatValidationRules.IsCombatMode(
                NetworkLobbySession.Instance.SelectedGameModeId))
            return;

        combatMode = true;

        life.OnValueChanged += OnLifeStateChanged;
        ApplyLocalControls();

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            life.Value = default;
            respawnAt.Value = 0d;
            lastDeathEventId.Value = 0UL;
        }

        Debug.Log(
            $"[Combat] Vida de red ACTIVADA: cliente={OwnerClientId}, " +
            $"vida={currentHealth.Value}/{maxHealth}, estado={LifeState}."
        );
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;
        if (combatMode)
            life.OnValueChanged -= OnLifeStateChanged;

        HealthChanged = null;
        LifeStateChanged = null;
        DamageReceived = null;
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (!combatMode)
            TryActivateCombatMode();

        if (!combatMode || !IsServer ||
            LifeState != PlayerLifeState.Dead ||
            NetworkManager.ServerTime.Time < respawnAt.Value ||
            NetworkMatchManager.Instance == null ||
            NetworkMatchManager.Instance.Phase != MatchPhase.Playing)
            return;

        NetworkLobbySession lobby = NetworkLobbySession.Instance;
        NetworkPlayerSpawner spawner = lobby != null
            ? lobby.GetComponent<NetworkPlayerSpawner>()
            : null;

        if (lobby == null || spawner == null ||
            !lobby.TryGetPlayerData(OwnerClientId, out LobbyPlayerData player) ||
            !spawner.TryGetRespawnPoint(player.TeamId,
                out Vector3 position, out Quaternion rotation) ||
            playerSetup == null)
        {
            // Se reintenta sin inundar el log cada frame.
            respawnAt.Value = NetworkManager.ServerTime.Time + 1d;
            return;
        }

        respawnSequence++;
        life.Value = new PlayerLifeSnapshot { State = PlayerLifeState.Respawning };
        respawnAt.Value = 0d;
        playerSetup.RespawnForMatch(position, rotation, respawnSequence);
    }

    public bool ApplyDamageFromPlayer(
        ulong attackerClientId,
        int damage,
        Vector3 damageOrigin,
        out int appliedDamage,
        out ulong deathEventId)
    {
        appliedDamage = 0;
        deathEventId = 0UL;

        NetworkLobbySession lobby = NetworkLobbySession.Instance;
        NetworkMatchManager match = NetworkMatchManager.Instance;

        if (!combatMode || !IsServer || !IsSpawned ||
            LifeState != PlayerLifeState.Alive ||
            match == null || match.Phase != MatchPhase.Playing ||
            lobby == null ||
            !lobby.TryGetPlayerData(attackerClientId, out LobbyPlayerData attacker) ||
            !lobby.TryGetPlayerData(OwnerClientId, out LobbyPlayerData victim) ||
            !CombatValidationRules.IsEnemy(attacker.TeamId, victim.TeamId))
            return false;

        int before = currentHealth.Value;
        int after = CombatValidationRules.HealthAfterDamage(before, damage);
        appliedDamage = before - after;
        if (appliedDamage <= 0)
            return false;

        currentHealth.Value = after;
        PresentDamageRpc(damageOrigin);
        if (!CombatValidationRules.IsLethalTransition(before, after))
            return true;

        // El dueño del NetworkTransform y el pickup usan la misma pose de eliminación.
        var deathPose = new PlayerLifeSnapshot
        {
            State = PlayerLifeState.Dead,
            DeathPosition = transform.position,
            DeathRotation = transform.rotation
        };
        life.Value = deathPose;
        respawnAt.Value = NetworkManager.ServerTime.Time + respawnDelaySeconds;
        combat?.CancelReloadForDeath();
        crouch?.ForceStandingOnServer();
        inventory?.DropWeaponOnDeathOnServer(deathPose.DeathPosition);

        if (match.TryRegisterPlayerDeath(
            OwnerClientId, attackerClientId, out PlayerDeathInfo death))
        {
            deathEventId = death.EventId;
            lastDeathEventId.Value = death.EventId;
        }

        return true;
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void PresentDamageRpc(Vector3 damageOrigin)
    {
        if (IsOwner)
            DamageReceived?.Invoke(damageOrigin);
    }

    public void ConfirmMatchRespawn(ulong sequence, ulong ownerClientId)
    {
        if (!IsSpawned || !IsOwner || ownerClientId != OwnerClientId)
            return;

        if (IsServer)
            ConfirmMatchRespawnOnServer(sequence, ownerClientId);
        else
            ConfirmMatchRespawnRpc(sequence);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ConfirmMatchRespawnRpc(
        ulong sequence, RpcParams rpcParams = default)
    {
        ConfirmMatchRespawnOnServer(sequence, rpcParams.Receive.SenderClientId);
    }

    private void ConfirmMatchRespawnOnServer(ulong sequence, ulong sender)
    {
        if (!IsServer || sender != OwnerClientId ||
            LifeState != PlayerLifeState.Respawning ||
            sequence != respawnSequence ||
            NetworkMatchManager.Instance == null ||
            NetworkMatchManager.Instance.Phase != MatchPhase.Playing)
            return;

        currentHealth.Value = maxHealth;
        combat?.ResetForRespawn();
        grenades?.ResetForRespawnOnServer();
        crouch?.ForceStandingOnServer();
        life.Value = default;
        Debug.Log($"[Combat] Cliente {OwnerClientId} reapareció con {maxHealth} HP.");
    }

    private void OnLifeStateChanged(PlayerLifeSnapshot previous, PlayerLifeSnapshot current)
    {
        ApplyLocalControls();
        LifeStateChanged?.Invoke(current.State);
    }

    private void OnHealthChanged(int previous, int current)
    {
        HealthChanged?.Invoke(current);
    }

    private void ApplyLocalControls()
    {
        if (combatMode && IsOwner && playerSetup != null)
            playerSetup.ApplyMatchLife(life.Value);
    }

    public bool Heal(int amount)
    {
        if (!IsSpawned || !IsServer ||
            !combatMode ||
            LifeState != PlayerLifeState.Alive ||
            amount <= 0)
        {
            return false;
        }

        int previousHealth = currentHealth.Value;

        currentHealth.Value = Mathf.Min(
            maxHealth,
            currentHealth.Value + amount
        );

        return currentHealth.Value > previousHealth;
    }
}
