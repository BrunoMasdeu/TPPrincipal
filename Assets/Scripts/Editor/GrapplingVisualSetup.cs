using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class GrapplingVisualSetup
{
    private const string NetworkPlayerPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string HeadModelPath = "Assets/Grappling/Models/Hookshot_head.fbx";
    private const string MetalTexturePath = "Assets/Grappling/Textures/Gradient Pallete Metal.png";
    private const string MaterialsFolder = "Assets/Grappling/Materials";

    [MenuItem("Tools/Grappling/Configurar NetworkPlayer")]
    public static void ConfigureNetworkPlayer()
    {
        GameObject prefabRoot = null;

        try
        {
            GameObject headModel = LoadRequiredAsset<GameObject>(HeadModelPath);
            Material metalMaterial = CreateOrUpdateLitMaterial(
                $"{MaterialsFolder}/HookshotMetal.mat",
                MetalTexturePath,
                0.8f,
                0.55f);
            Material cableMaterial = CreateOrUpdateCableMaterial();

            prefabRoot = PrefabUtility.LoadPrefabContents(NetworkPlayerPath);
            Grappling grappling = prefabRoot.GetComponent<Grappling>();
            if (grappling == null)
                throw new InvalidOperationException("NetworkPlayer no contiene el componente Grappling.");

            Transform rightHand = FindChildRecursive(prefabRoot.transform, "hand.R");
            if (rightHand == null)
                throw new InvalidOperationException("No se encontró el hueso hand.R en NetworkPlayer.");

            RemoveExistingVisual(prefabRoot.transform, rightHand);
            Bounds characterBounds = CalculateCharacterBounds(prefabRoot);
            float characterHeight = characterBounds.size.y;

            GameObject cableOrigin = CreateChild("CableOrigin", rightHand);

            GameObject visualRoot = CreateChild("GrapplingVisuals", prefabRoot.transform);

            GameObject cableObject = CreateChild("GrappleCable", visualRoot.transform);
            LineRenderer lineRenderer = cableObject.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lineRenderer, cableMaterial, characterHeight);

            GameObject hookHead = CreateChild("GrappleHookHead", visualRoot.transform);
            GameObject headInstance = InstantiateModel(headModel, hookHead.transform, "HookshotHeadModel");
            ApplyMaterial(headInstance, metalMaterial);
            ScaleModelToWorldSize(headInstance, characterHeight * 0.14f);
            hookHead.SetActive(false);

            SerializedObject serializedGrappling = new SerializedObject(grappling);
            serializedGrappling.FindProperty("gunTip").objectReferenceValue = cableOrigin.transform;
            serializedGrappling.FindProperty("lr").objectReferenceValue = lineRenderer;
            serializedGrappling.FindProperty("hookHead").objectReferenceValue = hookHead.transform;
            serializedGrappling.FindProperty("hookSurfaceOffset").floatValue = characterHeight * 0.02f;
            serializedGrappling.FindProperty("hookHeadRotationOffset").vector3Value = Vector3.zero;
            serializedGrappling.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, NetworkPlayerPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Gancho visual configurado correctamente en NetworkPlayer.prefab.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"No se pudo configurar el gancho visual: {exception}");
            throw;
        }
        finally
        {
            if (prefabRoot != null)
                PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static T LoadRequiredAsset<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new InvalidOperationException($"No se encontró el asset requerido: {path}");

        return asset;
    }

    private static Material CreateOrUpdateLitMaterial(
        string materialPath,
        string texturePath,
        float metallic,
        float smoothness)
    {
        EnsureFolder(MaterialsFolder);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("No se encontró el shader Universal Render Pipeline/Lit.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }

        material.shader = shader;
        material.name = System.IO.Path.GetFileNameWithoutExtension(materialPath);
        material.SetTexture("_BaseMap", LoadRequiredAsset<Texture2D>(texturePath));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOrUpdateCableMaterial()
    {
        EnsureFolder(MaterialsFolder);
        const string materialPath = MaterialsFolder + "/GrappleCable.mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            throw new InvalidOperationException("No se encontró el shader Universal Render Pipeline/Unlit.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }

        Color cableColor = new Color(1f, 0.55f, 0.03f, 1f);
        material.shader = shader;
        material.name = "GrappleCable";
        material.SetColor("_BaseColor", cableColor);
        material.SetColor("_Color", cableColor);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureLineRenderer(
        LineRenderer lineRenderer,
        Material material,
        float characterHeight)
    {
        lineRenderer.enabled = false;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = characterHeight * 0.012f;
        lineRenderer.endWidth = characterHeight * 0.007f;
        lineRenderer.numCapVertices = 4;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.sharedMaterial = material;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.lightProbeUsage = LightProbeUsage.Off;
        lineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private static GameObject InstantiateModel(GameObject model, Transform parent, string objectName)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(model, parent) as GameObject;
        if (instance == null)
            throw new InvalidOperationException($"No se pudo instanciar el modelo {model.name}.");

        instance.name = objectName;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        return instance;
    }

    private static void ApplyMaterial(GameObject model, Material material)
    {
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
                materials[index] = material;
            renderer.sharedMaterials = materials;
        }
    }

    private static void ScaleModelToWorldSize(GameObject model, float targetMaximumSize)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = CalculateRendererBounds(renderers);

        float maximumSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maximumSize > Mathf.Epsilon)
            model.transform.localScale *= targetMaximumSize / maximumSize;

        Bounds scaledBounds = CalculateRendererBounds(renderers);
        Vector3 mountPosition = model.transform.parent.position;
        model.transform.position += mountPosition - scaledBounds.center;
    }

    private static Bounds CalculateRendererBounds(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);

        return bounds;
    }

    private static Bounds CalculateCharacterBounds(GameObject prefabRoot)
    {
        Renderer[] renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException("NetworkPlayer no contiene renderers para calcular su escala visual.");

        return CalculateRendererBounds(renderers);
    }

    private static GameObject CreateChild(string name, Transform parent)
    {
        GameObject child = new GameObject(name);
        child.layer = parent.gameObject.layer;
        child.transform.SetParent(parent, false);
        return child;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform match = FindChildRecursive(child, childName);
            if (match != null)
                return match;
        }

        return null;
    }

    private static void RemoveExistingVisual(Transform prefabRoot, Transform rightHand)
    {
        Transform visualRoot = prefabRoot.Find("GrapplingVisuals");
        if (visualRoot != null)
            UnityEngine.Object.DestroyImmediate(visualRoot.gameObject);

        Transform launcher = FindChildRecursive(prefabRoot, "GrapplingLauncher");
        if (launcher != null)
            UnityEngine.Object.DestroyImmediate(launcher.gameObject);

        Transform cableOrigin = rightHand.Find("CableOrigin");
        if (cableOrigin != null)
            UnityEngine.Object.DestroyImmediate(cableOrigin.gameObject);
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string currentPath = parts[0];

        for (int index = 1; index < parts.Length; index++)
        {
            string nextPath = currentPath + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(nextPath))
                AssetDatabase.CreateFolder(currentPath, parts[index]);
            currentPath = nextPath;
        }
    }
}
