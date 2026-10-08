using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Crea una única raíz de sesión cuando el servidor comienza.
/// La raíz se spawnea con DestroyWithScene desactivado para sobrevivir
/// a las cargas Single realizadas por NetworkSceneManager.
/// </summary>
[RequireComponent(typeof(NetworkManager))]
public sealed class NetworkSessionBootstrap : MonoBehaviour
{
    [SerializeField] private NetworkObject sessionRootPrefab;

    private NetworkManager networkManager;
    private NetworkLobbySession preparedSession;
    private int preparedPlayerCount;
    private string activeJoinCode = string.Empty;

    public int PreparedPlayerCount => preparedPlayerCount;
    public string ActiveJoinCode => activeJoinCode;
    public bool HasPreparedSession => preparedSession != null &&
        preparedPlayerCount > 0;

    public NetworkLobbySession CurrentSession
    {
        get
        {
            if (preparedSession != null)
                return preparedSession;

            return NetworkLobbySession.Instance;
        }
    }

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();

        // MenuScene contiene un NetworkManager para el primer arranque. Al volver
        // desde una partida, ya existe uno persistente: descartar esta copia.
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton != networkManager)
        {
            Destroy(gameObject);
        }
    }

    public void SetActiveJoinCode(string joinCode)
    {
        activeJoinCode = joinCode ?? string.Empty;
    }

    private void OnEnable()
    {
        if (networkManager == null)
            networkManager = GetComponent<NetworkManager>();

        networkManager.OnServerStarted += OnServerStarted;
        networkManager.OnServerStopped += OnServerStopped;
    }

    public bool PrepareSession(
        GameModeDefinition definition,
        int playerCount,
        out string validationError)
    {
        if (networkManager.IsListening)
        {
            validationError =
                "La sesión debe configurarse antes de iniciar NetworkManager.";
            return false;
        }

        NetworkLobbySession session = EnsureLocalSession();

        if (session == null)
        {
            validationError =
                "No se pudo crear NetworkSessionRoot. Revise el prefab configurado.";
            return false;
        }

        if (definition != null && definition.GameModeId == GameModeId.TDM &&
            session.GetComponent<TdmMatchManager>() == null)
        {
            validationError =
                "NetworkSessionRoot no tiene TdmMatchManager. Agréguelo al prefab antes de iniciar TDM.";
            Debug.LogError($"[Session] {validationError}");
            return false;
        }

        if (definition != null && definition.GameModeId == GameModeId.CTF &&
            session.GetComponent<CtfMatchManager>() == null)
        {
            validationError =
                "NetworkSessionRoot no tiene CtfMatchManager. Agréguelo al prefab antes de iniciar CTF.";
            Debug.LogError($"[Session] {validationError}");
            return false;
        }

        bool configured = session.ConfigureSession(
            definition,
            playerCount,
            out validationError
        );

        if (configured)
            preparedPlayerCount = playerCount;

        return configured;
    }

    public void CancelPreparedSession()
    {
        if (preparedSession == null || preparedSession.NetworkObject.IsSpawned)
            return;

        Destroy(preparedSession.gameObject);
        preparedSession = null;
        preparedPlayerCount = 0;
    }

    private void OnServerStarted()
    {
        NetworkLobbySession session = EnsureLocalSession();

        if (session == null)
            return;

        NetworkObject sessionNetworkObject = session.NetworkObject;

        if (!sessionNetworkObject.IsSpawned)
        {
            // Spawn(false) indica que la raíz dinámica no pertenece a MenuScene.
            sessionNetworkObject.Spawn(false);
        }

        Debug.Log(
            $"[Session] Raíz creada: spawned={sessionNetworkObject.IsSpawned}; " +
            $"inScene={sessionNetworkObject.InScenePlaced}; " +
            $"destroyWithScene={sessionNetworkObject.DestroyWithScene}."
        );
    }

    private NetworkLobbySession EnsureLocalSession()
    {
        if (preparedSession != null)
            return preparedSession;

        if (sessionRootPrefab == null)
        {
            Debug.LogError(
                "NetworkSessionBootstrap no tiene NetworkSessionRoot configurado."
            );
            return null;
        }

        NetworkObject instance = Instantiate(sessionRootPrefab);
        preparedSession = instance.GetComponent<NetworkLobbySession>();

        if (preparedSession != null)
            return preparedSession;

        Debug.LogError(
            "El prefab NetworkSessionRoot no contiene NetworkLobbySession."
        );
        Destroy(instance.gameObject);
        return null;
    }

    private void OnServerStopped(bool wasClient)
    {
        CancelPreparedSession();
        preparedSession = null;
        preparedPlayerCount = 0;
        activeJoinCode = string.Empty;
    }

    private void OnDisable()
    {
        if (networkManager == null)
            return;

        networkManager.OnServerStarted -= OnServerStarted;
        networkManager.OnServerStopped -= OnServerStopped;
    }
}
