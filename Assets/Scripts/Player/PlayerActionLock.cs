
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
                // [MULTIJUGADOR] Antes: script.enabled = !accionesBloqueadas;
                // Eso prendía el script en TODAS las copias de los jugadores
                // (también las de los demás), y tu teclado los movía a todos.
                // Ahora acá solo se apaga; prender lo hace PlayerNetworkSetup,
                // que sabe si es tu jugador y si está vivo o en pausa.
                if (accionesBloqueadas)
                    script.enabled = false;
                else if (!Application.isPlaying)
                    script.enabled = true; // en el editor, fuera de Play, como antes
            }
        }

        // [MULTIJUGADOR] Al desbloquear durante el juego, PlayerNetworkSetup
        // vuelve a prender solo lo que corresponde.
        if (!accionesBloqueadas && Application.isPlaying)
        {
            PlayerNetworkSetup setup = GetComponent<PlayerNetworkSetup>();
            if (setup != null)
                setup.RefrescarControles();
        }
    }
}

