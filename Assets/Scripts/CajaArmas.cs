using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [ZOMBIES] Caja de armas que suelta el ultimo zombie de cada oleada (la crea el WaveManager).
/// Es dorada, brilla y tira chispas. Al acercarte aparece "E: abrir caja ($precio)".
/// Si tenes la plata: se abre con una luz fuerte y chispas, gira la ruleta en
/// pantalla (RuletaArmas) y te da un arma que todavia no tenes. Si ya tenes todas, te recarga la municion.
/// Las cajas que no compras se quedan (se acumulan) hasta que las compres o mueras.
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

    [Header("Compra")]
    public int precio = 800;
    public float distanciaParaAbrir = 2.5f;

    [Header("Sonidos (los pone el WaveManager)")]
    public AudioClip sonidoAmbiente;   // en loop mientras la caja está cerrada (3D)
    public AudioClip sonidoApertura;   // al pagar
    public AudioClip sonidoRuleta;     // cuando arranca la ruleta
    public AudioClip sonidoPremio;     // cuando la ruleta frena en el arma
    [Range(0f, 1f)] public float volumenAmbiente = 0.8f;
    [Range(0f, 1f)] public float volumenEfectos = 1f;

    [Header("Aspecto")]
    public float escala = 2.5f;
    public Color dorado = new Color(1f, 0.78f, 0.25f);
    public float intensidadLuz = 4f;
    public float alcanceLuz = 6f;

    // Solo una caja muestra el cartel a la vez (la que tenes cerca).
    private static CajaArmas cajaConCartel;
    private static GameObject cartel;
    private static TextMeshProUGUI textoCartel;

    private Animator animador;
    private AudioSource fuenteAmbiente;
    private Light luz;
    private ParticleSystem chispas;
    private bool abierta;

    private void Start()
    {
        transform.localScale = Vector3.one * escala;

        // El modelo trae la animacion de abrir la tapa: se usa recien al comprar.
        animador = GetComponent<Animator>();
        if (animador != null)
            animador.enabled = false;

        PintarDeDorado();
        CrearSonidoAmbiente();
        CrearColision();
        CrearLuz();
        CrearChispas();
    }

    private void Update()
    {
        // La luz late para llamar la atencion.
        if (luz != null && !abierta)
            luz.intensity = intensidadLuz * (0.7f + 0.3f * Mathf.Sin(Time.time * 4f));

        if (abierta)
            return;

        NetworkObject jugador = JugadorLocal();
        bool cerca = jugador != null &&
            Vector3.Distance(jugador.transform.position, transform.position) <= distanciaParaAbrir;

        ActualizarCartel(cerca);

        if (cerca && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            IntentarComprar(jugador);
    }

    private void OnDestroy()
    {
        if (cajaConCartel == this)
            MostrarCartel(null);
    }

    // =====================================================================
    // Compra
    // =====================================================================

    private void IntentarComprar(NetworkObject jugador)
    {
        NetworkPlayerCombat combate = jugador.GetComponent<NetworkPlayerCombat>();
        NetworkPlayerInventory inventario = jugador.GetComponent<NetworkPlayerInventory>();
        AchievementNotificationHUD aviso = jugador.GetComponentInChildren<AchievementNotificationHUD>(true);
        if (combate == null || inventario == null)
            return;

        if (!combate.TrySpendMoneyOnServer(precio))
        {
            if (aviso != null)
                aviso.MostrarAviso("NECESITÁS $" + precio, null);
            return;
        }

        StartCoroutine(Abrir(inventario, combate, aviso));
    }

    private IEnumerator Abrir(NetworkPlayerInventory inventario, NetworkPlayerCombat combate,
        AchievementNotificationHUD aviso)
    {
        abierta = true;
        ActualizarCartel(false);

        // [ZOMBIES] Se apaga el ambiente de la caja y suena la apertura.
        if (fuenteAmbiente != null)
            fuenteAmbiente.Stop();
        SonarEnCamara(sonidoApertura);

        // Elegir el premio: un arma que todavia no tengas.
        List<WeaponDefinition> todas = new List<WeaponDefinition>();
        inventario.CopyDefinitionsTo(todas);
        List<WeaponDefinition> posibles = todas.FindAll(
            arma => System.Array.IndexOf(ArmasPosibles, arma.Id) >= 0);
        List<WeaponDefinition> nuevas = posibles.FindAll(
            arma => !inventario.HasWeaponInLoadout(arma.Id));
        WeaponDefinition premio = nuevas.Count > 0 ? nuevas[Random.Range(0, nuevas.Count)] : null;

        // Abrir la tapa y dejarla quieta abierta (la animacion abre en la primera mitad).
        if (animador != null)
        {
            animador.enabled = true;
            animador.speed = 1f;
            animador.Play(0, 0, 0f);
        }

        // Luz fuerte desde adentro.
        if (luz != null)
        {
            luz.intensity = intensidadLuz * 2.5f;
            luz.range = alcanceLuz * 1.2f;
        }

        yield return new WaitForSeconds(0.9f);
        if (animador != null)
            animador.speed = 0f;

        // Mientras gira la ruleta en pantalla, la caja tira muchas mas chispas y la luz titila.
        if (chispas != null)
        {
            var emision = chispas.emission;
            emision.rateOverTime = 160f;
            var principal = chispas.main;
            principal.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
        }

        bool terminada = false;
        RuletaArmas.Mostrar(posibles, premio, () => terminada = true);
        SonarEnCamara(sonidoRuleta);

        // El sonido de premio suena justo cuando la ruleta frena en el arma.
        float girando = 0f;
        bool premioSonado = false;
        while (!terminada)
        {
            girando += Time.deltaTime;
            if (!premioSonado && girando >= RuletaArmas.DuracionGiro)
            {
                premioSonado = true;
                SonarEnCamara(sonidoPremio);
            }

            if (luz != null)
                luz.intensity = intensidadLuz * Random.Range(1.5f, 3f);
            yield return null;
        }

        Entregar(inventario, combate, aviso, premio);

        // Cerrar y desaparecer.
        if (chispas != null)
            chispas.Stop();
        if (animador != null)
            animador.speed = 1f;
        yield return new WaitForSeconds(1f);
        Destroy(gameObject);
    }

    private static void Entregar(NetworkPlayerInventory inventario, NetworkPlayerCombat combate,
        AchievementNotificationHUD aviso, WeaponDefinition premio)
    {
        if (inventario == null)
            return;

        if (premio != null && inventario.TryAddPurchasedWeaponOnServer(premio.Id))
        {
            if (aviso != null)
                aviso.MostrarAviso("RECIBISTE: " + premio.DisplayName.ToUpperInvariant(), premio.HudSprite);
        }
        else
        {
            // Ya tenias todas: la caja llena la municion.
            if (combate != null)
                combate.RefillAllWeaponsOnServer();
            if (aviso != null)
                aviso.MostrarAviso("MUNICIÓN RECARGADA", aviso.iconBalas);
        }
    }

    private static NetworkObject JugadorLocal()
    {
        NetworkManager red = NetworkManager.Singleton;
        if (red == null || red.LocalClient == null)
            return null;
        return red.LocalClient.PlayerObject;
    }

    // =====================================================================
    // Aspecto
    // =====================================================================

    private void PintarDeDorado()
    {
        foreach (Renderer parte in GetComponentsInChildren<Renderer>())
        {
            foreach (Material material in parte.materials)
            {
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", dorado);
                if (material.HasProperty("_Color")) material.SetColor("_Color", dorado);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.8f);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.7f);

                // Brillo propio, para que se vea aunque este oscuro.
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    material.SetColor("_EmissionColor", dorado * 0.12f);
                }
            }
        }
    }

    // [ZOMBIES] Sonido en loop que sale de la caja (se escucha al acercarse).
    private void CrearSonidoAmbiente()
    {
        if (sonidoAmbiente == null)
            return;

        fuenteAmbiente = gameObject.AddComponent<AudioSource>();
        fuenteAmbiente.clip = sonidoAmbiente;
        fuenteAmbiente.loop = true;
        fuenteAmbiente.volume = volumenAmbiente;
        fuenteAmbiente.spatialBlend = 1f;
        fuenteAmbiente.rolloffMode = AudioRolloffMode.Linear;
        fuenteAmbiente.minDistance = 2f;
        fuenteAmbiente.maxDistance = 15f;
        fuenteAmbiente.dopplerLevel = 0f;
        fuenteAmbiente.Play();
    }

    // Efectos que tiene que escuchar el jugador (no se cortan si la caja desaparece).
    private void SonarEnCamara(AudioClip clip)
    {
        Camera camara = Camera.main;
        if (clip != null && camara != null)
            AudioSource.PlayClipAtPoint(clip, camara.transform.position, volumenEfectos);
    }

    private void CrearColision()
    {
        // Una caja solida del tamanio del modelo, para que no se atraviese.
        Renderer[] partes = GetComponentsInChildren<Renderer>();
        if (partes.Length == 0)
            return;

        Bounds limites = partes[0].bounds;
        foreach (Renderer parte in partes)
            limites.Encapsulate(parte.bounds);

        BoxCollider colision = gameObject.AddComponent<BoxCollider>();
        Vector3 escalaReal = transform.lossyScale;
        colision.center = transform.InverseTransformPoint(limites.center);
        colision.size = new Vector3(
            limites.size.x / escalaReal.x,
            limites.size.y / escalaReal.y,
            limites.size.z / escalaReal.z);
    }

    private void CrearLuz()
    {
        GameObject objetoLuz = new GameObject("LuzCaja");
        objetoLuz.transform.SetParent(transform, false);
        objetoLuz.transform.position = transform.position + Vector3.up * 0.8f;
        luz = objetoLuz.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = dorado;
        luz.intensity = intensidadLuz;
        luz.range = alcanceLuz;
        luz.shadows = LightShadows.None;
    }

    private void CrearChispas()
    {
        // Mismo material de particulas que usan los impactos de bala.
        Material material = Resources.Load<Material>("CombatImpactParticle");
        if (material == null)
            return;

        GameObject objeto = new GameObject("ChispasCaja");
        objeto.transform.SetParent(transform, false);
        objeto.transform.position = transform.position + Vector3.up * 0.3f;
        objeto.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // hacia arriba

        chispas = objeto.AddComponent<ParticleSystem>();
        chispas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var principal = chispas.main;
        principal.loop = true;
        principal.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        principal.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
        principal.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        principal.startColor = dorado;
        principal.maxParticles = 200;
        principal.simulationSpace = ParticleSystemSimulationSpace.World;
        principal.scalingMode = ParticleSystemScalingMode.Shape;

        var emision = chispas.emission;
        emision.rateOverTime = 40f;

        // Columna angosta de chispas que sube: se ve de lejos.
        var forma = chispas.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 4f;
        forma.radius = 0.35f;

        var color = chispas.colorOverLifetime;
        color.enabled = true;
        Gradient degradado = new Gradient();
        degradado.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(dorado, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = degradado;

        objeto.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        chispas.Play();
    }

    // =====================================================================
    // Cartel "E: abrir caja"
    // =====================================================================

    private void ActualizarCartel(bool cerca)
    {
        if (cerca && (cajaConCartel == null || cajaConCartel == this))
            MostrarCartel(this);
        else if (!cerca && cajaConCartel == this)
            MostrarCartel(null);
    }

    private static void MostrarCartel(CajaArmas caja)
    {
        cajaConCartel = caja;

        if (caja == null)
        {
            if (cartel != null)
                cartel.SetActive(false);
            return;
        }

        if (cartel == null)
            CrearCartel();

        if (WaveManager.FuenteTextoTMP != null)
            textoCartel.font = WaveManager.FuenteTextoTMP; // [ZOMBIES] fuente del modo zombies
        textoCartel.text = "E: abrir caja ($" + caja.precio + ")";
        cartel.SetActive(true);
    }

    private static void CrearCartel()
    {
        cartel = new GameObject("CartelCajaArmas");
        Canvas canvas = cartel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var escalaUI = cartel.AddComponent<UnityEngine.UI.CanvasScaler>();
        escalaUI.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalaUI.referenceResolution = new Vector2(1920f, 1080f);
        escalaUI.matchWidthOrHeight = 0.5f;

        GameObject objetoTexto = new GameObject("Texto", typeof(RectTransform));
        objetoTexto.transform.SetParent(cartel.transform, false);
        textoCartel = objetoTexto.AddComponent<TextMeshProUGUI>();
        textoCartel.alignment = TextAlignmentOptions.Center;
        textoCartel.fontSize = 36f;
        textoCartel.fontStyle = FontStyles.Bold;
        textoCartel.color = new Color(1f, 0.85f, 0.3f);
        textoCartel.outlineWidth = 0.2f;
        textoCartel.outlineColor = Color.black;
        RectTransform rect = (RectTransform)objetoTexto.transform;
        rect.sizeDelta = new Vector2(900f, 60f);
        rect.anchoredPosition = new Vector2(0f, -220f);
    }
}
