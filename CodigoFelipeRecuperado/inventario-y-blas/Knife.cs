using UnityEngine;
using UnityEngine.InputSystem;

public class KnifeSystem : MonoBehaviour
{
    public Camera fpsCam;
    public float range = 2f;
    public int damage = 50;
    public float attackCooldown = 0.7f;

    private float nextAttackTime;

    void Start()
    {
        Debug.Log("KnifeSystem iniciado");
    }

    void Update()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("Clic detectado por el cuchillo");

            if (Time.time < nextAttackTime)
                return;

            nextAttackTime = Time.time + attackCooldown;
            Attack();
        }
    }

    void Attack()
    {
        if (fpsCam == null)
        {
            Debug.LogError("KnifeSystem: falta asignar Fps Cam");
            return;
        }

        Ray ray = new Ray(
            fpsCam.transform.position,
            fpsCam.transform.forward
        );

        Debug.DrawRay(ray.origin, ray.direction * range, Color.red, 2f);

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            Debug.Log("Cuchillo detectó: " + hit.collider.name);

            ShootingAi enemy =
                hit.collider.GetComponentInParent<ShootingAi>();

            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                Debug.Log("Daño aplicado: " + damage);
            }
            else
            {
                Debug.Log("El objeto detectado no tiene ShootingAi");
            }
        }
        else
        {
            Debug.Log("El cuchillo no detectó ningún objeto");
        }
    }
}
