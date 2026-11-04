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

    void Start()
    {
        EquipWeapon(knife);
    }

    void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            EquipWeapon(knife);
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            EquipWeapon(pistol);
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            EquipWeapon(rifle);
        }

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            EquipWeapon(Gancho);
        }
    }

    void EquipWeapon(GameObject weapon)
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
