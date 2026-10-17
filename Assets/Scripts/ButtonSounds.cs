using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonSounds : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public AudioClip hoverSound;
    public AudioClip clickSound;
    [Range(0f, 1f)] public float hoverVolume = 0.5f;
    [Range(0f, 1f)] public float clickVolume = 1f;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverSound != null) UISoundPlayer.Play(hoverSound, hoverVolume);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (clickSound != null) UISoundPlayer.Play(clickSound, clickVolume);
    }
}