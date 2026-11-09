using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
public class SpeedBoostPickup : NetworkBehaviour
{
    [SerializeField] private float speedMultiplier = 1.5f;
    [SerializeField] private float durationSeconds = 8f;
    [SerializeField] private float serverValidationDistance = 2.5f;
    [SerializeField] private Transform visual;
    [SerializeField] private float rotationSpeed = 90f;

    [Header("Reaparicion")]
    [SerializeField] private bool desaparecerAlAgarrar = true;

    [Min(0.1f)]
    [SerializeField] private float tiempoReaparicion = 30f;

    private readonly NetworkVariable<bool> collected = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private Renderer[] renderers;
    private bool[] rendererInitiallyEnabled;
    private Collider[] colliders;
    private bool[] colliderInitiallyEnabled;
    private Light[] lights;
    private bool[] lightInitiallyEnabled;
    private Coroutine respawnCoroutine;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        rendererInitiallyEnabled = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            rendererInitiallyEnabled[i] = renderers[i].enabled;

        colliders = GetComponentsInChildren<Collider>(true);
        colliderInitiallyEnabled = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
            colliderInitiallyEnabled[i] = colliders[i].enabled;

        lights = GetComponentsInChildren<Light>(true);
        lightInitiallyEnabled = new bool[lights.Length];
        for (int i = 0; i < lights.Length; i++)
            lightInitiallyEnabled[i] = lights[i].enabled;
    }

    public override void OnNetworkSpawn()
    {
        collected.OnValueChanged += OnCollectedChanged;
        if (IsServer)
            collected.Value = false;
        SetAvailable(!collected.Value);
    }

    public override void OnNetworkDespawn()
    {
        collected.OnValueChanged -= OnCollectedChanged;
        if (respawnCoroutine != null)
            StopCoroutine(respawnCoroutine);
        respawnCoroutine = null;
        SetAvailable(true);
    }

    private void OnCollectedChanged(bool previous, bool current)
    {
        SetAvailable(!current);
    }

    private void SetAvailable(bool available)
    {
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = available && rendererInitiallyEnabled[i];

        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = available && colliderInitiallyEnabled[i];

        for (int i = 0; i < lights.Length; i++)
            lights[i].enabled = available && lightInitiallyEnabled[i];
    }

    private void Update()
    {
        if (IsSpawned && !collected.Value && visual != null)
            visual.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsSpawned || collected.Value)
            return;

        NetworkObject playerObject =
            other.GetComponentInParent<NetworkObject>();

        if (playerObject == null || !playerObject.IsPlayerObject)
            return;

        if (IsServer)
        {
            TryCollectOnServer(playerObject, playerObject.OwnerClientId);
        }
        else if (playerObject.IsOwner)
        {
            RequestCollectionRpc(playerObject);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestCollectionRpc(
        NetworkObjectReference playerReference,
        RpcParams rpcParams = default
    )
    {
        if (!playerReference.TryGet(out NetworkObject playerObject))
            return;

        TryCollectOnServer(
            playerObject,
            rpcParams.Receive.SenderClientId
        );
    }

    private void TryCollectOnServer(
        NetworkObject playerObject,
        ulong requestingClientId
    )
    {
        if (!IsServer || collected.Value || playerObject == null)
            return;

        if (!playerObject.IsPlayerObject ||
            playerObject.OwnerClientId != requestingClientId)
        {
            return;
        }

        if (Vector3.Distance(transform.position, playerObject.transform.position) >
            serverValidationDistance)
        {
            return;
        }

        SpeedBoostController boostController =
            playerObject.GetComponent<SpeedBoostController>();

        if (boostController == null ||
            !boostController.TryApplyBoostOnServer(
                speedMultiplier,
                durationSeconds
            ))
        {
            return;
        }

        if (desaparecerAlAgarrar)
        {
            NetworkObject.Despawn(true);
            return;
        }

        collected.Value = true;
        respawnCoroutine = StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(tiempoReaparicion);
        if (IsSpawned && IsServer)
            collected.Value = false;
        respawnCoroutine = null;
    }

    private void OnValidate()
    {
        tiempoReaparicion = Mathf.Max(0.1f, tiempoReaparicion);
    }
}
