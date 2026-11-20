using System;
using System.Collections;
using System.Collections.Generic;
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
/// - Los cambios se muestran como borrador hasta que se guardan explícitamente.
/// - El guardado se habilita solo si todas las asignaciones pendientes son válidas.
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
    [SerializeField] private TMP_Text textoAvisoCambiosSinGuardar;

    [Header("Botones UI")]
    [SerializeField] private Button buttonGuardar;

    // La fila que está esperando una tecla nueva (null si ninguna).
    private Fila filaEsperando;

    // Cambios visibles en el panel que todavía no se aplicaron a Controles.
    private readonly Dictionary<Accion, Key> keyValues = new Dictionary<Accion, Key>();
    private bool asignacionInvalida;

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
        keyValues.Clear();
        asignacionInvalida = false;
        filaEsperando = null;
        MostrarAviso("");
        ActualizarEstadoGuardado();
        ActualizarTextos();
    }

    private void OnDisable()
    {
        DescartarCambiosPendientes();
    }

    private void Update()
    {
        if (filaEsperando == null || Keyboard.current == null)
            return;

        // Esc cancela el cambio.
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            filaEsperando = null;
            asignacionInvalida = false;
            MostrarAviso("");
            ActualizarEstadoGuardado();
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
        keyValues.Clear();
        asignacionInvalida = false;
        Controles.RestaurarPorDefecto();
        filaEsperando = null;
        MostrarAviso("Se restauraron las teclas por defecto.");
        ActualizarEstadoGuardado();
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

        Key cambioAnterior;
        bool teniaCambioAnterior = keyValues.TryGetValue(accion, out cambioAnterior);
        if (tecla == Controles.Tecla(accion))
            keyValues.Remove(accion);
        else
            keyValues[accion] = tecla;

        string error = ValidarAsignacion(accion, tecla);
        if (error != null)
        {
            if (teniaCambioAnterior)
                keyValues[accion] = cambioAnterior;
            else
                keyValues.Remove(accion);

            asignacionInvalida = true;
            MostrarAviso(error);
        }
        else
        {
            asignacionInvalida = false;
            MostrarAviso("");
        }

        ActualizarEstadoGuardado();
        ActualizarTextos();
    }

    public void GuardarCambios()
    {
        if (asignacionInvalida || keyValues.Count == 0 || ValidarCambiosPendientes() != null)
        {
            ActualizarEstadoGuardado();
            return;
        }

        Controles.AplicarCambios(keyValues);
        keyValues.Clear();
        asignacionInvalida = false;
        filaEsperando = null;
        MostrarAviso("Se guardaron los cambios.");
        ActualizarEstadoGuardado();
        ActualizarTextos();
    }

    private void ActualizarTextos()
    {
        foreach (Fila fila in filas)
        {
            Key tecla;
            if (!keyValues.TryGetValue(fila.accion, out tecla))
                tecla = Controles.Tecla(fila.accion);
            PonerTexto(fila, Controles.Nombre(tecla));
        }
    }

    private string ValidarCambiosPendientes()
    {
        foreach (KeyValuePair<Accion, Key> cambio in keyValues)
        {
            string error = ValidarAsignacion(cambio.Key, cambio.Value);
            if (error != null)
                return error;
        }

        return null;
    }

    private string ValidarAsignacion(Accion accion, Key tecla)
    {
        if (Controles.EsReservada(tecla))
            return $"La tecla {Controles.Nombre(tecla)} está reservada.";
        if (tecla == Key.None)
            return "No se puede asignar una tecla vacía.";

        if (!Controles.Asignar(accion, tecla))
        {
            Accion? otra = Controles.QuienUsa(tecla);
            if (otra.HasValue)
                return $"La tecla {Controles.Nombre(tecla)} ya la usa \"{NombreAccion(otra.Value)}\".";
        }

        foreach (KeyValuePair<Accion, Key> cambio in keyValues)
        {
            if (cambio.Key != accion && cambio.Value == tecla)
                return $"La tecla {Controles.Nombre(tecla)} ya la usa \"{NombreAccion(cambio.Key)}\".";
        }

        return null;
    }

    private void ActualizarEstadoGuardado()
    {
        if (buttonGuardar != null)
            buttonGuardar.interactable = !asignacionInvalida && keyValues.Count > 0 &&
                                         ValidarCambiosPendientes() == null;

        if (textoAvisoCambiosSinGuardar != null)
            textoAvisoCambiosSinGuardar.text = keyValues.Count > 0
                ? "*Cambios pendientes sin guardar"
                : "";
    }

    private void DescartarCambiosPendientes()
    {
        keyValues.Clear();
        asignacionInvalida = false;
        filaEsperando = null;
        if (buttonGuardar != null)
            buttonGuardar.interactable = false;
        if (textoAvisoCambiosSinGuardar != null)
            textoAvisoCambiosSinGuardar.text = "";
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
