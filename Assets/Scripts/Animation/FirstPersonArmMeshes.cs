using System.Collections.Generic;
using UnityEngine;

/// <summary>Recorta las mallas del cuerpo a las superficies de los brazos para la cámara local.</summary>
internal sealed class FirstPersonArmMeshes
{
    private sealed class Skin
    {
        public SkinnedMeshRenderer Renderer;
        public SkinnedMeshRenderer View;
        public Mesh ArmMesh;
        public bool WasHidden;
    }

    private readonly List<Skin> skins = new();
    private readonly Dictionary<Transform, Transform> bones = new();
    private readonly Transform boneRoot;
    public bool HasArms { get; }

    public FirstPersonArmMeshes(Transform player, Transform leftArm, Transform rightArm)
    {
        boneRoot = new GameObject("First Person Arm Bones").transform;
        boneRoot.SetParent(player, false);
        foreach (var renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null || !mesh.isReadable || renderer.transform.IsChildOf(leftArm) ||
                renderer.transform.IsChildOf(rightArm))
                continue;

            var sourceBones = renderer.bones;
            Mesh arms = ExtractArms(mesh, sourceBones, leftArm, rightArm);
            SkinnedMeshRenderer view = arms != null ? CreateView(renderer, arms, sourceBones) : null;
            skins.Add(new Skin { Renderer = renderer, View = view, ArmMesh = arms });
            HasArms |= arms != null;
        }
    }

    private SkinnedMeshRenderer CreateView(SkinnedMeshRenderer source, Mesh mesh, Transform[] sourceBones)
    {
        var go = new GameObject("First Person Arms");
        go.layer = source.gameObject.layer;
        go.transform.SetParent(source.transform, false);
        var view = go.AddComponent<SkinnedMeshRenderer>();
        view.sharedMesh = mesh;
        view.sharedMaterials = source.sharedMaterials;
        view.rootBone = source.rootBone;
        view.updateWhenOffscreen = true;
        view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        view.forceRenderingOff = true;
        var viewBones = new Transform[sourceBones.Length];
        for (int i = 0; i < viewBones.Length; i++)
        {
            Transform bone = sourceBones[i];
            if (bone == null) continue;
            if (!bones.TryGetValue(bone, out Transform copy))
            {
                copy = new GameObject(bone.name).transform;
                copy.SetParent(boneRoot, false);
                bones.Add(bone, copy);
            }
            viewBones[i] = copy;
        }
        view.bones = viewBones;
        return view;
    }

    public void UpdatePose(Vector3 worldPivot, Vector3 targetPivot, Quaternion rotation)
    {
        Vector3 parentScale = boneRoot.lossyScale;
        foreach (var pair in bones)
        {
            Transform source = pair.Key;
            Transform copy = pair.Value;
            copy.SetPositionAndRotation(targetPivot + rotation * (source.position - worldPivot), rotation * source.rotation);
            Vector3 scale = source.lossyScale;
            copy.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
        }
    }

    public void Show()
    {
        foreach (Skin skin in skins)
        {
            if (skin.Renderer == null) continue;
            skin.WasHidden = skin.Renderer.forceRenderingOff;
            skin.Renderer.forceRenderingOff = true;
            if (skin.View != null)
            {
                skin.View.enabled = skin.Renderer.enabled;
                skin.View.forceRenderingOff = skin.WasHidden;
            }
        }
    }

    public void Restore()
    {
        foreach (Skin skin in skins)
        {
            if (skin.Renderer == null) continue;
            skin.Renderer.forceRenderingOff = skin.WasHidden;
            if (skin.View != null) skin.View.forceRenderingOff = true;
        }
    }

    public void Release()
    {
        foreach (Skin skin in skins)
        {
            if (skin.ArmMesh != null) Destroy(skin.ArmMesh);
            if (skin.View != null) Destroy(skin.View.gameObject);
        }
        if (boneRoot != null) Destroy(boneRoot.gameObject);
    }

    private static void Destroy(Object obj)
    {
        if (Application.isPlaying) Object.Destroy(obj);
        else Object.DestroyImmediate(obj);
    }

    private static Mesh ExtractArms(Mesh body, Transform[] bones, Transform leftArm, Transform rightArm)
    {
        var armBone = new bool[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            armBone[i] = bones[i] != null && (bones[i].IsChildOf(leftArm) || bones[i].IsChildOf(rightArm));

        BoneWeight[] weights = body.boneWeights;
        if (weights.Length == 0 || bones.Length == 0)
            return null;

        var armVertex = new bool[weights.Length];
        for (int i = 0; i < weights.Length; i++)
        {
            BoneWeight w = weights[i];
            float influence = (armBone[w.boneIndex0] ? w.weight0 : 0f) +
                (armBone[w.boneIndex1] ? w.weight1 : 0f) +
                (armBone[w.boneIndex2] ? w.weight2 : 0f) +
                (armBone[w.boneIndex3] ? w.weight3 : 0f);
            armVertex[i] = influence > 0.8f;
        }

        var triangles = new List<int>[body.subMeshCount];
        bool hasTriangles = false;
        for (int subMesh = 0; subMesh < triangles.Length; subMesh++)
        {
            var kept = new List<int>();
            triangles[subMesh] = kept;
            if (body.GetTopology(subMesh) != MeshTopology.Triangles) continue;
            int[] indices = body.GetTriangles(subMesh);
            for (int i = 0; i < indices.Length; i += 3)
            {
                if (!armVertex[indices[i]] || !armVertex[indices[i + 1]] || !armVertex[indices[i + 2]]) continue;
                kept.Add(indices[i]);
                kept.Add(indices[i + 1]);
                kept.Add(indices[i + 2]);
            }
            hasTriangles |= kept.Count > 0;
        }
        if (!hasTriangles) return null;

        Mesh arms = Object.Instantiate(body);
        arms.name = body.name + " (First Person Arms)";
        arms.hideFlags = HideFlags.HideAndDontSave;
        for (int i = 0; i < triangles.Length; i++)
            arms.SetTriangles(triangles[i], i, false);
        return arms;
    }
}
