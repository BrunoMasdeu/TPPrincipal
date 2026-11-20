
using UnityEngine;
using UnityEngine.UI;

public class AplicarConfiguracionMira : MonoBehaviour
{
    [Header("Referencias")]
    public Image imagenMira;

    [Header("Diseños de mira")]
    public Sprite[] miras = new Sprite[3];

    private Color[] colores =
    {
        Color.black,
        Color.white,
        new Color(1f, 0.4f, 0.7f)
    };

    private void Start()
    {
        AplicarMira();
    }

    private void AplicarMira()
    {
        int indiceMira = PlayerPrefs.GetInt("MiraSeleccionada", 0);
        int indiceColor = PlayerPrefs.GetInt("ColorSeleccionado", 0);

        if (imagenMira == null)
        {
            Debug.LogError("No se asignó la Image de la mira.");
            return;
        }

        if (indiceMira < 0 || indiceMira >= miras.Length)
        {
            Debug.LogError("Índice de mira inválido.");
            return;
        }

        if (indiceColor < 0 || indiceColor >= colores.Length)
        {
            Debug.LogError("Índice de color inválido.");
            return;
        }

        if (miras[indiceMira] == null)
        {
            Debug.LogError("No se asignó el sprite de la mira seleccionada.");
            return;
        }

        imagenMira.sprite = miras[indiceMira];
        imagenMira.color = colores[indiceColor];

        Debug.Log("Mira aplicada: " + imagenMira.sprite.name);
        Debug.Log("Color aplicado: " + colores[indiceColor]);
    }
}

