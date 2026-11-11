using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [ZOMBIES] Caja que suelta el ultimo zombie de cada oleada (la crea el WaveManager).
/// Gira, flota y brilla. Al pasar por encima abre la ruleta y te da un arma
/// que todavia no tenes. Si ya tenes todas, te recarga la municion.
///
/// El modo zombies es de un solo jugador (el que hostea la partida), por eso
/// la caja no usa red: corre en la misma PC que hace de servidor.
/// </summary>
public class CajaArmas : MonoBehaviour
{
    // Armas que puede dar la caja (la pistola y el cuchillo ya vienen al empezar).
    private static readonly WeaponId[] ArmasPosibles =
    {
        WeaponId.Revolver,
        WeaponId.AK,
        WeaponId.M4,
        WeaponId.Sniper
    };

    [Header("Aspecto")]
    public Color colorLuz = new Color(1f, 0.8f, 0.2f);
    public float intensidadLuz = 4f;
    public float alcanceLuz = 5f;
    public float velocidadGiro = 90f;
    public float alturaFlotando = 0.3f;

    private Vector3 posicionInicial;
    private Light luz;
    private bool abierta;

    private void Start()
    {
        posicionInicial = transform.position;

        // El modelo trae un Animator de apertura; se apaga para que no pise el giro.
        Animator animador = GetComponent<Animator>();
        if (animador != null)
            animador.enabled = false;

        // Zona que detecta al jugador: un cubo de 1,5 m alrededor de la caja.
        BoxCollider zona = gameObject.AddComponent<BoxCollider>();
        zona.isTrigger = true;
        Vector3 escala = transform.lossyScale;
        zona.size = new Vector3(1.5f / escala.x, 2f / escala.y, 1.5f / escala.z);
        zona.center = new Vector3(0f, 1f / escala.y, 0f);

        // Luz dorada para que se vea en la oscuridad.
        GameObject objetoLuz = new GameObject("LuzCaja");
        objetoLuz.transform.SetParent(transform, false);
        objetoLuz.transform.localPosition = Vector3.up * (1f / escala.y);
        luz = objetoLuz.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = colorLuz;
        luz.intensity = intensidadLuz;
        luz.range = alcanceLuz;
        luz.shadows = LightShadows.None;
    }

    private void Update()
    {
        if (abierta)
            return;

        // Gira y sube/baja suave.
        transform.Rotate(0f, velocidadGiro * Time.deltaTime, 0f, Space.World);
        float subida = alturaFlotando + Mathf.Sin(Time.time * 2f) * 0.1f;
        transform.position = posicionInicial + Vector3.up * subida;

        // La luz late.
        if (luz != null)
            luz.intensity = intensidadLuz * (0.75f + 0.25f * Mathf.Sin(Time.time * 4f));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (abierta)
            return;

        NetworkPlayerInventory inventario = other.GetComponentInParent<NetworkPlayerInventory>();
        if (inventario == null || !inventario.IsServer)
            return;

        abierta = true;

        // Arma las listas: todas las armas posibles (para la tira) y las que todavia no tenes (el premio).
        List<WeaponDefinition> todas = new List<WeaponDefinition>();
        inventario.CopyDefinitionsTo(todas);

        List<WeaponDefinition> posibles = todas.FindAll(
            arma => System.Array.IndexOf(ArmasPosibles, arma.Id) >= 0);
        List<WeaponDefinition> nuevas = posibles.FindAll(
            arma => !inventario.HasWeaponInLoadout(arma.Id));

        WeaponDefinition premio = nuevas.Count > 0
            ? nuevas[Random.Range(0, nuevas.Count)]
            : null;

        // Esconde la caja mientras gira la ruleta.
        foreach (Renderer parte in GetComponentsInChildren<Renderer>())
            parte.enabled = false;
        foreach (Collider parte in GetComponentsInChildren<Collider>())
            parte.enabled = false;
        if (luz != null)
            luz.enabled = false;

        RuletaArmas.Mostrar(posibles, premio, () => Entregar(inventario, premio));
    }

    private void Entregar(NetworkPlayerInventory inventario, WeaponDefinition premio)
    {
        // Si la caja ya no existe (por ejemplo el jugador murio y se reinicio), no da nada.
        if (this == null || inventario == null)
            return;

        AchievementNotificationHUD aviso =
            inventario.GetComponentInChildren<AchievementNotificationHUD>(true);

        if (premio != null && inventario.TryAddPurchasedWeaponOnServer(premio.Id))
        {
            if (aviso != null)
                aviso.MostrarAviso("RECIBISTE: " + premio.DisplayName.ToUpperInvariant(), premio.HudSprite);
        }
        else
        {
            NetworkPlayerCombat combate = inventario.GetComponent<NetworkPlayerCombat>();
            if (combate != null)
                combate.RefillAllWeaponsOnServer();

            if (aviso != null)
                aviso.MostrarAviso("MUNICION RECARGADA", aviso.iconBalas);
        }

        Destroy(gameObject);
    }
}
