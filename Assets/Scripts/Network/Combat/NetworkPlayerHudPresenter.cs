using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Traduce el estado replicado del jugador a la vista local del HUD.
/// Solo el propietario se suscribe y muestra su interfaz.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkPlayerHealth))]
[RequireComponent(typeof(NetworkPlayerInventory))]
[RequireComponent(typeof(NetworkPlayerCombat))]
public sealed class NetworkPlayerHudPresenter : NetworkBehaviour
{
    [SerializeField] private PlayerHudView view;

    private NetworkPlayerHealth health;
    private NetworkPlayerInventory inventory;
    private NetworkPlayerCombat combat;
    private bool subscribed;
    private bool requestedVisible = true;
    private bool wasReloading;

    public PlayerHudView View => view;
    public bool IsHudVisible =>
        view != null && view.HudCanvas != null && view.HudCanvas.enabled;

    private void Awake()
    {
        health = GetComponent<NetworkPlayerHealth>();
        inventory = GetComponent<NetworkPlayerInventory>();
        combat = GetComponent<NetworkPlayerCombat>();
        view ??= GetComponentInChildren<PlayerHudView>(true);
    }

    public override void OnNetworkSpawn()
    {
        SetHudVisible(requestedVisible);

        if (!IsOwner)
            return;

        Subscribe();
        RefreshAll();
    }

    public override void OnNetworkDespawn()
    {
        Unsubscribe();
        if (view != null)
            view.SetVisible(false);
    }

    public override void OnDestroy()
    {
        Unsubscribe();
        base.OnDestroy();
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner || view == null || combat == null)
            return;

        bool reloading = combat.ReloadRemaining > 0f;
        if (reloading == wasReloading)
            return;

        wasReloading = reloading;
        RefreshAmmunition();
    }

    public void SetHudVisible(bool visible)
    {
        requestedVisible = visible;
        if (view != null)
            view.SetVisible(IsSpawned && IsOwner && visible);
    }

    public void RefreshAll()
    {
        if (view == null || health == null || inventory == null || combat == null)
            return;

        view.SetHealth(health.CurrentHealth, health.MaxHealth);
        view.SetLifeState(health.LifeState);
        RefreshSelectedWeapon();
        RefreshAmmunition();
    }

    private void Subscribe()
    {
        if (subscribed || health == null || inventory == null || combat == null)
            return;

        health.HealthChanged += OnHealthChanged;
        health.LifeStateChanged += OnLifeStateChanged;
        inventory.SelectedWeaponChanged += OnSelectedWeaponChanged;
        combat.AmmunitionChanged += OnAmmunitionChanged;
        combat.ReserveAmmunitionChanged += OnReserveAmmunitionChanged;
        combat.ReloadChanged += OnReloadChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (health != null)
        {
            health.HealthChanged -= OnHealthChanged;
            health.LifeStateChanged -= OnLifeStateChanged;
        }

        if (inventory != null)
            inventory.SelectedWeaponChanged -= OnSelectedWeaponChanged;

        if (combat != null)
        {
            combat.AmmunitionChanged -= OnAmmunitionChanged;
            combat.ReserveAmmunitionChanged -= OnReserveAmmunitionChanged;
            combat.ReloadChanged -= OnReloadChanged;
        }

        subscribed = false;
    }

    private void OnHealthChanged(int current)
    {
        if (view != null && health != null)
            view.SetHealth(current, health.MaxHealth);
    }

    private void OnLifeStateChanged(PlayerLifeState state)
    {
        if (view == null || health == null)
            return;

        view.SetHealth(health.CurrentHealth, health.MaxHealth);
        view.SetLifeState(state);
    }

    private void OnSelectedWeaponChanged(WeaponId _)
    {
        RefreshSelectedWeapon();
        RefreshAmmunition();
    }

    private void OnAmmunitionChanged(int _) => RefreshAmmunition();
    private void OnReserveAmmunitionChanged(int _) => RefreshAmmunition();
    private void OnReloadChanged() => RefreshAmmunition();

    private void RefreshSelectedWeapon()
    {
        WeaponDefinition selected = inventory.SelectedWeaponDefinition;
        view.SetSelectedSlot(selected != null ? selected.Slot : WeaponSlot.None);
    }

    private void RefreshAmmunition()
    {
        if (view == null || combat == null)
            return;

        view.SetAmmunition(
            combat.Ammunition,
            combat.ReserveAmmunition,
            combat.SelectedWeaponUsesAmmunition);

        wasReloading = combat.ReloadRemaining > 0f;
        view.SetReloading(wasReloading);
    }
}
