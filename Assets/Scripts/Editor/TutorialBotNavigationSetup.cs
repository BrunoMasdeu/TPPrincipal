using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Herramienta de Editor para configurar automáticamente la navegación y los parámetros de los bots en la escena del tutorial.
/// 
/// Responsabilidad:
/// - Abre la escena de entrenamiento, identifica la instancia correcta del piso "Factory1Floor02".
/// - Recolecta las fuentes geométricas de la escena, hornea y guarda el asset de <see cref="NavMeshData"/>.
/// - Configura los componentes <see cref="movimientoLateral"/>, <see cref="NavMeshAgent"/>, <see cref="Rigidbody"/> y <see cref="Animator"/> de los 5 bots.
/// </summary>
public static class TutorialBotNavigationSetup
{
    private const string ScenePath = "Assets/Scenes/CentroEntrenamiento.unity";
    private const string DataPath = "Assets/Scenes/CentroEntrenamiento/BotPatrolNavMesh.asset";

    /// <summary>
    /// Punto de entrada del menú de herramientas para generar la malla de navegación y asignar configuraciones.
    /// </summary>
    [MenuItem("Tools/Tutorial/Configurar y regenerar navegación de bots")]
    public static void Configure()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath);

        movimientoBot[] bots = UnityEngine.Object.FindObjectsByType<movimientoBot>(FindObjectsSortMode.None);
        if (bots.Length != 5) throw new InvalidOperationException("Se esperaban cinco bots en el tutorial.");

        // Selección de la instancia correcta de Factory1Floor02:
        // Existen dos instancias con el mismo nombre en la escena. Se selecciona el piso que contiene a la mayoría de los bots debajo/encima de él,
        // evitando seleccionarlo mediante un Find() arbitrario sin garantías.
        BoxCollider floor = UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)
            .Where(c => c.name == "Factory1Floor02")
            .OrderByDescending(c => bots.Count(b => c.bounds.min.x <= b.transform.position.x &&
                c.bounds.max.x >= b.transform.position.x && c.bounds.min.z <= b.transform.position.z &&
                c.bounds.max.z >= b.transform.position.z)).First();

        BotPatrolArea area = floor.GetComponent<BotPatrolArea>();
        if (area == null) area = floor.gameObject.AddComponent<BotPatrolArea>();
        area.floor = floor;

        Physics.SyncTransforms();

        // Recolección y filtrado de fuentes para el horneado de la navegación
        var sources = new List<NavMeshBuildSource>();
        Bounds bounds = floor.bounds;
        bounds.Expand(new Vector3(2f, 10f, 2f));
        UnityEngine.AI.NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders,
            1, new List<NavMeshBuildMarkup>(), sources);

        // Se excluyen triggers, componentes propios de bots o jugadores para no obstaculizar la malla de navegación.
        sources.RemoveAll(s => s.component == null || s.component.gameObject.scene != scene ||
            s.component.GetComponentInParent<movimientoBot>() != null ||
            s.component.GetComponentInParent<CharacterController>() != null ||
            s.component.GetComponentInParent<Move>() != null ||
            (s.component is Collider c && c.isTrigger));

        for (int i = 0; i < sources.Count; i++)
        {
            NavMeshBuildSource source = sources[i];
            source.area = source.component == floor ? 0 : 1;
            sources[i] = source;
        }

        if (!sources.Any(s => s.component == floor)) throw new InvalidOperationException("No se encontró el collider del piso.");

        // Ajustes de precisión para el horneado de NavMesh (dimensiones del agente y voxelización)
        var settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = 0.42f;
        settings.agentHeight = 1.95f;
        settings.agentClimb = 0.15f;
        settings.overrideVoxelSize = true;
        settings.voxelSize = 0.07f;
        settings.minRegionArea = 0.1f;

        // Generación y guardado/actualización del asset de NavMeshData
        NavMeshData data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources, bounds,
            Vector3.zero, Quaternion.identity);
        if (data == null) throw new InvalidOperationException("No se pudo construir la navegación.");

        data.name = "BotPatrolNavMesh";
        NavMeshData existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(DataPath);
        if (existing == null) AssetDatabase.CreateAsset(data, DataPath);
        else
        {
            EditorUtility.CopySerialized(data, existing);
            UnityEngine.Object.DestroyImmediate(data);
            data = existing;
            EditorUtility.SetDirty(data);
        }

        area.navigationData = data;
        EditorUtility.SetDirty(area);
        PrefabUtility.RecordPrefabInstancePropertyModifications(area);

        // Configuración individual de cada bot
        foreach (movimientoBot bot in bots)
        {
            bot.patrolArea = area;
            bot.puedeSaltar = bot.name == "bot2" || bot.name == "bot3";
            if (bot.GetComponent<NavMeshAgent>() == null) bot.gameObject.AddComponent<NavMeshAgent>();

            Rigidbody body = bot.GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var animator = bot.GetComponent<Animator>();
            animator.applyRootMotion = false;

            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            EditorUtility.SetDirty(bot);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"Tutorial navigation configured: {bots.Length} bots, 2 jumpers; floor {floor.bounds}; {sources.Count} sources.");
    }
}