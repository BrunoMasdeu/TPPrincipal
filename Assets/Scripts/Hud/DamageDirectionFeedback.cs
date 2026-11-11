using UnityEngine;
using UnityEngine.UI;

/// <summary>Dibuja impactos sobre un círculo centrado en la mira.</summary>
[DisallowMultipleComponent]
public sealed class DamageDirectionFeedback : MaskableGraphic
{
    [SerializeField, Min(0.1f)] private float duration = 0.85f;
    [SerializeField, Range(0.1f, 0.45f)] private float radiusFraction = 0.36f;
    [SerializeField, Range(10f, 90f)] private float arcDegrees = 48f;
    [SerializeField, Min(1f)] private float bandWidth = 16f;

    private struct Mark
    {
        public Vector2 Origin;
        public float ExpiresAt;
    }

    private const int ArcSegments = 24;
    private readonly Mark[] marks = new Mark[4];
    private Transform viewer;
    private bool hasMarks;

    public void Present(Vector3 origin, Transform player)
    {
        viewer = player;
        Vector2 point = new(origin.x, origin.z);
        float now = Time.unscaledTime;
        int slot = 0;
        for (int i = 0; i < marks.Length; i++)
        {
            // Renueva la misma franja cuando llegan varios impactos del mismo lugar.
            if (marks[i].ExpiresAt > now && (marks[i].Origin - point).sqrMagnitude < 1f)
            {
                slot = i;
                break;
            }
            if (marks[i].ExpiresAt < marks[slot].ExpiresAt)
                slot = i;
        }

        marks[slot] = new Mark { Origin = point, ExpiresAt = now + duration };
        hasMarks = true;
        SetVerticesDirty();
    }

    public void Clear()
    {
        if (!hasMarks)
            return;

        System.Array.Clear(marks, 0, marks.Length);
        hasMarks = false;
        SetVerticesDirty();
    }

    private void LateUpdate()
    {
        if (!hasMarks)
            return;

        hasMarks = false;
        float now = Time.unscaledTime;
        foreach (Mark mark in marks)
            hasMarks |= mark.ExpiresAt > now;
        // Usa la pose de este frame, después de procesar el giro del mouse.
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        if (!hasMarks || viewer == null)
            return;

        Rect rect = rectTransform.rect;
        float radius = Mathf.Min(rect.width, rect.height) * radiusFraction;
        if (radius <= 0f)
            return;

        Vector3 position = viewer.position;
        Vector3 forward = viewer.forward;
        Vector3 right = viewer.right;
        float now = Time.unscaledTime;
        foreach (Mark mark in marks)
        {
            if (mark.ExpiresAt <= now)
                continue;

            Vector3 offset = new(mark.Origin.x - position.x, 0f, mark.Origin.y - position.z);
            if (offset.sqrMagnitude < 0.0001f)
                continue;

            float angle = Mathf.Atan2(Vector3.Dot(offset, forward), Vector3.Dot(offset, right));
            float opacity = Mathf.SmoothStep(0f, 1f, (mark.ExpiresAt - now) / (duration * 0.7f));
            AddArc(vertices, rect.center, radius, angle, opacity);
        }
    }

    private void AddArc(VertexHelper vertices, Vector2 center, float radius, float angle, float opacity)
    {
        int first = vertices.currentVertCount;
        float spread = arcDegrees * Mathf.Deg2Rad;
        for (int i = 0; i <= ArcSegments; i++)
        {
            float t = (float)i / ArcSegments;
            float a = angle + (t - 0.5f) * spread;
            var radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float taper = Mathf.Sin(t * Mathf.PI);
            float halfWidth = bandWidth * Mathf.Lerp(0.15f, 0.5f, taper);
            Color tint = color;
            tint.a *= opacity * Mathf.Clamp01(taper * 3f);
            Color edge = tint;
            edge.a = 0f;

            AddVertex(vertices, center + radial * (radius - halfWidth - 2f), edge);
            AddVertex(vertices, center + radial * (radius - halfWidth), tint);
            AddVertex(vertices, center + radial * (radius + halfWidth), tint);
            AddVertex(vertices, center + radial * (radius + halfWidth + 2f), edge);
            if (i == 0)
                continue;

            int start = first + (i - 1) * 4;
            for (int j = 0; j < 3; j++)
            {
                int corner = start + j;
                vertices.AddTriangle(corner, corner + 4, corner + 1);
                vertices.AddTriangle(corner + 1, corner + 4, corner + 5);
            }
        }
    }

    private static void AddVertex(VertexHelper vertices, Vector2 position, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vertices.AddVert(vertex);
    }
}
