using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Arma provisional para TDM/CTF. El cliente pide disparar; el servidor
/// controla munición, cadencia, impacto y daño. No recibe daño ni víctima.
/// </summary>
public class NetworkPlayerCombat : NetworkBehaviour
{
    private enum ShotOutcome : byte
    {
        Miss,
        Blocked,
        FriendlyBlocked,
        Hit,
        Kill,
        NotPlaying,
        Dead,
        NoAmmo,
        Reloading,
        Cooldown,
        InvalidAim
    }

    [Header("Arma provisional (valores de la pistola actual)")]
    [SerializeField, Min(1)] private int damage = 25;
    [SerializeField, Min(0.1f)] private float range = 100f;
    [SerializeField, Min(1)] private int magazineSize = 12;
    [SerializeField, Min(0f)] private float secondsBetweenShots = 0.5f;
    [SerializeField, Min(0f)] private float spread = 0.01f;
    [SerializeField, Min(0f)] private float reloadSeconds = 1.3f;
    [SerializeField] private Camera aimCamera;

    private readonly NetworkVariable<int> ammunition = new(
        12, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<double> reloadEndsAt = new(
        0d, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkPlayerHealth health;
    private bool combatMode;
    private double nextShotAt;
    private ulong nextLocalShotId;
    private ulong lastServerShotId;
    private string lastShotSummary = "Sin disparos";

    public event Action<int> AmmunitionChanged;
    public event Action ReloadChanged;

    public int Ammunition => ammunition.Value;
    public int MagazineSize => magazineSize;
    public bool IsCombatActive => combatMode;
    public string LastShotSummary => lastShotSummary;
    public float ReloadRemaining => reloadEndsAt.Value > 0d && NetworkManager != null
        ? Mathf.Max(0f, (float)(reloadEndsAt.Value - NetworkManager.ServerTime.Time))
        : 0f;

    private void Awake()
    {
        health = GetComponent<NetworkPlayerHealth>();
    }

    public override void OnNetworkSpawn()
    {
        ammunition.OnValueChanged += OnAmmunitionChanged;
        reloadEndsAt.OnValueChanged += OnReloadChanged;
        Debug.Log(
            $"[Combat] NetworkPlayerCombat presente: cliente={OwnerClientId}, " +
            $"owner={IsOwner}, servidor={IsServer}, " +
            $"modo={NetworkLobbySession.Instance?.SelectedGameModeId.ToString() ?? "sin sesión"}."
        );

        // Una copia remota nunca debe leer el mouse local, ni siquiera en carrera.
        if (!IsOwner && TryGetComponent(out GunSystem remoteLegacyGun))
            remoteLegacyGun.enabled = false;

        TryActivateCombatMode();
    }

    public override void OnNetworkDespawn()
    {
        ammunition.OnValueChanged -= OnAmmunitionChanged;
        reloadEndsAt.OnValueChanged -= OnReloadChanged;
        AmmunitionChanged = null;
        ReloadChanged = null;
    }

    private void OnAmmunitionChanged(int previous, int current) =>
        AmmunitionChanged?.Invoke(current);

    private void OnReloadChanged(double previous, double current) =>
        ReloadChanged?.Invoke();

    private void TryActivateCombatMode()
    {
        if (combatMode || !IsSpawned ||
            NetworkLobbySession.Instance == null ||
            !CombatValidationRules.IsCombatMode(
                NetworkLobbySession.Instance.SelectedGameModeId))
            return;

        combatMode = true;

        // GunSystem/ShootingAi son la implementación local anterior. En los
        // modos de equipos no deben competir con el circuito de red.
        GunSystem legacyGun = GetComponent<GunSystem>();
        if (legacyGun != null)
            legacyGun.enabled = false;

        ShootingAi legacyHealth = GetComponent<ShootingAi>();
        if (legacyHealth != null)
            legacyHealth.enabled = false;

        if (IsServer)
        {
            ammunition.Value = magazineSize;
            reloadEndsAt.Value = 0d;
            nextShotAt = 0d;
        }

        Debug.Log(
            $"[Combat] Combate de red ACTIVADO: cliente={OwnerClientId}, " +
            $"owner={IsOwner}, modo={NetworkLobbySession.Instance.SelectedGameModeId}; " +
            $"GunSystem activo={legacyGun != null && legacyGun.enabled}."
        );
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (!combatMode)
            TryActivateCombatMode();

        if (!combatMode)
            return;

        if (IsServer && reloadEndsAt.Value > 0d &&
            NetworkManager.ServerTime.Time >= reloadEndsAt.Value)
        {
            reloadEndsAt.Value = 0d;
            if (health != null && health.LifeState == PlayerLifeState.Alive)
                ammunition.Value = magazineSize;
        }

        if (!IsOwner)
            return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            RequestLocalShot();

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            RequestReloadRpc();
    }

    private void RequestLocalShot()
    {
        if (aimCamera == null)
        {
            lastShotSummary = "Sin cámara de mira configurada";
            return;
        }

        Transform aim = aimCamera.transform;
        RequestShotRpc(++nextLocalShotId, aim.position, aim.forward);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestShotRpc(
        ulong shotId,
        Vector3 aimOrigin,
        Vector3 aimDirection,
        RpcParams rpcParams = default)
    {
        if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId ||
            shotId <= lastServerShotId)
            return;

        lastServerShotId = shotId;
        double now = NetworkManager.ServerTime.Time;
        NetworkMatchManager match = NetworkMatchManager.Instance;
        MatchPhase phase = match != null
            ? match.Phase
            : MatchPhase.WaitingForPlayers;

        if (phase != MatchPhase.Playing)
        {
            ShotFeedbackRpc(ShotOutcome.NotPlaying, 0UL, 0, 0UL);
            return;
        }
        if (health == null || health.LifeState != PlayerLifeState.Alive)
        {
            ShotFeedbackRpc(ShotOutcome.Dead, 0UL, 0, 0UL);
            return;
        }
        if (reloadEndsAt.Value > 0d)
        {
            ShotFeedbackRpc(ShotOutcome.Reloading, 0UL, 0, 0UL);
            return;
        }
        if (ammunition.Value <= 0)
        {
            ShotFeedbackRpc(ShotOutcome.NoAmmo, 0UL, 0, 0UL);
            return;
        }
        if (now < nextShotAt)
        {
            ShotFeedbackRpc(ShotOutcome.Cooldown, 0UL, 0, 0UL);
            return;
        }
        if (!IsFinite(aimOrigin) || !IsFinite(aimDirection) ||
            Vector3.Distance(aimOrigin, transform.position) > 4f ||
            aimDirection.sqrMagnitude < 0.9f ||
            aimDirection.sqrMagnitude > 1.1f)
        {
            ShotFeedbackRpc(ShotOutcome.InvalidAim, 0UL, 0, 0UL);
            return;
        }

        // La validación anterior y el cambio de munición son del servidor.
        if (!CombatValidationRules.CanFire(
            phase, health.LifeState, ammunition.Value,
            reloadEndsAt.Value, nextShotAt, now))
            return;

        ammunition.Value--;
        nextShotAt = now + secondsBetweenShots;
        Debug.Log($"[Combat] Servidor aceptó disparo de {OwnerClientId}; balas={ammunition.Value}.");
        ResolveShot(aimOrigin, ApplySpread(aimDirection.normalized));
    }

    private void ResolveShot(Vector3 origin, Vector3 direction)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            origin, direction, range, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (left, right) =>
            left.distance.CompareTo(right.distance));

        NetworkLobbySession lobby = NetworkLobbySession.Instance;
        if (lobby == null ||
            !lobby.TryGetPlayerData(OwnerClientId, out LobbyPlayerData attacker))
        {
            ShotFeedbackRpc(ShotOutcome.Blocked, 0UL, 0, 0UL);
            return;
        }

        foreach (RaycastHit hit in hits)
        {
            NetworkPlayerHealth victim =
                hit.collider.GetComponentInParent<NetworkPlayerHealth>();

            if (victim == health)
                continue;

            if (victim == null)
            {
                ShotFeedbackRpc(ShotOutcome.Blocked, 0UL, 0, 0UL);
                return;
            }

            if (!lobby.TryGetPlayerData(victim.OwnerClientId,
                    out LobbyPlayerData victimData))
            {
                ShotFeedbackRpc(ShotOutcome.Blocked, 0UL, 0, 0UL);
                return;
            }

            if (!CombatValidationRules.IsEnemy(
                    attacker.TeamId, victimData.TeamId))
            {
                ShotFeedbackRpc(
                    ShotOutcome.FriendlyBlocked, victim.OwnerClientId, 0, 0UL);
                return;
            }

            if (victim.ApplyDamageFromPlayer(
                OwnerClientId, damage, out int applied, out ulong deathId))
            {
                Debug.Log(
                    $"[Combat] Impacto confirmado: {OwnerClientId} -> " +
                    $"{victim.OwnerClientId}, daño={applied}, " +
                    $"vida restante={victim.CurrentHealth}."
                );
                ShotFeedbackRpc(deathId > 0UL ? ShotOutcome.Kill : ShotOutcome.Hit,
                    victim.OwnerClientId, applied, deathId);
            }
            else
            {
                ShotFeedbackRpc(ShotOutcome.Blocked, 0UL, 0, 0UL);
            }
            return;
        }

        ShotFeedbackRpc(ShotOutcome.Miss, 0UL, 0, 0UL);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestReloadRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        MatchPhase phase = NetworkMatchManager.Instance != null
            ? NetworkMatchManager.Instance.Phase
            : MatchPhase.WaitingForPlayers;

        if (health == null || !CombatValidationRules.CanReload(
            phase, health.LifeState, ammunition.Value,
            magazineSize, reloadEndsAt.Value))
            return;

        reloadEndsAt.Value = NetworkManager.ServerTime.Time + reloadSeconds;
    }

    [Rpc(SendTo.Owner)]
    private void ShotFeedbackRpc(
        ShotOutcome outcome,
        ulong victimClientId,
        int appliedDamage,
        ulong deathEventId)
    {
        lastShotSummary = outcome switch
        {
            ShotOutcome.Hit => $"Impacto confirmado: jugador {victimClientId}, -{appliedDamage} HP",
            ShotOutcome.Kill => $"Muerte confirmada: jugador {victimClientId}, evento #{deathEventId}",
            ShotOutcome.FriendlyBlocked => "Aliado delante: sin daño",
            ShotOutcome.Blocked => "Impacto en obstáculo / objetivo invulnerable",
            ShotOutcome.Miss => "Disparo sin impacto",
            ShotOutcome.NoAmmo => "Sin balas: R para recargar",
            ShotOutcome.Reloading => "Recargando",
            ShotOutcome.Cooldown => "Cadencia: esperá el próximo disparo",
            ShotOutcome.Dead => "No podés disparar mientras estás muerto",
            ShotOutcome.NotPlaying => "La partida aún no está en Playing",
            _ => "Mira inválida: disparo rechazado"
        };
    }

    public void CancelReloadForDeath()
    {
        if (IsServer && combatMode)
            reloadEndsAt.Value = 0d;
    }

    public void ResetForRespawn()
    {
        if (!IsServer || !combatMode)
            return;

        ammunition.Value = magazineSize;
        reloadEndsAt.Value = 0d;
        nextShotAt = 0d;
    }

    private Vector3 ApplySpread(Vector3 direction)
    {
        Vector3 right = Vector3.Cross(Vector3.up, direction);
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.right;
        right.Normalize();
        Vector3 up = Vector3.Cross(direction, right).normalized;
        return (direction +
            right * UnityEngine.Random.Range(-spread, spread) +
            up * UnityEngine.Random.Range(-spread, spread)).normalized;
    }

    private static bool IsFinite(Vector3 vector) =>
        IsFinite(vector.x) && IsFinite(vector.y) && IsFinite(vector.z);

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
