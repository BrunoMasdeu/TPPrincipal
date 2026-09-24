using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class SpeedBoostController : NetworkBehaviour
{
    [SerializeField] private Move movement;

    private readonly NetworkVariable<double> boostEndServerTime = new(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<float> activeMultiplier = new(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private GUIStyle boostStyle;

    public float RemainingSeconds
    {
        get
        {
            if (!IsSpawned || NetworkManager == null)
                return 0f;

            return SpeedBoostTime.CalculateRemainingSeconds(
                boostEndServerTime.Value,
                NetworkManager.ServerTime.Time
            );
        }
    }

    private void Awake()
    {
        if (movement == null)
            movement = GetComponent<Move>();
    }

    public override void OnNetworkSpawn()
    {
        ApplyOwnerMovementSpeed();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && movement != null)
            movement.SetSpeedMultiplier(1f);
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (IsServer &&
            boostEndServerTime.Value > 0d &&
            NetworkManager.ServerTime.Time >= boostEndServerTime.Value)
        {
            boostEndServerTime.Value = 0d;
            activeMultiplier.Value = 1f;
        }

        ApplyOwnerMovementSpeed();
    }

    public bool TryApplyBoostOnServer(float multiplier, float durationSeconds)
    {
        if (!IsServer || multiplier <= 1f || durationSeconds <= 0f)
            return false;

        activeMultiplier.Value = multiplier;
        boostEndServerTime.Value =
            NetworkManager.ServerTime.Time + durationSeconds;

        return true;
    }

    private void ApplyOwnerMovementSpeed()
    {
        if (!IsOwner || movement == null)
            return;

        float multiplier = RemainingSeconds > 0f
            ? activeMultiplier.Value
            : 1f;

        movement.SetSpeedMultiplier(multiplier);
    }

    private void OnGUI()
    {
        if (!IsOwner || RemainingSeconds <= 0f)
            return;

        if (boostStyle == null)
        {
            boostStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };

            boostStyle.normal.textColor = new Color(0.25f, 1f, 0.95f);
        }
        Rect statusRect = new Rect(
                    Screen.width * 0.5f - 150f,
                    120f, // Lo bajamos para que no choque con el tiempo
                    300f,
                    58f
                );

        GUI.Box(
            statusRect,
            $"VELOCIDAD x{activeMultiplier.Value:0.0}\n" +
            $"{RemainingSeconds:0.0} s",
            boostStyle
        );
    }

}

public static class SpeedBoostTime
{
    public static float CalculateRemainingSeconds(
        double endServerTime,
        double currentServerTime
    )
    {
        return Mathf.Max(0f, (float)(endServerTime - currentServerTime));
    }
}
