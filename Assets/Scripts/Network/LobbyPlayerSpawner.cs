using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;


[RequireComponent(typeof(NetworkManager))]
public class LobbyPlayerSpawner : MonoBehaviour
{
    // Puente temporal para la carrera. La sesión y el spawn por equipos
    // pertenecen a NetworkLobbySession y NetworkPlayerSpawner.
    [SerializeField] private GameObject networkPlayerPrefab;
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string spawnPointName = "Respawn";
    [SerializeField] private float playerSpacing = 1.5f;
    [SerializeField, Min(1)] private int requiredPlayerCount = 2;

    private NetworkManager networkManager;
    private NetworkSessionBootstrap sessionBootstrap;
    private NetworkPlayerSpawner teamSpawner;
    private readonly HashSet<ulong> loadedClientIds = new();
    private bool gameSceneLoadCompletedWithoutTimeout;

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
        bool modeSessionActive = lobbySession != null &&
            lobbySession.IsSpawned &&
            lobbySession.SelectedGameModeId != GameModeId.None;

        bool modeSessionPrepared = sessionBootstrap != null &&
            sessionBootstrap.HasPreparedSession;

        int capacity = modeSessionActive
            ? lobbySession.RequiredPlayerCount
            : modeSessionPrepared
                ? sessionBootstrap.PreparedPlayerCount
                : requiredPlayerCount;

        response.Approved =
            networkManager.ConnectedClientsIds.Count < capacity &&
            (!modeSessionActive || lobbySession.Phase == SessionPhase.Lobby);

        // No crear al jugador mientras permanece en el lobby.
        response.CreatePlayerObject = false;

        response.Position = null;
        response.Rotation = null;
        response.Reason = response.Approved
            ? string.Empty
            : "La sala está completa o la partida ya comenzó.";
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
        gameSceneLoadCompletedWithoutTimeout = false;
        teamSpawner = null;
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
            sceneName != gameSceneName)
            return;

        loadedClientIds.Clear();

        foreach (ulong clientId in clientsCompleted)
        {
            if (networkManager.ConnectedClients.ContainsKey(clientId))
                loadedClientIds.Add(clientId);
        }

        gameSceneLoadCompletedWithoutTimeout = clientsTimedOut.Count == 0;

        foreach (ulong clientId in loadedClientIds)
            SpawnPlayer(clientId);

        TryStartRace();
    }

    private void OnClientConnected(ulong clientId)
    {
        // Permite generar un jugador si alguien entra cuando
        // GameScene ya se encuentra abierta.
        if (networkManager.IsServer &&
            (NetworkLobbySession.Instance == null ||
             NetworkLobbySession.Instance.SelectedGameModeId == GameModeId.None) &&
            SceneManager.GetActiveScene().name == gameSceneName)
        {
            SpawnPlayer(clientId);
            TryStartRace();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        loadedClientIds.Remove(clientId);
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

    private void TryStartRace()
    {
        if (!networkManager.IsServer ||
            (NetworkLobbySession.Instance != null &&
             NetworkLobbySession.Instance.SelectedGameModeId != GameModeId.None) ||
            !gameSceneLoadCompletedWithoutTimeout)
        {
            return;
        }

        IReadOnlyList<ulong> connectedClientIds =
            networkManager.ConnectedClientsIds;

        int loadedPlayerCount = 0;
        int spawnedPlayerCount = 0;

        foreach (ulong clientId in connectedClientIds)
        {
            if (loadedClientIds.Contains(clientId))
                loadedPlayerCount++;

            if (!networkManager.ConnectedClients.TryGetValue(
                    clientId, out NetworkClient client))
            {
                continue;
            }

            NetworkObject playerObject = client.PlayerObject;

            if (playerObject != null &&
                playerObject.IsSpawned &&
                playerObject.gameObject.scene.name == gameSceneName)
            {
                spawnedPlayerCount++;
            }
        }

        if (!PlayerSpawnReadinessRules.ArePlayersReady(
                requiredPlayerCount,
                connectedClientIds.Count,
                loadedPlayerCount,
                spawnedPlayerCount,
                0))
        {
            return;
        }

        GameManager gameManager = FindFirstObjectByType<GameManager>();

        if (gameManager == null || !gameManager.IsSpawned)
            return;

        gameManager.IniciarCarrera();
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
