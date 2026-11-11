using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CreditsController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private float scrollDuration = 40f;
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip creditsMusic;

    private Coroutine scrollCoroutine;

    private void OnEnable()
    {
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;

        if (audioSource != null && creditsMusic != null)
        {
            audioSource.clip = creditsMusic;
            audioSource.Play();
        }

        if (scrollCoroutine != null)
            StopCoroutine(scrollCoroutine);

        scrollCoroutine = StartCoroutine(AutoScrollRoutine());
    }

    private void OnDisable()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        if (scrollCoroutine != null)
            StopCoroutine(scrollCoroutine);
    }

    private IEnumerator AutoScrollRoutine()
    {
        float elapsedTime = 0f;

        while (elapsedTime < scrollDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / scrollDuration);

            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = Mathf.Lerp(1f, 0f, progress);

            yield return null;
        }

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }
}