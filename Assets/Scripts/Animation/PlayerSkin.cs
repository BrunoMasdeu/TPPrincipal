using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Un solo prefab de jugador con un modelo (skin) por equipo.
///
/// Al aparecer el jugador, prende el modelo de su equipo, le pone al
/// Animator el Avatar de ese modelo, lleva las armas a su mano derecha y
/// acomoda la cámara en primera persona.
///
/// Las animaciones son Humanoid, así que el mismo Animator Controller sirve
/// para todos los modelos: lo único que cambia es el Avatar.
///
/// El equipo sale de la lista de jugadores del lobby, que ya viaja por red:
/// cada PC lo calcula sola y no hace falta sincronizar nada más.
///
/// Este componente tiene que estar ANTES que PlayerAnimationController en el
/// prefab: cambiar el Avatar reinicia el Animator, y eso tiene que pasar
/// antes de que PlayerAnimationController prepare sus capas.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSkin : NetworkBehaviour
{
    [Serializable]
    public class Skin
    {
        [Tooltip("Equipo que usa este modelo.")]
        public TeamId equipo;
        public GameObject modelo;
        public Avatar avatar;

        [Header("Armas en la mano derecha")]
        [Tooltip("Cada esqueleto tiene la mano orientada distinto, por eso " +
                 "el contenedor de armas tiene una posición propia por modelo.")]
        public Vector3 posicionArmas;
        public Quaternion rotacionArmas = Quaternion.identity;
        public Vector3 escalaArmas = Vector3.one;

        [Header("Cámara en primera persona")]
        public Vector3 posicionCamara;

        [Tooltip("Mallas de la cabeza. En tu PC pasan a la capa 'Head', " +
                 "que tu cámara no dibuja, para no verte la cabeza por " +
                 "dentro. En las PCs de los demás se ven normal.")]
        public Renderer[] cabeza;

        // [ANIMACIONES] Dos equipos pueden usar el mismo modelo con otro color.
        [Header("Color de la ropa")]
        [Tooltip("Materiales originales de la ropa (uniforme, casco, chaleco...). " +
                 "Vacío = el modelo queda con sus colores originales.")]
        public Material[] materialesRopa;

        [Tooltip("Copia teñible de cada uno, en el mismo orden. El shader " +
                 "original del soldado no acepta color, por eso hace falta.")]
        public Material[] materialesTenidos;

        [Tooltip("Color con el que se tiñe la ropa.")]
        public Color colorRopa = Color.white;
    }

    // La cámara del jugador no dibuja esta capa (ver Culling Mask de MainCamera).
    private const string CapaCabezaPropia = "Head";
    private static readonly int EstadoAgachado = Animator.StringToHash("Crouch Idle");
    private const float VelocidadRetornoCamara = 20f;
    private const float AlturaCamaraMuerte = 4f;
    private const float RadioCamaraMuerte = 0.15f;

    [SerializeField] private Animator animator;

    [Tooltip("Contenedor de todas las armas (GrappleGunSocket).")]
    [SerializeField] private Transform armas;

    [SerializeField] private Transform camara;

    [Tooltip("El primero es el que se usa cuando el jugador no tiene equipo " +
             "(tutorial, zombies, carrera).")]
    [SerializeField] private Skin[] skins;

    private NetworkPlayerCrouch agachado;
    private NetworkPlayerHealth vida;
    private Transform cabezaAnimada;
    private Transform caderaAnimada;
    private Vector3 ojosEnCabeza;
    private bool siguiendoCabeza;
    private Camera camaraJugador;
    private bool mostrandoMuerte;
    private Quaternion rotacionPrimeraPersona;
    private Vector3 posicionPrimeraPersona;
    private int capasPrimeraPersona;
    private int capasObstaculosCamara;

    public Skin SkinActual { get; private set; }

    public override void OnNetworkSpawn()
    {
        agachado = GetComponent<NetworkPlayerCrouch>();
        vida = GetComponent<NetworkPlayerHealth>();
        camaraJugador = camara != null ? camara.GetComponent<Camera>() : null;
        capasObstaculosCamara = Physics.DefaultRaycastLayers &
            ~LayerMask.GetMask("Player", CapaCabezaPropia, "Ropa Jugador", "Iconos");
        AplicarSkin(EquipoDelDueno());
    }

    public override void OnNetworkDespawn()
    {
        RestaurarCamaraPrimeraPersona();
        if (siguiendoCabeza && camara != null)
            camara.localPosition = SkinActual.posicionCamara;
        siguiendoCabeza = false;
    }

    private void LateUpdate()
    {
        if (!IsSpawned || !IsOwner || camara == null)
            return;

        if (vida != null && vida.LifeState != PlayerLifeState.Alive)
        {
            MostrarCamaraMuerte();
            return;
        }

        RestaurarCamaraPrimeraPersona();
        if (cabezaAnimada == null)
            return;

        // La pose del Animator ya se evaluó, incluso si saltamos agachados.
        // Solo seguimos la posición de los ojos. El mouse sigue controlando la mirada.
        if (agachado.IsCrouched ||
            animator.GetCurrentAnimatorStateInfo(0).shortNameHash == EstadoAgachado ||
            animator.GetNextAnimatorStateInfo(0).shortNameHash == EstadoAgachado)
        {
            camara.position = cabezaAnimada.TransformPoint(ojosEnCabeza);
            siguiendoCabeza = true;
        }
        else if (siguiendoCabeza)
        {
            Vector3 posicionDePie = SkinActual.posicionCamara;
            camara.localPosition = Vector3.Lerp(camara.localPosition, posicionDePie,
                1f - Mathf.Exp(-VelocidadRetornoCamara * Time.deltaTime));
            if ((camara.localPosition - posicionDePie).sqrMagnitude < 0.000001f)
            {
                camara.localPosition = posicionDePie;
                siguiendoCabeza = false;
            }
        }
    }

    private void MostrarCamaraMuerte()
    {
        if (!mostrandoMuerte)
        {
            mostrandoMuerte = true;
            rotacionPrimeraPersona = camara.localRotation;
            posicionPrimeraPersona = camara.localPosition;
            siguiendoCabeza = false;
            if (camaraJugador != null)
            {
                capasPrimeraPersona = camaraJugador.cullingMask;
                camaraJugador.cullingMask |= LayerMask.GetMask(CapaCabezaPropia);
            }
        }

        Vector3 objetivo = caderaAnimada != null ? caderaAnimada.position : transform.position;
        float altura = AlturaCamaraMuerte;
        if (Physics.SphereCast(objetivo, RadioCamaraMuerte, Vector3.up, out RaycastHit techo,
                altura, capasObstaculosCamara, QueryTriggerInteraction.Ignore))
            altura = Mathf.Max(RadioCamaraMuerte, techo.distance);

        camara.SetPositionAndRotation(objetivo + Vector3.up * altura,
            Quaternion.LookRotation(Vector3.down, transform.forward));
    }

    private void RestaurarCamaraPrimeraPersona()
    {
        if (!mostrandoMuerte)
            return;

        mostrandoMuerte = false;
        if (camara != null)
            camara.SetLocalPositionAndRotation(
                SkinActual?.posicionCamara ?? posicionPrimeraPersona, rotacionPrimeraPersona);
        if (camaraJugador != null)
            camaraJugador.cullingMask = capasPrimeraPersona;
    }

    private TeamId EquipoDelDueno()
    {
        NetworkLobbySession lobby = NetworkLobbySession.Instance;

        if (lobby != null &&
            lobby.TryGetPlayerData(OwnerClientId, out LobbyPlayerData datos))
        {
            return datos.TeamId;
        }

        return TeamId.None;
    }

    public void AplicarSkin(TeamId equipo)
    {
        Skin elegido = BuscarSkin(skins, equipo);

        if (elegido == null || elegido.modelo == null || elegido == SkinActual)
            return;

        // 1. Se prende el modelo nuevo. El anterior se apaga al final, cuando
        //    las armas ya no cuelgan de su mano.
        elegido.modelo.SetActive(true);

        // 2. Avatar del modelo. Rebind hace que el Animator encuentre los
        //    huesos del esqueleto nuevo.
        if (animator != null && elegido.avatar != null &&
            animator.avatar != elegido.avatar)
        {
            animator.avatar = elegido.avatar;
            animator.Rebind();
        }

        // 3. Las armas pasan a la mano derecha del modelo. Se le pide la mano
        //    al Animator porque cada esqueleto le pone otro nombre al hueso.
        bool tieneAvatarHumano = animator != null && animator.isHuman;
        Transform mano = tieneAvatarHumano
            ? animator.GetBoneTransform(HumanBodyBones.RightHand)
            : null;

        if (armas != null && mano != null)
        {
            armas.SetParent(mano, false);
            armas.localPosition = elegido.posicionArmas;
            armas.localRotation = elegido.rotacionArmas;
            armas.localScale = elegido.escalaArmas;
        }

        // 4. Cámara a la altura de los ojos de este modelo.
        caderaAnimada = tieneAvatarHumano ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        if (camara != null)
        {
            camara.localPosition = elegido.posicionCamara;
            // Los equipos que comparten avatar también comparten este anclaje.
            Transform cabeza = agachado != null && tieneAvatarHumano
                ? animator.GetBoneTransform(HumanBodyBones.Head)
                : null;
            if (cabeza != cabezaAnimada)
            {
                cabezaAnimada = cabeza;
                if (cabezaAnimada != null)
                    ojosEnCabeza = cabezaAnimada.InverseTransformPoint(camara.position);
            }
            siguiendoCabeza = false;
        }

        // 5. Se apagan los otros modelos (salvo que sea el mismo con otro color).
        foreach (Skin otro in skins)
        {
            if (otro != elegido && otro.modelo != null && otro.modelo != elegido.modelo)
                otro.modelo.SetActive(false);
        }

        TenirRopa(elegido); // [ANIMACIONES]

        // 6. Solo en mi PC: no veo mi propia cabeza.
        if (IsSpawned && IsOwner)
            OcultarCabezaPropia(elegido);

        SkinActual = elegido;
    }

    // [ANIMACIONES] Materiales con los que vino cada malla, para poder volver.
    private readonly Dictionary<Renderer, Material[]> materialesOriginales = new();

    /// <summary>
    /// [ANIMACIONES] Cambia la ropa del modelo por su copia teñible y le pone
    /// el color del skin. Lo demás vuelve a su material original, así dos
    /// skins pueden compartir el mismo modelo.
    /// </summary>
    private void TenirRopa(Skin skin)
    {
        var color = new MaterialPropertyBlock();
        color.SetColor("_BaseColor", skin.colorRopa);

        foreach (Renderer malla in skin.modelo.GetComponentsInChildren<Renderer>(true))
        {
            if (!materialesOriginales.TryGetValue(malla, out Material[] originales))
                materialesOriginales[malla] = originales = malla.sharedMaterials;

            var puestos = (Material[])originales.Clone();
            for (int i = 0; i < puestos.Length; i++)
            {
                Material tenido = CopiaTenida(skin, originales[i]);
                if (tenido != null)
                    puestos[i] = tenido;
                malla.SetPropertyBlock(tenido != null ? color : null, i);
            }

            malla.sharedMaterials = puestos;
        }
    }

    /// <summary>La copia teñible de un material de ropa, o null si no es ropa.</summary>
    private static Material CopiaTenida(Skin skin, Material original)
    {
        if (original == null || skin.materialesRopa == null || skin.materialesTenidos == null)
            return null;

        for (int i = 0; i < skin.materialesRopa.Length && i < skin.materialesTenidos.Length; i++)
        {
            Material ropa = skin.materialesRopa[i];
            // Por nombre también: otro script pudo haber instanciado el material.
            if (ropa != null && (ropa == original || original.name.StartsWith(ropa.name)))
                return skin.materialesTenidos[i];
        }

        return null;
    }

    /// <summary>
    /// Pasa la cabeza del modelo a la capa que la cámara no dibuja. Se hace
    /// solo en la PC del dueño: en el prefab la cabeza está en Default, así
    /// que los demás jugadores la siguen viendo.
    /// </summary>
    private static void OcultarCabezaPropia(Skin skin)
    {
        if (skin.cabeza == null || skin.cabeza.Length == 0)
            return;

        int capa = LayerMask.NameToLayer(CapaCabezaPropia);
        if (capa < 0)
        {
            Debug.LogWarning(
                $"[PlayerSkin] No existe la capa '{CapaCabezaPropia}': " +
                "la cabeza propia va a verse en primera persona.");
            return;
        }

        foreach (Renderer parte in skin.cabeza)
        {
            if (parte != null)
                parte.gameObject.layer = capa;
        }
    }

    /// <summary>
    /// El skin del equipo, o el primero de la lista si ninguno coincide.
    /// </summary>
    public static Skin BuscarSkin(Skin[] lista, TeamId equipo)
    {
        if (lista == null || lista.Length == 0)
            return null;

        foreach (Skin skin in lista)
        {
            if (skin != null && skin.equipo == equipo)
                return skin;
        }

        return lista[0];
    }
}
