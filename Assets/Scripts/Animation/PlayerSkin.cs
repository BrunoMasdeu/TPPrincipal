using System;
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
    }

    // La cámara del jugador no dibuja esta capa (ver Culling Mask de MainCamera).
    private const string CapaCabezaPropia = "Head";

    [SerializeField] private Animator animator;

    [Tooltip("Contenedor de todas las armas (GrappleGunSocket).")]
    [SerializeField] private Transform armas;

    [SerializeField] private Transform camara;

    [Tooltip("El primero es el que se usa cuando el jugador no tiene equipo " +
             "(tutorial, zombies, carrera).")]
    [SerializeField] private Skin[] skins;

    public Skin SkinActual { get; private set; }

    public override void OnNetworkSpawn()
    {
        AplicarSkin(EquipoDelDueno());
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
        Transform mano = animator != null && animator.isHuman
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
        if (camara != null)
        {
            camara.localPosition = elegido.posicionCamara;

            // El agachado de Felipe recuerda dónde está la cámara parado.
            Crouch agachado = GetComponent<Crouch>();
            if (agachado != null)
                agachado.GuardarPosicionCamara();
        }

        // 5. Se apagan los otros modelos.
        foreach (Skin otro in skins)
        {
            if (otro != elegido && otro.modelo != null)
                otro.modelo.SetActive(false);
        }

        // 6. Solo en mi PC: no veo mi propia cabeza.
        if (IsSpawned && IsOwner)
            OcultarCabezaPropia(elegido);

        SkinActual = elegido;
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
