using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentación local de impactos ya confirmados; no decide daño.</summary>
[DisallowMultipleComponent]
public sealed class HitConfirmationFeedback : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float hitDuration = .12f;
    [SerializeField, Min(0.01f)] private float killDuration = .3f;
    [SerializeField, Range(0f, 1f)] private float volume = .32f;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private AudioClip killClip;

    private RectTransform marker;
    private CanvasGroup opacity;
    private readonly Image[] strokes = new Image[4];
    private AudioSource sound;
    private AudioClip generatedHit;
    private AudioClip generatedKill;
    private float startedAt;
    private float duration;
    private float lastSoundAt = float.NegativeInfinity;
    public bool IsShowingKill { get; private set; }
    public bool IsVisible => marker != null && marker.gameObject.activeSelf;

    public void Present(bool lethal)
    {
        EnsurePresentation();
        // Una granada puede confirmar varios objetivos en el mismo frame.
        if (IsVisible && IsShowingKill && !lethal && Time.unscaledTime < startedAt + duration)
            return;
        IsShowingKill = lethal;
        duration = lethal ? killDuration : hitDuration;
        startedAt = Time.unscaledTime;
        marker.gameObject.SetActive(true);
        opacity.alpha = 1f;
        marker.localScale = Vector3.one * 1.2f;
        foreach (Image stroke in strokes)
        {
            stroke.color = lethal ? new Color(.4f, 1f, 1f) : Color.white;
            stroke.rectTransform.sizeDelta = new Vector2(lethal ? 11f : 7f, lethal ? 3f : 2f);
        }
        // Evita sumar volumen por múltiples impactos simultáneos.
        if (lethal || Time.unscaledTime - lastSoundAt >= .045f)
        {
            sound.Stop();
            sound.clip = lethal ? killClip : hitClip;
            sound.volume = volume;
            sound.Play();
            lastSoundAt = Time.unscaledTime;
        }
    }

    public void Clear()
    {
        if (marker != null)
            marker.gameObject.SetActive(false);
        if (sound != null)
            sound.Stop();
        IsShowingKill = false;
        lastSoundAt = float.NegativeInfinity;
    }

    private void Update()
    {
        if (!IsVisible)
            return;
        float progress = (Time.unscaledTime - startedAt) / duration;
        if (progress >= 1f)
        {
            Clear();
            return;
        }
        marker.localScale = Vector3.one * Mathf.Lerp(1.2f, 1f, Mathf.Clamp01(progress * 3f));
        opacity.alpha = 1f - Mathf.SmoothStep(0f, 1f, progress);
    }

    private void EnsurePresentation()
    {
        if (marker != null)
            return;
        var root = new GameObject("HitConfirmation", typeof(RectTransform), typeof(CanvasGroup));
        marker = (RectTransform)root.transform;
        marker.SetParent(transform, false);
        marker.anchorMin = marker.anchorMax = new Vector2(.5f, .5f);
        marker.anchoredPosition = Vector2.zero;
        marker.sizeDelta = new Vector2(64f, 64f);
        // Último hijo del HUD, también visible sobre la mira del sniper.
        marker.SetAsLastSibling();
        opacity = root.GetComponent<CanvasGroup>();
        opacity.blocksRaycasts = false;
        opacity.interactable = false;
        for (int i = 0; i < strokes.Length; i++)
        {
            var line = new GameObject("Stroke", typeof(RectTransform), typeof(Image), typeof(Outline));
            line.transform.SetParent(marker, false);
            Image stroke = line.GetComponent<Image>();
            strokes[i] = stroke;
            stroke.raycastTarget = false;
            float angle = 45f + 90f * i;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            stroke.rectTransform.anchoredPosition = direction * 13f;
            stroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            Outline outline = line.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, .8f);
            outline.effectDistance = new Vector2(1f, -1f);
        }
        sound = root.AddComponent<AudioSource>();
        sound.playOnAwake = false;
        sound.spatialBlend = 0f;
        sound.loop = false;
        if (hitClip == null)
            hitClip = generatedHit = CreateCue(false);
        if (killClip == null)
            killClip = generatedKill = CreateCue(true);
    }

    // Señales originales, cortas y sin assets externos. Los clips son reemplazables.
    private static AudioClip CreateCue(bool lethal)
    {
        const int sampleRate = 44100;
        float seconds = lethal ? .16f : .055f;
        var samples = new float[Mathf.CeilToInt(seconds * sampleRate)];
        var random = new System.Random(73);
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Min(1f, t / .002f) * Mathf.Exp(-t * (lethal ? 30f : 90f));
            float frequency = lethal ? 680f : 1900f;
            float tone = Mathf.Sin(2f * Mathf.PI * frequency * t);
            float noise = (float)random.NextDouble() * 2f - 1f;
            samples[i] = envelope * (tone * .55f + noise * .25f);
        }
        AudioClip clip = AudioClip.Create(lethal ? "Confirmed kill" : "Confirmed hit", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDisable() => Clear();

    private void OnDestroy()
    {
        if (generatedHit != null) Destroy(generatedHit);
        if (generatedKill != null) Destroy(generatedKill);
    }
}
