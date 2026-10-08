using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Ciclo común autoritativo. Replica hitos temporales y cada cliente calcula
/// el tiempo visible con el reloj sincronizado de Netcode.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkLobbySession))]
public class NetworkMatchManager : NetworkBehaviour
{
    public static NetworkMatchManager Instance { get; private set; }

    [SerializeField, Min(0f)] private float countdownSeconds = 3f;

    private readonly NetworkVariable<MatchPhase> phase = new(
        MatchPhase.WaitingForPlayers,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private readonly NetworkVariable<float> durationSeconds = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private readonly NetworkVariable<double> countdownEndsAt = new(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private readonly NetworkVariable<double> matchStartedAt = new(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private readonly NetworkVariable<double> matchFinishedAt = new(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private readonly NetworkVariable<MatchResultData> result = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkLobbySession lobbySession;
    private bool legacyRaceActive;
    private bool finishNotificationSent;
    private ulong nextDeathEventId;
    private ulong lastAnnouncedDeathEventId;
    private int lastCountdownSecond = -1;

    public event Action<MatchPhase> PhaseChanged;
    public event Action<MatchResultData> MatchFinished;
    public event Action<int> CountdownSecondChanged;
    // Sólo presentación en host y clientes; las reglas escuchan ServerDeathConfirmed.
    public event Action<PlayerDeathInfo> DeathAnnounced;
    public event Action TimeExpired;
    // Los modos pueden escuchar esta confirmación del servidor para contar puntos.
    public event Action<PlayerDeathInfo> ServerDeathConfirmed;

    public MatchPhase Phase => phase.Value;
    public float DurationSeconds => durationSeconds.Value;
    public double MatchStartedAt => matchStartedAt.Value;
    public MatchResultData Result => result.Value;

    public float CountdownRemaining => phase.Value == MatchPhase.Countdown
        ? Mathf.Max(0f, (float)(countdownEndsAt.Value - NetworkManager.ServerTime.Time))
        : 0f;

    public float TimeRemaining
    {
        get
        {
            if (phase.Value == MatchPhase.WaitingForPlayers ||
                phase.Value == MatchPhase.Countdown)
                return durationSeconds.Value;

            double endTime = phase.Value == MatchPhase.Finished
                ? matchFinishedAt.Value
                : NetworkManager.ServerTime.Time;

            return Mathf.Max(
                0f,
                durationSeconds.Value -
                    (float)(endTime - matchStartedAt.Value)
            );
        }
    }

    private void Awake()
    {
        lobbySession = GetComponent<NetworkLobbySession>();
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
        phase.OnValueChanged += OnPhaseValueChanged;
        result.OnValueChanged += OnResultValueChanged;

        if (!IsServer)
            return;

        ResetValues();
        lobbySession.LobbyStateChanged += OnLobbyStateChanged;
        OnLobbyStateChanged();
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;

        phase.OnValueChanged -= OnPhaseValueChanged;
        result.OnValueChanged -= OnResultValueChanged;

        if (lobbySession != null)
            lobbySession.LobbyStateChanged -= OnLobbyStateChanged;

        TimeExpired = null;
        PhaseChanged = null;
        MatchFinished = null;
        CountdownSecondChanged = null;
        DeathAnnounced = null;
        ServerDeathConfirmed = null;
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        NotifyCountdownSecond();

        if (!IsServer)
            return;

        if (phase.Value == MatchPhase.Countdown &&
            NetworkManager.ServerTime.Time >= countdownEndsAt.Value)
            BeginMatch();

        if (phase.Value != MatchPhase.Playing || legacyRaceActive ||
            TimeRemaining > 0f)
            return;

        TimeExpired?.Invoke();

        // Hasta que TDM/CTF calculen sus marcadores, un timeout sin
        // consumidor se cierra como empate. El primer cierre queda congelado.
        if (phase.Value == MatchPhase.Playing)
        {
            TryFinishMatch(new MatchResultData(
                TeamId.None,
                MatchEndReason.TimeExpired,
                true
            ));
        }
    }

    public bool TryStartCountdown()
    {
        if (!IsServer || !IsSpawned ||
            lobbySession == null ||
            lobbySession.Phase != SessionPhase.InMatch ||
            !MatchStateRules.CanStartCountdown(phase.Value))
            return false;

        GameModeDefinition definition = lobbySession.SelectedDefinition;

        if (definition == null || !definition.IsValid(out _))
            return false;

        legacyRaceActive = false;
        durationSeconds.Value = definition.DurationSeconds;
        countdownEndsAt.Value = NetworkManager.ServerTime.Time + countdownSeconds;
        matchStartedAt.Value = 0d;
        matchFinishedAt.Value = 0d;
        result.Value = default;
        phase.Value = MatchPhase.Countdown;
        return true;
    }

    public bool TryCancelCountdown()
    {
        if (!IsServer ||
            !MatchStateRules.CanCancelCountdown(phase.Value))
            return false;

        countdownEndsAt.Value = 0d;
        matchStartedAt.Value = 0d;
        matchFinishedAt.Value = 0d;
        durationSeconds.Value = 0f;
        result.Value = default;
        legacyRaceActive = false;
        phase.Value = MatchPhase.WaitingForPlayers;
        return true;
    }

    public bool TryBeginLegacyRace(float raceDurationSeconds)
    {
        if (!IsServer || !IsSpawned || raceDurationSeconds <= 0f ||
            lobbySession == null ||
            lobbySession.SelectedGameModeId != GameModeId.None ||
            !MatchStateRules.CanStartCountdown(phase.Value))
            return false;

        legacyRaceActive = true;
        durationSeconds.Value = raceDurationSeconds;
        countdownEndsAt.Value = NetworkManager.ServerTime.Time;
        result.Value = default;
        phase.Value = MatchPhase.Countdown;
        return BeginMatch();
    }

    public bool TryFinishMatch(MatchResultData finalResult)
    {
        if (!IsServer || !IsSpawned ||
            !MatchStateRules.CanFinishMatch(phase.Value) ||
            finalResult.IsPending)
            return false;

        matchFinishedAt.Value = NetworkManager.ServerTime.Time;
        result.Value = finalResult;
        phase.Value = MatchPhase.Finished;
        return true;
    }

    public bool TryRegisterPlayerDeath(
        ulong victimClientId,
        ulong attackerClientId,
        out PlayerDeathInfo death)
    {
        death = default;

        if (!IsServer || !IsSpawned || phase.Value != MatchPhase.Playing ||
            lobbySession == null ||
            !CombatValidationRules.IsCombatMode(lobbySession.SelectedGameModeId) ||
            !lobbySession.TryGetPlayerData(victimClientId, out LobbyPlayerData victim) ||
            !lobbySession.TryGetPlayerData(attackerClientId, out LobbyPlayerData attacker) ||
            !CombatValidationRules.IsEnemy(attacker.TeamId, victim.TeamId))
            return false;

        death = new PlayerDeathInfo(
            ++nextDeathEventId,
            victimClientId,
            victim.TeamId,
            true,
            attackerClientId,
            attacker.TeamId,
            PlayerDeathCause.PlayerAttack
        );

        Debug.Log($"[Combat] Muerte #{death.EventId}: {attackerClientId} eliminó a {victimClientId}.");
        ServerDeathConfirmed?.Invoke(death);
        AnnounceDeathRpc(death);
        return true;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void AnnounceDeathRpc(PlayerDeathInfo death)
    {
        if (death.EventId == 0UL || death.EventId <= lastAnnouncedDeathEventId)
            return;

        lastAnnouncedDeathEventId = death.EventId;
        DeathAnnounced?.Invoke(death);
    }

    private void NotifyCountdownSecond()
    {
        if (phase.Value != MatchPhase.Countdown)
        {
            lastCountdownSecond = -1;
            return;
        }

        int second = Mathf.CeilToInt(CountdownRemaining);
        if (second <= 0 || second == lastCountdownSecond)
            return;

        lastCountdownSecond = second;
        CountdownSecondChanged?.Invoke(second);
    }

    public bool TryResetForRematch()
    {
        if (!IsServer || !IsSpawned ||
            !MatchStateRules.CanResetForRematch(phase.Value, true))
            return false;

        ResetValues();
        return true;
    }

    private bool BeginMatch()
    {
        if (!IsServer ||
            !MatchStateRules.CanBeginMatch(phase.Value))
            return false;

        matchStartedAt.Value = NetworkManager.ServerTime.Time;
        countdownEndsAt.Value = 0d;
        phase.Value = MatchPhase.Playing;
        return true;
    }

    private void OnLobbyStateChanged()
    {
        if (!IsServer || lobbySession == null)
            return;

        if (lobbySession.Phase == SessionPhase.InMatch)
        {
            if (phase.Value == MatchPhase.Finished)
                TryResetForRematch();

            TryStartCountdown();
        }
        else if (lobbySession.Phase == SessionPhase.Lobby)
        {
            TryCancelCountdown();
        }
    }

    private void ResetValues()
    {
        legacyRaceActive = false;
        durationSeconds.Value = 0f;
        countdownEndsAt.Value = 0d;
        matchStartedAt.Value = 0d;
        matchFinishedAt.Value = 0d;
        result.Value = default;
        phase.Value = MatchPhase.WaitingForPlayers;
        finishNotificationSent = false;
    }

    private void OnPhaseValueChanged(MatchPhase previous, MatchPhase current)
    {
        lastCountdownSecond = -1;
        PhaseChanged?.Invoke(current);

        if (current != MatchPhase.Finished)
            finishNotificationSent = false;

        TryNotifyFinished();
    }

    private void OnResultValueChanged(
        MatchResultData previous,
        MatchResultData current)
    {
        TryNotifyFinished();
    }

    private void TryNotifyFinished()
    {
        if (finishNotificationSent || phase.Value != MatchPhase.Finished ||
            result.Value.IsPending)
            return;

        finishNotificationSent = true;
        MatchFinished?.Invoke(result.Value);
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        base.OnDestroy();
    }
}
