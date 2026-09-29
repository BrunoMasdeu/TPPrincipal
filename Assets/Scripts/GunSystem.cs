
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class GunSystem : MonoBehaviour
{
    public int damage;
    public float timeBetweenShooting;
    public float spread;
    public float range;
    public float reloadTime;
    public float timeBetweenShots;
    public int magazineSize;
    public int bulletsPerTap;
    public bool allowButtonHold;

    private int bulletsLeft;
    private int bulletsShot;

    private bool shooting;
    private bool readyToShoot;
    private bool reloading;

    public Camera fpsCam;
    public Transform attackPoint;
    public RaycastHit rayHit;
    public LayerMask whatIsEnemy;

    public GameObject muzzleFlash;
    public GameObject bulletHoleGraphic;
    //public CamShake camShake;
    public float camShakeMagnitude;
    public float camShakeDuration;
    public TextMeshProUGUI text;

    private void Awake()
    {
        bulletsLeft = magazineSize;
        readyToShoot = true;
    }

    private void Update()
    {
        MyInput();

        if (text != null)
        {
            text.SetText(bulletsLeft + " / " + magazineSize);
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

        if (Physics.Raycast(
            fpsCam.transform.position,
            direction,
            out rayHit,
            range,
            whatIsEnemy))
        {
            Debug.Log("Impactó a: " + rayHit.collider.name);

            if (rayHit.collider.CompareTag("Enemy"))
            {
                ShootingAi enemy =
                   rayHit.collider.GetComponent<ShootingAi>();

                if (enemy != null)
                {
                    enemy.TakeDamage(damage);
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

        if (muzzleFlash != null &&
            attackPoint != null)
        {
            Instantiate(
                muzzleFlash,
                attackPoint.position,
                attackPoint.rotation
            );
        }

       // if (camShake != null)
       // {
       //     camShake.Shake(
       //         camShakeDuration,
       //         camShakeMagnitude
       //     );
       // }

        bulletsLeft--;
        bulletsShot--;

        if (bulletsLeft == 0)
        {
            Debug.LogWarning("Cargador vacío. Presioná R para recargar.");
        }

        Invoke(
            nameof(ResetShot),
            timeBetweenShooting
        );

        if (bulletsShot > 0 &&
            bulletsLeft > 0)
        {
            Invoke(
                nameof(Shoot),
                timeBetweenShots
            );
        }
    }

    private void ResetShot()
    {
        readyToShoot = true;
    }

    private void Reload()
    {
        reloading = true;

        Debug.Log(
            "Recargando... Munición actual: " +
            bulletsLeft + " / " + magazineSize
        );

        Invoke(
            nameof(ReloadFinished),
            reloadTime
        );
    }

    private void ReloadFinished()
    {
        bulletsLeft = magazineSize;
        reloading = false;

        Debug.Log(
            "Recarga completa. Munición: " +
            bulletsLeft + " / " + magazineSize
        );
    }
}




