using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkObject))]
public class WeaponPickup : NetworkBehaviour
{
    [Serializable]
    private struct PickupVisual
    {
        public WeaponId weaponId;
        public GameObject model; // hijo del prefab, con el modelo del arma
    }

    [SerializeField] private PickupVisual[] visuals = Array.Empty<PickupVisual>();
    [SerializeField, Min(0f)] private float lifetimeSeconds = 30f; // 0 = no desaparece
    [SerializeField, Min(0.5f)] private float pickupRadius = 2.5f;

    private readonly NetworkVariable<WeaponId> weaponId = new(
        WeaponId.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private static readonly List<WeaponPickup> all = new();

    private double serverSpawnTime;
    private bool consumed;

    public override void OnNetworkSpawn()
    {
        all.Add(this);
        weaponId.OnValueChanged += OnWeaponChanged;
        ShowVisual(weaponId.Value);

        if (IsServer)
            serverSpawnTime = NetworkManager.ServerTime.Time;
    }

    public override void OnNetworkDespawn()
    {
        all.Remove(this);
        weaponId.OnValueChanged -= OnWeaponChanged;
    }

    /// <summary>Llamar en el servidor DESPUÉS de Spawn().</summary>
    public void SetWeaponOnServer(WeaponId id)
    {
        if (IsServer && IsSpawned)
            weaponId.Value = id;
    }

    private void OnWeaponChanged(WeaponId previous, WeaponId current) => ShowVisual(current);

    private void ShowVisual(WeaponId id)
    {
        foreach (PickupVisual v in visuals)
        {
            if (v.model != null)
            {
                v.model.SetActive(v.weaponId == id);
                if (v.model.activeSelf)
                    PlaceVisualOnGround(v.model);
            }
        }
    }

    private void PlaceVisualOnGround(GameObject model)
    {
        float bottom = float.PositiveInfinity;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (renderer.enabled)
                bottom = Mathf.Min(bottom, renderer.bounds.min.y);
        }
        if (bottom < float.PositiveInfinity)
            model.transform.position += Vector3.up * (transform.position.y + 0.02f - bottom);
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        // Servidor: el pickup desaparece solo si nadie lo agarra.
        if (IsServer && !consumed && lifetimeSeconds > 0f &&
            NetworkManager.ServerTime.Time - serverSpawnTime >= lifetimeSeconds)
        {
            consumed = true;
            NetworkObject.Despawn(true);
            return;
        }

        // Cliente local: tecla E si este es el pickup más cercano en rango.
        if (MatchDebugConsole.IsInputCaptured || NetworkPlayerShop.IsInputCaptured ||
            Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            return;

        if (CanLocalPlayerInteract())
            PickupRpc();
    }

    private bool CanLocalPlayerInteract()
    {
        if (!IsSpawned || consumed || weaponId.Value == WeaponId.None)
            return false;

        NetworkObject player = NetworkManager.LocalClient?.PlayerObject;
        if (player == null ||
            !player.TryGetComponent(out NetworkPlayerHealth health) ||
            health.LifeState != PlayerLifeState.Alive ||
            !player.TryGetComponent(out PlayerNetworkSetup setup) ||
            !setup.LocalGameplayInputEnabled)
            return false;

        Vector3 position = player.transform.position;
        float distance = (position - transform.position).sqrMagnitude;
        if (distance > pickupRadius * pickupRadius)
            return false;

        foreach (WeaponPickup other in all)
        {
            if (other == this || other == null || other.consumed ||
                other.weaponId.Value == WeaponId.None)
                continue;

            float otherDistance = (position - other.transform.position).sqrMagnitude;
            if (otherDistance <= other.pickupRadius * other.pickupRadius &&
                (otherDistance < distance ||
                 (otherDistance == distance && other.NetworkObjectId < NetworkObjectId)))
                return false;
        }
        return true;
    }

    // El cliente pide, el servidor valida todo.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PickupRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || consumed || weaponId.Value == WeaponId.None)
            return;

        ulong sender = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.ConnectedClients.TryGetValue(sender, out NetworkClient client) ||
            client.PlayerObject == null)
            return;

        NetworkObject playerObject = client.PlayerObject;
        NetworkPlayerHealth health = playerObject.GetComponent<NetworkPlayerHealth>();
        NetworkPlayerInventory inventory = playerObject.GetComponent<NetworkPlayerInventory>();

        if (health == null || inventory == null ||
            health.LifeState != PlayerLifeState.Alive ||
            Vector3.Distance(playerObject.transform.position, transform.position) > pickupRadius + 1f)
            return;

        if (inventory.TryAddPurchasedWeaponOnServer(weaponId.Value))
        {
            consumed = true;
            NetworkObject.Despawn(true);
        }
    }

    // Cartel en pantalla para el jugador local.
    private void OnGUI()
    {
        if (!CanLocalPlayerInteract())
            return;

        GUI.Box(new Rect(Screen.width * 0.5f - 110f, Screen.height * 0.62f, 220f, 32f),
            $"E: agarrar {weaponId.Value}");
    }
}
