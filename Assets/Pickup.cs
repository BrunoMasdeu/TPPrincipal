
using System.Collections;
using UnityEngine;

public class Pickup : MonoBehaviour
{
    public enum TipoPickup
    {
        Vida,
        Velocidad,
        Municion
    }

    [Header("Tipo")]
    [SerializeField] private TipoPickup tipo;

    [Header("Jugador")]
    [SerializeField] private Move movimiento;
    [SerializeField] private NetworkPlayerHealth vida;
    [SerializeField] private GunSystem arma;

    [Header("Vida")]
    [SerializeField] private int cantidadVida = 25;

    [Header("Velocidad")]
    [Min(1.01f)]
    [SerializeField] private float multiplicadorVelocidad = 1.5f;

    [Min(0.1f)]
    [SerializeField] private float duracionVelocidad = 5f;

    [Header("Municion")]
    [SerializeField] private int cantidadMunicion = 30;

    [Header("Reaparicion")]
    [Min(0.1f)]
    [SerializeField] private float tiempoReaparicion = 30f;

    private bool recogido;
    private Collider[] collidersObjeto;
    private Renderer[] renderersObjeto;

    private void Awake()
    {
        collidersObjeto = GetComponentsInChildren<Collider>(true);
        renderersObjeto = GetComponentsInChildren<Renderer>(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (recogido)
            return;

        Move movimientoJugador =
            other.GetComponentInParent<Move>();

        if (movimientoJugador == null)
        {
            movimientoJugador =
                other.transform.root.GetComponentInChildren<Move>();
        }

        NetworkPlayerHealth vidaJugador =
            other.GetComponentInParent<NetworkPlayerHealth>();

        GunSystem armaJugador =
            other.GetComponentInParent<GunSystem>();

        if (movimientoJugador == null)
            movimientoJugador = movimiento;

        if (vidaJugador == null)
            vidaJugador = vida;

        if (armaJugador == null)
            armaJugador = arma;

        bool aplicado = false;

        switch (tipo)
        {
            case TipoPickup.Vida:
                if (vidaJugador != null)
                    aplicado = vidaJugador.Heal(cantidadVida);
                break;

            case TipoPickup.Velocidad:
                if (movimientoJugador != null)
                {
                    movimientoJugador.ApplySpeedBoost(
                        multiplicadorVelocidad,
                        duracionVelocidad
                    );

                    aplicado = true;
                }
                break;

            case TipoPickup.Municion:
                if (armaJugador != null)
                {
                    armaJugador.AddAmmo(cantidadMunicion);
                    aplicado = true;
                }
                break;
        }

        if (aplicado)
            StartCoroutine(OcultarYReaparecer());
    }

    private IEnumerator OcultarYReaparecer()
    {
        recogido = true;

        foreach (Collider col in collidersObjeto)
            col.enabled = false;

        foreach (Renderer rend in renderersObjeto)
            rend.enabled = false;

        yield return new WaitForSeconds(tiempoReaparicion);

        foreach (Renderer rend in renderersObjeto)
            rend.enabled = true;

        foreach (Collider col in collidersObjeto)
            col.enabled = true;

        recogido = false;
    }

    private void OnValidate()
    {
        multiplicadorVelocidad =
            Mathf.Max(1.01f, multiplicadorVelocidad);

        duracionVelocidad =
            Mathf.Max(0.1f, duracionVelocidad);

        tiempoReaparicion =
            Mathf.Max(0.1f, tiempoReaparicion);
    }
}





