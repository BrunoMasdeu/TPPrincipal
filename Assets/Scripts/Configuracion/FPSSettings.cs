
using UnityEngine;

/// <summary>
/// Límite de FPS (código de Felipe).
///
/// [MENU] Antes se cambiaba con las teclas Y, U, I, O, P y L para probar.
/// Ahora solo se cambia desde Opciones > Video (lo llama ConfiguracionVideo).
/// </summary>
public static class FPSSettings
{
    // fps = -1 significa "sin límite".
    public static void CambiarFPS(int fps)
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fps;

        Debug.Log("Limite de FPS configurado: " + fps);
    }
}
