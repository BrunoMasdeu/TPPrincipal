using System;
using System.Text;

/// <summary>
/// Reglas del nombre del jugador (COG-587).
///
/// - No puede estar vacío y tiene como máximo 13 caracteres.
/// - Solo caracteres ASCII normales (de espacio a "~") y además la ñ / Ñ.
/// - No se permiten "&lt;" ni "&gt;": los textos de Unity (TextMeshPro) los
///   toman como formato y alguien podría cambiar cómo se ven los textos.
/// - Dos jugadores no pueden tener el mismo nombre (sin importar mayúsculas).
///
/// La PC del jugador revisa el nombre antes de conectarse, y el servidor lo
/// vuelve a revisar al aprobar la conexión (LobbyPlayerSpawner).
/// </summary>
public static class NombreJugadorRules
{
    public const int LargoMaximo = 13;

    /// <summary>Devuelve el motivo si el nombre no es válido, o null si está bien.</summary>
    public static string Validar(string nombre)
    {
        nombre = Limpiar(nombre);

        if (nombre.Length == 0)
            return "Ingresá tu nombre.";

        if (nombre.Length > LargoMaximo)
            return $"El nombre puede tener hasta {LargoMaximo} caracteres.";

        foreach (char letra in nombre)
        {
            if (!EsCaracterPermitido(letra))
                return $"El carácter \"{letra}\" no está permitido en el nombre.";
        }

        return null;
    }

    public static bool EsCaracterPermitido(char letra)
    {
        if (letra == 'ñ' || letra == 'Ñ')
            return true;

        if (letra == '<' || letra == '>')
            return false;

        return letra >= ' ' && letra <= '~';
    }

    /// <summary>Saca los espacios del principio y del final.</summary>
    public static string Limpiar(string nombre)
    {
        return nombre == null ? string.Empty : nombre.Trim();
    }

    /// <summary>"Tomas" y "tomas" cuentan como el mismo nombre.</summary>
    public static bool SonIguales(string a, string b)
    {
        return string.Equals(Limpiar(a), Limpiar(b), StringComparison.OrdinalIgnoreCase);
    }

    // El nombre viaja con el pedido de conexión (ConnectionData) como bytes.
    public static byte[] ABytes(string nombre)
    {
        return Encoding.UTF8.GetBytes(Limpiar(nombre));
    }

    public static string DesdeBytes(byte[] datos)
    {
        if (datos == null || datos.Length == 0)
            return string.Empty;

        try
        {
            return Limpiar(Encoding.UTF8.GetString(datos));
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }
}
