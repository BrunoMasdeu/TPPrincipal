using UnityEngine;

/// <summary>
/// Marca el punto visual donde se coloca una bandera transportada.
/// No contiene estado de red ni reglas de CTF.
/// </summary>
[DisallowMultipleComponent]
public sealed class CtfFlagCarrySocket : MonoBehaviour
{
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.95f);
        Gizmos.DrawWireSphere(transform.position, 0.08f);
        Gizmos.DrawLine(transform.position,
            transform.position + transform.up * 0.35f);
    }
}
