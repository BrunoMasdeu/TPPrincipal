using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Pruebas unitarias e integraciones en EditMode para verificar las configuraciones de los bots y el cálculo del área de patrullaje.
/// </summary>
public class TutorialBotConfigurationTests
{
    /// <summary>
    /// Escenario:
    /// - Un GameObject simulando un piso inclinado/rotado y escalado con un <see cref="BoxCollider"/> y <see cref="BotPatrolArea"/>.
    /// 
    /// Comportamiento verificado:
    /// - Verifica que las coordenadas locales consideren correctamente la rotación, escala y el radio del bot.
    /// - Confirma que los puntos dentro de los márgenes retornen <c>true</c> y los puntos cercanos a los bordes exteriores retornen <c>false</c>.
    /// 
    /// Error que pretende detectar:
    /// - Errores de cálculo perimetral al utilizar AABB (límites alineados con los ejes globales) en lugar del Transform local del piso rotado.
    /// </summary>
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

    /// <summary>
    /// Escenario:
    /// - Carga aditiva de la escena de entrenamiento guardada en el proyecto.
    /// 
    /// Comportamiento verificado:
    /// - Verifica que existan exactamente 5 bots en la escena.
    /// - Verifica que exactamente 2 bots tengan habilitada la capacidad de saltar (<c>canJump</c>).
    /// - Comprueba que todos compartan el mismo <see cref="BotPatrolArea"/>, asociado a "Factory1Floor02".
    /// - Valida que cada bot posea NavMeshAgent, Rigidbody kinemático y Animator sin Root Motion.
    /// 
    /// Error que pretende detectar:
    /// - Desconfiguraciones en la escena del tutorial o prefabs de bots (e.g., más/menos bots, jumpers incorrectos o ajustes físicos/animación desalineados).
    /// </summary>
    [Test]
    public void TutorialHasFiveBotsOnOneBakedFloorAndExactlyTwoJumpers()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/CentroEntrenamiento.unity", OpenSceneMode.Additive);
        try
        {
            var bots = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<movimientoBot>(true)).ToArray();
            Assert.That(bots.Length, Is.EqualTo(5));
            Assert.That(bots.Count(b => b.puedeSaltar), Is.EqualTo(2));

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