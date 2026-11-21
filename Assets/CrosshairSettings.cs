using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

public class CrosshairSettings : MonoBehaviour
{
    [Header("Referencias")]
    public TMP_Dropdown dropdownMira;
    public TMP_Dropdown dropdownColor;
    [SerializeField] private Slider sliderSensibilidad;

    public static event System.Action MiraCambiada;

    private void Start()
    {
        dropdownMira.ClearOptions();
        dropdownMira.AddOptions(new List<string>
        {
            "Mira 1",
            "Mira 2",
            "Mira 3"
        });

        dropdownColor.ClearOptions();
        dropdownColor.AddOptions(new List<string>
        {
            "Negro",
            "Blanco",
            "Rosa"
        });

        dropdownMira.onValueChanged.AddListener(CambiarMira);
        dropdownColor.onValueChanged.AddListener(CambiarColor);

        int miraGuardada = PlayerPrefs.GetInt("MiraSeleccionada", 0);
        int colorGuardado = PlayerPrefs.GetInt("ColorSeleccionado", 0);

        dropdownMira.SetValueWithoutNotify(Mathf.Clamp(miraGuardada, 0, 2));
        dropdownColor.SetValueWithoutNotify(Mathf.Clamp(colorGuardado, 0, 2));

        dropdownMira.RefreshShownValue();
        dropdownColor.RefreshShownValue();

        if (sliderSensibilidad != null)
        {
            float sensibilidad = PlayerPrefs.GetFloat(CameraMovement.ClaveSensibilidad, Move.SensibilidadPredeterminada);
            sliderSensibilidad.SetValueWithoutNotify(sensibilidad);
            sliderSensibilidad.onValueChanged.AddListener(GuardarSensibilidad);
        }
    }

    private void GuardarSensibilidad(float valor)
    {
        PlayerPrefs.SetFloat(CameraMovement.ClaveSensibilidad, valor);
        PlayerPrefs.Save();
    }

    public void CambiarMira(int indice)
    {
        PlayerPrefs.SetInt("MiraSeleccionada", indice);
        PlayerPrefs.Save();
        MiraCambiada?.Invoke();
    }

    public void CambiarColor(int indice)
    {
        PlayerPrefs.SetInt("ColorSeleccionado", indice);
        PlayerPrefs.Save();
        MiraCambiada?.Invoke();
    }
}


