using UnityEngine;

public class HeliceMortal : MonoBehaviour
{
    [Header("Respawn")]
    public Transform puntoDeRespawn; // Acá vamos a conectar tu objeto "Respawn"

    private void OnCollisionEnter(Collision collision)
    {
        // Verifica si lo que chocó tiene la etiqueta "Player"
        if (collision.gameObject.CompareTag("Player"))
        {
            // 1. Teletransportar al jugador
            collision.gameObject.transform.position = puntoDeRespawn.position;

            // 2. Frenar las fuerzas físicas (para que no salga volando al reaparecer)
            Rigidbody rbJugador = collision.gameObject.GetComponent<Rigidbody>();
            if (rbJugador != null)
            {
                rbJugador.linearVelocity = Vector3.zero;
                rbJugador.angularVelocity = Vector3.zero;
            }
        }
    }
}
