using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Panel local de prueba: no modifica ni replica reglas de juego.</summary>
public class CombatDebugOverlay : NetworkBehaviour
{
    private NetworkPlayerHealth health;
    private NetworkPlayerCombat combat;
    private bool visible = true;

    private void Awake()
    {
        health = GetComponent<NetworkPlayerHealth>();
        combat = GetComponent<NetworkPlayerCombat>();
    }

    private void Update()
    {
        if (IsSpawned && IsOwner && Keyboard.current != null &&
            Keyboard.current.f8Key.wasPressedThisFrame)
            visible = !visible;
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
    }
}
