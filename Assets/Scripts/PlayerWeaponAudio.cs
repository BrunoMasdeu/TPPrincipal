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

    [Header("Debug")]
    [Tooltip("Escribe en la Console cada ataque que llega y si tiene sonido configurado. Desactivalo antes de la entrega final.")]
    [SerializeField] private bool debugLog = true;

    private bool wasReloading;

    public override void OnNetworkSpawn()
    {
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
    }

    // Misma forma de detectar la recarga que usa NetworkPlayerHudPresenter:
    // ReloadRemaining > 0 significa "recargando". Cuando pasa de falso a
    // verdadero, empezó una recarga y suena la secuencia del arma elegida.
    private void Update()
    {
        if (!IsSpawned || !IsOwner || combat == null)
            return;

        bool reloading = combat.ReloadRemaining > 0f;
        if (reloading == wasReloading)
            return;

        wasReloading = reloading;
        if (!reloading)
            return;

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
            sound.PlayReload();
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