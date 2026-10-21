using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public class UnifiedPlayerPrefabTests
{
    private const string PlayerPrefabPath = "Assets/Prefabs/PlayerPrefab.prefab";
    private const string BluePlayerPrefabPath =
        "Assets/Prefabs/Survivalist (3).prefab";
    private const string GrenadePrefabPath = "Assets/Prefabs/Granada.prefab";
    private const string NetworkPrefabsPath =
        "Assets/DefaultNetworkPrefabs.asset";

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
            "crouch",
            "grenades",
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

    [TestCase("KnifeDefinition", "knife.png")]
    [TestCase("PistolDefinition", "pistola.png")]
    [TestCase("RevolverDefinition", "revolver.png")]
    [TestCase("AkDefinition", "icon_weapon_rifle.png")]
    [TestCase("M4Definition", "m4_icon.png")]
    [TestCase("GrappleDefinition", "arpon (2).png")]
    public void DefinicionesDeArmasTienenSpriteParaElHud(
        string definitionName,
        string spriteName)
    {
        WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
            $"Assets/Settings/Weapons/{definitionName}.asset");
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            $"Assets/UI/Icons/Armas/{spriteName}");

        Assert.That(definition, Is.Not.Null, definitionName);
        Assert.That(sprite, Is.Not.Null, spriteName);
        Assert.That(definition.HudSprite, Is.SameAs(sprite), definitionName);
    }

    [TestCase(PlayerPrefabPath)]
    [TestCase(BluePlayerPrefabPath)]
    public void PrefabsDeEquipoConfiguranAgachadoYGranadas(string prefabPath)
    {
        GameObject prefab = LoadPrefab(prefabPath);

        NetworkPlayerCrouch crouch =
            prefab.GetComponent<NetworkPlayerCrouch>();
        Assert.That(crouch, Is.Not.Null,
            $"{prefabPath} necesita NetworkPlayerCrouch.");
        Assert.That(crouch.Capsule, Is.Not.Null,
            $"{prefabPath} no tiene asignado el CapsuleCollider del agachado.");
        Assert.That(crouch.PlayerCamera, Is.Not.Null,
            $"{prefabPath} no tiene asignada la camara del agachado.");

        NetworkPlayerGrenades grenades =
            prefab.GetComponent<NetworkPlayerGrenades>();
        Assert.That(grenades, Is.Not.Null,
            $"{prefabPath} necesita NetworkPlayerGrenades.");
        Assert.That(grenades.HasRequiredReferences, Is.True,
            $"{prefabPath} tiene referencias incompletas para lanzar granadas.");
        Assert.That(grenades.ThrowPoint.name, Is.EqualTo("GrenadeThrowPoint"));

        GameObject expectedGrenade = LoadPrefab(GrenadePrefabPath);
        Assert.That(grenades.GrenadePrefab.gameObject, Is.SameAs(expectedGrenade),
            $"{prefabPath} debe usar el prefab de granada de red definitivo.");
    }

    [Test]
    public void GranadaTieneFisicaVisualLogicaDeRedYRegistro()
    {
        GameObject grenade = LoadPrefab(GrenadePrefabPath);

        Assert.That(grenade.GetComponent<NetworkObject>(), Is.Not.Null);
        Assert.That(grenade.GetComponent<Rigidbody>(), Is.Not.Null);
        Assert.That(grenade.GetComponent<Collider>(), Is.Not.Null);

        NetworkGrenadeProjectile projectile =
            grenade.GetComponent<NetworkGrenadeProjectile>();
        Assert.That(projectile, Is.Not.Null);
        Assert.That(projectile.FuseTime, Is.GreaterThan(0f));
        Assert.That(projectile.ExplosionRadius, Is.GreaterThan(0f));
        Assert.That(projectile.Damage, Is.GreaterThan(0));

        Transform visual = grenade.transform.Find("Visual");
        Assert.That(visual, Is.Not.Null,
            "La granada necesita un hijo Visual separado de su fisica.");
        Assert.That(visual.GetComponentInChildren<Renderer>(true), Is.Not.Null,
            "El hijo Visual necesita una malla renderizable.");

        NetworkPrefabsList networkPrefabs =
            AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(
                NetworkPrefabsPath);
        Assert.That(networkPrefabs, Is.Not.Null,
            $"No existe {NetworkPrefabsPath}.");
        Assert.That(networkPrefabs.Contains(grenade), Is.True,
            "La granada debe estar registrada como prefab de red.");
    }

    private static GameObject LoadPlayerPrefab()
    {
        return LoadPrefab(PlayerPrefabPath);
    }

    private static GameObject LoadPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, $"No existe {path}.");
        return prefab;
    }
}
