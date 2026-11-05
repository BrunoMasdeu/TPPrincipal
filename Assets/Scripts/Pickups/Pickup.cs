using System.Collections;
using UnityEngine;

public class Pickup : MonoBehaviour
{
    public enum TipoPickup
    {
        Vida,
        Municion
    }

    [Header("Tipo")]
    [SerializeField] private TipoPickup tipo;

    [Header("Jugador")]
    [SerializeField] private NetworkPlayerHealth vida;

    [Header("Vida")]
    [SerializeField] private int cantidadVida = 25;

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

        NetworkPlayerHealth vidaJugador =
            other.GetComponentInParent<NetworkPlayerHealth>();

        if (vidaJugador == null)
            vidaJugador = vida;

        bool aplicado = false;

        switch (tipo)
        {
            case TipoPickup.Vida:
                if (vidaJugador != null)
                    aplicado = vidaJugador.Heal(cantidadVida);
                break;

            case TipoPickup.Municion:
                Inventory inventarioJugador =
                    other.GetComponentInParent<Inventory>();

                if (inventarioJugador != null &&
                    inventarioJugador.CurrentWeapon != null)
                {
                    GunSystem armaJugador =
                        inventarioJugador.CurrentWeapon
                        .GetComponent<GunSystem>();

                    if (armaJugador != null)
                    {
                        aplicado = armaJugador.AddAmmo(cantidadMunicion);
                    }
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
        tiempoReaparicion = Mathf.Max(0.1f, tiempoReaparicion);
    }
}