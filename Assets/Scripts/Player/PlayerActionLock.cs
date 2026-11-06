
using UnityEngine;

public class PlayerActionLock : MonoBehaviour
{
    [Header("Bloqueo de acciones")]
    public bool accionesBloqueadas = false;

    [Header("Scripts que se bloquearan")]
    [SerializeField] private MonoBehaviour[] scriptsABloquear;

    private void Start()
    {
        ActualizarBloqueo();
    }

    private void OnValidate()
    {
        ActualizarBloqueo();
    }

    public void ActualizarBloqueo()
    {
        foreach (MonoBehaviour script in scriptsABloquear)
        {
            if (script != null)
            {
                script.enabled = !accionesBloqueadas;
            }
        }
    }
}

