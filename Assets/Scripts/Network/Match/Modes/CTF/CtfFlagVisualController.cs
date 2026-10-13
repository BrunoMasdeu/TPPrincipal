using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Presenta localmente el estado replicado por CtfMatchManager.
/// Las banderas son objetos visuales: no deciden recogidas, capturas ni daño.
/// </summary>
[DisallowMultipleComponent]
public sealed class CtfFlagVisualController : MonoBehaviour
{
    [Header("Prefabs visuales")]
    [SerializeField] private GameObject redFlagPrefab;
    [SerializeField] private GameObject blueFlagPrefab;

    [Header("Puntos de base (opcionales)")]
    [Tooltip("Si queda vacío, se descubre por FlagSpawnPoint.TeamId.")]
    [SerializeField] private FlagSpawnPoint redSpawnOverride;
    [Tooltip("Si queda vacío, se descubre por FlagSpawnPoint.TeamId.")]
    [SerializeField] private FlagSpawnPoint blueSpawnOverride;

    [Header("Presentación")]
    [SerializeField] private Vector3 carriedLocalPosition;
    [SerializeField] private Vector3 carriedLocalEulerAngles;
    [SerializeField, Min(0f)] private float droppedVerticalOffset = 0.02f;
    [SerializeField] private Vector3 droppedEulerAngles;

    private CtfMatchManager manager;
    private FlagSpawnPoint redSpawn;
    private FlagSpawnPoint blueSpawn;
    private GameObject redVisual;
    private GameObject blueVisual;
    private bool refreshPending = true;

    public bool HasRequiredPrefabs =>
        redFlagPrefab != null && blueFlagPrefab != null;

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveSpawnPoints();
        TryBindManager();
        refreshPending = true;
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (!TryBindManager() || manager == null || !manager.IsCtf)
        {
            SetVisualsActive(false);
            refreshPending = true;
            return;
        }

        if (!HasRequiredPrefabs)
        {
            SetVisualsActive(false);
            refreshPending = true;
            return;
        }

        ResolveSpawnPoints();
        EnsureVisuals();

        if (refreshPending)
            refreshPending = !RefreshPresentation();
    }

    private void OnDisable()
    {
        UnbindManager();

        if (!Application.isPlaying)
            return;

        DestroyVisual(ref redVisual);
        DestroyVisual(ref blueVisual);
    }

    private bool TryBindManager()
    {
        CtfMatchManager current = CtfMatchManager.Instance;
        if (manager == current)
            return manager != null;

        UnbindManager();
        manager = current;
        if (manager == null)
            return false;

        manager.FlagsChanged += OnFlagsChanged;
        refreshPending = true;
        return true;
    }

    private void UnbindManager()
    {
        if (manager != null)
            manager.FlagsChanged -= OnFlagsChanged;

        manager = null;
    }

    private void OnFlagsChanged()
    {
        refreshPending = true;
    }

    private void ResolveSpawnPoints()
    {
        redSpawn = redSpawnOverride != null ? redSpawnOverride : redSpawn;
        blueSpawn = blueSpawnOverride != null ? blueSpawnOverride : blueSpawn;
        if (redSpawn != null && blueSpawn != null)
            return;

        FlagSpawnPoint[] spawnPoints = FindObjectsByType<FlagSpawnPoint>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (FlagSpawnPoint spawnPoint in spawnPoints)
        {
            if (spawnPoint.TeamId == TeamId.Red && redSpawn == null)
                redSpawn = spawnPoint;
            else if (spawnPoint.TeamId == TeamId.Blue && blueSpawn == null)
                blueSpawn = spawnPoint;
        }
    }

    private void EnsureVisuals()
    {
        if (redVisual == null)
            redVisual = CreateVisual(redFlagPrefab, "Red_Flag_Visual");
        if (blueVisual == null)
            blueVisual = CreateVisual(blueFlagPrefab, "Blue_Flag_Visual");
    }

    private GameObject CreateVisual(GameObject prefab, string runtimeName)
    {
        if (prefab == null)
            return null;

        GameObject visual = Instantiate(prefab, transform);
        visual.name = runtimeName;
        visual.SetActive(false);
        return visual;
    }

    private bool RefreshPresentation()
    {
        if (manager == null || redVisual == null || blueVisual == null)
            return false;

        bool redReady = ApplySnapshot(
            manager.RedFlag,
            redSpawn,
            redVisual);
        bool blueReady = ApplySnapshot(
            manager.BlueFlag,
            blueSpawn,
            blueVisual);
        return redReady && blueReady;
    }

    private bool ApplySnapshot(
        CtfFlagSnapshot snapshot,
        FlagSpawnPoint spawnPoint,
        GameObject visual)
    {
        if (visual == null)
            return false;

        return snapshot.State switch
        {
            FlagState.AtBase => PlaceAtBase(visual, spawnPoint),
            FlagState.Carried => PlaceOnCarrier(
                visual,
                snapshot.CarrierClientId),
            FlagState.Dropped => PlaceDropped(
                visual,
                snapshot.DropPosition),
            _ => HideUnknownState(visual)
        };
    }

    private static bool PlaceAtBase(
        GameObject visual,
        FlagSpawnPoint spawnPoint)
    {
        if (spawnPoint == null)
        {
            visual.SetActive(false);
            return false;
        }

        Transform visualTransform = visual.transform;
        visualTransform.SetParent(spawnPoint.transform, false);
        visualTransform.localPosition = Vector3.zero;
        visualTransform.localRotation = Quaternion.identity;
        visual.SetActive(true);
        return true;
    }

    private bool PlaceOnCarrier(GameObject visual, ulong carrierClientId)
    {
        CtfFlagCarrySocket socket = FindCarrierSocket(carrierClientId);
        if (socket == null)
        {
            visual.SetActive(false);
            return false;
        }

        Transform visualTransform = visual.transform;
        visualTransform.SetParent(socket.transform, false);
        visualTransform.localPosition = carriedLocalPosition;
        visualTransform.localRotation = Quaternion.Euler(
            carriedLocalEulerAngles);
        visual.SetActive(true);
        return true;
    }

    private bool PlaceDropped(GameObject visual, Vector3 dropPosition)
    {
        Transform visualTransform = visual.transform;
        visualTransform.SetParent(transform, true);
        visualTransform.SetPositionAndRotation(
            dropPosition + Vector3.up * droppedVerticalOffset,
            Quaternion.Euler(droppedEulerAngles));
        visual.SetActive(true);
        return true;
    }

    private static CtfFlagCarrySocket FindCarrierSocket(ulong carrierClientId)
    {
        NetworkObject[] networkObjects = FindObjectsByType<NetworkObject>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (NetworkObject networkObject in networkObjects)
        {
            if (!networkObject.IsPlayerObject ||
                networkObject.OwnerClientId != carrierClientId)
                continue;

            return networkObject.GetComponentInChildren<CtfFlagCarrySocket>(
                true);
        }

        return null;
    }

    private static bool HideUnknownState(GameObject visual)
    {
        visual.SetActive(false);
        return true;
    }

    private void SetVisualsActive(bool active)
    {
        if (redVisual != null)
            redVisual.SetActive(active);
        if (blueVisual != null)
            blueVisual.SetActive(active);
    }

    private static void DestroyVisual(ref GameObject visual)
    {
        if (visual == null)
            return;

        Destroy(visual);
        visual = null;
    }
}
