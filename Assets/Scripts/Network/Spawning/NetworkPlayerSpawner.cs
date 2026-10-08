using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Crea un PlayerObject por cliente después de que todos completan la carga.
/// Selecciona puntos por TeamId sin conocer reglas particulares de un modo.
/// </summary>
[RequireComponent(typeof(NetworkLobbySession))]
public class NetworkPlayerSpawner : NetworkBehaviour
{
    private readonly HashSet<ulong> loadedClientIds = new();
    private readonly HashSet<ulong> timedOutClientIds = new();
    private readonly HashSet<ulong> spawnedClientIds = new();
    private readonly Dictionary<TeamId, List<TeamSpawnPoint>> spawnPointsByTeam =
        new();
    private readonly Dictionary<TeamId, int> assignmentsByTeam = new();

    private NetworkLobbySession lobbySession;

    private void Awake()
    {
        lobbySession = GetComponent<NetworkLobbySession>();
    }

    public override void OnNetworkSpawn()
    {
        if (NetworkManager.SceneManager != null)
            NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;

        if (!IsServer)
            return;

        if (lobbySession == null)
        {
            Debug.LogError(
                "NetworkPlayerSpawner no encontró NetworkLobbySession."
            );
            return;
        }

        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

        if (NetworkManager.SceneManager != null)
        {
            NetworkManager.SceneManager.OnLoadEventCompleted +=
                OnLoadEventCompleted;
            Debug.Log("[Spawn] NetworkPlayerSpawner espera la carga de escena del servidor.");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager != null && NetworkManager.SceneManager != null)
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;

        if (IsServer && lobbySession != null &&
            lobbySession.Phase == SessionPhase.Loading)
        {
            Debug.LogWarning(
                "[Spawn] NetworkSessionRoot dejó de estar spawneado durante la carga."
            );
        }

        if (NetworkManager != null && IsServer)
        {
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;

            if (NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.OnLoadEventCompleted -=
                    OnLoadEventCompleted;
            }
        }

        ClearSceneState();
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneEventType != SceneEventType.Load &&
            sceneEvent.SceneEventType != SceneEventType.LoadComplete &&
            sceneEvent.SceneEventType != SceneEventType.LoadEventCompleted)
            return;

        Debug.Log(
            $"[SceneSync] {(IsServer ? "Servidor" : "Cliente")} " +
            $"evento={sceneEvent.SceneEventType}; " +
            $"cliente={sceneEvent.ClientId}; escena={sceneEvent.SceneName}."
        );
    }

    private void OnLoadEventCompleted(
        string sceneName,
        LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut)
    {
        if (!IsServer)
            return;

        if (loadSceneMode == LoadSceneMode.Single)
            ClearSceneState();

        GameModeDefinition definition = lobbySession.SelectedDefinition;

        Debug.Log(
            $"[Spawn] Carga completada: {sceneName}; " +
            $"fase={lobbySession.Phase}; " +
            $"completaron={clientsCompleted.Count}; " +
            $"timeout={clientsTimedOut.Count}."
        );

        if (lobbySession.Phase != SessionPhase.Loading ||
            definition == null ||
            sceneName != definition.SceneName)
        {
            return;
        }

        RegisterLoadResults(clientsCompleted, clientsTimedOut);

        int connectedPlayerCount = NetworkManager.ConnectedClientsIds.Count;

        if (!PlayerSpawnReadinessRules.CanBeginSpawn(
                true,
                lobbySession.RequiredPlayerCount,
                connectedPlayerCount,
                loadedClientIds.Count,
                timedOutClientIds.Count))
        {
            FailSpawn(
                "No se inició el spawn porque faltan clientes cargados o hubo timeouts."
            );
            return;
        }

        if (!DiscoverSpawnPoints(out string validationError) ||
            !ValidateTeamCapacity(out validationError))
        {
            FailSpawn(validationError);
            return;
        }

        for (int i = 0; i < lobbySession.Players.Count; i++)
        {
            LobbyPlayerData player = lobbySession.Players[i];

            if (!SpawnPlayer(player))
            {
                FailSpawn(
                    $"No se pudo crear el jugador del cliente {player.ClientId}."
                );
                return;
            }
        }

        int spawnedPlayerCount = CountSpawnedPlayers(sceneName);

        if (!PlayerSpawnReadinessRules.ArePlayersReady(
                lobbySession.RequiredPlayerCount,
                connectedPlayerCount,
                loadedClientIds.Count,
                spawnedPlayerCount,
                timedOutClientIds.Count))
        {
            FailSpawn(
                "El spawn terminó con una cantidad de PlayerObject inesperada."
            );
            return;
        }

        if (!lobbySession.NotifyPlayersSpawned())
        {
            FailSpawn(
                "NetworkLobbySession rechazó la confirmación final del spawn."
            );
        }
        else
        {
            Debug.Log(
                $"[Spawn] {spawnedPlayerCount} PlayerObject creados en {sceneName}."
            );
        }
    }

    private void RegisterLoadResults(
        IReadOnlyList<ulong> clientsCompleted,
        IReadOnlyList<ulong> clientsTimedOut)
    {
        for (int i = 0; i < clientsCompleted.Count; i++)
        {
            ulong clientId = clientsCompleted[i];

            if (NetworkManager.ConnectedClients.ContainsKey(clientId))
                loadedClientIds.Add(clientId);
        }

        for (int i = 0; i < clientsTimedOut.Count; i++)
            timedOutClientIds.Add(clientsTimedOut[i]);
    }

    private bool DiscoverSpawnPoints(out string validationError)
    {
        spawnPointsByTeam.Clear();
        assignmentsByTeam.Clear();
        spawnPointsByTeam[TeamId.None] = new List<TeamSpawnPoint>();
        spawnPointsByTeam[TeamId.Red] = new List<TeamSpawnPoint>();
        spawnPointsByTeam[TeamId.Blue] = new List<TeamSpawnPoint>();
        assignmentsByTeam[TeamId.None] = 0;
        assignmentsByTeam[TeamId.Red] = 0;
        assignmentsByTeam[TeamId.Blue] = 0;

        TeamSpawnPoint[] spawnPoints = FindObjectsByType<TeamSpawnPoint>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            TeamSpawnPoint spawnPoint = spawnPoints[i];

            if (spawnPointsByTeam.TryGetValue(
                    spawnPoint.TeamId,
                    out List<TeamSpawnPoint> teamPoints))
            {
                teamPoints.Add(spawnPoint);
            }
        }

        foreach (List<TeamSpawnPoint> teamPoints in spawnPointsByTeam.Values)
        {
            teamPoints.Sort(
                (left, right) => string.CompareOrdinal(
                    GetHierarchyKey(left.transform),
                    GetHierarchyKey(right.transform)
                )
            );
        }

        bool requiresTeams = lobbySession.SelectedDefinition.RequiresTeams;

        if (requiresTeams &&
            (spawnPointsByTeam[TeamId.Red].Count == 0 ||
             spawnPointsByTeam[TeamId.Blue].Count == 0))
        {
            validationError =
                "La escena necesita al menos un TeamSpawnPoint Red y uno Blue.";
            return false;
        }

        if (!requiresTeams && spawnPointsByTeam[TeamId.None].Count == 0)
        {
            validationError =
                "La escena sin equipos necesita un TeamSpawnPoint None.";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    private bool ValidateTeamCapacity(out string validationError)
    {
        int redPlayers = 0;
        int bluePlayers = 0;
        int unassignedPlayers = 0;
        bool requiresTeams = lobbySession.SelectedDefinition.RequiresTeams;

        for (int i = 0; i < lobbySession.Players.Count; i++)
        {
            switch (lobbySession.Players[i].TeamId)
            {
                case TeamId.Red:
                    if (!requiresTeams)
                        goto default;
                    redPlayers++;
                    break;
                case TeamId.Blue:
                    if (!requiresTeams)
                        goto default;
                    bluePlayers++;
                    break;
                case TeamId.None:
                    if (requiresTeams)
                        goto default;
                    unassignedPlayers++;
                    break;
                default:
                    validationError =
                        $"El cliente {lobbySession.Players[i].ClientId} no tiene equipo.";
                    return false;
            }
        }

        if (redPlayers > spawnPointsByTeam[TeamId.Red].Count ||
            bluePlayers > spawnPointsByTeam[TeamId.Blue].Count ||
            unassignedPlayers > spawnPointsByTeam[TeamId.None].Count)
        {
            validationError =
                "No hay suficientes TeamSpawnPoint para todos los jugadores.";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    private bool SpawnPlayer(LobbyPlayerData player)
    {
        if (!NetworkManager.ConnectedClients.TryGetValue(
                player.ClientId,
                out NetworkClient client))
        {
            return false;
        }

        bool alreadyHasPlayerObject = client.PlayerObject != null ||
            spawnedClientIds.Contains(player.ClientId);

        if (!PlayerSpawnReadinessRules.CanSpawnPlayer(
                IsServer,
                true,
                loadedClientIds.Contains(player.ClientId),
                alreadyHasPlayerObject))
        {
            return alreadyHasPlayerObject;
        }

        if (!spawnPointsByTeam.TryGetValue(
                player.TeamId,
                out List<TeamSpawnPoint> teamSpawnPoints))
        {
            return false;
        }

        int assignedCount = assignmentsByTeam[player.TeamId];
        int spawnIndex = TeamSpawnPoint.GetDeterministicIndex(
            assignedCount,
            teamSpawnPoints.Count
        );

        if (spawnIndex < 0)
            return false;

        GameObject playerPrefab = NetworkManager.NetworkConfig.PlayerPrefab;

        if (playerPrefab == null)
        {
            Debug.LogError(
                "NetworkManager no tiene PlayerPrefab configurado."
            );
            return false;
        }

        TeamSpawnPoint selectedSpawn = teamSpawnPoints[spawnIndex];
        Vector3 position = selectedSpawn.transform.position;
        Quaternion rotation = selectedSpawn.transform.rotation *
            playerPrefab.transform.rotation;

        GameObject playerInstance = Instantiate(
            playerPrefab,
            position,
            rotation
        );

        NetworkObject playerNetworkObject =
            playerInstance.GetComponent<NetworkObject>();

        if (playerNetworkObject == null)
        {
            Debug.LogError("PlayerPrefab no contiene NetworkObject.");
            Destroy(playerInstance);
            return false;
        }

        bool hasCombat = playerInstance.TryGetComponent<NetworkPlayerCombat>(out _);
        bool hasHealth = playerInstance.TryGetComponent<NetworkPlayerHealth>(out _);
        bool hasOverlay = playerInstance.TryGetComponent<CombatDebugOverlay>(out _);
        Debug.Log(
            $"[Spawn] Prefab de {player.ClientId}: modo={lobbySession.SelectedGameModeId}, " +
            $"combate={hasCombat}, vida={hasHealth}, panel={hasOverlay}."
        );

        if (CombatValidationRules.IsCombatMode(lobbySession.SelectedGameModeId) &&
            (!hasCombat || !hasHealth || !hasOverlay))
        {
            Debug.LogError(
                "[Spawn] PlayerPrefab no tiene todos los componentes de combate. " +
                "Reimportá el prefab y verificá NetworkPlayerCombat, " +
                "NetworkPlayerHealth y CombatDebugOverlay en el Inspector."
            );
        }

        playerNetworkObject.SpawnAsPlayerObject(player.ClientId, true);
        assignmentsByTeam[player.TeamId] = assignedCount + 1;
        spawnedClientIds.Add(player.ClientId);
        return true;
    }

    public bool TryGetRespawnPoint(
        TeamId teamId,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;

        if (!IsServer || !IsSpawned ||
            !spawnPointsByTeam.TryGetValue(teamId, out List<TeamSpawnPoint> points) ||
            points.Count == 0)
            return false;

        int assignment = assignmentsByTeam[teamId];
        int index = TeamSpawnPoint.GetDeterministicIndex(assignment, points.Count);
        if (index < 0)
            return false;

        TeamSpawnPoint point = points[index];
        position = point.transform.position;
        GameObject playerPrefab = NetworkManager.NetworkConfig.PlayerPrefab;
        rotation = playerPrefab != null
            ? point.transform.rotation * playerPrefab.transform.rotation
            : point.transform.rotation;
        assignmentsByTeam[teamId] = assignment + 1;
        return true;
    }

    private int CountSpawnedPlayers(string sceneName)
    {
        int spawnedPlayerCount = 0;

        IReadOnlyList<ulong> connectedClientIds =
            NetworkManager.ConnectedClientsIds;

        for (int i = 0; i < connectedClientIds.Count; i++)
        {
            ulong clientId = connectedClientIds[i];

            if (!NetworkManager.ConnectedClients.TryGetValue(
                    clientId,
                    out NetworkClient client))
            {
                continue;
            }

            NetworkObject playerObject = client.PlayerObject;

            if (playerObject != null &&
                playerObject.IsSpawned &&
                playerObject.gameObject.scene.name == sceneName)
            {
                spawnedPlayerCount++;
            }
        }

        return spawnedPlayerCount;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        loadedClientIds.Remove(clientId);
        timedOutClientIds.Remove(clientId);
        spawnedClientIds.Remove(clientId);
    }

    private void FailSpawn(string reason)
    {
        Debug.LogError(reason);
        lobbySession.NotifySpawnFailed(reason);
    }

    private void ClearSceneState()
    {
        loadedClientIds.Clear();
        timedOutClientIds.Clear();
        spawnedClientIds.Clear();
        spawnPointsByTeam.Clear();
        assignmentsByTeam.Clear();
    }

    private static string GetHierarchyKey(Transform transform)
    {
        string key = $"{transform.GetSiblingIndex():D4}:{transform.name}";
        Transform parent = transform.parent;

        while (parent != null)
        {
            key = $"{parent.GetSiblingIndex():D4}:{parent.name}/{key}";
            parent = parent.parent;
        }

        return key;
    }
}
