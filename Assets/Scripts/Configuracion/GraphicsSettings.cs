
using UnityEngine;
using System;

/// <summary>
/// Calidad gráfica (código de Felipe). Usa los niveles "Bajo", "Medio" y
/// "Alto" de Project Settings > Quality.
///
/// [MENU] Antes se cambiaba con las teclas 8, 9 y 0 para probar.
/// Ahora solo se cambia desde Opciones > Video (lo llama ConfiguracionVideo).
/// </summary>
public static class GraphicsSettings
{
    public static void CambiarCalidad(string nombre)
    {
        string[] niveles = QualitySettings.names;

        int indice = Array.IndexOf(niveles, nombre);

        if (indice == -1)
        {
            Debug.LogError("No existe el nivel de calidad: " + nombre);
            return;
        }

        QualitySettings.SetQualityLevel(indice, true);

        Debug.Log("Calidad gráfica cambiada a: " + nombre);
    }
}
