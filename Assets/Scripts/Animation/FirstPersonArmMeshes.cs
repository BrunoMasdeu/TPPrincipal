using System.Collections.Generic;
using UnityEngine;

/// <summary>Recorta las mallas del cuerpo a las superficies de los brazos para la cámara local.</summary>
internal sealed class FirstPersonArmMeshes
{
    private sealed class Skin
    {
        public Renderer Renderer;
        public SkinnedMeshRenderer View;
        public Mesh ArmMesh;
        public bool WasHidden;
    }

    private struct CutEdge
    {
        public int Start;
        public int End;
        public bool Kept;
        public bool Removed;
    }

    private sealed class CutVertex
    {
        public Vector3 Position;
        public BoneWeight Weight;
        public Transform[] Bones;
        public Matrix4x4[] BindPoses;

        public Vector3 Sample() =>
            SampleBone(Weight.boneIndex0, Weight.weight0) +
            SampleBone(Weight.boneIndex1, Weight.weight1) +
            SampleBone(Weight.boneIndex2, Weight.weight2) +
            SampleBone(Weight.boneIndex3, Weight.weight3);

        private Vector3 SampleBone(int index, float weight) => weight > 0f && Bones[index] != null
            ? Bones[index].TransformPoint(BindPoses[index].MultiplyPoint3x4(Position)) * weight
            : Vector3.zero;
    }

    private readonly List<Skin> skins = new();
    private readonly Dictionary<Transform, Transform> bones = new();
    private readonly Dictionary<Transform, Bounds> armBounds = new();
    private readonly List<CutVertex> sleeveEdges = new();
    private readonly Transform boneRoot;
    public bool HasArms { get; }

    public FirstPersonArmMeshes(Transform player, Transform model, Transform leftArm, Transform rightArm)
    {
        boneRoot = new GameObject("First Person Arm Bones").transform;
        boneRoot.SetParent(player, false);
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.transform.IsChildOf(leftArm) || renderer.transform.IsChildOf(rightArm))
                continue; // Las armas y los accesorios de mano acompañan al hueso original.

            var skin = new Skin { Renderer = renderer };
            skins.Add(skin);
            if (renderer is not SkinnedMeshRenderer skinned || skinned.sharedMesh == null ||
                !skinned.sharedMesh.isReadable)
                continue;

            Transform[] sourceBones = skinned.bones;
            skin.ArmMesh = ExtractArms(skinned.sharedMesh, sourceBones, leftArm, rightArm);
            if (skin.ArmMesh != null)
            {
                skin.View = CreateView(skinned, skin.ArmMesh, sourceBones);
                HasArms = true;
            }
        }
    }

    private SkinnedMeshRenderer CreateView(SkinnedMeshRenderer source, Mesh mesh, Transform[] sourceBones)
    {
        var go = new GameObject("First Person Arms");
        go.layer = source.gameObject.layer;
        go.transform.SetParent(source.transform, false);
        var view = go.AddComponent<SkinnedMeshRenderer>();
        view.sharedMesh = mesh;
        Material[] materials = source.sharedMaterials;
        view.sharedMaterials = materials;
        // PlayerSkin ya aplicó los materiales y colores de este equipo.
        var properties = new MaterialPropertyBlock();
        source.GetPropertyBlock(properties);
        if (!properties.isEmpty)
            view.SetPropertyBlock(properties);
        for (int i = 0; i < materials.Length; i++)
        {
            source.GetPropertyBlock(properties, i);
            if (!properties.isEmpty)
                view.SetPropertyBlock(properties, i);
        }
        view.rootBone = source.rootBone;
        view.updateWhenOffscreen = true;
        view.skinnedMotionVectors = false;
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

    public void EncapsulateArms(Matrix4x4 worldToView, ref Bounds bounds)
    {
        // La piel interpola entre huesos, así que sus vértices quedan dentro de esta envolvente.
        foreach (var pair in armBounds)
            FirstPersonView.EncapsulateBounds(pair.Value, worldToView * pair.Key.localToWorldMatrix, ref bounds);
    }

    public float GetSleeveLowering(Matrix4x4 worldToView, float verticalSlope)
    {
        float lowering = 0f;
        foreach (CutVertex vertex in sleeveEdges)
        {
            Vector3 position = worldToView.MultiplyPoint3x4(vertex.Sample());
            // El borde recortado debe quedar debajo del límite inferior de la imagen.
            lowering = Mathf.Max(lowering, position.y + Mathf.Max(position.z, 0f) * verticalSlope + 0.015f);
        }
        return lowering;
    }

    public void UpdatePose(Vector3 worldPivot, Vector3 targetPivot, Quaternion rotation, float viewScale)
    {
        Vector3 parentScale = boneRoot.lossyScale;
        foreach (var pair in bones)
        {
            Transform source = pair.Key;
            Transform copy = pair.Value;
            copy.SetPositionAndRotation(targetPivot + rotation * (source.position - worldPivot) * viewScale,
                rotation * source.rotation);
            Vector3 scale = source.lossyScale * viewScale;
            copy.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
        }
    }

    public void Show(bool drawArms)
    {
        foreach (Skin skin in skins)
        {
            if (skin.Renderer == null) continue;
            skin.WasHidden = skin.Renderer.forceRenderingOff;
            skin.Renderer.forceRenderingOff = true;
            if (skin.View != null)
            {
                skin.View.enabled = skin.Renderer.enabled;
                skin.View.forceRenderingOff = skin.WasHidden || !drawArms;
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

    private Mesh ExtractArms(Mesh body, Transform[] sourceBones, Transform leftArm, Transform rightArm)
    {
        var armBone = new bool[sourceBones.Length];
        for (int i = 0; i < sourceBones.Length; i++)
            armBone[i] = sourceBones[i] != null &&
                (sourceBones[i].IsChildOf(leftArm) || sourceBones[i].IsChildOf(rightArm));

        BoneWeight[] weights = body.boneWeights;
        if (weights.Length == 0 || sourceBones.Length == 0)
            return null;

        var influence = new float[weights.Length];
        for (int i = 0; i < weights.Length; i++)
        {
            BoneWeight w = weights[i];
            influence[i] = (w.weight0 > 0f && armBone[w.boneIndex0] ? w.weight0 : 0f) +
                (w.weight1 > 0f && armBone[w.boneIndex1] ? w.weight1 : 0f) +
                (w.weight2 > 0f && armBone[w.boneIndex2] ? w.weight2 : 0f) +
                (w.weight3 > 0f && armBone[w.boneIndex3] ? w.weight3 : 0f);
        }

        var triangles = new List<int>[body.subMeshCount];
        var keptVertices = new HashSet<int>();
        Vector3[] vertices = body.vertices;
        int[] weld = WeldPositions(vertices);
        var edges = new Dictionary<(int, int), CutEdge>();
        for (int subMesh = 0; subMesh < triangles.Length; subMesh++)
        {
            var kept = new List<int>();
            triangles[subMesh] = kept;
            if (body.GetTopology(subMesh) != MeshTopology.Triangles) continue;
            int[] indices = body.GetTriangles(subMesh);
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                // Mantiene las caras de transición de la manga hacia el hombro.
                bool keep = influence[a] + influence[b] + influence[c] >= 1.5f;
                RecordEdge(edges, weld, a, b, keep);
                RecordEdge(edges, weld, b, c, keep);
                RecordEdge(edges, weld, c, a, keep);
                if (!keep)
                    continue;

                kept.Add(a);
                kept.Add(b);
                kept.Add(c);
                keptVertices.Add(a);
                keptVertices.Add(b);
                keptVertices.Add(c);
            }
        }
        if (keptVertices.Count == 0)
            return null;

        Matrix4x4[] bindPoses = body.bindposes;
        foreach (int index in keptVertices)
        {
            Vector3 vertex = vertices[index];
            BoneWeight weight = weights[index];
            IncludeVertex(vertex, weight.boneIndex0, weight.weight0, sourceBones, bindPoses);
            IncludeVertex(vertex, weight.boneIndex1, weight.weight1, sourceBones, bindPoses);
            IncludeVertex(vertex, weight.boneIndex2, weight.weight2, sourceBones, bindPoses);
            IncludeVertex(vertex, weight.boneIndex3, weight.weight3, sourceBones, bindPoses);
        }

        var seam = new HashSet<int>();
        foreach (CutEdge edge in edges.Values)
        {
            if (!edge.Kept || !edge.Removed)
                continue;
            seam.Add(edge.Start);
            seam.Add(edge.End);
        }
        foreach (int index in seam)
            sleeveEdges.Add(new CutVertex
            {
                Position = vertices[index], Weight = weights[index], Bones = sourceBones, BindPoses = bindPoses
            });

        Mesh arms = Object.Instantiate(body);
        arms.name = body.name + " (First Person Arms)";
        arms.hideFlags = HideFlags.HideAndDontSave;
        for (int i = 0; i < triangles.Length; i++)
            arms.SetTriangles(triangles[i], i, false);
        return arms;
    }

    private void IncludeVertex(Vector3 vertex, int index, float weight, Transform[] sourceBones, Matrix4x4[] bindPoses)
    {
        if (weight <= 0f || sourceBones[index] == null)
            return;

        Transform bone = sourceBones[index];
        Vector3 local = bindPoses[index].MultiplyPoint3x4(vertex);
        if (!armBounds.TryGetValue(bone, out Bounds bounds))
            bounds = new Bounds(local, Vector3.zero);
        else
            bounds.Encapsulate(local);
        armBounds[bone] = bounds;
    }

    private static int[] WeldPositions(Vector3[] vertices)
    {
        // Las costuras de UV pueden usar índices distintos para la misma posición.
        var unique = new Dictionary<Vector3Int, int>();
        var weld = new int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = Vector3Int.RoundToInt(vertices[i] * 10000f);
            if (!unique.TryGetValue(key, out int representative))
                unique.Add(key, representative = i);
            weld[i] = representative;
        }
        return weld;
    }

    private static void RecordEdge(Dictionary<(int, int), CutEdge> edges, int[] weld, int a, int b, bool keep)
    {
        int wa = weld[a], wb = weld[b];
        if (wa == wb)
            return;
        var key = wa < wb ? (wa, wb) : (wb, wa);
        edges.TryGetValue(key, out CutEdge edge);
        if (keep)
        {
            edge.Kept = true;
            edge.Start = a;
            edge.End = b;
        }
        else
            edge.Removed = true;
        edges[key] = edge;
    }
}
