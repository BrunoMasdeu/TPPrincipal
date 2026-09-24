using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;


[RequireComponent(typeof(NetworkManager))]
public class LobbyPlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject networkPlayerPrefab;
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string spawnPointName = "Respawn";
    [SerializeField] private float playerSpacing = 1.5f;
    [SerializeField, Min(1)] private int requiredPlayerCount = 2;

    private NetworkManager networkManager;
    private readonly HashSet<ulong> loadedClientIds = new();
    private bool gameSceneLoadCompletedWithoutTimeout;

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();

        networkManager.ConnectionApprovalCallback = ApproveConnection;
        networkManager.OnServerStarted += OnServerStarted;
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
        response.Approved =
            networkManager.ConnectedClientsIds.Count < requiredPlayerCount;

        // No crear al jugador mientras permanece en el lobby.
        response.CreatePlayerObject = false;

        response.Position = null;
        response.Rotation = null;
        response.Reason = response.Approved
            ? string.Empty
            : "La sala ya tiene dos jugadores.";
        response.Pending = false;
    }

    private void OnServerStarted()
    {
        networkManager.SceneManager.OnLoadEventCompleted
            += OnLoadEventCompleted;
    }

    private void OnLoadEventCompleted(
        string sceneName,
        LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut)
    {
        if (!networkManager.IsServer || sceneName != gameSceneName)
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

        if (!RaceStartReadiness.ArePlayersReady(
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
        return requiredPlayerCount > 0 &&
            timedOutPlayerCount == 0 &&
            connectedPlayerCount == requiredPlayerCount &&
            loadedPlayerCount == requiredPlayerCount &&
            spawnedPlayerCount == requiredPlayerCount;
    }
}
