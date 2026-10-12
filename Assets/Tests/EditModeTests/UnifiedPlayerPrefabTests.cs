using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public class UnifiedPlayerPrefabTests
{
    private const string PlayerPrefabPath = "Assets/Prefabs/PlayerPrefab.prefab";

    [Test]
    public void PrefabDefinitivoTieneComponentesYReferenciasObligatorias()
    {
        GameObject prefab = LoadPlayerPrefab();

        Assert.That(prefab.GetComponent<NetworkPlayerHealth>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<NetworkPlayerCombat>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<NetworkPlayerInventory>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<NetworkPlayerHudPresenter>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<Grappling>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<WallRun>(), Is.Not.Null);

        CtfFlagCarrySocket[] flagSockets =
            prefab.GetComponentsInChildren<CtfFlagCarrySocket>(true);
        Assert.That(flagSockets, Has.Length.EqualTo(1),
            "PlayerPrefab necesita un único CtfFlagCarrySocket.");

        PlayerHudView view = prefab.GetComponentInChildren<PlayerHudView>(true);
        Assert.That(view, Is.Not.Null, "Falta PlayerHudView dentro del prefab.");
        Assert.That(view.HasRequiredReferences, Is.True,
            "PlayerHudView tiene referencias visuales incompletas.");

        PlayerNetworkSetup setup = prefab.GetComponent<PlayerNetworkSetup>();
        SerializedObject serializedSetup = new(setup);
        string[] requiredReferences =
        {
            "playerCamera",
            "audioListener",
            "cameraMovement",
            "move",
            "wallRun",
            "grappling",
            "playerUI",
            "inventory",
            "combat",
            "hudPresenter"
        };

        foreach (string field in requiredReferences)
        {
            Assert.That(
                serializedSetup.FindProperty(field)?.objectReferenceValue,
                Is.Not.Null,
                $"PlayerNetworkSetup no tiene asignado {field}.");
        }
    }

    [Test]
    public void PrefabDefinitivoNoConservaSistemasLegacyNiDuplicadosLocales()
    {
        GameObject prefab = LoadPlayerPrefab();

        Assert.That(prefab.GetComponent<GunSystem>(), Is.Null);
        Assert.That(prefab.GetComponent<ShootingAi>(), Is.Null);
        Assert.That(prefab.GetComponentsInChildren<Camera>(true), Has.Length.EqualTo(1));
        Assert.That(prefab.GetComponentsInChildren<AudioListener>(true), Has.Length.EqualTo(1));
        Assert.That(prefab.GetComponentsInChildren<Canvas>(true), Has.Length.EqualTo(1));
        Assert.That(prefab.GetComponentsInChildren<EventSystem>(true), Is.Empty,
            "El prefab de jugador no debe crear un EventSystem por jugador.");

        int missingScripts = 0;
        foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
        {
            missingScripts += GameObjectUtility
                .GetMonoBehavioursWithMissingScriptCount(child.gameObject);
        }

        Assert.That(missingScripts, Is.Zero,
            "El PlayerPrefab contiene componentes Missing Script.");
    }

    [Test]
    public void InventarioConfiguraTodasLasVisualesYElGanchoComoUtilidad()
    {
        NetworkPlayerInventory inventory =
            LoadPlayerPrefab().GetComponent<NetworkPlayerInventory>();
        SerializedObject serializedInventory = new(inventory);
        SerializedProperty visuals =
            serializedInventory.FindProperty("weaponVisuals");

        Assert.That(visuals, Is.Not.Null);
        Assert.That(visuals.arraySize, Is.EqualTo(6));

        HashSet<int> configuredIds = new();
        for (int index = 0; index < visuals.arraySize; index++)
        {
            SerializedProperty binding = visuals.GetArrayElementAtIndex(index);
            int weaponId = binding.FindPropertyRelative("WeaponId").enumValueIndex;
            Object visual = binding.FindPropertyRelative("Visual").objectReferenceValue;

            Assert.That(weaponId, Is.Not.EqualTo((int)WeaponId.None));
            Assert.That(configuredIds.Add(weaponId), Is.True,
                $"WeaponId repetido en weaponVisuals: {weaponId}.");
            Assert.That(visual, Is.Not.Null,
                $"WeaponId {weaponId} no tiene visual asignada.");
        }

        CollectionAssert.AreEquivalent(
            new[] { 1, 2, 3, 4, 5, 6 },
            configuredIds);

        SerializedProperty definitions =
            serializedInventory.FindProperty("weaponDefinitions");
        WeaponDefinition grapple = null;

        for (int index = 0; index < definitions.arraySize; index++)
        {
            WeaponDefinition definition = definitions
                .GetArrayElementAtIndex(index)
                .objectReferenceValue as WeaponDefinition;
            if (definition != null && definition.Id == WeaponId.Grapple)
            {
                grapple = definition;
                break;
            }
        }

        Assert.That(grapple, Is.Not.Null,
            "El catálogo del inventario necesita la definición del gancho.");
        Assert.That(grapple.Slot, Is.EqualTo(WeaponSlot.Utility));
    }

    private static GameObject LoadPlayerPrefab()
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(prefab, Is.Not.Null, $"No existe {PlayerPrefabPath}.");
        return prefab;
    }
}
