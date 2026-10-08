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
}
