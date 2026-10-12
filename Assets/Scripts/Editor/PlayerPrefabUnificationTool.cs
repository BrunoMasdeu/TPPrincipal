using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Migracion idempotente del PlayerPrefab de red al HUD visual de Juan.
/// Se ejecuta desde Unity para que PrefabUtility preserve correctamente las
/// referencias internas y la instancia anidada de PlayerUI.
/// </summary>
public static class PlayerPrefabUnificationTool
{
    private const string PlayerPrefabPath = "Assets/Prefabs/PlayerPrefab.prefab";
    private const string PlayerUiPath = "Assets/Prefabs/PlayerUI.prefab";

    [MenuItem("Tools/Player/Unificar PlayerPrefab")]
    public static void UnifyPlayerPrefab()
    {
        ConfigurePlayerUiPrefab();
        ConfigureNetworkPlayerPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "[PlayerPrefab] Unificacion completada: HUD nuevo, presentador de red " +
            "y referencias locales configuradas.");
    }

    [MenuItem("Tools/Player/Unificar PlayerPrefab", true)]
    private static bool ValidateUnifyPlayerPrefab() =>
        AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) != null &&
        AssetDatabase.LoadAssetAtPath<GameObject>(PlayerUiPath) != null;

    private static void ConfigurePlayerUiPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerUiPath);

        try
        {
            PlayerHudView view = root.GetComponent<PlayerHudView>();
            if (view == null)
                view = root.AddComponent<PlayerHudView>();

            view.ResolveReferences();
            if (!view.HasRequiredReferences)
            {
                throw new InvalidOperationException(
                    "PlayerUI no contiene Salud, Cargador y las cuatro ranuras esperadas.");
            }

            EditorUtility.SetDirty(view);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerUiPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureNetworkPlayerPrefab()
    {
        GameObject playerUiAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(PlayerUiPath);
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        try
        {
            Transform previousUi = root.transform.Find("PlayerUI");
            if (previousUi != null)
                UnityEngine.Object.DestroyImmediate(previousUi.gameObject);

            GameObject playerUi = (GameObject)PrefabUtility.InstantiatePrefab(
                playerUiAsset,
                root.transform);
            playerUi.name = "PlayerUI";
            playerUi.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            playerUi.transform.localScale = Vector3.one;

            PlayerHudView view = playerUi.GetComponent<PlayerHudView>();
            Canvas canvas = playerUi.GetComponent<Canvas>();
            if (view == null || canvas == null)
            {
                throw new InvalidOperationException(
                    "El prefab PlayerUI no tiene PlayerHudView y Canvas configurados.");
            }

            RemoveLegacyComponents<GunSystem>(root);
            RemoveLegacyComponents<ShootingAi>(root);

            NetworkPlayerInventory inventory =
                RequireComponent<NetworkPlayerInventory>(root);
            NetworkPlayerCombat combat = RequireComponent<NetworkPlayerCombat>(root);
            NetworkPlayerHudPresenter presenter =
                root.GetComponent<NetworkPlayerHudPresenter>() ??
                root.AddComponent<NetworkPlayerHudPresenter>();
            PlayerNetworkSetup setup = RequireComponent<PlayerNetworkSetup>(root);

            SerializedObject serializedPresenter = new(presenter);
            SetObjectReference(serializedPresenter, "view", view);
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedSetup = new(setup);
            SetObjectReference(serializedSetup, "playerUI", canvas);
            SetObjectReference(serializedSetup, "inventory", inventory);
            SetObjectReference(serializedSetup, "combat", combat);
            SetObjectReference(serializedSetup, "hudPresenter", presenter);
            serializedSetup.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(presenter);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static T RequireComponent<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>();
        if (component == null)
        {
            throw new InvalidOperationException(
                $"{root.name} no contiene el componente requerido {typeof(T).Name}.");
        }

        return component;
    }

    private static void RemoveLegacyComponents<T>(GameObject root)
        where T : Component
    {
        foreach (T component in root.GetComponents<T>())
            UnityEngine.Object.DestroyImmediate(component);
    }

    private static void SetObjectReference(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                $"No existe la propiedad serializada {propertyName} en " +
                serializedObject.targetObject.GetType().Name + ".");
        }

        property.objectReferenceValue = value;
    }
}
