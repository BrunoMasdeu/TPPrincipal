using UnityEngine;

public static class UISoundPlayer
{
    static AudioSource source;

    public static void Play(AudioClip clip, float volume)
    {
        if (source == null)
        {
            var go = new GameObject("UISoundPlayer");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }
        source.PlayOneShot(clip, volume);
    }
}