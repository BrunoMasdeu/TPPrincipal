using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// [ZOMBIES] Rehace la navegación de MapaZombies en una sola malla para todo el mapa.
///
/// Qué hace:
/// 1. Apaga los 30 NavMeshSurface de las piezas de calle (no los borra).
/// 2. Hornea una sola malla con los colliders del mapa (lo mismo que pisa el jugador).
/// 3. La guarda en Assets/Scenes/MapaZombies/NavegacionZombies.asset y la carga
///    con el objeto "NavegacionZombies" de la escena.
///
/// Se puede volver a correr cuando cambie el mapa: no duplica nada.
/// </summary>
public static class ZombiesNavegacionSetup
{
    private const string ScenePath = "Assets/Scenes/MapaZombies.unity";
    private const string DataPath = "Assets/Scenes/MapaZombies/NavegacionZombies.asset";

    [MenuItem("Tools/Zombies/Regenerar navegacion de MapaZombies")]
    public static void Regenerar()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Navegación zombies", "Salí del modo Play primero.", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath);

        // 1. Apagar las mallas viejas (una por pieza de calle).
        int apagadas = 0;
        foreach (MonoBehaviour componente in Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (componente == null || componente.GetType().Name != "NavMeshSurface" || !componente.enabled)
                continue;

            componente.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(componente);
            EditorUtility.SetDirty(componente);
            apagadas++;
        }

        // 2. Juntar los colliders del mapa (sin triggers, jugador, zombies ni objetos de red).
        Physics.SyncTransforms();
        var fuentes = new List<NavMeshBuildSource>();
        Bounds limites = new Bounds(Vector3.zero, Vector3.zero);
        bool hayLimites = false;

        foreach (Collider collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (collider.isTrigger || collider.gameObject.scene != scene)
                continue;

            if (hayLimites) limites.Encapsulate(collider.bounds);
            else { limites = collider.bounds; hayLimites = true; }
        }

        if (!hayLimites)
        {
            Debug.LogError("Navegación zombies: no se encontraron colliders en MapaZombies.");
            return;
        }

        limites.Expand(new Vector3(2f, 4f, 2f));

        UnityEngine.AI.NavMeshBuilder.CollectSources(limites, ~0, NavMeshCollectGeometry.PhysicsColliders,
            0, new List<NavMeshBuildMarkup>(), fuentes);

        fuentes.RemoveAll(fuente => fuente.component == null ||
            fuente.component.gameObject.scene != scene ||
            (fuente.component is Collider c && c.isTrigger) ||
            fuente.component.GetComponentInParent<Zombie>() != null ||
            fuente.component.GetComponentInParent<Move>() != null ||
            fuente.component.GetComponentInParent<Unity.Netcode.NetworkObject>() != null);

        // 3. Hornear con el agente por defecto (el que usan los zombies).
        NavMeshBuildSettings ajustes = NavMesh.GetSettingsByID(0);
        NavMeshData datos = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(ajustes, fuentes, limites,
            Vector3.zero, Quaternion.identity);

        if (datos == null)
        {
            Debug.LogError("Navegación zombies: no se pudo hornear la malla.");
            return;
        }

        datos.name = "NavegacionZombies";
        NavMeshData existente = AssetDatabase.LoadAssetAtPath<NavMeshData>(DataPath);
        if (existente == null)
        {
            AssetDatabase.CreateAsset(datos, DataPath);
        }
        else
        {
            EditorUtility.CopySerialized(datos, existente);
            Object.DestroyImmediate(datos);
            datos = existente;
            EditorUtility.SetDirty(datos);
        }

        // 4. Objeto de la escena que carga la malla al jugar.
        NavegacionZombies cargador = Object.FindAnyObjectByType<NavegacionZombies>(FindObjectsInactive.Include);
        if (cargador == null)
        {
            GameObject objeto = new GameObject("NavegacionZombies");
            SceneManager_MoveToScene(objeto, scene);
            cargador = objeto.AddComponent<NavegacionZombies>();
        }

        cargador.datos = datos;
        EditorUtility.SetDirty(cargador);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"Navegación zombies lista: {apagadas} mallas viejas apagadas, " +
                  $"{fuentes.Count} colliders usados, límites {limites.size}.");
    }

    private static void SceneManager_MoveToScene(GameObject objeto, UnityEngine.SceneManagement.Scene scene)
    {
        if (objeto.scene != scene)
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(objeto, scene);
    }
}
