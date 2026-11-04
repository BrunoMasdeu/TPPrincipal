using UnityEngine;
using UnityEngine.InputSystem;

public class Inventory : MonoBehaviour
{
    [Header("Armas")]
    public GameObject knife;
    public GameObject pistol;
    public GameObject rifle;
    public GameObject Gancho;

    private GameObject currentWeapon;

    // [MULTIJUGADOR] En red, el arma la confirma el servidor (por la tienda y
    // para que los demás vean qué arma tenés).
    private NetworkPlayerInventory network;

    // [MULTIJUGADOR] El arma que está en la mano (la usa el HUD).
    public GameObject CurrentWeapon => currentWeapon;

    // [MULTIJUGADOR]
    void Awake()
    {
        network = GetComponent<NetworkPlayerInventory>();
    }

    void Start()
    {
        EquipWeapon(pistol); // [COMO HOY] antes empezaba con el cuchillo
    }

    void Update()
    {
        if (Keyboard.current == null)
            return;

        // [MULTIJUGADOR] Con la tienda o la consola de depuración abiertas no
        // se cambia de arma.
        if (MatchDebugConsole.IsInputCaptured || NetworkPlayerShop.IsInputCaptured)
            return;

        // [COMO HOY] Teclas como hoy: 1 principal, 2 secundaria, 3 cuchillo,
        // 4 gancho. Antes: 1 cuchillo, 2 pistola, 3 rifle, 4 gancho.
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            SelectWeapon(rifle);
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SelectWeapon(pistol);
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            SelectWeapon(knife);
        }

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            SelectWeapon(Gancho);
        }
    }

    // [MULTIJUGADOR] Sin red se equipa directo. En red se le pide al servidor;
    // cuando confirma, NetworkPlayerInventory llama a EquipWeapon en todas
    // las PCs.
    void SelectWeapon(GameObject weapon)
    {
        if (network != null)
            network.RequestEquip(weapon);
        else
            EquipWeapon(weapon);
    }

    public void EquipWeapon(GameObject weapon) // [MULTIJUGADOR] ahora es public
    {
        if (weapon == null)
            return;

        if (currentWeapon != null)
        {
            currentWeapon.SetActive(false);
        }

        currentWeapon = weapon;
        currentWeapon.SetActive(true);
    }
}
