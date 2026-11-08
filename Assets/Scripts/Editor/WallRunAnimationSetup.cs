using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Imports Blender cycles and places them below the existing weapon layer.</summary>
public static class WallRunAnimationSetup
{
    private const string Folder = "Assets/Animations/WallRun/";
    private const string Model = "Assets/Prefabs/Soldier/Mark_1_Olive_IdlePose.fbx";

    [MenuItem("Tools/Player/Build Wall Run Animations")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        var left = Import("Left");
        var right = Import("Right");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Animations/PlayerAnimatorController.controller");
        if (!controller.parameters.Any(p => p.name == "WallRunSide"))
            controller.AddParameter("WallRunSide", AnimatorControllerParameterType.Int);
        if (!controller.parameters.Any(p => p.name == "WallRunRate"))
            controller.AddParameter(new AnimatorControllerParameter { name = "WallRunRate",
                type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        // Rebuild only this layer; upper-body pose/fire/reload states remain intact.
        int existing = Array.FindIndex(controller.layers, l => l.name == "Wall Run");
        if (existing >= 0) controller.RemoveLayer(existing);
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(Folder + "WallRunMask.mask");
        if (mask == null)
        {
            mask = new AvatarMask { name = "WallRunMask" };
            AssetDatabase.CreateAsset(mask, Folder + "WallRunMask.mask");
        }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,
                i != (int)AvatarMaskBodyPart.Root &&
                i != (int)AvatarMaskBodyPart.LeftFootIK && i != (int)AvatarMaskBodyPart.RightFootIK &&
                i != (int)AvatarMaskBodyPart.LeftHandIK && i != (int)AvatarMaskBodyPart.RightHandIK);
        controller.AddLayer("Wall Run");
        var layers = controller.layers.ToList();
        var wall = layers[layers.Count - 1];
        layers.RemoveAt(layers.Count - 1);
        wall.avatarMask = mask;
        wall.defaultWeight = 0f;
        wall.blendingMode = AnimatorLayerBlendingMode.Override;
        var lstate = wall.stateMachine.AddState("Wall Run Left", new Vector3(250, 80));
        var rstate = wall.stateMachine.AddState("Wall Run Right", new Vector3(250, 180));
        lstate.motion = left;
        rstate.motion = right;
        foreach (var state in new[] { lstate, rstate })
        {
            state.writeDefaultValues = false;
            state.speedParameter = "WallRunRate";
            state.speedParameterActive = true;
        }
        wall.stateMachine.defaultState = lstate;
        Transition(lstate, rstate, 1);
        Transition(rstate, lstate, -1);
        layers.Insert(layers.FindIndex(l => l.name == "Upper Body"), wall);
        controller.layers = layers.ToArray();
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(mask);
        AssetDatabase.SaveAssets();
        Debug.Log("WALLRUN_SETUP_OK: left/right humanoid cycles below Upper Body.");
    }

    private static void Transition(AnimatorState from, AnimatorState to, int side)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.12f;
        transition.AddCondition(AnimatorConditionMode.Equals, side, "WallRunSide");
    }

    // Batch entry point: render the actual prefab and weapon layer for visual QA.
    public static void RenderPreviews()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        var player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/PlayerPrefab.prefab"));
        player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (var camera in player.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (var canvas in player.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
        var inventory = new SerializedObject(player.GetComponent<NetworkPlayerInventory>());
        var bindings = inventory.FindProperty("weaponVisuals");
        var animator = player.GetComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        int wall = animator.GetLayerIndex("Wall Run");
        int upper = animator.GetLayerIndex("Upper Body");
        animator.SetLayerWeight(wall, 1);
        animator.SetFloat("WallRunRate", 1);
        animator.SetInteger("WallRunSide", 1);
        var cam = new GameObject("Preview Camera").AddComponent<Camera>();
        cam.transform.position = new Vector3(-2.8f, 1.8f, 3.5f);
        cam.transform.LookAt(new Vector3(0, 0.9f, 0));
        cam.orthographic = true;
        cam.orthographicSize = 1.05f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.14f, 0.17f, 0.21f);
        var light = new GameObject("Key").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2;
        light.transform.rotation = Quaternion.Euler(40, -30, 0);
        RenderSettings.ambientLight = Color.gray;
        var rt = new RenderTexture(800, 800, 24);
        cam.targetTexture = rt;
        System.IO.Directory.CreateDirectory("ArtSource/WallRun");
        foreach (string weapon in new[] { "Pistol", "Rifle" })
        {
            var weaponId = weapon == "Pistol" ? WeaponId.Pistol : WeaponId.AK;
            for (int i = 0; i < bindings.arraySize; i++)
            {
                var binding = bindings.GetArrayElementAtIndex(i);
                var visual = binding.FindPropertyRelative("Visual").objectReferenceValue as GameObject;
                if (visual != null)
                {
                    visual.SetActive(binding.FindPropertyRelative("WeaponId").intValue == (int)weaponId);
                    if (visual.activeSelf)
                        for (var p = visual.transform.parent; p != null; p = p.parent) p.gameObject.SetActive(true);
                }
            }
            animator.SetInteger("WeaponPose", weapon == "Pistol" ? 1 : 2);
            foreach (string action in new[] { "Pose", "Fire" })
            {
                animator.Play("Wall Run Right", wall, 0.125f);
                animator.Play(weapon + " Combat " + action, upper, 0.2f);
                animator.Update(0);
                // Multiple Camera.Render calls in one editor frame can reuse the
                // first GPU skinning result. Bake each pose for an accurate still.
                var skins = player.GetComponentsInChildren<SkinnedMeshRenderer>();
                var baked = new System.Collections.Generic.List<GameObject>();
                foreach (var skin in skins)
                {
                    var mesh = new Mesh();
                    skin.BakeMesh(mesh);
                    var go = new GameObject("Baked pose");
                    go.transform.SetParent(skin.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    skin.enabled = false;
                    baked.Add(go);
                }
                cam.Render();
                RenderTexture.active = rt;
                var texture = new Texture2D(800, 800, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
                texture.Apply();
                System.IO.File.WriteAllBytes($"ArtSource/WallRun/unity-{weapon}-{action}.png", texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                foreach (var go in baked)
                {
                    UnityEngine.Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh);
                    UnityEngine.Object.DestroyImmediate(go);
                }
                foreach (var skin in skins) skin.enabled = true;
            }
        }
        RenderTexture.active = null;
        cam.targetTexture = null;
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        Debug.Log("WALLRUN_PREVIEWS_OK");
    }

    private static AnimationClip Import(string side)
    {
        string path = Folder + "WallRun" + side + ".fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().First();
        importer.importAnimation = true;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        var clip = importer.defaultClipAnimations.First();
        clip.name = "WallRun" + side;
        clip.loopTime = true;
        clip.loopPose = true;
        clip.lockRootRotation = true;
        clip.lockRootHeightY = true;
        clip.lockRootPositionXZ = true;
        clip.keepOriginalOrientation = true;
        clip.keepOriginalPositionY = true;
        clip.keepOriginalPositionXZ = true;
        importer.clipAnimations = new[] { clip };
        importer.SaveAndReimport();
        var result = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .First(c => !c.name.StartsWith("__preview"));
        if (!result.humanMotion || result.length < 0.5f)
            throw new InvalidOperationException("Invalid humanoid cycle: " + path);
        return result;
    }
}
