using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class CrosshairSettings : MonoBehaviour
{
    [Header("Referencias")]
    public TMP_Dropdown dropdownMira;
    public TMP_Dropdown dropdownColor;

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

        dropdownMira.value = Mathf.Clamp(miraGuardada, 0, 2);
        dropdownColor.value = Mathf.Clamp(colorGuardado, 0, 2);

        dropdownMira.RefreshShownValue();
        dropdownColor.RefreshShownValue();
    }

    public void CambiarMira(int indice)
    {
        PlayerPrefs.SetInt("MiraSeleccionada", indice);
        PlayerPrefs.Save();
    }

    public void CambiarColor(int indice)
    {
        PlayerPrefs.SetInt("ColorSeleccionado", indice);
        PlayerPrefs.Save();
    }
}


