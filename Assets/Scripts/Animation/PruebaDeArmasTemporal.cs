#if UNITY_EDITOR
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// [ANIMACIONES] [TEMPORAL] Teclas para probar armas y skins en los mapas de
/// prueba de TDM y CTF (tdm_scene_prueba y ctf_scene_prueba). Las usa solo el
/// host. Solo funciona dentro del editor de Unity. Para sacarlo: borrar este
/// archivo.
///
///   F1 cuchillo   F2 pistola   F3 revólver   F4 AK   F5 M4
///   F6 gancho     F7 francotirador           F9 cambia de skin (original, rojo, azul)
///
/// El arma nueva pasa a la mano. Si ya la tenías, se elige con 1-4 como siempre.
/// </summary>
public sealed class PruebaDeArmasTemporal : MonoBehaviour
{
    private static readonly string[] MapasDePrueba = { "tdm_scene_prueba", "ctf_scene_prueba" };

    [RuntimeInitializeOnLoadMethod]
    private static void Crear()
    {
        var objeto = new GameObject("[TEMPORAL] Prueba de armas");
        DontDestroyOnLoad(objeto);
        objeto.AddComponent<PruebaDeArmasTemporal>();
    }

    private void Update()
    {
        Keyboard teclado = Keyboard.current;
        NetworkManager red = NetworkManager.Singleton;

        // Solo en los mapas de prueba y solo el host (el servidor), que es
        // quien puede darse armas.
        if (teclado == null ||
            System.Array.IndexOf(MapasDePrueba, SceneManager.GetActiveScene().name) < 0 ||
            red == null || !red.IsServer || red.LocalClient == null ||
            red.LocalClient.PlayerObject == null)
            return;

        GameObject jugador = red.LocalClient.PlayerObject.gameObject;

        if (teclado.f1Key.wasPressedThisFrame) DarArma(jugador, WeaponId.Knife);
        if (teclado.f2Key.wasPressedThisFrame) DarArma(jugador, WeaponId.Pistol);
        if (teclado.f3Key.wasPressedThisFrame) DarArma(jugador, WeaponId.Revolver);
        if (teclado.f4Key.wasPressedThisFrame) DarArma(jugador, WeaponId.AK);
        if (teclado.f5Key.wasPressedThisFrame) DarArma(jugador, WeaponId.M4);
        if (teclado.f6Key.wasPressedThisFrame) DarArma(jugador, WeaponId.Grapple);
        if (teclado.f7Key.wasPressedThisFrame) DarArma(jugador, WeaponId.Sniper);
        if (teclado.f9Key.wasPressedThisFrame) CambiarSkin(jugador);
    }

    // La misma función que usa la tienda al comprar.
    private static void DarArma(GameObject jugador, WeaponId arma)
    {
        var inventario = jugador.GetComponent<NetworkPlayerInventory>();
        bool nueva = inventario != null && inventario.TryAddPurchasedWeaponOnServer(arma);

        Debug.Log(nueva
            ? $"[PRUEBA] {arma} en la mano."
            : $"[PRUEBA] {arma}: ya la tenés (elegila con 1-4) o no está configurada.");
    }

    private static void CambiarSkin(GameObject jugador)
    {
        var skin = jugador.GetComponent<PlayerSkin>();
        if (skin == null)
            return;

        // Original -> rojo -> azul -> original...
        TeamId actual = skin.SkinActual != null ? skin.SkinActual.equipo : TeamId.None;
        TeamId siguiente = actual == TeamId.None ? TeamId.Red
            : actual == TeamId.Red ? TeamId.Blue
            : TeamId.None;

        skin.AplicarSkin(siguiente);
        Debug.Log($"[PRUEBA] Skin: {siguiente}");
    }
}
#endif
