using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class TutorialWeaponPickup : MonoBehaviour
{
    [SerializeField] private WeaponId weaponId = WeaponId.None;
    [SerializeField, FormerlySerializedAs("pickupRadius"), Min(0.5f)]
    private float maxRayDistance = 2.5f;
    [SerializeField] private LayerMask raycastLayers = ~0;

    private static TutorialWeaponPickup focusedPickup;
    private static int lastProcessedFrame = -1;

    private void OnDisable()
    {
        if (focusedPickup == this)
            focusedPickup = null;
    }

    private void Update()
    {
        if (lastProcessedFrame == Time.frameCount)
            return;

        lastProcessedFrame = Time.frameCount;
        focusedPickup = FindFocusedPickup(out NetworkPlayerInventory inventory);

        if (focusedPickup == null ||
            MatchDebugConsole.IsInputCaptured ||
            NetworkPlayerShop.IsInputCaptured ||
            OpcionesEnPausa.Ocupado ||
            Keyboard.current == null ||
            !Keyboard.current.eKey.wasPressedThisFrame)
            return;

        if (inventory.TryAddPurchasedWeaponOnServer(focusedPickup.weaponId))
        {
            focusedPickup = null;
        }
    }

    private TutorialWeaponPickup FindFocusedPickup(
        out NetworkPlayerInventory inventory)
    {
        inventory = null;
        if (!TutorialManager.IsTutorialLoaded ||
            MatchDebugConsole.IsInputCaptured ||
            NetworkPlayerShop.IsInputCaptured ||
            OpcionesEnPausa.Ocupado)
            return null;

        NetworkObject player =
            NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (player == null ||
            !player.TryGetComponent(out NetworkPlayerHealth health) ||
            health.LifeState != PlayerLifeState.Alive ||
            !player.TryGetComponent(out PlayerNetworkSetup setup) ||
            !setup.LocalGameplayInputEnabled ||
            !player.TryGetComponent(out inventory) ||
            !inventory.IsSpawned ||
            !inventory.IsServer)
            return null;

        Camera playerCamera = setup.PlayerCamera;
        if (playerCamera == null)
            return null;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            maxRayDistance,
            raycastLayers,
            QueryTriggerInteraction.Collide);

        float nearestDistance = float.PositiveInfinity;
        Collider nearestCollider = null;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform == player.transform ||
                hit.collider.transform.IsChildOf(player.transform) ||
                (hit.collider.isTrigger &&
                 hit.collider.GetComponentInParent<TutorialWeaponPickup>() == null) ||
                hit.distance >= nearestDistance)
                continue;

            nearestDistance = hit.distance;
            nearestCollider = hit.collider;
        }

        if (nearestCollider == null)
            return null;

        TutorialWeaponPickup pickup =
            nearestCollider.GetComponentInParent<TutorialWeaponPickup>();
        if (pickup == null ||
            !pickup.isActiveAndEnabled ||
            pickup.weaponId == WeaponId.None ||
            inventory.HasWeaponInLoadout(pickup.weaponId))
            return null;

        return pickup;
    }

    private void OnGUI()
    {
        if (focusedPickup != this)
            return;

        GUI.Box(
            new Rect(Screen.width * 0.5f - 110f, Screen.height * 0.62f, 220f, 32f),
            $"E: agarrar {weaponId}");
    }

    private void OnValidate()
    {
        maxRayDistance = Mathf.Max(0.5f, maxRayDistance);
    }
}