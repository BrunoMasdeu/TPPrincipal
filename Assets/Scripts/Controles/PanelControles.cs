using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>
/// Conecta la sección "CONTROLES" del panel de Opciones (MenuScene) con
/// Controles.
///
/// - Cada fila del panel tiene un botón que muestra la tecla actual.
/// - Al tocar el botón se espera la próxima tecla que apriete el jugador
///   (Esc cancela) y se asigna a esa acción.
/// - Si la tecla ya la usa otra acción, se rechaza y se muestra un aviso.
/// - Los cambios se guardan al instante (lo hace Controles).
/// </summary>
public class PanelControles : MonoBehaviour
{
    [Serializable]
    public class Fila
    {
        public Accion accion;
        public Button boton; // el botón que muestra la tecla
    }

    [Header("Filas del panel (una por acción)")]
    [SerializeField] private Fila[] filas;

    [Header("Texto para avisos (opcional)")]
    [SerializeField] private TMP_Text textoAviso;

    // La fila que está esperando una tecla nueva (null si ninguna).
    private Fila filaEsperando;

    private void Awake()
    {
        foreach (Fila fila in filas)
        {
            if (fila.boton == null)
                continue;

            Fila estaFila = fila; // copia para usarla dentro del listener
            fila.boton.onClick.AddListener(() => EmpezarACambiar(estaFila));
        }
    }

    private void OnEnable()
    {
        // Cada vez que se abre Opciones se muestran las teclas actuales.
        filaEsperando = null;
        MostrarAviso("");
        ActualizarTextos();
    }

    private void Update()
    {
        if (filaEsperando == null || Keyboard.current == null)
            return;

        // Esc cancela el cambio.
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            filaEsperando = null;
            MostrarAviso("");
            ActualizarTextos();
            return;
        }

        // Se busca la tecla que se apretó en este frame.
        foreach (KeyControl tecla in Keyboard.current.allKeys)
        {
            if (tecla != null && tecla.wasPressedThisFrame)
            {
                TerminarCambio(tecla.keyCode);
                return;
            }
        }
    }

    /// <summary>Lo llama el botón RESTAURAR (OnClick en el Inspector).</summary>
    public void Restaurar()
    {
        Controles.RestaurarPorDefecto();
        filaEsperando = null;
        MostrarAviso("Se restauraron las teclas por defecto.");
        ActualizarTextos();
    }

    private void EmpezarACambiar(Fila fila)
    {
        ActualizarTextos(); // por si otra fila estaba esperando
        filaEsperando = fila;
        PonerTexto(fila, "...");
        MostrarAviso("Presioná una tecla (Esc para cancelar).");

        // Se quita la selección del botón: si no, Espacio o Enter lo
        // volverían a "apretar" en vez de asignarse como tecla.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void TerminarCambio(Key tecla)
    {
        Accion accion = filaEsperando.accion;
        filaEsperando = null;

        if (Controles.EsReservada(tecla))
        {
            MostrarAviso($"La tecla {Controles.Nombre(tecla)} está reservada.");
        }
        else if (!Controles.Asignar(accion, tecla))
        {
            Accion otra = Controles.QuienUsa(tecla).Value;
            MostrarAviso($"La tecla {Controles.Nombre(tecla)} ya la usa \"{NombreAccion(otra)}\".");
        }
        else
        {
            MostrarAviso("");
        }

        ActualizarTextos();
    }

    private void ActualizarTextos()
    {
        foreach (Fila fila in filas)
            PonerTexto(fila, Controles.Nombre(Controles.Tecla(fila.accion)));
    }

    // El texto de la tecla es el que está adentro del botón.
    private static void PonerTexto(Fila fila, string texto)
    {
        if (fila.boton == null)
            return;

        TMP_Text textoBoton = fila.boton.GetComponentInChildren<TMP_Text>();
        if (textoBoton != null)
            textoBoton.text = texto;
    }

    private void MostrarAviso(string mensaje)
    {
        if (textoAviso != null)
            textoAviso.text = mensaje;
        else if (mensaje != "")
            Debug.Log("[Controles] " + mensaje);
    }

    // Los mismos nombres que se ven en el panel.
    private static string NombreAccion(Accion accion)
    {
        switch (accion)
        {
            case Accion.Adelante: return "Caminar hacia adelante";
            case Accion.Atras: return "Caminar hacia atras";
            case Accion.Izquierda: return "Caminar hacia la izquierda";
            case Accion.Derecha: return "Caminar hacia la derecha";
            case Accion.Saltar: return "Saltar";
            case Accion.Correr: return "Correr";
            case Accion.Agacharse: return "Agacharse";
            case Accion.Recargar: return "Recargar";
            case Accion.Granada: return "Equipar granada";
            case Accion.Tienda: return "Menu de compra";
            case Accion.Marcador: return "Marcador";
            case Accion.ArmaPrincipal: return "Equipar arma principal";
            case Accion.ArmaSecundaria: return "Equipar arma secundaria";
            case Accion.Cuchillo: return "Equipar cuchillo";
            case Accion.Gancho: return "Equipar gancho";
            default: return accion.ToString();
        }
    }
}
