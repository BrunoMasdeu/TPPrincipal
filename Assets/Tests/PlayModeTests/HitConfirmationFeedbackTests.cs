using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class HitConfirmationFeedbackTests
{
    private GameObject root;
    private HitConfirmationFeedback feedback;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Feedback test", typeof(RectTransform), typeof(Canvas));
        feedback = root.AddComponent<HitConfirmationFeedback>();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Object.Destroy(root);
        yield return null;
    }

    [UnityTest]
    public IEnumerator HitExpiresAndDoesNotBlockPointer()
    {
        feedback.Present(false);
        Assert.That(feedback.IsVisible, Is.True);
        Assert.That(feedback.IsShowingKill, Is.False);
        Assert.That(root.GetComponentInChildren<CanvasGroup>().blocksRaycasts, Is.False);
        foreach (Image image in root.GetComponentsInChildren<Image>())
            Assert.That(image.raycastTarget, Is.False);
        AudioSource audio = root.GetComponentInChildren<AudioSource>();
        Assert.That(audio.spatialBlend, Is.Zero);
        Assert.That(audio.clip, Is.Not.Null);
        yield return new WaitForSecondsRealtime(.18f);
        Assert.That(feedback.IsVisible, Is.False);
    }

    [UnityTest]
    public IEnumerator KillHasPriorityOverFollowingHitAndDisableClearsIt()
    {
        feedback.Present(false);
        AudioClip hit = root.GetComponentInChildren<AudioSource>().clip;
        feedback.Present(true);
        Assert.That(root.GetComponentInChildren<AudioSource>().clip, Is.Not.SameAs(hit));
        feedback.Present(false);
        Assert.That(feedback.IsShowingKill, Is.True);
        yield return new WaitForSecondsRealtime(.17f);
        Assert.That(feedback.IsVisible, Is.True);
        root.SetActive(false);
        Assert.That(feedback.IsVisible, Is.False);
        root.SetActive(true);
        Assert.That(feedback.IsVisible, Is.False);
    }

    [UnityTest]
    public IEnumerator SurfaceImpactUsesBundledShaderAndCleansUp()
    {
        Material material = Resources.Load<Material>("CombatImpactParticle");
        Assert.That(material, Is.Not.Null);
        Assert.That(material.shader.name, Is.EqualTo("COG/CombatImpactParticle"));
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = new Vector3(1000, 1000, 1000);
        Physics.SyncTransforms();
        Assert.That(Physics.Raycast(wall.transform.position - Vector3.forward * 2f,
            Vector3.forward, out RaycastHit hit, 3f), Is.True);
        SurfaceImpactFeedback.Play(hit);
        var impact = GameObject.Find("Bullet dust");
        Assert.That(impact, Is.Not.Null);
        Assert.That(impact.GetComponent<AudioSource>(), Is.Null);
        Object.Destroy(wall);
        yield return new WaitForSeconds(.6f);
        Assert.That(impact == null, Is.True);
    }
}
