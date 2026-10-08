using UnityEngine;
using UnityEngine.AI;

/// <summary>Shared navigation and perimeter for the tutorial bots.</summary>
[DefaultExecutionOrder(-100)]
public class BotPatrolArea : MonoBehaviour
{
    public BoxCollider floor;
    public NavMeshData navigationData;
    [Min(0f)] public float edgeMargin = 0.08f;
    private NavMeshDataInstance navigationInstance;

    public bool IsReady => navigationInstance.valid && floor != null && floor.enabled;

    private void OnEnable()
    {
        if (navigationData != null)
            navigationInstance = NavMesh.AddNavMeshData(navigationData);
    }

    private void OnDisable()
    {
        if (navigationInstance.valid) navigationInstance.Remove();
    }

    // Use the oriented floor, not its larger world-axis-aligned bounds.
    // Ignore Y so jumping keeps the same perimeter.
    public bool Contains(Vector3 position, float radius)
    {
        if (floor == null) return false;
        Vector3 local = floor.transform.InverseTransformPoint(position) - floor.center;
        Vector3 scale = floor.transform.lossyScale;
        float marginX = (radius + edgeMargin) / Mathf.Max(Mathf.Abs(scale.x), 0.001f);
        float marginZ = (radius + edgeMargin) / Mathf.Max(Mathf.Abs(scale.z), 0.001f);
        return Mathf.Abs(local.x) <= floor.size.x * 0.5f - marginX &&
               Mathf.Abs(local.z) <= floor.size.z * 0.5f - marginZ;
    }

    public Vector3 RandomPoint()
    {
        Vector3 local = floor.center + new Vector3(
            Random.Range(-0.5f, 0.5f) * floor.size.x,
            floor.size.y * 0.5f,
            Random.Range(-0.5f, 0.5f) * floor.size.z);
        return floor.transform.TransformPoint(local);
    }

    private void OnDrawGizmosSelected()
    {
        if (floor == null) return;
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = floor.transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(floor.center, floor.size);
        Gizmos.matrix = previous;
    }
}
