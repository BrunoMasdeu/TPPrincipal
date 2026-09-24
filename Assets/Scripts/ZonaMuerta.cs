using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ZonaMuerte : MonoBehaviour
{
    public Transform puntoDeRespawn; // Lo dejamos para que los tests de Unity no lloren

    private readonly HashSet<int> jugadoresProcesados = new HashSet<int>();

    private void OnTriggerEnter(Collider other) { TryRespawn(other); }
    private void OnTriggerStay(Collider other) { TryRespawn(other); }

    private void OnTriggerExit(Collider other)
    {
        int playerId = GetPlayerId(other);
        if (playerId != 0)
            jugadoresProcesados.Remove(playerId);
    }

    private void TryRespawn(Collider other)
    {
        PlayerNetworkSetup networkPlayer = other.GetComponentInParent<PlayerNetworkSetup>();
        if (networkPlayer != null)
        {
            if (networkPlayer.IsSpawned && !networkPlayer.IsServer) return;

            if (!jugadoresProcesados.Add(networkPlayer.gameObject.GetInstanceID()))
                return;

            Debug.Log("Situación inválida: el jugador de red cayó al vacío.");
            networkPlayer.RespawnAlUltimoCheckpoint();
            return;
        }

        // Por si se cae un objeto común con Rigidbody
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null || !rb.CompareTag("Player")) return;

        if (!jugadoresProcesados.Add(rb.gameObject.GetInstanceID()))
            return;

        Debug.Log("Situación inválida: el jugador cayó al vacío.");

        if (puntoDeRespawn != null)
        {
            rb.position = puntoDeRespawn.position;
            rb.rotation = puntoDeRespawn.rotation;
        }
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private static int GetPlayerId(Collider other)
    {
        PlayerNetworkSetup networkPlayer = other.GetComponentInParent<PlayerNetworkSetup>();
        if (networkPlayer != null)
            return networkPlayer.gameObject.GetInstanceID();

        Rigidbody rb = other.attachedRigidbody;
        return rb != null ? rb.gameObject.GetInstanceID() : 0;
    }
}
