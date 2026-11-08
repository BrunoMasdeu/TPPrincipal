using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textoFPS;

    private float deltaTime;

    private void Update()
    {
        // [MENU] Se muestra o se oculta según Opciones > Video > Mostrar FPS.
        textoFPS.enabled = ConfiguracionVideo.MostrarFPS;
        if (!textoFPS.enabled)
            return;

        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        float fps = 1.0f / deltaTime;

        textoFPS.text = "FPS: " + Mathf.Ceil(fps);
    }
}
