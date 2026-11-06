using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public struct WeaponVisualBinding
{
    public WeaponId WeaponId;
    public GameObject Visual;
}

/// <summary>
/// Parte de red del inventario de Felipe (Inventory).
///
/// - Inventory lee las teclas 1-4 y llama a RequestEquip con el objeto del
///   arma. Aquí se traduce a WeaponId y se le pide al servidor.
/// - El servidor sabe qué armas tiene cada jugador (ranuras y compras) y
///   decide el arma en la mano. La replica con NetworkVariables.
/// - En todas las PCs, cuando cambia el arma en la mano, se llama al
///   EquipWeapon de Felipe para mostrarla.
/// </summary>
[DisallowMultipleComponent]
public class NetworkPlayerInventory : NetworkBehaviour
{
    [Header("Definiciones disponibles")]
    [SerializeField] private WeaponDefinition[] weaponDefinitions =
        Array.Empty<WeaponDefinition>();

    [Header("Equipamiento inicial")]
    [SerializeField] private WeaponDefinition meleeWeapon;
    [SerializeField] private WeaponDefinition startingSidearm;
    [SerializeField] private WeaponDefinition startingPrimary;
    [SerializeField] private WeaponDefinition startingUtility;

    [Header("Objeto de cada arma dentro del jugador")]
    [SerializeField] private WeaponVisualBinding[] weaponVisuals =
        Array.Empty<WeaponVisualBinding>();

    // Arma en la mano. Read Everyone: los demás deben ver qué arma tenés.
    // Write Server: el cliente solo puede pedir el cambio.
    private readonly NetworkVariable<WeaponId> selectedWeapon = new(
        WeaponId.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    // Secundaria y principal: cambian al comprar en la tienda.
    private readonly NetworkVariable<WeaponId> replicatedSidearmWeapon = new(
        WeaponId.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<WeaponId> replicatedPrimaryWeapon = new(
        WeaponId.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private Inventory weaponInventory;
    private NetworkPlayerHealth health;
    private Grappling grappling;
    private bool inventoryMode;
    // Escena de zombies (singleplayer): no depende del lobby.
    private bool inZombiesScene;
    private WeaponId shownWeapon = WeaponId.None;

    // Solo en el servidor: arma de cada ranura.
    private WeaponId meleeWeaponId;
    private WeaponId sidearmWeaponId;
    private WeaponId primaryWeaponId;
    private WeaponId utilityWeaponId;

    public event Action<WeaponId> SelectedWeaponChanged;
    public event Action LoadoutChanged;

    public WeaponId SelectedWeaponId => selectedWeapon.Value;
    public WeaponDefinition SidearmWeaponDefinition => DefinitionOrNull(replicatedSidearmWeapon.Value);
    public WeaponDefinition PrimaryWeaponDefinition => DefinitionOrNull(replicatedPrimaryWeapon.Value);
    public WeaponDefinition SelectedWeaponDefinition => DefinitionOrNull(selectedWeapon.Value);

    private void Awake()
    {
        weaponInventory = GetComponent<Inventory>();
        health = GetComponent<NetworkPlayerHealth>();
        grappling = GetComponent<Grappling>();

        // El Inventory de Felipe lee el teclado: solo se enciende en el dueño.
        SetLocalInputEnabled(false);
    }

    public override void OnNetworkSpawn()
    {
        inZombiesScene = WaveManager.IsLoaded;

        selectedWeapon.OnValueChanged += OnSelectedWeaponChanged;
        replicatedSidearmWeapon.OnValueChanged += OnLoadoutWeaponChanged;
        replicatedPrimaryWeapon.OnValueChanged += OnLoadoutWeaponChanged;
    }

    public override void OnNetworkDespawn()
    {
        selectedWeapon.OnValueChanged -= OnSelectedWeaponChanged;
        replicatedSidearmWeapon.OnValueChanged -= OnLoadoutWeaponChanged;
        replicatedPrimaryWeapon.OnValueChanged -= OnLoadoutWeaponChanged;
        SelectedWeaponChanged = null;
        LoadoutChanged = null;
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (!inventoryMode)
            TryActivateInventoryMode();

        if (inventoryMode)
            ShowSelectedWeapon();
    }

    /// <summary>Lo llama PlayerNetworkSetup: el teclado solo funciona en el dueño.</summary>
    public void SetLocalInputEnabled(bool enabled)
    {
        if (weaponInventory != null)
            weaponInventory.enabled = enabled;
    }

    private void TryActivateInventoryMode()
    {
        if (!inZombiesScene)
        {
            NetworkLobbySession lobby = NetworkLobbySession.Instance;
            if (lobby == null ||
                !CombatValidationRules.SupportsInventoryPreview(
                    lobby.SelectedGameModeId,
                    IsTutorialTraining()))
                return;
        }

        inventoryMode = true;

        // Se apagan todas las armas; después se prende solo la elegida.
        foreach (WeaponVisualBinding binding in weaponVisuals)
        {
            if (binding.Visual != null)
                binding.Visual.SetActive(false);
        }

        if (IsServer)
        {
            // Modo zombies: solo cuchillo y pistola al empezar.
            // El prefab se comparte con los modos multijugador, por eso
            // se decide por código y no se tocan las ranuras del Inspector.
            bool zombiesLoadout = inZombiesScene;

            meleeWeaponId = IdIfSlot(meleeWeapon, WeaponSlot.Melee);
            sidearmWeaponId = IdIfSlot(startingSidearm, WeaponSlot.Secondary);
            primaryWeaponId = zombiesLoadout
                ? WeaponId.None
                : IdIfSlot(startingPrimary, WeaponSlot.Primary);
            utilityWeaponId = zombiesLoadout
                ? WeaponId.None
                : IdIfSlot(startingUtility, WeaponSlot.Utility);
            PublishLoadoutWeapons();
            selectedWeapon.Value = sidearmWeaponId != WeaponId.None
                ? sidearmWeaponId
                : meleeWeaponId;
        }
    }

    /// <summary>
    /// Mantiene el Inventory de Felipe igual que lo que decidió el servidor:
    /// qué objeto es la pistola y el rifle (cambian al comprar) y qué arma
    /// está en la mano.
    /// </summary>
    private void ShowSelectedWeapon()
    {
        if (weaponInventory == null)
            return;

        weaponInventory.knife = VisualFor(WeaponId.Knife);
        weaponInventory.pistol = VisualFor(replicatedSidearmWeapon.Value);
        weaponInventory.rifle = VisualFor(replicatedPrimaryWeapon.Value);
        weaponInventory.Gancho = VisualFor(WeaponId.Grapple);

        GameObject selected = VisualFor(selectedWeapon.Value);
        if (selected != null && weaponInventory.CurrentWeapon != selected)
            weaponInventory.EquipWeapon(selected);

        if (shownWeapon != selectedWeapon.Value)
        {
            shownWeapon = selectedWeapon.Value;
            if (grappling != null)
                grappling.SetEquipped(shownWeapon == WeaponId.Grapple);
        }
    }

    // =====================================================================
    // Pedido del dueño (lo llama el Inventory de Felipe)
    // =====================================================================

    public void RequestEquip(GameObject weaponObject)
    {
        if (!IsSpawned || !IsOwner || weaponObject == null)
            return;

        foreach (WeaponVisualBinding binding in weaponVisuals)
        {
            if (binding.Visual == weaponObject)
            {
                RequestEquipRpc(binding.WeaponId);
                return;
            }
        }
    }

    // SendTo.Server: el servidor revisa que tengas esa arma y que estés vivo.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestEquipRpc(WeaponId weaponId, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId ||
            !IsInCurrentLoadout(weaponId) ||
            health == null || health.LifeState != PlayerLifeState.Alive ||
            NetworkMatchManager.Instance?.Phase == MatchPhase.Finished)
            return;

        selectedWeapon.Value = weaponId;
    }

    // =====================================================================
    // Servidor: tienda y loadout
    // =====================================================================

    /// <summary>Lo llama NetworkPlayerCombat al confirmar una compra.</summary>
    public bool TryAddPurchasedWeaponOnServer(WeaponId weaponId)
    {
        if (!IsServer || !IsSpawned || !inventoryMode ||
            !TryGetDefinition(weaponId, out WeaponDefinition definition) ||
            IsInCurrentLoadout(weaponId))
            return false;

        switch (definition.Slot)
        {
            case WeaponSlot.Melee: meleeWeaponId = weaponId; break;
            case WeaponSlot.Secondary: sidearmWeaponId = weaponId; break;
            case WeaponSlot.Primary: primaryWeaponId = weaponId; break;
            case WeaponSlot.Utility: utilityWeaponId = weaponId; break;
            default: return false;
        }

        PublishLoadoutWeapons();
        // El arma comprada pasa a la mano.
        selectedWeapon.Value = weaponId;
        return true;
    }

    /// <summary>Punto de integración para un futuro menú de loadout.</summary>
    public bool TrySetLoadoutOnServer(WeaponId sidearm, WeaponId primary)
    {
        if (!IsServer ||
            !TryGetDefinition(sidearm, out WeaponDefinition sidearmDefinition) ||
            !TryGetDefinition(primary, out WeaponDefinition primaryDefinition) ||
            sidearmDefinition.Slot != WeaponSlot.Secondary ||
            primaryDefinition.Slot != WeaponSlot.Primary)
            return false;

        sidearmWeaponId = sidearm;
        primaryWeaponId = primary;
        PublishLoadoutWeapons();

        if (!IsInCurrentLoadout(selectedWeapon.Value))
            selectedWeapon.Value = sidearmWeaponId;

        return true;
    }

    public bool HasWeaponInLoadout(WeaponId weaponId) => IsInCurrentLoadout(weaponId);

    // =====================================================================
    // Catálogo y ayudantes
    // =====================================================================

    public bool TryGetDefinition(WeaponId weaponId, out WeaponDefinition definition)
    {
        definition = null;
        if (weaponId == WeaponId.None)
            return false;

        foreach (WeaponDefinition candidate in AllDefinitions())
        {
            if (candidate != null && candidate.Id == weaponId)
            {
                definition = candidate;
                return true;
            }
        }

        return false;
    }

    public void CopyDefinitionsTo(List<WeaponDefinition> destination)
    {
        if (destination == null)
            return;

        destination.Clear();
        foreach (WeaponDefinition definition in AllDefinitions())
        {
            if (definition != null && !destination.Exists(d => d.Id == definition.Id))
                destination.Add(definition);
        }
    }

    public static bool TryGetSlotForNumber(int number, out WeaponSlot slot)
    {
        slot = number switch
        {
            1 => WeaponSlot.Primary,
            2 => WeaponSlot.Secondary,
            3 => WeaponSlot.Melee,
            4 => WeaponSlot.Utility,
            _ => WeaponSlot.None
        };
        return slot != WeaponSlot.None;
    }

    private IEnumerable<WeaponDefinition> AllDefinitions()
    {
        yield return meleeWeapon;
        yield return startingSidearm;
        yield return startingPrimary;
        yield return startingUtility;

        foreach (WeaponDefinition definition in weaponDefinitions)
            yield return definition;
    }

    private bool IsInCurrentLoadout(WeaponId weaponId) =>
        weaponId != WeaponId.None &&
        (weaponId == meleeWeaponId ||
         weaponId == sidearmWeaponId ||
         weaponId == primaryWeaponId ||
         weaponId == utilityWeaponId);

    private GameObject VisualFor(WeaponId weaponId)
    {
        if (weaponId == WeaponId.None)
            return null;

        foreach (WeaponVisualBinding binding in weaponVisuals)
        {
            if (binding.WeaponId == weaponId)
                return binding.Visual;
        }

        return null;
    }

    private WeaponDefinition DefinitionOrNull(WeaponId weaponId) =>
        TryGetDefinition(weaponId, out WeaponDefinition definition) ? definition : null;

    private void PublishLoadoutWeapons()
    {
        replicatedSidearmWeapon.Value = sidearmWeaponId;
        replicatedPrimaryWeapon.Value = primaryWeaponId;
    }

    private void OnSelectedWeaponChanged(WeaponId previous, WeaponId current)
    {
        SelectedWeaponChanged?.Invoke(current);
    }

    private void OnLoadoutWeaponChanged(WeaponId previous, WeaponId current)
    {
        LoadoutChanged?.Invoke();
    }

    private static WeaponId IdIfSlot(WeaponDefinition definition, WeaponSlot slot) =>
        definition != null && definition.Slot == slot ? definition.Id : WeaponId.None;

    private static bool IsTutorialTraining() =>
        NetworkLobbySession.Instance != null &&
        NetworkLobbySession.Instance.SelectedGameModeId == GameModeId.None &&
        TutorialManager.IsTutorialLoaded;
}