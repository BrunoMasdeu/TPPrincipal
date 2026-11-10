using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Builds the player's short, full-body backward fall from the bundled clip.</summary>
public static class PlayerDeathAnimationSetup
{
    private const string ClipPath = "Assets/Animations/Player_DeathBack.anim";
    private const string ControllerPath = "Assets/Animations/PlayerAnimatorController.controller";

    [MenuItem("Tools/Player/Build Death Animation")]
    public static void Build()
    {
        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Zombie/Animations/Z_FallingBack.anim");
        if (source == null || !source.humanMotion)
            throw new InvalidOperationException("Missing humanoid backward fall.");

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, ClipPath);
        }
        EditorUtility.CopySerialized(source, clip);
        clip.name = "Player_DeathBack";
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        settings.loopBlend = false;
        settings.loopBlendOrientation = true;
        settings.loopBlendPositionY = true;
        settings.loopBlendPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        int existing = Array.FindIndex(controller.layers, layer => layer.name == "Death");
        if (existing >= 0)
            controller.RemoveLayer(existing);
        controller.AddLayer("Death");
        var layers = controller.layers;
        var death = layers[layers.Length - 1];
        death.defaultWeight = 0f;
        death.blendingMode = AnimatorLayerBlendingMode.Override;
        death.avatarMask = null;
        var state = death.stateMachine.AddState("Death Back", new Vector3(260, 80));
        state.motion = clip;
        state.speed = clip.length / 0.8f;
        state.writeDefaultValues = false;
        death.stateMachine.defaultState = state;
        controller.layers = layers;
        EditorUtility.SetDirty(clip);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("PLAYER_DEATH_SETUP_OK: 0.8s backward fall, full-body override, no loop.");
    }

    // Batch entry point. Use -deathPreviewOutput <folder> to keep captures outside Assets.
    public static void RenderPreviews()
    {
        string[] args = Environment.GetCommandLineArgs();
        int outputArg = Array.IndexOf(args, "-deathPreviewOutput");
        string output = outputArg >= 0 ? args[outputArg + 1] : "ArtSource/Death";
        System.IO.Directory.CreateDirectory(output);
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        var player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/PlayerPrefab.prefab"));
        player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (var camera in player.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (var canvas in player.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
        var animator = player.GetComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.SetLayerWeight(animator.GetLayerIndex("Upper Body"), 0f);
        int death = animator.GetLayerIndex("Death");
        animator.SetLayerWeight(death, 1f);

        var cam = new GameObject("Death Preview Camera").AddComponent<Camera>();
        cam.transform.position = new Vector3(3.5f, 2.2f, 2.8f);
        cam.transform.LookAt(new Vector3(0, 0.7f, -0.45f));
        cam.orthographic = true;
        cam.orthographicSize = 1.6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.14f, 0.17f, 0.21f);
        var light = new GameObject("Key").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2f;
        light.transform.rotation = Quaternion.Euler(40, -30, 0);
        RenderSettings.ambientLight = Color.gray;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Ground";
        var rt = new RenderTexture(800, 800, 24);
        cam.targetTexture = rt;
        try
        {
            for (int frame = 0; frame <= 4; frame++)
            {
                animator.Play("Death Back", death, frame / 4f);
                animator.Update(0f);
                var skins = player.GetComponentsInChildren<SkinnedMeshRenderer>();
                var baked = new System.Collections.Generic.List<GameObject>();
                foreach (var skin in skins)
                {
                    var mesh = new Mesh();
                    skin.BakeMesh(mesh);
                    var go = new GameObject("Baked death pose");
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
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(output, $"death-{frame}.png"),
                    texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                foreach (var go in baked)
                {
                    UnityEngine.Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh);
                    UnityEngine.Object.DestroyImmediate(go);
                }
                foreach (var skin in skins) skin.enabled = true;
            }
        }
        finally
        {
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(cam.gameObject);
            UnityEngine.Object.DestroyImmediate(light.gameObject);
            UnityEngine.Object.DestroyImmediate(floor);
        }
        Debug.Log("PLAYER_DEATH_PREVIEWS_OK");
    }
}
