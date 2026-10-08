using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Mantiene y gestiona los datos de navegación e instancia del NavMesh y define los límites del perímetro
/// de patrullaje basándose en un <see cref="BoxCollider"/> que representa el piso.
/// 
/// Relación con otros componentes:
/// - Se ejecuta con prioridad previa (<c>[DefaultExecutionOrder(-100)]</c>) para registrar el <see cref="NavMeshData"/> antes del Start de los bots.
/// - Es referenciado por <see cref="movimientoLateral"/> para validar las posiciones y generar destinos aleatorios dentro del piso.
/// </summary>
[DefaultExecutionOrder(-100)]
public class BotPatrolArea : MonoBehaviour
{
    public BoxCollider floor;
    public NavMeshData navigationData;

    /// <summary>
    /// Margen adicional de seguridad interno respecto al borde del piso.
    /// </summary>
    [Min(0f)] public float edgeMargin = 0.08f;

    private NavMeshDataInstance navigationInstance;

    /// <summary>
    /// Indica si la instancia de navegación del NavMesh es válida y el piso está asignado y activo.
    /// </summary>
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

    /// <summary>
    /// Determina si una posición dada (y el radio del bot) se encuentra completamente dentro del perímetro del piso.
    /// 
    /// Lógica de coordenadas y radio:
    /// - Transforma la posición de coordenadas del mundo a coordenadas locales del <see cref="floor"/> mediante <see cref="Transform.InverseTransformPoint"/>.
    /// - Esto contempla cualquier rotación y traslación que tenga el piso en la escena.
    /// - Modula las dimensiones considerando la escala local (<c>lossyScale</c>) para que el radio del bot y el margen se mantengan precisos.
    /// - Ignora el eje Y para que los saltos dentro del área sigan reconociéndose dentro del perímetro.
    /// </summary>
    /// <param name="position">Posición a evaluar en coordenadas del mundo.</param>
    /// <param name="radius">Radio del bot para considerar el grosor de su cápsula.</param>
    /// <returns>Verdadero si el punto y el radio del bot caben en el piso; de lo contrario, falso.</returns>
    public bool Contains(Vector3 position, float radius)
    {
        if (floor == null) return false;

        // Transformación a coordenadas locales del piso restando el centro del BoxCollider
        Vector3 local = floor.transform.InverseTransformPoint(position) - floor.center;
        Vector3 scale = floor.transform.lossyScale;

        // Ajuste de margen considerando la escala del piso
        float marginX = (radius + edgeMargin) / Mathf.Max(Mathf.Abs(scale.x), 0.001f);
        float marginZ = (radius + edgeMargin) / Mathf.Max(Mathf.Abs(scale.z), 0.001f);

        return Mathf.Abs(local.x) <= floor.size.x * 0.5f - marginX &&
               Mathf.Abs(local.z) <= floor.size.z * 0.5f - marginZ;
    }

    /// <summary>
    /// Genera un punto aleatorio en la superficie del piso en coordenadas del mundo.
    /// </summary>
    /// <returns>Punto tridimensional en coordenadas globales.</returns>
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