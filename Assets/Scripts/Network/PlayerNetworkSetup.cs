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

    [Header("Checkpoint")]
    private Vector3 ultimoCheckpointPos;
    private Quaternion ultimoCheckpointRot = Quaternion.identity;

    private Rigidbody playerRigidbody;
    private NetworkTransform networkTransform;
    private float nextRespawnRequestTime;

    private const float RespawnRequestCooldown = 0.5f;

    private void Awake()
    {
        playerRigidbody = GetComponent<Rigidbody>();
        networkTransform = GetComponent<NetworkTransform>();
        GuardarCheckpointInicial();
    }

    public override void OnNetworkSpawn()
    {
        bool esPropietario = IsOwner;

        if (playerCamera != null)
            playerCamera.enabled = esPropietario;

        if (audioListener != null)
            audioListener.enabled = esPropietario;

        if (cameraMovement != null)
            cameraMovement.enabled = esPropietario;

        if (move != null)
            move.enabled = esPropietario;

        if (wallRun != null)
            wallRun.enabled = esPropietario;

        if (grappling != null)
            grappling.enabled = esPropietario;

        if (playerUI != null)          
            playerUI.enabled = esPropietario;

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

    private void GuardarCheckpointInicial()
    {
        ultimoCheckpointPos = transform.position;
        ultimoCheckpointRot = GetValidRotation(transform.rotation);
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
