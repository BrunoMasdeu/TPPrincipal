using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class GunSystem : MonoBehaviour
{
    [Header("Estadísticas")]
    public int damage;
    public float timeBetweenShooting;
    public float spread;
    public float range;
    public float reloadTime;
    public float timeBetweenShots;
    public int magazineSize;
    public int bulletsPerTap;
    public bool allowButtonHold;
    public bool IsPrimary;

    [Header("Munición")]
    public int ExtraBullets;

    private int bulletsLeft;
    private int bulletsShot;

    private int extraBulletsLeft;

    private bool shooting;
    private bool readyToShoot;
    private bool reloading;

    public Camera fpsCam;
    public Transform attackPoint;
    public RaycastHit rayHit;
    public LayerMask whatIsEnemy;
    public LayerMask whatIsGround;

    public GameObject muzzleFlash;
    public GameObject bulletHoleGraphic;

    public float camShakeMagnitude;
    public float camShakeDuration;

    public TextMeshProUGUI text;

    private void Awake()
    {
        bulletsLeft = magazineSize;
        extraBulletsLeft = ExtraBullets;
        readyToShoot = true;
    }

    private void Update()
    {
        MyInput();

        if (text != null)
        {
            text.SetText(
                bulletsLeft + " / " + extraBulletsLeft
            );
        }
    }

    private void MyInput()
    {
        if (Mouse.current != null)
        {
            if (allowButtonHold)
            {
                shooting = Mouse.current.leftButton.isPressed;
            }
            else
            {
                shooting = Mouse.current.leftButton.wasPressedThisFrame;
            }
        }

        if (Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame &&
            bulletsLeft < magazineSize &&
            extraBulletsLeft > 0 &&
            !reloading)
        {
            Reload();
        }

        if (readyToShoot &&
            shooting &&
            !reloading &&
            bulletsLeft > 0)
        {
            bulletsShot = bulletsPerTap;
            Shoot();
        }
    }

    private void Shoot()
    {
        Debug.Log("DISPARÓ");

        readyToShoot = false;

        float x = Random.Range(-spread, spread);
        float y = Random.Range(-spread, spread);

        Vector3 direction =
            fpsCam.transform.forward +
            new Vector3(x, y, 0);

        LayerMask hitMask = whatIsEnemy | whatIsGround;

        if (Physics.Raycast(
            fpsCam.transform.position,
            direction.normalized,
            out rayHit,
            range,
            hitMask,
            QueryTriggerInteraction.Ignore))
        {
            Debug.Log("Impactó a: " + rayHit.collider.name);

            if (rayHit.collider.CompareTag("Enemy"))
            {
                movimientoBot enemy =
                    rayHit.collider.GetComponentInParent<movimientoBot>();

                if (enemy != null)
                {
                    enemy.TakeDamage(damage);
                }
            }
            else if (rayHit.collider.CompareTag("Target"))
            {
                Diana target =
                    rayHit.collider.GetComponentInParent<Diana>();

                if (target != null)
                {
                    target.Derribar();
                }
            }

            if (bulletHoleGraphic != null)
            {
                Instantiate(
                    bulletHoleGraphic,
                    rayHit.point,
                    Quaternion.LookRotation(rayHit.normal)
                );
            }
        }

        if (muzzleFlash != null && attackPoint != null)
        {
            Instantiate(
                muzzleFlash,
                attackPoint.position,
                attackPoint.rotation
            );
        }

        bulletsLeft--;
        bulletsShot--;

        if (bulletsLeft == 0)
        {
            Debug.LogWarning(
                "Cargador vacío. Presioná R para recargar."
            );
        }

        Invoke(nameof(ResetShot), timeBetweenShooting);

        if (bulletsShot > 0 && bulletsLeft > 0)
        {
            Invoke(nameof(Shoot), timeBetweenShots);
        }
    }

    private void ResetShot()
    {
        readyToShoot = true;
    }

    private void Reload()
    {
        if (extraBulletsLeft <= 0)
        {
            Debug.LogWarning("No quedan balas de reserva.");
            return;
        }

        reloading = true;

        Debug.Log("Recargando...");

        Invoke(nameof(ReloadFinished), reloadTime);
    }

    private void ReloadFinished()
    {
        int bulletsNeeded = magazineSize - bulletsLeft;
        int bulletsToLoad = Mathf.Min(
            bulletsNeeded,
            extraBulletsLeft
        );

        bulletsLeft += bulletsToLoad;
        extraBulletsLeft -= bulletsToLoad;

        reloading = false;

        Debug.Log(
            "Recarga completa. Cargador: " +
            bulletsLeft + " | Reserva: " + extraBulletsLeft
        );
    }
}



