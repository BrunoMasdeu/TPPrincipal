using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonSounds : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("EL MOUSE PASO POR ARRIBA DEL BOTON");
        if (UIAudioManager.Instance != null)
        {
            UIAudioManager.Instance.PlayHover();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("HICISTE CLICK EN EL BOTON");
        if (UIAudioManager.Instance != null)
        {
            UIAudioManager.Instance.PlayClick();
        }
    }
}