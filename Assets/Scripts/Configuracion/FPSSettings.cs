
using UnityEngine;
using UnityEngine.InputSystem;

public class FPSSettings : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.yKey.wasPressedThisFrame)
        {
            CambiarFPS(30);
        }

        if (Keyboard.current.uKey.wasPressedThisFrame)
        {
            CambiarFPS(60);
        }

        if (Keyboard.current.iKey.wasPressedThisFrame)
        {
            CambiarFPS(75);
        }

        if (Keyboard.current.oKey.wasPressedThisFrame)
        {
            CambiarFPS(144);
        }

        if (Keyboard.current.pKey.wasPressedThisFrame)
        {
            CambiarFPS(250);
        }

        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            CambiarFPS(-1);
        }
    }

    private void CambiarFPS(int fps)
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fps;

        Debug.Log("Limite de FPS configurado: " + fps);
    }
}

