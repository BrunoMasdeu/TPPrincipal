using UnityEngine;

/// <summary>
/// Marca el volumen de una base. CtfMatchManager comprueba la entrada en el
/// servidor usando este BoxCollider; no se necesita un objeto de red por zona.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class CtfBaseZone : MonoBehaviour
{
    [SerializeField] private TeamId teamId;
    private BoxCollider boxCollider;

    public TeamId TeamId => teamId;
    public bool IsConfigured =>
        (teamId == TeamId.Red || teamId == TeamId.Blue) &&
        boxCollider != null && boxCollider.enabled && boxCollider.isTrigger;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
    }

    public bool Contains(Collider playerCollider)
    {
        return IsConfigured && playerCollider != null &&
            playerCollider.enabled &&
            boxCollider.bounds.Intersects(playerCollider.bounds);
    }

    private void OnDrawGizmos()
    {
        BoxCollider zoneCollider = GetComponent<BoxCollider>();
        if (zoneCollider == null)
            return;

        Gizmos.color = teamId switch
        {
            TeamId.Red => new Color(1f, 0.1f, 0.1f, 0.8f),
            TeamId.Blue => new Color(0.1f, 0.4f, 1f, 0.8f),
            _ => Color.white
        };

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(zoneCollider.center, zoneCollider.size);
        Gizmos.matrix = previousMatrix;
    }
}
