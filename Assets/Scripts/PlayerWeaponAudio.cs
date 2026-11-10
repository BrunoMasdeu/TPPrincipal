using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Conecta el combate de red con los sonidos de las armas, SIN modificar
/// NetworkPlayerCombat (así no se pisa el trabajo de quien lo mantiene ni
/// se generan conflictos al hacer merge).
///
/// NetworkPlayerCombat ya avisa a TODOS los clientes (y al host) cada vez
/// que el servidor acepta un ataque, mediante el evento AttackPresented.
/// Este componente se suscribe a ese evento y le pide el sonido al
/// WeaponShotAudio del arma que corresponda.
///
/// Cómo usarlo:
/// 1. Poné este archivo en Assets/Scripts y esperá a que compile.
/// 2. Abrí PlayerPrefab y seleccioná el objeto RAÍZ (el que tiene
///    NetworkObject y NetworkPlayerCombat).
/// 3. Add Component > Player Weapon Audio.
/// 4. En "Weapon Sounds" agregá un elemento por arma:
///      - Weapon: el arma (se elige de la lista).
///      - Sound: arrastrá el objeto que tiene el componente
///        Weapon Shot Audio (por ejemplo "Guns").
/// 5. Guardá el prefab.
///
/// Quién lo escucha cómo: IsOwner es true solo en la máquina de quien
/// disparó, así que ahí suena el clip de primera persona; en las demás
/// suena el de tercera persona o el lejano, según la distancia.
/// </summary>
[DisallowMultipleComponent]
public class PlayerWeaponAudio : NetworkBehaviour
{
    [Serializable]
    public class WeaponSound
    {
        public WeaponId weapon;
        public WeaponShotAudio sound;
    }

    [Header("Referencias")]
    [Tooltip("Si queda vacío, se busca solo en este objeto o en sus padres.")]
    [SerializeField] private NetworkPlayerCombat combat;

    [Header("Sonido por arma")]
    [SerializeField] private WeaponSound[] weaponSounds;

    [Header("Recarga")]
    [Tooltip("Si queda vacío, se busca solo en este objeto o en sus padres.")]
    [SerializeField] private NetworkPlayerInventory inventory;

    [Header("Clic de arma vacía")]
    [Tooltip("Pausa mínima (segundos) entre dos clics de arma vacía.")]
    [SerializeField] private float emptyClickCooldown = 0.2f;
    [Tooltip("Si está activo, el clic solo suena con el cursor bloqueado (jugando), no al hacer clic en menús o pausa.")]
    [SerializeField] private bool requireLockedCursor = true;

    [Header("Debug")]
    [Tooltip("Escribe en la Console cada ataque que llega y si tiene sonido configurado. Desactivalo antes de la entrega final.")]
    [SerializeField] private bool debugLog = true;

    private bool wasReloading;
    private WeaponShotAudio reloadingSound;
    private bool reloadingLastFrame;
    private float lastEmptyClickTime = -999f;

    public override void OnNetworkSpawn()
    {
        wasReloading = false;
        if (inventory == null)
            inventory = GetComponentInParent<NetworkPlayerInventory>();

        if (combat == null)
            combat = GetComponentInParent<NetworkPlayerCombat>();

        if (combat == null)
        {
            Debug.LogError(
                $"[PlayerWeaponAudio] '{name}' no encontró NetworkPlayerCombat. " +
                "Ponelo en el objeto raíz del jugador o arrastralo al campo Combat.",
                this);
            return;
        }

        combat.AttackPresented += OnAttackPresented;
    }

    public override void OnNetworkDespawn()
    {
        if (combat != null)
            combat.AttackPresented -= OnAttackPresented;
        StopReloadAudio();
        wasReloading = false;
    }

    // Misma forma de detectar la recarga que usa NetworkPlayerHudPresenter:
    // ReloadRemaining > 0 significa "recargando". Cuando pasa de falso a
    // verdadero, empezó una recarga y suena la secuencia del arma elegida.
    private void Update()
    {
        // ReloadRemaining viene del servidor y se replica a todos los clientes.
        if (!IsSpawned || !IsClient || combat == null)
            return;

        // El clic de arma vacía solo lo oye (y lo genera) quien aprieta el gatillo.
        if (IsOwner)
            HandleEmptyClick();

        bool reloading = combat.ReloadRemaining > 0f;
        if (reloading == wasReloading)
            return;

        wasReloading = reloading;
        if (!reloading)
            return;

        StopReloadAudio();
        WeaponDefinition selected = inventory != null ? inventory.SelectedWeaponDefinition : null;
        if (selected == null)
            return;

        WeaponShotAudio sound = FindSound(selected.Id);

        if (debugLog)
        {
            Debug.Log(
                $"[PlayerWeaponAudio] Empezó la recarga de '{selected.Id}' | " +
                $"sonido configurado: {sound != null}",
                this);
        }

        if (sound != null)
        {
            reloadingSound = sound;
            sound.PlayReload();
        }
    }

    // Clic de arma vacía: suena UNA vez por cada vez que se aprieta el botón
    // de disparo con el cargador en cero. No modifica ningún script de combate.
    private void HandleEmptyClick()
    {
        // El estado de recarga se compara con el del frame anterior: así, si
        // el arma arranca la recarga sola en este mismo frame, igual suena el
        // clic de gatillo seco y después la recarga.
        GunSystem gun = combat.CurrentGun;
        bool reloadingNow = gun != null && gun.IsReloading;
        bool reloadingBefore = reloadingLastFrame;
        reloadingLastFrame = reloadingNow;

        if (gun == null || !combat.IsCombatActive)
            return;

        if (combat.Ammunition > 0 || reloadingBefore)
            return;

        if (requireLockedCursor && Cursor.lockState != CursorLockMode.Locked)
            return;

        if (!FirePressedThisFrame())
            return;

        if (Time.time - lastEmptyClickTime < emptyClickCooldown)
            return;

        WeaponDefinition selected = inventory != null ? inventory.SelectedWeaponDefinition : null;
        if (selected == null)
            return;

        WeaponShotAudio sound = FindSound(selected.Id);

        if (debugLog)
        {
            Debug.Log(
                $"[PlayerWeaponAudio] Clic sin balas con '{selected.Id}' | " +
                $"sonido configurado: {sound != null}",
                this);
        }

        if (sound == null)
            return;

        lastEmptyClickTime = Time.time;
        sound.PlayEmpty();
    }

    private static bool FirePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private void StopReloadAudio()
    {
        if (reloadingSound != null)
            reloadingSound.StopReload();
        reloadingSound = null;
    }

    private void OnAttackPresented(WeaponId weaponId, WeaponAttackType attackType)
    {
        WeaponShotAudio sound = FindSound(weaponId);

        if (debugLog)
        {
            Debug.Log(
                $"[PlayerWeaponAudio] Ataque con '{weaponId}' ({attackType}) | " +
                $"IsOwner: {IsOwner} | sonido configurado: {sound != null}",
                this);
        }

        if (sound == null)
            return;

        // IsOwner: true solo en la máquina de quien disparó.
        sound.PlayShot(IsOwner);
    }

    private WeaponShotAudio FindSound(WeaponId weaponId)
    {
        if (weaponSounds == null)
            return null;

        foreach (WeaponSound entry in weaponSounds)
        {
            if (entry != null && entry.weapon.Equals(weaponId))
                return entry.sound;
        }

        return null;
    }
}