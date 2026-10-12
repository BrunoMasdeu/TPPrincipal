using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Linq;
#endif

/// <summary>
/// Pruebas integradas en PlayMode para simular en tiempo real el comportamiento de movimiento, salto,
/// evasión de paredes/obstáculos dinámicos y recepción de daño de los bots.
/// </summary>
public class TutorialBotPatrolTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private NavMeshData data;
    private BotPatrolArea area;
    private Random.State randomState;

    [SetUp]
    public void SetUp()
    {
        randomState = Random.state;
        Random.InitState(814);
        Time.timeScale = 4f; // Acelera la ejecución de las pruebas
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        objects.Clear();
        if (data != null) Object.DestroyImmediate(data);
        Time.timeScale = 1f;
        Random.state = randomState;
    }

    private BoxCollider Box(string name, Vector3 position, Vector3 size)
    {
        var go = new GameObject(name);
        objects.Add(go);
        go.transform.position = position;
        var box = go.AddComponent<BoxCollider>();
        box.size = size;
        return box;
    }

    private void BuildArea(params BoxCollider[] obstacles)
    {
        BoxCollider floor = Box("TestFloor", new Vector3(0, -0.1f, 0), new Vector3(12, 0.2f, 12));
        area = floor.gameObject.AddComponent<BotPatrolArea>();
        area.enabled = false;
        area.floor = floor;

        var sources = new List<NavMeshBuildSource> { Source(floor, 0) };
        foreach (var obstacle in obstacles) sources.Add(Source(obstacle, 1));

        var settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = 0.42f;
        settings.agentHeight = 1.95f;
        settings.agentClimb = 0.15f;
        settings.overrideVoxelSize = true;
        settings.voxelSize = 0.07f;
        settings.minRegionArea = 0.1f;

        data = NavMeshBuilder.BuildNavMeshData(settings, sources,
            new Bounds(Vector3.zero, new Vector3(14, 8, 14)), Vector3.zero, Quaternion.identity);
        Assert.That(data, Is.Not.Null);

        area.navigationData = data;
        area.enabled = true;
        Physics.SyncTransforms();
    }

    private static NavMeshBuildSource Source(BoxCollider box, int areaIndex) => new NavMeshBuildSource
    {
        shape = NavMeshBuildSourceShape.Box,
        transform = Matrix4x4.TRS(box.transform.TransformPoint(box.center), box.transform.rotation, box.transform.lossyScale),
        size = box.size,
        area = areaIndex
    };

    private movimientoBot Bot(Vector3 position, bool jumps)
    {
        var go = new GameObject("TestBot");
        objects.Add(go);
        go.transform.position = position;

        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.radius = 0.29f;
        capsule.height = 1.9f;
        capsule.center = Vector3.up * 0.95f;

        var bot = go.AddComponent<movimientoBot>();
        bot.patrolArea = area;
        bot.puedeSaltar = jumps;
        bot.jumpInterval = new Vector2(0.6f, 0.8f);
        bot.pauseDuration = new Vector2(0.1f, 0.2f);
        return bot;
    }

    /// <summary>
    /// Comprueba que no exista penetración física profunda entre la cápsula del bot y un obstáculo.
    /// </summary>
    private static void AssertNoPenetration(movimientoBot bot, Collider obstacle)
    {
        var capsule = bot.GetComponent<CapsuleCollider>();
        bool intersects = Physics.ComputePenetration(capsule, bot.transform.position, bot.transform.rotation,
            obstacle, obstacle.transform.position, obstacle.transform.rotation, out _, out float depth);
        Assert.That(intersects && depth > 0.005f, Is.False, $"Bot penetrated {obstacle.name}: {depth}");
    }

    /// <summary>
    /// Escenario:
    /// - Un bot patrullando en un piso con una pared central durante 18 segundos simulados.
    /// 
    /// Comportamiento verificado:
    /// - El bot se desplaza dinámicamente sin quedarse atascado (distancia recorrida > 8m).
    /// - Mantiene todo su cuerpo dentro del perímetro del piso.
    /// - No traspasa las paredes de la escena.
    /// 
    /// Error que pretende detectar:
    /// - Rutas de patrullaje fuera del piso o traspaso de geometrías durante la navegación.
    /// </summary>
    [UnityTest]
    public IEnumerator PatrolAvoidsWallsAndKeepsEntireBodyInsideFloor()
    {
        var wall = Box("Wall", new Vector3(0, 1.5f, 0), new Vector3(0.4f, 3f, 6f));
        BuildArea(wall);
        var bot = Bot(new Vector3(-3, 0.1f, 0), false);
        yield return null;

        Vector3 start = bot.transform.position;
        float travelled = 0;
        Vector3 last = start;
        float end = Time.time + 18f;

        while (Time.time < end)
        {
            yield return null;
            Assert.That(bot.enabled, Is.True);
            Assert.That(area.Contains(bot.transform.position, 0.29f), Is.True);
            AssertNoPenetration(bot, wall);
            travelled += Vector3.Distance(last, bot.transform.position);
            last = bot.transform.position;
        }
        Assert.That(travelled, Is.GreaterThan(8f), "Un bot estático no satisface el patrullaje.");
    }

    /// <summary>
    /// Escenario:
    /// - Tres bots en la escena: dos con capacidad de saltar (<c>canJump = true</c>) y uno terrestre (<c>canJump = false</c>).
    /// 
    /// Comportamiento verificado:
    /// - Los dos bots saltadores se elevan (> 0.5m) y vuelven a aterrizar de forma segura.
    /// - El bot no saltador permanece en el suelo (elevación < 0.15m).
    /// - Ningún bot se traslapa ni atraviesa a los demás bots durante el vuelo o caminata.
    /// 
    /// Error que pretende detectar:
    /// - Interrupciones incorrectas de saltos, fallos en la detección de aterrizaje o colisión entre agentes durante el salto.
    /// </summary>
    [UnityTest]
    public IEnumerator TwoJumpersLandAndThirdBotStaysOnGround()
    {
        BuildArea();
        var bots = new[] { Bot(new Vector3(-3, 0.1f, -3), true),
            Bot(new Vector3(3, 0.1f, 3), true), Bot(new Vector3(-3, 0.1f, 3), false) };
        yield return null;

        var baseline = new[] { bots[0].transform.position.y, bots[1].transform.position.y, bots[2].transform.position.y };
        var maxHeight = new float[3];
        var airborne = new bool[3];
        var landed = new bool[3];
        float end = Time.time + 10f;

        while (Time.time < end)
        {
            yield return null;
            for (int i = 0; i < bots.Length; i++)
            {
                Assert.That(area.Contains(bots[i].transform.position, 0.29f), Is.True);
                maxHeight[i] = Mathf.Max(maxHeight[i], bots[i].transform.position.y - baseline[i]);
                airborne[i] |= bots[i].IsJumping;
                landed[i] |= airborne[i] && !bots[i].IsJumping;
                for (int j = i + 1; j < bots.Length; j++)
                    AssertNoPenetration(bots[i], bots[j].GetComponent<Collider>());
            }
        }

        Assert.That(maxHeight[0], Is.GreaterThan(0.5f));
        Assert.That(maxHeight[1], Is.GreaterThan(0.5f));
        Assert.That(landed[0] && landed[1], Is.True);
        Assert.That(maxHeight[2], Is.LessThan(0.15f));
        Assert.That(airborne[2], Is.False);
    }

    /// <summary>
    /// Escenario:
    /// - Un bot saltador ubicado debajo de un techo bajo (altura 2.25m) e invocaciones seguidas a <see cref="movimientoBot.TakeDamage"/>.
    /// 
    /// Comportamiento verificado:
    /// - El bot detecta la falta de espacio libre superior mediante barrido de cápsula y aborta/cancela la acción de saltar.
    /// - Al recibir daño letal repetido, notifica la destrucción mediante el evento <see cref="movimientoBot.OnTargetDestroyed"/> una sola vez.
    /// 
    /// Error que pretende detectar:
    /// - Saltos atravesando techos u obstáculos superiores y notificaciones duplicadas de muerte en el sistema de eventos.
    /// </summary>
    [UnityTest]
    public IEnumerator LowCeilingPreventsJumpAndLethalDamageNotifiesOnlyOnce()
    {
        BuildArea();
        var ceiling = Box("LowCeiling", new Vector3(0, 2.25f, 0), new Vector3(12, 0.2f, 12));
        var bot = Bot(new Vector3(0, 0.1f, 0), true);
        yield return null;

        float end = Time.time + 3f;
        while (Time.time < end)
        {
            yield return null;
            Assert.That(bot.IsJumping, Is.False);
            AssertNoPenetration(bot, ceiling);
        }

        int deaths = 0;
        System.Action<string> listener = _ => deaths++;
        movimientoBot.OnTargetDestroyed += listener;
        try
        {
            bot.TakeDamage(100);
            bot.TakeDamage(100);
            Assert.That(deaths, Is.EqualTo(1));
        }
        finally { movimientoBot.OnTargetDestroyed -= listener; }

        yield return null;
        Assert.That(bot == null, Is.True);
    }

    /// <summary>
    /// Escenario:
    /// - Se coloca un obstáculo dinámico nuevo en el camino del bot después de haber horneado la navegación.
    /// 
    /// Comportamiento verificado:
    /// - Aunque el NavMesh horneado no conozca la barrera, el control físico mediante CapsuleCast detiene la marcha antes del contacto.
    /// 
    /// Error que pretende detectar:
    /// - Bot atravesando obstáculos dinámicos o recién aparecidos confiando ciegamente únicamente en las rutas del NavMesh.
    /// </summary>
    [UnityTest]
    public IEnumerator NewObstacleInFrontOfBotDoesNotGetCrossed()
    {
        BuildArea();
        var bot = Bot(new Vector3(-3, 0.1f, 0), false);
        yield return new WaitForSeconds(1f);

        // Agrega una barrera dinámica física después del horneado
        var wall = Box("NewBarrier", new Vector3(0, 1.5f, 0), new Vector3(0.4f, 3f, 12));
        bot.transform.position = new Vector3(-3, bot.transform.position.y, 0);
        bot.GetComponent<NavMeshAgent>().Warp(new Vector3(-3, bot.transform.position.y - 0.04f, 0));
        Physics.SyncTransforms();

        float end = Time.time + 8f;
        while (Time.time < end)
        {
            yield return null;
            AssertNoPenetration(bot, wall);
            Assert.That(bot.transform.position.x, Is.LessThan(-0.45f));
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Escenario:
    /// - Carga real en PlayMode de la escena `CentroEntrenamiento.unity` guardada.
    /// 
    /// Comportamiento verificado:
    /// - Simula durante 30 segundos el patrullaje de los 5 bots reales en la escena.
    /// - Verifica que todos los bots patrullen activamente, respeten sus capacidades de salto configuradas, no salgan del área ni penetren obstáculos.
    /// 
    /// Error que pretende detectar:
    /// - Regresiones en la escena real del tutorial, assets de NavMesh corruptos o desincronización en las preferencias de los prefabs.
    /// </summary>
    [UnityTest]
    public IEnumerator SavedTutorialBotsPatrolWithoutLeavingFloorOrPenetratingObstacles()
    {
        UnityEngine.Events.UnityAction<Scene, LoadSceneMode> prepare = (scene, mode) =>
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != null && !(behaviour is movimientoBot) && !(behaviour is BotPatrolArea))
                        behaviour.enabled = false;
        };
        SceneManager.sceneLoaded += prepare;

        try
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/CentroEntrenamiento.unity", new LoadSceneParameters(LoadSceneMode.Additive));
        }
        finally { SceneManager.sceneLoaded -= prepare; }

        var tutorial = SceneManager.GetSceneByPath("Assets/Scenes/CentroEntrenamiento.unity");
        try
        {
            var bots = tutorial.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<movimientoBot>()).ToArray();
            Assert.That(bots.Length, Is.EqualTo(5));
            yield return null;

            var last = bots.Select(b => b.transform.position).ToArray();
            var travelled = new float[5];
            var jumped = new bool[5];
            float end = Time.time + 30f;

            while (Time.time < end)
            {
                yield return null;
                for (int i = 0; i < bots.Length; i++)
                {
                    var bot = bots[i];
                    Assert.That(bot.enabled, Is.True, bot.name);
                    Assert.That(bot.patrolArea.Contains(bot.transform.position, 0.29f), Is.True, bot.name);

                    Vector3 delta = bot.transform.position - last[i];
                    delta.y = 0;
                    travelled[i] += delta.magnitude;
                    last[i] = bot.transform.position;
                    jumped[i] |= bot.IsJumping;

                    Vector3 bottom = bot.transform.position + Vector3.up * 0.29f;
                    Vector3 top = bot.transform.position + Vector3.up * 1.61f;

                    foreach (var other in Physics.OverlapCapsule(bottom, top, 0.29f, ~0, QueryTriggerInteraction.Ignore))
                        if (!other.transform.IsChildOf(bot.transform) && other != bot.patrolArea.floor)
                            AssertNoPenetration(bot, other);
                }
            }

            for (int i = 0; i < bots.Length; i++)
            {
                Assert.That(travelled[i], Is.GreaterThan(5f), bots[i].name + " no patrulló");
                Assert.That(jumped[i], Is.EqualTo(bots[i].puedeSaltar), bots[i].name);
            }
        }
        finally
        {
            foreach (var root in tutorial.GetRootGameObjects()) Object.DestroyImmediate(root);
            SceneManager.UnloadSceneAsync(tutorial);
        }
    }
#endif
}