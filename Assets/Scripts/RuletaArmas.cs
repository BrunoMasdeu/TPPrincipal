using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [ZOMBIES] Ruleta horizontal estilo CS:GO que se muestra al abrir la caja de armas.
/// Una tira de casillas con iconos de armas se desliza de derecha a izquierda,
/// va frenando y se detiene con el arma ganadora debajo de la linea amarilla.
///
/// Arma su propia interfaz por codigo la primera vez que se usa, asi no hay
/// que tocar el Canvas del jugador. Para cambiar el aspecto, tocar las
/// constantes de abajo.
/// </summary>
public class RuletaArmas : MonoBehaviour
{
    // Tamanio de cada casilla y espacio entre ellas (en pixeles de una pantalla 1920x1080).
    private const float AnchoCasilla = 170f;
    private const float AltoCasilla = 120f;
    private const float Separacion = 8f;

    // Cuantas casillas tiene la tira y en cual cae el premio.
    private const int CantidadCasillas = 45;
    private const int IndiceGanador = 38;

    // Segundos que gira y segundos que queda mostrando el premio.
    public const float DuracionGiro = 5f; // [ZOMBIES] público: la caja lo usa para el sonido de premio
    private const float TiempoMostrandoPremio = 1.5f;

    private static RuletaArmas instancia;

    private GameObject panel;
    private RectTransform tira;
    private TextMeshProUGUI titulo;
    private Coroutine giro;

    /// <summary>
    /// Muestra la ruleta y, cuando termina, llama a alTerminar.
    /// Si no hay premio o no hay opciones, llama a alTerminar enseguida.
    /// </summary>
    public static void Mostrar(List<WeaponDefinition> opciones, WeaponDefinition premio, Action alTerminar)
    {
        if (opciones == null || opciones.Count == 0 || premio == null)
        {
            alTerminar?.Invoke();
            return;
        }

        if (instancia == null)
            instancia = Crear();

        instancia.Girar(opciones, premio, alTerminar);
    }

    /// <summary>Oculta la ruleta sin dar el premio (por ejemplo si el jugador muere).</summary>
    public static void Cerrar()
    {
        if (instancia == null)
            return;

        if (instancia.giro != null)
            instancia.StopCoroutine(instancia.giro);

        instancia.giro = null;
        instancia.panel.SetActive(false);
    }

    private void Girar(List<WeaponDefinition> opciones, WeaponDefinition premio, Action alTerminar)
    {
        if (giro != null)
            StopCoroutine(giro);

        giro = StartCoroutine(RutinaGiro(opciones, premio, alTerminar));
    }

    private IEnumerator RutinaGiro(List<WeaponDefinition> opciones, WeaponDefinition premio, Action alTerminar)
    {
        // Rellena la tira: casillas al azar y el premio en IndiceGanador.
        foreach (Transform hijo in tira)
            Destroy(hijo.gameObject);

        RectTransform casillaGanadora = null;
        for (int i = 0; i < CantidadCasillas; i++)
        {
            WeaponDefinition arma = i == IndiceGanador
                ? premio
                : opciones[UnityEngine.Random.Range(0, opciones.Count)];

            RectTransform casilla = CrearCasilla(arma, i);
            if (i == IndiceGanador)
                casillaGanadora = casilla;
        }

        if (WaveManager.FuenteTextoTMP != null)
            titulo.font = WaveManager.FuenteTextoTMP; // [ZOMBIES] fuente del modo zombies
        titulo.text = "CAJA DE ARMAS";
        tira.anchoredPosition = Vector2.zero;
        panel.SetActive(true);

        // Donde tiene que terminar la tira: el premio bajo la linea central,
        // con un pequenio desvio al azar para que no frene siempre en el medio exacto.
        float paso = AnchoCasilla + Separacion;
        float desvio = UnityEngine.Random.Range(-AnchoCasilla * 0.35f, AnchoCasilla * 0.35f);
        float destino = -(IndiceGanador * paso) + desvio;

        float tiempo = 0f;
        while (tiempo < DuracionGiro)
        {
            tiempo += Time.deltaTime;
            float t = Mathf.Clamp01(tiempo / DuracionGiro);

            // Arranca rapido y frena de a poco (curva "ease out").
            float frenado = 1f - Mathf.Pow(1f - t, 4f);
            tira.anchoredPosition = new Vector2(Mathf.Lerp(0f, destino, frenado), 0f);
            yield return null;
        }

        // Resalta el premio.
        titulo.text = premio.DisplayName.ToUpperInvariant();
        if (casillaGanadora != null)
            casillaGanadora.localScale = Vector3.one * 1.12f;

        yield return new WaitForSeconds(TiempoMostrandoPremio);

        panel.SetActive(false);
        giro = null;
        alTerminar?.Invoke();
    }

    // =====================================================================
    // Construccion de la interfaz (solo la primera vez)
    // =====================================================================

    private static RuletaArmas Crear()
    {
        GameObject raiz = new GameObject("RuletaArmas");
        RuletaArmas ruleta = raiz.AddComponent<RuletaArmas>();

        Canvas canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler escala = raiz.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 0.5f;

        // Fondo oscuro.
        ruleta.panel = CrearImagen("Panel", raiz.transform, new Color(0f, 0f, 0f, 0.8f));
        RectTransform rectPanel = (RectTransform)ruleta.panel.transform;
        rectPanel.sizeDelta = new Vector2(960f, 230f);
        rectPanel.anchoredPosition = new Vector2(0f, -170f);

        // Titulo arriba.
        GameObject objetoTitulo = new GameObject("Titulo", typeof(RectTransform));
        objetoTitulo.transform.SetParent(rectPanel, false);
        ruleta.titulo = objetoTitulo.AddComponent<TextMeshProUGUI>();
        ruleta.titulo.alignment = TextAlignmentOptions.Center;
        ruleta.titulo.fontSize = 34f;
        ruleta.titulo.fontStyle = FontStyles.Bold;
        ruleta.titulo.color = new Color(1f, 0.82f, 0.2f);
        RectTransform rectTitulo = (RectTransform)objetoTitulo.transform;
        rectTitulo.sizeDelta = new Vector2(900f, 50f);
        rectTitulo.anchoredPosition = new Vector2(0f, 85f);

        // Ventana que recorta la tira (lo que queda afuera no se ve).
        GameObject ventana = new GameObject("Ventana", typeof(RectTransform));
        ventana.transform.SetParent(rectPanel, false);
        ventana.AddComponent<RectMask2D>();
        RectTransform rectVentana = (RectTransform)ventana.transform;
        rectVentana.sizeDelta = new Vector2(920f, AltoCasilla + 20f);
        rectVentana.anchoredPosition = new Vector2(0f, -15f);

        // La tira que se mueve.
        GameObject objetoTira = new GameObject("Tira", typeof(RectTransform));
        objetoTira.transform.SetParent(rectVentana, false);
        ruleta.tira = (RectTransform)objetoTira.transform;

        // Linea central amarilla.
        GameObject linea = CrearImagen("Linea", rectPanel, new Color(1f, 0.82f, 0.2f));
        RectTransform rectLinea = (RectTransform)linea.transform;
        rectLinea.sizeDelta = new Vector2(4f, AltoCasilla + 30f);
        rectLinea.anchoredPosition = new Vector2(0f, -15f);

        ruleta.panel.SetActive(false);
        return ruleta;
    }

    private RectTransform CrearCasilla(WeaponDefinition arma, int indice)
    {
        GameObject casilla = CrearImagen("Casilla", tira, new Color(0.16f, 0.16f, 0.18f, 1f));
        RectTransform rect = (RectTransform)casilla.transform;
        rect.sizeDelta = new Vector2(AnchoCasilla, AltoCasilla);
        rect.anchoredPosition = new Vector2(indice * (AnchoCasilla + Separacion), 0f);

        // Barra de color abajo, como la rareza en CS:GO.
        GameObject barra = CrearImagen("Barra", rect, ColorDe(arma.Id));
        RectTransform rectBarra = (RectTransform)barra.transform;
        rectBarra.anchorMin = new Vector2(0f, 0f);
        rectBarra.anchorMax = new Vector2(1f, 0f);
        rectBarra.pivot = new Vector2(0.5f, 0f);
        rectBarra.sizeDelta = new Vector2(0f, 8f);
        rectBarra.anchoredPosition = Vector2.zero;

        // Icono del arma.
        GameObject icono = new GameObject("Icono", typeof(RectTransform));
        icono.transform.SetParent(rect, false);
        Image imagen = icono.AddComponent<Image>();
        imagen.sprite = arma.HudSprite;
        imagen.preserveAspect = true;
        imagen.enabled = arma.HudSprite != null;
        RectTransform rectIcono = (RectTransform)icono.transform;
        rectIcono.sizeDelta = new Vector2(AnchoCasilla - 24f, AltoCasilla - 34f);
        rectIcono.anchoredPosition = new Vector2(0f, 4f);

        return rect;
    }

    private static GameObject CrearImagen(string nombre, Transform padre, Color color)
    {
        GameObject objeto = new GameObject(nombre, typeof(RectTransform));
        objeto.transform.SetParent(padre, false);
        Image imagen = objeto.AddComponent<Image>();
        imagen.color = color;
        imagen.raycastTarget = false;
        return objeto;
    }

    private static Color ColorDe(WeaponId arma)
    {
        switch (arma)
        {
            case WeaponId.Sniper: return new Color(0.95f, 0.75f, 0.15f); // dorado
            case WeaponId.AK: return new Color(0.9f, 0.25f, 0.25f);      // rojo
            case WeaponId.M4: return new Color(0.65f, 0.3f, 0.95f);      // violeta
            default: return new Color(0.3f, 0.5f, 1f);                   // azul
        }
    }
}
