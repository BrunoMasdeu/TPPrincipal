using UnityEngine;
using UnityEngine.InputSystem;

public class Grenade : MonoBehaviour
{
    [Header("Lanzamiento")]
    public GameObject grenadePrefab;
    public Transform throwPoint;
    public float throwForce = 12f;
    public float upwardForce = 4f;

    [Header("Municion")]
    public int grenades = 3;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.gKey.wasPressedThisFrame && grenades > 0)
        {
            ThrowGrenade();
        }
    }

 private void ThrowGrenade()
{
    if (grenadePrefab == null || throwPoint == null)
    {
        Debug.LogError("Asigna Grenade Prefab y Throw Point en el Inspector.");
        return;
    }

    GameObject grenade = Instantiate(
        grenadePrefab,
        throwPoint.position,
        throwPoint.rotation
    );

    Rigidbody rb = grenade.GetComponent<Rigidbody>();

    if (rb == null)
    {
        Debug.LogError("La granada necesita un Rigidbody.");
        Destroy(grenade);
        return;
    }

    rb.isKinematic = false;
    rb.useGravity = true;
    rb.linearVelocity = Vector3.zero;
    rb.angularVelocity = Vector3.zero;

    Vector3 direction = Camera.main.transform.forward;

    Vector3 force = direction * throwForce;

    rb.AddForce(force, ForceMode.Impulse);

    Collider grenadeCollider = grenade.GetComponent<Collider>();

    if (grenadeCollider != null)
    {
        Collider[] playerColliders = GetComponentsInChildren<Collider>();

        foreach (Collider playerCollider in playerColliders)
        {
            Physics.IgnoreCollision(grenadeCollider, playerCollider);
        }
    }

    GrenadeExplosion explosion = grenade.GetComponent<GrenadeExplosion>();

    if (explosion == null)
    {
        Debug.LogError("Falta GrenadeExplosion en el prefab.");
        Destroy(grenade);
        return;
    }

    explosion.StartExplosion();

    grenades--;
}
}
