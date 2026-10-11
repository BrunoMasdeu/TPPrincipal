using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public struct WeaponVisualBinding
{
    public WeaponId WeaponId;
    public GameObject Visual;
}

/// <summary>
/// Equipamiento autoritativo del jugador. El propietario pide una ranura,
/// el servidor elige el arma de su loadout y replica la seleccion.
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

    [Header("Visuales del PlayerPrefab")]
    [SerializeField] private WeaponVisualBinding[] weaponVisuals =
        Array.Empty<WeaponVisualBinding>();

    private readonly NetworkVariable<WeaponId> selectedWeapon = new(
        WeaponId.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkPlayerHealth health;
    private Grappling grappling;
    private WeaponId meleeWeaponId;
    private WeaponId sidearmWeaponId;
    private WeaponId primaryWeaponId;
    private WeaponId utilityWeaponId;
    private bool inventoryMode;

    public event Action<WeaponId> SelectedWeaponChanged;

    public WeaponId SelectedWeaponId => selectedWeapon.Value;
    public WeaponDefinition SelectedWeaponDefinition =>
        TryGetDefinition(selectedWeapon.Value, out WeaponDefinition definition)
            ? definition
            : null;

    private void Awake()
    {
        health = GetComponent<NetworkPlayerHealth>();
        grappling = GetComponent<Grappling>();
    }

    public override void OnNetworkSpawn()
    {
        selectedWeapon.OnValueChanged += OnSelectedWeaponChanged;
        TryActivateInventoryMode();
    }

    public override void OnNetworkDespawn()
    {
        selectedWeapon.OnValueChanged -= OnSelectedWeaponChanged;
        SelectedWeaponChanged = null;
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (!inventoryMode)
            TryActivateInventoryMode();

        if (!inventoryMode || !IsOwner || MatchDebugConsole.IsInputCaptured)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        int requestedNumber = keyboard.digit1Key.wasPressedThisFrame ? 1 :
            keyboard.digit2Key.wasPressedThisFrame ? 2 :
            keyboard.digit3Key.wasPressedThisFrame ? 3 :
            keyboard.digit4Key.wasPressedThisFrame ? 4 : 0;

        if (TryGetSlotForNumber(requestedNumber, out WeaponSlot slot))
            RequestEquipSlotRpc(slot);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestEquipSlotRpc(
        WeaponSlot slot,
        RpcParams rpcParams = default)
    {
        if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId ||
            !TryGetLoadoutWeapon(slot, out WeaponId weaponId) ||
            health == null || health.LifeState != PlayerLifeState.Alive ||
            NetworkMatchManager.Instance?.Phase == MatchPhase.Finished)
            return;

        selectedWeapon.Value = weaponId;
    }

    /// <summary>
    /// Punto de integracion para un futuro menu de loadout. Solo el servidor
    /// puede cambiar las armas que ocupan las ranuras secundaria y principal.
    /// </summary>
    public bool TrySetLoadoutOnServer(
        WeaponId sidearm,
        WeaponId primary)
    {
        if (!IsServer ||
            !TryGetDefinition(sidearm, out WeaponDefinition sidearmDefinition) ||
            !TryGetDefinition(primary, out WeaponDefinition primaryDefinition) ||
            sidearmDefinition.Slot != WeaponSlot.Secondary ||
            primaryDefinition.Slot != WeaponSlot.Primary)
            return false;

        sidearmWeaponId = sidearm;
        primaryWeaponId = primary;

        if (!IsInCurrentLoadout(selectedWeapon.Value))
            selectedWeapon.Value = sidearmWeaponId;

        return true;
    }

    public bool TryGetDefinition(
        WeaponId weaponId,
        out WeaponDefinition definition)
    {
        definition = null;
        if (weaponId == WeaponId.None)
            return false;

        if (TryMatchDefinition(meleeWeapon, weaponId, out definition) ||
            TryMatchDefinition(startingSidearm, weaponId, out definition) ||
            TryMatchDefinition(startingPrimary, weaponId, out definition) ||
            TryMatchDefinition(startingUtility, weaponId, out definition))
            return true;

        foreach (WeaponDefinition candidate in weaponDefinitions)
        {
            if (TryMatchDefinition(candidate, weaponId, out definition))
                return true;
        }

        return false;
    }

    public void CopyDefinitionsTo(List<WeaponDefinition> destination)
    {
        if (destination == null)
            return;

        destination.Clear();
        AddUniqueDefinition(destination, meleeWeapon);
        AddUniqueDefinition(destination, startingSidearm);
        AddUniqueDefinition(destination, startingPrimary);
        AddUniqueDefinition(destination, startingUtility);

        foreach (WeaponDefinition definition in weaponDefinitions)
            AddUniqueDefinition(destination, definition);
    }

    public static bool TryGetSlotForNumber(
        int number,
        out WeaponSlot slot)
    {
        slot = number switch
        {
            1 => WeaponSlot.Melee,
            2 => WeaponSlot.Secondary,
            3 => WeaponSlot.Primary,
            4 => WeaponSlot.Utility,
            _ => WeaponSlot.None
        };
        return slot != WeaponSlot.None;
    }

    private void InitializeLoadout()
    {
        meleeWeaponId = GetConfiguredId(meleeWeapon, WeaponSlot.Melee);
        sidearmWeaponId = GetConfiguredId(startingSidearm, WeaponSlot.Secondary);
        primaryWeaponId = GetConfiguredId(startingPrimary, WeaponSlot.Primary);
        utilityWeaponId = GetConfiguredId(startingUtility, WeaponSlot.Utility);

        if (sidearmWeaponId == WeaponId.None)
        {
            Debug.LogError(
                $"[Inventario] '{name}' no tiene un arma secundaria valida.",
                this);
        }

        if (utilityWeaponId == WeaponId.None)
        {
            Debug.LogWarning(
                $"[Inventario] '{name}' no tiene una utilidad configurada.",
                this);
        }
    }

    private bool TryGetLoadoutWeapon(
        WeaponSlot slot,
        out WeaponId weaponId)
    {
        weaponId = slot switch
        {
            WeaponSlot.Melee => meleeWeaponId,
            WeaponSlot.Secondary => sidearmWeaponId,
            WeaponSlot.Primary => primaryWeaponId,
            WeaponSlot.Utility => utilityWeaponId,
            _ => WeaponId.None
        };
        return weaponId != WeaponId.None &&
            TryGetDefinition(weaponId, out _);
    }

    private bool IsInCurrentLoadout(WeaponId weaponId) =>
        weaponId != WeaponId.None &&
        (weaponId == meleeWeaponId ||
         weaponId == sidearmWeaponId ||
         weaponId == primaryWeaponId ||
         weaponId == utilityWeaponId);

    private void OnSelectedWeaponChanged(
        WeaponId previous,
        WeaponId current)
    {
        if (!inventoryMode)
            return;

        ApplyWeaponVisual(current);
        SelectedWeaponChanged?.Invoke(current);
    }

    private void TryActivateInventoryMode()
    {
        if (inventoryMode || !IsSpawned || !SupportsInventoryPreview())
            return;

        inventoryMode = true;

        if (IsServer)
        {
            InitializeLoadout();
            selectedWeapon.Value = sidearmWeaponId != WeaponId.None
                ? sidearmWeaponId
                : meleeWeaponId;
        }

        ApplyWeaponVisual(selectedWeapon.Value);
    }

    private void ApplyWeaponVisual(WeaponId current)
    {
        foreach (WeaponVisualBinding binding in weaponVisuals)
        {
            if (binding.Visual != null)
                binding.Visual.SetActive(binding.WeaponId == current);
        }

        grappling?.SetEquipped(current == WeaponId.Grapple);
    }

    private bool SupportsInventoryPreview() =>
        NetworkLobbySession.Instance != null &&
        CombatValidationRules.SupportsInventoryPreview(
            NetworkLobbySession.Instance.SelectedGameModeId);

    private static WeaponId GetConfiguredId(
        WeaponDefinition definition,
        WeaponSlot expectedSlot) =>
        definition != null && definition.Slot == expectedSlot
            ? definition.Id
            : WeaponId.None;

    private static bool TryMatchDefinition(
        WeaponDefinition candidate,
        WeaponId weaponId,
        out WeaponDefinition definition)
    {
        definition = candidate != null && candidate.Id == weaponId
            ? candidate
            : null;
        return definition != null;
    }

    private static void AddUniqueDefinition(
        List<WeaponDefinition> destination,
        WeaponDefinition definition)
    {
        if (definition == null)
            return;

        foreach (WeaponDefinition existing in destination)
        {
            if (existing != null && existing.Id == definition.Id)
                return;
        }

        destination.Add(definition);
    }
}
