using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerNetworkSetup : NetworkBehaviour
{
    [Header("Componentes locales")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener audioListener;
    [SerializeField] private CameraMovement cameraMovement;
    [SerializeField] private Move move;
    [SerializeField] private WallRun wallRun;
    [SerializeField] private Grappling grappling;
    [SerializeField] private Canvas playerUI;
    [SerializeField] private NetworkPlayerInventory inventory;
    [SerializeField] private NetworkPlayerCombat combat;
    [SerializeField] private NetworkPlayerHudPresenter hudPresenter;

    [Header("Checkpoint")]
    private Vector3 ultimoCheckpointPos;
    private Quaternion ultimoCheckpointRot = Quaternion.identity;

    private Rigidbody playerRigidbody;
    private NetworkTransform networkTransform;
    private float nextRespawnRequestTime;
    private bool matchControlsEnabled = true;
    private bool presentationControlsEnabled = true;
    private bool hudVisible = true;

    private const float RespawnRequestCooldown = 0.5f;

    private void Awake()
    {
        playerRigidbody = GetComponent<Rigidbody>();
        networkTransform = GetComponent<NetworkTransform>();
        inventory ??= GetComponent<NetworkPlayerInventory>();
        combat ??= GetComponent<NetworkPlayerCombat>();
        hudPresenter ??= GetComponent<NetworkPlayerHudPresenter>();
        GuardarCheckpointInicial();
    }

    public override void OnNetworkSpawn()
    {
        bool esPropietario = IsOwner;

        if (playerCamera != null)
            playerCamera.enabled = esPropietario;

        if (audioListener != null)
            audioListener.enabled = esPropietario;

        ApplyLocalState();

        // Al conectarse, actualiza el punto seguro con la posición de red definitiva.
        GuardarCheckpointInicial();
    }

    public void Respawn(Vector3 position, Quaternion rotation)
    {
        if (!IsSpawned)
        {
            ApplyRespawn(position, rotation);
            return;
        }

        // El host valida la zona de muerte. Como el NetworkTransform usa
        // autoridad del propietario, al cliente remoto se le ordena efectuar
        // el teletransporte sobre su propia instancia autoritativa.
        if (!IsServer || Time.unscaledTime < nextRespawnRequestTime)
            return;

        nextRespawnRequestTime = Time.unscaledTime + RespawnRequestCooldown;

        if (IsOwner)
            ApplyRespawn(position, rotation);
        else
            RespawnOwnerRpc(position, rotation);
    }

    [Rpc(SendTo.Owner)]
    private void RespawnOwnerRpc(Vector3 position, Quaternion rotation)
    {
        ApplyRespawn(position, rotation);
    }

    private void ApplyRespawn(Vector3 position, Quaternion rotation)
    {
        if (IsSpawned && !IsOwner)
            return;

        Quaternion validRotation = GetValidRotation(rotation);

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.position = position;
            playerRigidbody.rotation = validRotation;

            if (!IsSpawned)
                transform.SetPositionAndRotation(position, validRotation);
        }
        else
        {
            transform.SetPositionAndRotation(position, validRotation);
        }

        // Teleport evita que la interpolacion recorra visualmente toda la
        // distancia desde la zona de muerte hasta el punto de respawn.
        if (IsSpawned && networkTransform != null)
        {
            networkTransform.Teleport(
                position,
                validRotation,
                transform.localScale
            );
        }
    }

    public void SetMatchControlsEnabled(bool enabled)
    {
        matchControlsEnabled = enabled;
        ApplyLocalState();
    }

    /// <summary>
    /// Controla pausa, tutorial y pantallas finales sin alterar el estado
    /// autoritativo de vida ni detener la replicacion de combate/inventario.
    /// </summary>
    public void SetPresentationControlsEnabled(bool enabled)
    {
        presentationControlsEnabled = enabled;
        ApplyLocalState();
    }

    public void SetLocalHudVisible(bool visible)
    {
        hudVisible = visible;
        ApplyHudState();
    }

    public bool LocalGameplayInputEnabled =>
        IsSpawned && IsOwner && matchControlsEnabled &&
        presentationControlsEnabled;

    // Separado del checkpoint de carrera: la salud confirma el teletransporte
    // antes de volver a habilitar controles y vida.
    public void RespawnForMatch(Vector3 position, Quaternion rotation, ulong sequence)
    {
        if (!IsServer || !IsSpawned)
            return;

        if (IsOwner)
        {
            ApplyRespawn(position, rotation);
            GetComponent<NetworkPlayerHealth>()?.ConfirmMatchRespawn(sequence, OwnerClientId);
        }
        else
        {
            RespawnForMatchOwnerRpc(position, rotation, sequence);
        }
    }

    [Rpc(SendTo.Owner)]
    private void RespawnForMatchOwnerRpc(
        Vector3 position,
        Quaternion rotation,
        ulong sequence)
    {
        ApplyRespawn(position, rotation);
        GetComponent<NetworkPlayerHealth>()?.ConfirmMatchRespawn(sequence, OwnerClientId);
    }

    private void GuardarCheckpointInicial()
    {
        ultimoCheckpointPos = transform.position;
        ultimoCheckpointRot = GetValidRotation(transform.rotation);
    }

    private void ApplyLocalState()
    {
        bool localPresentation = IsSpawned && IsOwner &&
            presentationControlsEnabled;
        bool localGameplay = localPresentation && matchControlsEnabled;

        if (cameraMovement != null)
            cameraMovement.enabled = localPresentation;
        if (move != null)
            move.enabled = localGameplay;
        if (wallRun != null)
            wallRun.enabled = localGameplay;
        if (grappling != null)
            grappling.enabled = localGameplay;

        inventory?.SetLocalInputEnabled(localGameplay);
        combat?.SetLocalInputEnabled(localGameplay);
        ApplyHudState();

        if (!localGameplay && playerRigidbody != null && IsOwner)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void ApplyHudState()
    {
        bool visible = IsSpawned && IsOwner && hudVisible;

        if (playerUI != null)
            playerUI.enabled = visible;

        hudPresenter?.SetHudVisible(visible);
    }

    private static Quaternion GetValidRotation(Quaternion rotation)
    {
        float sqrMagnitude =
            rotation.x * rotation.x +
            rotation.y * rotation.y +
            rotation.z * rotation.z +
            rotation.w * rotation.w;

        if (float.IsNaN(sqrMagnitude) ||
            float.IsInfinity(sqrMagnitude) ||
            sqrMagnitude <= Mathf.Epsilon)
        {
            return Quaternion.identity;
        }

        float inverseMagnitude = 1f / Mathf.Sqrt(sqrMagnitude);
        return new Quaternion(
            rotation.x * inverseMagnitude,
            rotation.y * inverseMagnitude,
            rotation.z * inverseMagnitude,
            rotation.w * inverseMagnitude
        );
    }

    // --- NUEVOS MÉTODOS PARA CHECKPOINTS ---

    public void GuardarCheckpoint(Vector3 nuevaPos, Quaternion nuevaRot)
    {
        ultimoCheckpointPos = nuevaPos;
        ultimoCheckpointRot = GetValidRotation(nuevaRot);
        Debug.Log("¡Checkpoint guardado exitosamente!");
    }

    public void RespawnAlUltimoCheckpoint()
    {
        // Reutiliza tu lógica robusta de respawn en red
        Respawn(ultimoCheckpointPos, ultimoCheckpointRot);
    }
}
