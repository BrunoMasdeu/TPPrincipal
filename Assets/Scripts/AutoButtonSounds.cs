using UnityEngine;
using UnityEngine.UI;

public class AutoButtonSounds : MonoBehaviour
{
    public AudioClip hoverSound;
    public AudioClip clickSound;
    [Range(0f, 1f)] public float hoverVolume = 0.5f;
    [Range(0f, 1f)] public float clickVolume = 1f;

    void Awake()
    {
        // true = incluye objetos desactivados
        foreach (var item in GetComponentsInChildren<Selectable>(true))
        {
            if (item.GetComponent<ButtonSounds>() != null) continue;

            var s = item.gameObject.AddComponent<ButtonSounds>();
            s.hoverSound = hoverSound;
            s.clickSound = clickSound;
            s.hoverVolume = hoverVolume;
            s.clickVolume = clickVolume;
        }
    }
}