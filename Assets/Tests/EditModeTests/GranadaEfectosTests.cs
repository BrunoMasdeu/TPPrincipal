using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [ANIMACIONES] Revisa que la granada tenga efecto y sonidos asignados
/// (menú Tools > Player > Agregar Efectos a la Granada).
/// </summary>
public class GranadaEfectosTests
{
    private const string GranadaPath = "Assets/Prefabs/Granada.prefab";

    [Test]
    public void LaGranadaTieneEfectoDeExplosionYSonidos()
    {
        GameObject granada = AssetDatabase.LoadAssetAtPath<GameObject>(GranadaPath);
        Assert.That(granada, Is.Not.Null, $"No existe {GranadaPath}.");

        GrenadeEffects efectos = granada.GetComponent<GrenadeEffects>();
        Assert.That(efectos, Is.Not.Null,
            "Falta GrenadeEffects: correr Tools > Player > Agregar Efectos a la Granada.");

        Assert.That(efectos.ExplosionPrefab, Is.Not.Null, "Falta el efecto de explosión.");
        Assert.That(efectos.ExplosionPrefab.GetComponentInChildren<ParticleSystem>(true),
            Is.Not.Null, "El efecto de explosión necesita partículas.");
        Assert.That(efectos.ExplosionSounds, Is.Not.Null.And.Not.Empty,
            "Faltan los sonidos de explosión.");
        Assert.That(efectos.ExplosionSounds, Has.None.Null);
        Assert.That(efectos.ThrowSound, Is.Not.Null, "Falta el sonido de lanzamiento.");
    }

    [Test]
    public void LosMaterialesDelEfectoSonDeURP()
    {
        GameObject granada = AssetDatabase.LoadAssetAtPath<GameObject>(GranadaPath);
        GrenadeEffects efectos = granada.GetComponent<GrenadeEffects>();
        Assert.That(efectos, Is.Not.Null);

        foreach (var renderer in efectos.ExplosionPrefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)))
                    continue;
                Assert.That(material.shader.name, Does.Contain("Universal Render Pipeline"),
                    $"'{material.name}' no es de URP y se vería rosa.");
            }
        }
    }
}
