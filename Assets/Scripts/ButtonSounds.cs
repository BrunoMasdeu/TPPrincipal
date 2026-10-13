using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(AudioSource))]
public class ButtonSounds : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Header("Sonidos")]
    public AudioClip hoverSound;
    public AudioClip clickSound;

    [Header("Volumen")]
    [Range(0f, 1f)] public float hoverVolume = 0.5f;
    [Range(0f, 1f)] public float clickVolume = 1f;

    AudioSource source;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f; // sonido 2D, ideal para UI
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverSound != null)
            source.PlayOneShot(hoverSound, hoverVolume);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (clickSound != null)
            source.PlayOneShot(clickSound, clickVolume);
    }
}