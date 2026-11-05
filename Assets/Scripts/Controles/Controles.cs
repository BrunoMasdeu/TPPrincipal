using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Acciones del jugador que se pueden reasignar en Opciones > Controles.
/// Los clics del mouse (disparar y gancho) quedan fijos.
/// </summary>
public enum Accion
{
    Adelante,
    Atras,
    Izquierda,
    Derecha,
    Saltar,
    Correr,
    Agacharse,
    Recargar,
    Granada,
    Tienda,
    Marcador,
    ArmaPrincipal,
    ArmaSecundaria,
    Cuchillo,
    Gancho
}

/// <summary>
/// Guarda qué tecla corresponde a cada acción.
///
/// Los scripts del jugador le preguntan la tecla en vez de tenerla escrita.
/// Por ejemplo, en vez de Keyboard.current.spaceKey se usa:
///     Keyboard.current[Controles.Tecla(Accion.Saltar)]
///
/// Las teclas se guardan en PlayerPrefs, así se recuperan al reiniciar el
/// juego. No tiene nada de red: cada PC guarda su propia configuración.
/// </summary>
public static class Controles
{
    // Las teclas de siempre. Se usan la primera vez y al restaurar.
    private static readonly Dictionary<Accion, Key> teclasPorDefecto = new Dictionary<Accion, Key>
    {
        { Accion.Adelante, Key.W },
        { Accion.Atras, Key.S },
        { Accion.Izquierda, Key.A },
        { Accion.Derecha, Key.D },
        { Accion.Saltar, Key.Space },
        { Accion.Correr, Key.LeftShift },
        { Accion.Agacharse, Key.LeftCtrl },
        { Accion.Recargar, Key.R },
        { Accion.Granada, Key.G },
        { Accion.Tienda, Key.B },
        { Accion.Marcador, Key.Tab },
        { Accion.ArmaPrincipal, Key.Digit1 },
        { Accion.ArmaSecundaria, Key.Digit2 },
        { Accion.Cuchillo, Key.Digit3 },
        { Accion.Gancho, Key.Digit4 }
    };

    // Las teclas actuales. Se cargan de PlayerPrefs la primera vez que se usan.
    private static Dictionary<Accion, Key> teclas;

    /// <summary>La tecla que tiene asignada esta acción.</summary>
    public static Key Tecla(Accion accion)
    {
        CargarSiHaceFalta();
        return teclas[accion];
    }

    /// <summary>
    /// Asigna la tecla a la acción y la guarda al instante.
    /// Devuelve false (y no cambia nada) si otra acción ya usa esa tecla.
    /// </summary>
    public static bool Asignar(Accion accion, Key tecla)
    {
        CargarSiHaceFalta();

        Accion? otra = QuienUsa(tecla);
        if (otra.HasValue && otra.Value != accion)
            return false;

        teclas[accion] = tecla;
        PlayerPrefs.SetInt(Clave(accion), (int)tecla);
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>La acción que ya usa esa tecla, o null si está libre.</summary>
    public static Accion? QuienUsa(Key tecla)
    {
        CargarSiHaceFalta();

        foreach (KeyValuePair<Accion, Key> par in teclas)
        {
            if (par.Value == tecla)
                return par.Key;
        }

        return null;
    }

    /// <summary>
    /// Teclas que otros scripts usan fijas y no se pueden asignar:
    /// Esc (pausa), E (puerta del easter egg), T (consola) y F8 (depuración).
    /// </summary>
    public static bool EsReservada(Key tecla)
    {
        return tecla == Key.Escape || tecla == Key.E ||
               tecla == Key.T || tecla == Key.F8;
    }

    /// <summary>Vuelve todas las acciones a su tecla de siempre.</summary>
    public static void RestaurarPorDefecto()
    {
        teclas = new Dictionary<Accion, Key>(teclasPorDefecto);

        foreach (Accion accion in teclasPorDefecto.Keys)
            PlayerPrefs.DeleteKey(Clave(accion));

        PlayerPrefs.Save();
    }

    /// <summary>El nombre que se muestra en el panel ("Espacio", "Ctrl", "W"...).</summary>
    public static string Nombre(Key tecla)
    {
        switch (tecla)
        {
            case Key.Space: return "Espacio";
            case Key.LeftShift: return "Shift";
            case Key.RightShift: return "Shift der.";
            case Key.LeftCtrl: return "Ctrl";
            case Key.RightCtrl: return "Ctrl der.";
            case Key.LeftAlt: return "Alt";
            case Key.RightAlt: return "Alt der.";
            case Key.Enter: return "Enter";
            case Key.Backspace: return "Borrar";
            case Key.UpArrow: return "Flecha arriba";
            case Key.DownArrow: return "Flecha abajo";
            case Key.LeftArrow: return "Flecha izq.";
            case Key.RightArrow: return "Flecha der.";
        }

        // Digit1 -> "1", Numpad1 -> "Num 1", W -> "W"
        string nombre = tecla.ToString();
        if (nombre.StartsWith("Digit"))
            return nombre.Substring("Digit".Length);
        if (nombre.StartsWith("Numpad"))
            return "Num " + nombre.Substring("Numpad".Length);

        return nombre;
    }

    private static void CargarSiHaceFalta()
    {
        if (teclas != null)
            return;

        teclas = new Dictionary<Accion, Key>();

        foreach (KeyValuePair<Accion, Key> par in teclasPorDefecto)
        {
            // Si nunca se cambió, PlayerPrefs devuelve la tecla por defecto.
            Key guardada = (Key)PlayerPrefs.GetInt(Clave(par.Key), (int)par.Value);
            bool valida = guardada != Key.None &&
                          System.Enum.IsDefined(typeof(Key), guardada);

            teclas[par.Key] = valida ? guardada : par.Value;
        }
    }

    // Nombre con el que se guarda en PlayerPrefs, por ejemplo "Tecla_Saltar".
    private static string Clave(Accion accion)
    {
        return "Tecla_" + accion;
    }
}
