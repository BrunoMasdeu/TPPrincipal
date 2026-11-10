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

    [Header("Zoom")]
    public bool canZoom = false;
    public float normalFOV = 60f;
    public float zoomFOV = 15f;
    public float zoomSpeed = 10f;
    public GameObject scopeOverlay;
    public GameObject normalScopeOverlay;

    private bool isZooming;

    public GameObject muzzleFlash;
    public GameObject bulletHoleGraphic;

    public float camShakeMagnitude;
    public float camShakeDuration;

    public TextMeshProUGUI text;

    // [MULTIJUGADOR] Avisa a la red cuando se dispara y cuando el disparo toca
    // a otro jugador. Está en la raíz del jugador (este script va en el arma).
    private NetworkPlayerCombat network;

    private void Awake()
    {
        bulletsLeft = magazineSize;
        extraBulletsLeft = ExtraBullets;
        readyToShoot = true;

        // [MULTIJUGADOR]
        network = GetComponentInParent<NetworkPlayerCombat>();

        if (fpsCam != null)
        {
            normalFOV = fpsCam.fieldOfView;
        }
    }

    private void Update()
    {
        // [MULTIJUGADOR] Con la tienda o la consola de depuración abiertas,
        // el mouse es de esas ventanas: no se dispara ni se recarga.
        if (MatchDebugConsole.IsInputCaptured || NetworkPlayerShop.IsInputCaptured)
        {
            if (canZoom && isZooming)
            {
                isZooming = false;
                ShowSniperScope(false);
                if (fpsCam != null)
                    fpsCam.fieldOfView = normalFOV;
            }
            return;
        }

        MyInput();
        UpdateZoom();

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

        // [TECLAS] La tecla de recargar sale de Controles.
        if (Keyboard.current != null &&
            Keyboard.current[Controles.Tecla(Accion.Recargar)].wasPressedThisFrame &&
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

    private void UpdateZoom()
    {
        if (fpsCam == null)
        {
            return;
        }

        if (canZoom && Mouse.current != null)
        {
            isZooming = Mouse.current.rightButton.isPressed;
        }
        else
        {
            isZooming = false;
        }

        ShowSniperScope(isZooming);

        float targetFOV = isZooming ? zoomFOV : normalFOV;

        fpsCam.fieldOfView = Mathf.Lerp(
            fpsCam.fieldOfView,
            targetFOV,
            Time.deltaTime * zoomSpeed
        );
    }

    private void ShowSniperScope(bool isZooming)
    {
        if (!canZoom)
            return;

        if (normalScopeOverlay != null) normalScopeOverlay.SetActive(!isZooming);
        if (scopeOverlay != null) scopeOverlay.SetActive(isZooming);
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

            // [ZOMBIES] Los zombies tienen su propia vida (Zombie.cs). Se revisa
            // primero para que tenga prioridad aunque el prefab esté con el
            // tag "Enemy".
            Zombie zombie = rayHit.collider.GetComponentInParent<Zombie>();

            if (zombie != null)
            {
                zombie.zombieHitDamage(damage);
            }
            else if (rayHit.collider.CompareTag("Enemy"))
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
            // [MULTIJUGADOR] Si tocó a otro jugador, la vida la resta el
            // servidor (ver NetworkPlayerCombat.ReportHit).
            else if (network != null)
            {
                network.ReportHit(rayHit.collider, damage);
            }

            bool characterHit = zombie != null ||
                rayHit.collider.GetComponentInParent<NetworkPlayerHealth>() != null ||
                rayHit.collider.GetComponentInParent<movimientoBot>() != null ||
                rayHit.collider.GetComponentInParent<ShootingAi>() != null ||
                rayHit.collider.CompareTag("Target");
            if (!characterHit)
            {
                SurfaceImpactFeedback.Play(rayHit);
                if (bulletHoleGraphic != null)
                {
                    GameObject decal = Instantiate(bulletHoleGraphic,
                        rayHit.point + rayHit.normal * .005f,
                        Quaternion.LookRotation(rayHit.normal));
                    Destroy(decal, 10f);
                }
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

        // [MULTIJUGADOR] Todos los jugadores escuchan el disparo y ven la
        // animación.
        if (network != null)
        {
            network.ReportShot();
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

    public bool AddAmmo(int cantidad)
    {
        if (cantidad <= 0)
            return false;

        extraBulletsLeft += cantidad;

        Debug.Log(
            "Munición recogida: +" + cantidad +
            ". Cargador: " + bulletsLeft +
            " | Reserva: " + extraBulletsLeft
        );

        return true;
    }

    // ------------------------------------------------------------------
    // [COMO HOY] Lo que sigue no estaba en la versión original.
    // ------------------------------------------------------------------

    // Al cambiar de arma este objeto se apaga: la recarga en curso se
    // cancela, como en el juego actual.
    private void OnDisable()
    {
        CancelInvoke();
        reloading = false;
        readyToShoot = true;

        if (canZoom)
        {
            isZooming = false;
            ShowSniperScope(false);
            if (fpsCam != null)
                fpsCam.fieldOfView = normalFOV;
        }
    }

    // Al reaparecer, el arma vuelve con el cargador y la reserva llenos.
    public void Refill()
    {
        bulletsLeft = magazineSize;
        extraBulletsLeft = ExtraBullets;
    }

    // Para que el HUD muestre las balas.
    public int BulletsLeft => bulletsLeft;
    public int ExtraBulletsLeft => extraBulletsLeft;
    public bool IsReloading => reloading;
}
