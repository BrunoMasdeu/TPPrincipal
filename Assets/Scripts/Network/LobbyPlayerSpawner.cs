using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;


[RequireComponent(typeof(NetworkManager))]
public class LobbyPlayerSpawner : MonoBehaviour
{
    // Puente de las escenas locales sin modo. Las partidas con modo seleccionado usan
    // NetworkLobbySession y NetworkPlayerSpawner.
    [SerializeField] private GameObject networkPlayerPrefab;
    [SerializeField] private string tutorialSceneName = "CentroEntrenamiento";
    [SerializeField] private string zombiesSceneName = "MapaZombies";
    [SerializeField] private string spawnPointName = "Respawn";
    [SerializeField] private float playerSpacing = 1.5f;
    [SerializeField, Min(1)] private int requiredPlayerCount = 2;

    private NetworkManager networkManager;
    private NetworkSessionBootstrap sessionBootstrap;
    private NetworkPlayerSpawner teamSpawner;
    private readonly HashSet<ulong> loadedClientIds = new();

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
        sessionBootstrap = GetComponent<NetworkSessionBootstrap>();

        networkManager.ConnectionApprovalCallback = ApproveConnection;
        networkManager.OnServerStarted += OnServerStarted;
        networkManager.OnServerStopped += OnServerStopped;
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public void ConfigureRequiredPlayerCount(int playerCount)
    {
        if (networkManager != null && networkManager.IsListening)
        {
            Debug.LogWarning(
                "No se puede cambiar la cantidad de jugadores con la red iniciada."
            );
            return;
        }

        requiredPlayerCount = Mathf.Max(1, playerCount);
    }

    private void ApproveConnection(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        NetworkLobbySession lobbySession = NetworkLobbySession.Instance;
        bool networkSessionActive = lobbySession != null &&
            lobbySession.IsSpawned;

        bool networkSessionPrepared = sessionBootstrap != null &&
            sessionBootstrap.HasPreparedSession;

        bool multiplayerSession = networkSessionActive || networkSessionPrepared;

        int capacity = multiplayerSession
            ? MultiplayerSessionLimits.RoomCapacity
            : requiredPlayerCount;

        // [COG-586] Si el host ya eligió la cantidad de jugadores, la sala se
        // llena con esa cantidad (antes siempre dejaba entrar hasta 8).
        if (networkSessionActive && lobbySession.SelectedPlayerCount > 0)
            capacity = lobbySession.SelectedPlayerCount;

        int connected = networkManager.ConnectedClientsIds.Count;
        string playerName = NombreJugadorRules.DesdeBytes(request.Payload);
        string rejection = null;

        if (networkSessionActive && lobbySession.Phase != SessionPhase.Lobby)
        {
            rejection = "La partida ya comenzó.";
        }
        else if (!LobbyValidationRules.HasCapacity(connected, capacity))
        {
            rejection = $"La sala está llena ({connected}/{capacity}).";
        }
        else if (multiplayerSession)
        {
            // [COG-587] El servidor vuelve a revisar el nombre: no alcanza con
            // que lo revise la PC del jugador.
            rejection = NombreJugadorRules.Validar(playerName);

            if (rejection == null && NetworkLobbySession.NombreEnUso(playerName))
                rejection = "Ese nombre ya está en uso en la sala.";
        }

        response.Approved = rejection == null;

        if (response.Approved && multiplayerSession)
            NetworkLobbySession.GuardarNombreAprobado(request.ClientNetworkId, playerName);

        // No crear al jugador mientras permanece en el lobby.
        response.CreatePlayerObject = false;

        response.Position = null;
        response.Rotation = null;
        response.Reason = rejection ?? string.Empty;
        response.Pending = false;
    }

    private void OnServerStarted()
    {
        if (sessionBootstrap != null && sessionBootstrap.HasPreparedSession)
        {
            NetworkLobbySession session = sessionBootstrap.CurrentSession;
            teamSpawner = session != null
                ? session.GetComponent<NetworkPlayerSpawner>()
                : null;

            if (session == null || teamSpawner == null)
            {
                Debug.LogError(
                    "La sesión multijugador necesita NetworkLobbySession " +
                    "y NetworkPlayerSpawner en NetworkSessionRoot."
                );
            }
        }

        networkManager.SceneManager.OnLoadEventCompleted
            += OnLoadEventCompleted;
    }

    private void OnServerStopped(bool wasClient)
    {
        if (networkManager.SceneManager != null)
        {
            networkManager.SceneManager.OnLoadEventCompleted -=
                OnLoadEventCompleted;
        }

        loadedClientIds.Clear();
        teamSpawner = null;
        NetworkLobbySession.OlvidarNombresAprobados();
    }

    private void OnLoadEventCompleted(
        string sceneName,
        LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut)
    {
        if (!networkManager.IsServer ||
            (NetworkLobbySession.Instance != null &&
             NetworkLobbySession.Instance.SelectedGameModeId != GameModeId.None) ||
            !IsLocalScene(sceneName))
            return;

        loadedClientIds.Clear();

        foreach (ulong clientId in clientsCompleted)
        {
            if (networkManager.ConnectedClients.ContainsKey(clientId))
                loadedClientIds.Add(clientId);
        }

        if (clientsTimedOut.Count > 0)
        {
            Debug.LogError(
                "El tutorial local no puede iniciar porque la carga agotó el tiempo."
            );
            return;
        }

        foreach (ulong clientId in loadedClientIds)
            SpawnPlayer(clientId);
    }

    private void OnClientConnected(ulong clientId)
    {
        // Permite generar un jugador si alguien entra cuando
        // GameScene ya se encuentra abierta.
        if (networkManager.IsServer &&
            (NetworkLobbySession.Instance == null ||
             NetworkLobbySession.Instance.SelectedGameModeId == GameModeId.None) &&
            IsLocalScene(SceneManager.GetActiveScene().name))
        {
            SpawnPlayer(clientId);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        loadedClientIds.Remove(clientId);

        if (networkManager == null || networkManager.IsServer ||
            clientId != networkManager.LocalClientId ||
            SceneManager.GetActiveScene().name == "MenuScene")
            return;

        MatchHudPresenter hud = FindAnyObjectByType<MatchHudPresenter>(
            FindObjectsInactive.Include);
        if (hud != null && hud.IsLeaving)
            return;

        DisconnectedClientReturnToMenu.Begin(networkManager);
    }

    private bool IsLocalScene(string sceneName)
    {
        return sceneName == tutorialSceneName ||
            sceneName == zombiesSceneName;
    }

    private void SpawnPlayer(ulong clientId)
    {
        if (!networkManager.ConnectedClients.TryGetValue(
                clientId, out NetworkClient client))
            return;

        if (client.PlayerObject != null)
            return;

        Transform spawnPoint = null;
        GameObject spawnObject = GameObject.Find(spawnPointName);

        if (spawnObject != null)
            spawnPoint = spawnObject.transform;

        IReadOnlyList<ulong> connectedClientIds =
            networkManager.ConnectedClientsIds;

        int playerIndex = 0;

        for (int i = 0; i < connectedClientIds.Count; i++)
        {
            if (connectedClientIds[i] == clientId)
            {
                playerIndex = i;
                break;
            }
        }

        int playerCount = connectedClientIds.Count;

        float offset =
            (playerIndex - (playerCount - 1) * 0.5f) * playerSpacing;

        Vector3 position = spawnPoint != null
            ? spawnPoint.position + spawnPoint.right * offset
            : Vector3.right * offset;

        Quaternion rotation = spawnPoint != null
            ? spawnPoint.rotation * networkPlayerPrefab.transform.rotation
            : networkPlayerPrefab.transform.rotation;

        GameObject playerInstance =
            Instantiate(networkPlayerPrefab, position, rotation);

        NetworkObject networkObject =
            playerInstance.GetComponent<NetworkObject>();

        networkObject.SpawnAsPlayerObject(clientId, true);
    }

    private void OnDestroy()
    {
        if (networkManager == null)
            return;

        networkManager.OnServerStarted -= OnServerStarted;
        networkManager.OnServerStopped -= OnServerStopped;
        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnected;

        if (networkManager.SceneManager != null)
        {
            networkManager.SceneManager.OnLoadEventCompleted
                -= OnLoadEventCompleted;
        }
    }
}

public static class RaceStartReadiness
{
    public static bool ArePlayersReady(
        int requiredPlayerCount,
        int connectedPlayerCount,
        int loadedPlayerCount,
        int spawnedPlayerCount,
        int timedOutPlayerCount)
    {
        return PlayerSpawnReadinessRules.ArePlayersReady(
            requiredPlayerCount,
            connectedPlayerCount,
            loadedPlayerCount,
            spawnedPlayerCount,
            timedOutPlayerCount
        );
    }
}
