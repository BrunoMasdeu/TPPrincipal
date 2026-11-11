using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textoFPS;

    private float deltaTime;

    public string TextoActual { get; private set; }

    private void Update()
    {
        bool mostrar = ConfiguracionVideo.MostrarFPS;
        // En Zombies, WaveManager dibuja el valor junto al dinero y la dificultad.
        textoFPS.enabled = mostrar && !WaveManager.IsActive;
        if (!mostrar)
        {
            TextoActual = null;
            return;
        }

        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        float fps = 1.0f / deltaTime;
        TextoActual = "FPS: " + Mathf.Ceil(fps);
        if (textoFPS.enabled)
            textoFPS.text = TextoActual;
    }
}
