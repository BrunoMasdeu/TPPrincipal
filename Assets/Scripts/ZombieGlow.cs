using UnityEngine;

[DisallowMultipleComponent]
public class ZombieGlow : MonoBehaviour
{
    public Color glowColor = new Color(0.2f, 1f, 0.2f);
    [Min(0f)] public float intensity = 3f;

    public bool useLight = false;
    [Min(0f)] public float lightRange = 6f;
    [Min(0f)] public float lightIntensity = 2f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Material glowMaterial;
    private Light glowLight;

    private void Awake()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Material original = null;
        foreach (Renderer r in renderers)
        {
            if (r.sharedMaterial != null)
            {
                original = r.sharedMaterial;
                break;
            }
        }

        if (original == null)
            return;

        if (!original.HasProperty(EmissionColorId))
        {
            Debug.LogWarning(
                "ZombieGlow: el shader del material '" + original.name +
                "' no tiene emisión. Usá un shader Lit de URP.");
            return;
        }

        glowMaterial = new Material(original);
        glowMaterial.EnableKeyword("_EMISSION");
        glowMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        glowMaterial.SetColor(EmissionColorId, glowColor * intensity);

        foreach (Renderer r in renderers)
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == original)
                    mats[i] = glowMaterial;
            }
            r.sharedMaterials = mats;
        }

        if (useLight)
            CreateLight();
    }

    private void CreateLight()
    {
        GameObject lightObject = new GameObject("ZombieGlowLight");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = Vector3.up * 1.2f;

        glowLight = lightObject.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = glowColor;
        glowLight.range = lightRange;
        glowLight.intensity = lightIntensity;
        glowLight.shadows = LightShadows.None;
    }

    public void StopGlow()
    {
        if (glowMaterial != null)
            glowMaterial.SetColor(EmissionColorId, Color.black);

        if (glowLight != null)
            glowLight.enabled = false;
    }

    private void OnDestroy()
    {
        if (glowMaterial != null)
            Destroy(glowMaterial);
    }
}