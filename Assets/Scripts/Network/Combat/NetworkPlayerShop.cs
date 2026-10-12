using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkPlayerCombat))]
[RequireComponent(typeof(NetworkPlayerInventory))]
[RequireComponent(typeof(NetworkPlayerHealth))]
public class NetworkPlayerShop : NetworkBehaviour
{
    [Serializable]
    private sealed class ShopItem
    {
        public string Label;
        public WeaponDefinition Weapon;
        [Min(0)] public int Price;
    }

    [Header("Ventana de compra")]
    [SerializeField, Min(1f)] private float purchaseSeconds = 15f;

    [Header("Catalogo")]
    [SerializeField] private ShopItem[] items =
    {
        new ShopItem { Label = "AK", Price = 600 },
        new ShopItem { Label = "Gancho", Price = 300 },
        new ShopItem { Label = "M4", Price = 500 },
        new ShopItem { Label = "Revolver", Price = 250 }
    };

    private readonly NetworkVariable<double> purchaseEndsAt = new(
        0d,
        NetworkVariableReadPermission.Owner,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<uint> ownedItems = new(
        0u,
        NetworkVariableReadPermission.Owner,
        NetworkVariableWritePermission.Server);

    private NetworkPlayerCombat combat;
    private NetworkPlayerInventory inventory;
    private NetworkPlayerHealth health;

    private bool menuOpen;
    private string feedback = "";
    private Vector2 scroll;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    private static NetworkPlayerShop openShop;

    public static bool IsInputCaptured =>
        openShop != null && openShop.menuOpen;

    private int ItemCount => items == null ? 0 : Mathf.Min(items.Length, 32);

    private bool IsPlayingShopMode
    {
        get
        {
            NetworkLobbySession lobby = NetworkLobbySession.Instance;
            NetworkMatchManager match = NetworkMatchManager.Instance;

            if (lobby == null || match == null ||
                match.Phase != MatchPhase.Playing)
            {
                return false;
            }

            return lobby.SelectedGameModeId == GameModeId.TDM ||
               lobby.SelectedGameModeId == GameModeId.CTF;
        }
    }


    private float RemainingSeconds =>
        NetworkManager == null
            ? 0f
            : Mathf.Max(
                0f,
                (float)(purchaseEndsAt.Value -
                        NetworkManager.ServerTime.Time));

    private bool CanUseShop =>
        IsSpawned &&
        IsPlayingShopMode &&
        health != null &&
        health.LifeState == PlayerLifeState.Alive &&
        RemainingSeconds > 0f;

    private void Awake()
    {
        combat = GetComponent<NetworkPlayerCombat>();
        inventory = GetComponent<NetworkPlayerInventory>();
        health = GetComponent<NetworkPlayerHealth>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            purchaseEndsAt.Value = 0d;
            ownedItems.Value = 0u;
            RegisterOwnedItems();
        }
    }

    public override void OnNetworkDespawn()
    {
        CloseMenu();
        base.OnNetworkDespawn();
    }

    private void OnDisable()
    {
        CloseMenu();
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (IsServer)
        {
            RegisterOwnedItems();

            if (!IsPlayingShopMode ||
                health.LifeState != PlayerLifeState.Alive)
            {
                purchaseEndsAt.Value = 0d;
            }
        }

        if (!IsOwner)
            return;

        if (menuOpen &&
            (!CanUseShop || MatchDebugConsole.IsInputCaptured))
        {
            CloseMenu();
        }

        if (MatchDebugConsole.IsInputCaptured ||
            Keyboard.current == null)
            return;

        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            if (menuOpen)
                CloseMenu();
            else if (CanUseShop)
                OpenMenu();
        }

        if (menuOpen &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseMenu();
        }
    }

    public void BeginPurchaseWindowOnServer()
    {
        if (!IsServer || !IsSpawned || !IsPlayingShopMode)
            return;

        RegisterOwnedItems();

        purchaseEndsAt.Value =
            NetworkManager.ServerTime.Time +
            Mathf.Max(1f, purchaseSeconds);
    }

        private void RegisterOwnedItems()
        {
            if (!IsServer)
                return;

            uint mask = 0u;

            for (int i = 0; i < ItemCount; i++)
            {
                ShopItem item = items[i];

                if (item != null &&
                    item.Weapon != null &&
                    item.Weapon.Id != WeaponId.None &&
                    inventory.HasWeaponInLoadout(item.Weapon.Id))
                {
                    mask |= 1u << i;
                }
            }

            if (mask != ownedItems.Value)
                ownedItems.Value = mask;
        }


    private bool OwnsItem(int index)
    {
        return (ownedItems.Value & (1u << index)) != 0u;
    }

    private void OpenMenu()
    {
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        menuOpen = true;
        openShop = this;
        feedback = "";

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CloseMenu()
    {
        if (!menuOpen)
            return;

        menuOpen = false;

        if (openShop == this)
            openShop = null;

        if (IsOwner &&
            IsPlayingShopMode &&
            health != null &&
            health.LifeState == PlayerLifeState.Alive &&
            !MatchDebugConsole.IsInputCaptured)
        {
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }
    }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void BuyItemRpc(int index, RpcParams rpcParams = default)
        {
            if (!IsServer ||
                rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            if (!CanUseShop)
            {
                PurchaseFeedbackRpc("El tiempo de compra termino.");
                return;
            }

            if (index < 0 || index >= ItemCount)
                return;

            ShopItem item = items[index];

            if (item == null ||
                item.Weapon == null ||
                item.Weapon.Id == WeaponId.None ||
                item.Price < 0)
            {
                PurchaseFeedbackRpc("Articulo sin configurar.");
                return;
            }

            RegisterOwnedItems();

            if (OwnsItem(index))
            {
                PurchaseFeedbackRpc("Ya tenes ese objeto.");
                return;
            }

            if (!combat.TryBuyWeaponOnServer(
                    item.Weapon.Id,
                    item.Price))
            {
                PurchaseFeedbackRpc(
                    "Compra rechazada: saldo insuficiente, " +
                    "arma propia o configuracion invalida.");
                return;
            }

            RegisterOwnedItems();

            PurchaseFeedbackRpc("Compraste: " + item.Label);
        }


    [Rpc(SendTo.Owner)]
    private void PurchaseFeedbackRpc(string message)
    {
        feedback = message;
    }

    private void OnGUI()
    {
        if (!IsSpawned || !IsOwner)
            return;

        if (!menuOpen)
        {
            if (CanUseShop)
            {
                GUI.Box(
                    new Rect(12f, Screen.height - 48f, 330f, 36f),
                    $"B: tienda | {RemainingSeconds:0.0}s | " +
                    $"Saldo: ${combat.Money}");
            }

            return;
        }

        float width = Mathf.Min(430f, Screen.width - 20f);
        float height = Mathf.Min(380f, Screen.height - 20f);

        Rect area = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        GUI.Box(area, string.Empty);

        GUILayout.BeginArea(new Rect(
            area.x + 12f,
            area.y + 12f,
            area.width - 24f,
            area.height - 24f));

        GUILayout.Label("TIENDA | B / Esc: cerrar");
        GUILayout.Label($"Tiempo restante: {RemainingSeconds:0.0}s");
        GUILayout.Label($"Dinero: ${combat.Money}");
        GUILayout.Space(10f);

        scroll = GUILayout.BeginScrollView(scroll);

        for (int i = 0; i < ItemCount; i++)
        {
            ShopItem item = items[i];

            if (item == null)
                continue;

            bool configured = item.Weapon != null;
            bool owned = OwnsItem(i);

            string text = owned
                ? $"{item.Label} - YA LO TENES"
                : $"{item.Label} - ${item.Price}";

            if (!configured)
                text += " (sin configurar)";

            bool previousEnabled = GUI.enabled;

            GUI.enabled = previousEnabled &&
                          configured &&
                          !owned &&
                          item.Price >= 0 &&
                          combat.Money >= item.Price &&
                          CanUseShop;

            if (GUILayout.Button(text, GUILayout.Height(40f)))
                BuyItemRpc(i);

            GUI.enabled = previousEnabled;
        }

        GUILayout.EndScrollView();

        GUILayout.Space(8f);
        GUILayout.Label(feedback);

        if (GUILayout.Button("Cerrar"))
            CloseMenu();

        GUILayout.EndArea();
    }
}
