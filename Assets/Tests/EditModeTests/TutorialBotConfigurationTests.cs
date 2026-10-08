using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public class TutorialBotConfigurationTests
{
    [Test]
    public void PerimeterAccountsForRotatedScaledFloorAndBodyRadius()
    {
        var go = new GameObject("RotatedFloor");
        try
        {
            go.transform.SetPositionAndRotation(new Vector3(5, 0, 8), Quaternion.Euler(0, 37, 0));
            go.transform.localScale = new Vector3(2, 1, 0.5f);
            var floor = go.AddComponent<BoxCollider>();
            floor.size = new Vector3(10, 0.2f, 10);
            var area = go.AddComponent<BotPatrolArea>();
            area.floor = floor;
            Assert.That(area.Contains(go.transform.TransformPoint(new Vector3(4, 20, 3)), 0.3f), Is.True);
            Assert.That(area.Contains(go.transform.TransformPoint(new Vector3(4.95f, 0, 0)), 0.3f), Is.False);
            Assert.That(area.Contains(go.transform.TransformPoint(new Vector3(0, 0, 4.5f)), 0.3f), Is.False);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void TutorialHasFiveBotsOnOneBakedFloorAndExactlyTwoJumpers()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/CentroEntrenamiento.unity", OpenSceneMode.Additive);
        try
        {
            var bots = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<movimientoLateral>(true)).ToArray();
            Assert.That(bots.Length, Is.EqualTo(5));
            Assert.That(bots.Count(b => b.canJump), Is.EqualTo(2));
            BotPatrolArea area = bots[0].patrolArea;
            Assert.That(area, Is.Not.Null);
            Assert.That(area.floor.name, Is.EqualTo("Factory1Floor02"));
            Assert.That(area.navigationData, Is.Not.Null);
            foreach (var bot in bots)
            {
                Assert.That(bot.patrolArea, Is.SameAs(area));
                Assert.That(area.Contains(bot.transform.position, 0.29f), Is.True);
                Assert.That(bot.GetComponent<NavMeshAgent>(), Is.Not.Null);
                Assert.That(bot.GetComponent<Rigidbody>().isKinematic, Is.True);
                Assert.That(bot.GetComponent<Animator>().applyRootMotion, Is.False);
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
}
