using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Panel local de prueba: no modifica ni replica reglas de juego.</summary>
public class CombatDebugOverlay : NetworkBehaviour
{
    private NetworkPlayerHealth health;
    private NetworkPlayerCombat combat;
    private NetworkMatchManager subscribedMatch;
    private CameraMovement cameraMovement;
    private readonly List<DeathNotice> deathNotices = new();
    private ulong lastDisplayedDeathEventId;
    private bool resultsCursorEnabled;
    private bool visible = true;

    private struct DeathNotice
    {
        public PlayerDeathInfo Death;
        public float ExpiresAt;
    }

    private void Awake()
    {
        health = GetComponent<NetworkPlayerHealth>();
        combat = GetComponent<NetworkPlayerCombat>();
        cameraMovement = GetComponentInChildren<CameraMovement>(true);
    }

    private void Update()
    {
        if (IsSpawned && IsOwner && subscribedMatch == null &&
            NetworkMatchManager.Instance != null)
        {
            subscribedMatch = NetworkMatchManager.Instance;
            subscribedMatch.DeathAnnounced += OnDeathAnnounced;
        }

        deathNotices.RemoveAll(notice => notice.ExpiresAt <= Time.unscaledTime);

        GameModeId mode = NetworkLobbySession.Instance != null
            ? NetworkLobbySession.Instance.SelectedGameModeId
            : GameModeId.None;
        if (IsSpawned && IsOwner && !resultsCursorEnabled &&
            (mode == GameModeId.TDM || mode == GameModeId.CTF) &&
            NetworkMatchManager.Instance?.Phase == MatchPhase.Finished)
        {
            resultsCursorEnabled = true;
            if (cameraMovement != null)
                cameraMovement.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (IsSpawned && IsOwner && Keyboard.current != null &&
            Keyboard.current.f8Key.wasPressedThisFrame)
            visible = !visible;
    }

    public override void OnNetworkDespawn()
    {
        if (subscribedMatch != null)
            subscribedMatch.DeathAnnounced -= OnDeathAnnounced;

        subscribedMatch = null;
        deathNotices.Clear();
        lastDisplayedDeathEventId = 0UL;
    }

    private void OnDeathAnnounced(PlayerDeathInfo death)
    {
        if (death.EventId == 0UL || death.EventId <= lastDisplayedDeathEventId)
            return;

        lastDisplayedDeathEventId = death.EventId;
        deathNotices.Add(new DeathNotice
        {
            Death = death,
            ExpiresAt = Time.unscaledTime + 5f
        });

        if (deathNotices.Count > 3)
            deathNotices.RemoveAt(0);
    }

    private void OnGUI()
    {
        if (!IsSpawned || !IsOwner || !visible ||
            health == null || combat == null)
            return;

        NetworkLobbySession lobby = NetworkLobbySession.Instance;
        if (lobby != null && lobby.SelectedGameModeId == GameModeId.Race)
            return;

        TeamId team = lobby != null &&
            lobby.TryGetPlayerData(OwnerClientId, out LobbyPlayerData player)
                ? player.TeamId : TeamId.None;
        MatchPhase phase = NetworkMatchManager.Instance != null
            ? NetworkMatchManager.Instance.Phase
            : MatchPhase.WaitingForPlayers;

        Rect panel = new Rect(12f, 12f, 490f, 225f);
        GUI.Box(panel, string.Empty);
        GUILayout.BeginArea(new Rect(22f, 18f, 470f, 215f));

        GUIStyle label = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true
        };

        GUILayout.Label($"DEBUG COMBATE [F8]  |  Cliente {OwnerClientId}  |  {team}", label);
        GUILayout.Label($"Modo: {(lobby != null ? lobby.SelectedGameModeId.ToString() : "SIN SESIÓN")}  |  Fase: {phase}", label);
        GUILayout.Label($"Circuito de red: combate={combat.IsCombatActive}, vida={health.IsCombatActive}", label);
        GUILayout.Label($"Vida: {health.CurrentHealth}/{health.MaxHealth}  |  Estado: {health.LifeState}", label);
        GUILayout.Label($"Balas: {combat.Ammunition}/{combat.MagazineSize}  |  Recarga: {combat.ReloadRemaining:0.0}s", label);
        GUILayout.Label($"Último disparo: {combat.LastShotSummary}", label);
        GUILayout.Label($"Última muerte: #{health.LastDeathEventId}  |  Respawn: {health.RespawnRemaining:0.0}s", label);
        GUILayout.Label("Clic izquierdo: disparar  |  R: recargar  |  Clic derecho: gancho", label);
        GUILayout.EndArea();

        if (lobby != null && lobby.SelectedGameModeId == GameModeId.TDM)
        {
            DrawTdmPanel(label, phase);
            DrawDeathNotices(label);
        }
        else if (lobby != null && lobby.SelectedGameModeId == GameModeId.CTF)
        {
            DrawCtfPanel(label, phase);
        }
    }

    private void DrawCtfPanel(GUIStyle label, MatchPhase phase)
    {
        CtfMatchManager ctf = CtfMatchManager.Instance;
        NetworkMatchManager match = NetworkMatchManager.Instance;
        float x = Screen.width < 920 ? 12f : Screen.width - 402f;
        float y = Screen.width < 920 ? 245f : 12f;
        float height = 265f + (phase == MatchPhase.Finished &&
            NetworkManager != null && NetworkManager.IsHost ? 38f : 0f);

        GUI.Box(new Rect(x, y, 390f, height), string.Empty);
        GUILayout.BeginArea(new Rect(x + 10f, y + 6f, 370f, height - 12f));
        GUILayout.Label("DEBUG CTF [F8]", label);
        GUILayout.Label($"Fase: {phase}", label);

        if (match != null)
        {
            if (phase == MatchPhase.Countdown)
                GUILayout.Label($"Cuenta regresiva: {match.CountdownRemaining:0.0}s", label);
            GUILayout.Label($"Tiempo restante: {match.TimeRemaining:0.0}s", label);
        }

        if (ctf == null)
        {
            GUILayout.Label("CTF: componente no configurado", label);
        }
        else
        {
            GUILayout.Label($"Capturas: Rojo {ctf.RedCaptures} | Azul {ctf.BlueCaptures}", label);
            GUILayout.Label(ctf.TryGetCaptureLimit(out int limit)
                ? $"Límite por equipo: {limit} capturas"
                : "Límite por equipo: no definido", label);
            GUILayout.Label(ctf.HasBaseZones
                ? "Zonas de base: Rojo y Azul configuradas"
                : "Zonas de base: sin configurar en servidor", label);
            GUILayout.Label(DescribeFlag("Bandera roja", ctf.RedFlag,
                ctf.GetReturnRemaining(TeamId.Red)), label);
            GUILayout.Label(DescribeFlag("Bandera azul", ctf.BlueFlag,
                ctf.GetReturnRemaining(TeamId.Blue)), label);
        }

        if (match != null && phase == MatchPhase.Finished && !match.Result.IsPending)
        {
            MatchResultData result = match.Result;
            GUILayout.Label(result.IsDraw
                ? $"Resultado: empate ({result.EndReason})"
                : $"Resultado: ganó {result.WinningTeam} ({result.EndReason})", label);
        }

        if (phase == MatchPhase.Finished && NetworkManager != null &&
            NetworkManager.IsHost && GUILayout.Button("Volver al lobby (host)"))
        {
            Debug.Log("[CTF] El host pulsó Volver al lobby.");
            if (NetworkLobbySession.Instance == null ||
                !NetworkLobbySession.Instance.TryReturnToLobbyAfterMatch())
                Debug.LogWarning("[CTF] No se pudo solicitar el regreso al lobby.");
        }

        GUILayout.EndArea();
    }

    private static string DescribeFlag(string label, CtfFlagSnapshot flag,
        float returnRemaining)
    {
        return flag.State switch
        {
            FlagState.Carried => $"{label}: Carried por jugador {flag.CarrierClientId}",
            FlagState.Dropped => $"{label}: Dropped en {flag.DropPosition} | vuelve en {returnRemaining:0.0}s",
            _ => $"{label}: AtBase"
        };
    }

    private void DrawTdmPanel(GUIStyle label, MatchPhase phase)
    {
        TdmMatchManager tdm = TdmMatchManager.Instance;
        NetworkMatchManager match = NetworkMatchManager.Instance;
        int count = tdm != null ? tdm.PlayerStatsCount : 0;
        float height = 210f + count * 19f +
            (phase == MatchPhase.Finished && NetworkManager != null &&
             NetworkManager.IsHost ? 38f : 0f);
        bool narrowScreen = Screen.width < 920;
        float x = narrowScreen ? 12f : Screen.width - 402f;
        float y = narrowScreen ? 245f : 12f;

        GUI.Box(new Rect(x, y, 390f, height), string.Empty);
        GUILayout.BeginArea(new Rect(x + 10f, y + 6f, 370f, height - 12f));
        GUILayout.Label("DEBUG TDM [F8]", label);
        GUILayout.Label($"Modo: TDM  |  Fase: {phase}", label);

        if (match != null)
        {
            if (phase == MatchPhase.Countdown)
                GUILayout.Label($"Cuenta regresiva: {match.CountdownRemaining:0.0}s", label);
            GUILayout.Label($"Tiempo restante: {match.TimeRemaining:0.0}s", label);
        }

        if (tdm == null)
        {
            GUILayout.Label("Marcador TDM: componente no configurado", label);
        }
        else
        {
            GUILayout.Label($"Eliminaciones: Rojo {tdm.RedEliminations}  |  Azul {tdm.BlueEliminations}", label);
            GUILayout.Label(tdm.TryGetOfficialScoreLimit(out int limit)
                ? $"Límite por equipo: {limit} eliminaciones"
                : "Límite por equipo: sin límite oficial", label);

            GUILayout.Label("Tabla (jugador ID | equipo | kills | deaths):", label);
            for (int i = 0; i < count; i++)
            {
                if (tdm.TryGetPlayerStats(i, out PlayerMatchStats stats))
                    GUILayout.Label($"{stats.ClientId} | {stats.TeamId} | {stats.Kills} | {stats.Deaths}", label);
            }
        }

        if (match != null && phase == MatchPhase.Finished && !match.Result.IsPending)
        {
            MatchResultData result = match.Result;
            GUILayout.Label(result.IsDraw
                ? $"Resultado: empate ({result.EndReason})"
                : $"Resultado: ganó {result.WinningTeam} ({result.EndReason})", label);
        }

        if (phase == MatchPhase.Finished && NetworkManager != null &&
            NetworkManager.IsHost && GUILayout.Button("Volver al lobby (host)"))
        {
            Debug.Log("[TDM] El host pulsó Volver al lobby.");
            if (NetworkLobbySession.Instance == null ||
                !NetworkLobbySession.Instance.TryReturnToLobbyAfterMatch())
                Debug.LogWarning("[TDM] No se pudo solicitar el regreso al lobby.");
        }

        GUILayout.EndArea();
    }

    private void DrawDeathNotices(GUIStyle label)
    {
        if (deathNotices.Count == 0)
            return;

        float y = Screen.width < 920
            ? 467f + (TdmMatchManager.Instance?.PlayerStatsCount ?? 0) * 19f +
              (NetworkMatchManager.Instance?.Phase == MatchPhase.Finished &&
               NetworkManager != null && NetworkManager.IsHost ? 38f : 0f)
            : 250f;
        float height = 28f + deathNotices.Count * 46f;
        GUI.Box(new Rect(12f, y, 490f, height), string.Empty);
        GUILayout.BeginArea(new Rect(22f, y + 5f, 470f, height - 10f));
        GUILayout.Label("MUERTES CONFIRMADAS", label);

        foreach (DeathNotice notice in deathNotices)
        {
            PlayerDeathInfo death = notice.Death;
            GUILayout.Label($"Eliminado: {death.VictimClientId} | {death.VictimTeamId}", label);
            GUILayout.Label(death.HasAttacker
                ? $"Eliminado por: {death.AttackerClientId} | {death.AttackerTeamId}"
                : $"Causa: {death.Cause}", label);
        }

        GUILayout.EndArea();
    }
}
