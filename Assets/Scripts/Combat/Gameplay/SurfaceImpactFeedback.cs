using UnityEngine;

/// <summary>Polvo o chispas locales, sin sonido de confirmación de daño.</summary>
public sealed class SurfaceImpactFeedback : MonoBehaviour
{
    public enum Surface { Dust, Metal }
    [SerializeField] private Surface surface;
    private static Material particleMaterial;

    public static void Play(RaycastHit hit)
    {
        if (particleMaterial == null)
            particleMaterial = Resources.Load<Material>("CombatImpactParticle");
        if (particleMaterial == null)
            return;
        var settings = hit.collider.GetComponentInParent<SurfaceImpactFeedback>();
        bool metal = settings != null ? settings.surface == Surface.Metal : IsMetal(hit.collider);
        var go = new GameObject(metal ? "Bullet sparks" : "Bullet dust");
        go.transform.SetPositionAndRotation(hit.point + hit.normal * .015f, Quaternion.LookRotation(hit.normal));
        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = .4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(metal ? 1.5f : .2f, metal ? 3f : .65f);
        main.startSize = new ParticleSystem.MinMaxCurve(metal ? .015f : .04f, metal ? .03f : .09f);
        main.startColor = metal ? new Color(1f, .72f, .3f) : new Color(.6f, .56f, .5f, .55f);
        main.gravityModifier = metal ? .5f : 0f;
        main.maxParticles = 8;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = .01f;
        var fading = particles.colorOverLifetime;
        fading.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fading.color = gradient;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        particles.Play();
        particles.Emit(metal ? 6 : 4);
        Destroy(go, .5f);
    }

    private static bool IsMetal(Collider collider)
    {
        Renderer renderer = collider.GetComponent<Renderer>() ?? collider.GetComponentInParent<Renderer>();
        if (renderer == null) return false;
        Material material = renderer.sharedMaterial;
        return material != null && material.HasProperty("_Metallic") && material.GetFloat("_Metallic") >= .5f;
    }
}
