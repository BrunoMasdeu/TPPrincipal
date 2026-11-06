
using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class GraphicsSettings : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // Graficos bajos
        if (Keyboard.current.digit8Key.wasPressedThisFrame ||
            Keyboard.current.numpad8Key.wasPressedThisFrame)
        {
            CambiarCalidad("Bajo");
        }

        // Graficos medios
        if (Keyboard.current.digit9Key.wasPressedThisFrame ||
            Keyboard.current.numpad9Key.wasPressedThisFrame)
        {
            CambiarCalidad("Medio");
        }

        // Graficos altos
        if (Keyboard.current.digit0Key.wasPressedThisFrame ||
            Keyboard.current.numpad0Key.wasPressedThisFrame)
        {
            CambiarCalidad("Alto");
        }
    }

    private void CambiarCalidad(string nombre)
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

