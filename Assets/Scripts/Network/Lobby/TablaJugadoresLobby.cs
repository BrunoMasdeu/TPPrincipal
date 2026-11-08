using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [COG-586] Tabla de jugadores del lobby: una fila por jugador con su nombre
/// y un botón EXPULSAR que solo ve el host (nunca en su propia fila).
///
/// Cómo se arma en la escena:
///   TablaJugadores (este script + Vertical Layout Group)
///     FilaJugador (APAGADA: es el molde que se copia para cada jugador)
///       Nombre      (texto)
///       BtnExpulsar (botón)
///
/// ConnectionManager llama a Actualizar() cada vez que cambia el lobby.
/// </summary>
public class TablaJugadoresLobby : MonoBehaviour
{
    [SerializeField] private Transform contenedor;  // donde se agregan las filas
    [SerializeField] private GameObject filaModelo; // FilaJugador (apagada)

    private readonly List<GameObject> filas = new List<GameObject>();
    private string ultimoEstado = string.Empty;

    public void Actualizar(NetworkLobbySession lobby)
    {
        NetworkManager manager = NetworkManager.Singleton;
        bool haySesion = lobby != null && lobby.IsSpawned;
        bool soyHost = manager != null && manager.IsHost;
        bool enLobby = haySesion && lobby.Phase == SessionPhase.Lobby;

        // Solo se rearma si cambió algo (así no se borra un botón mientras
        // el host lo está apretando).
        string estado = DescribirEstado(lobby, haySesion, soyHost, enLobby);
        if (estado == ultimoEstado)
            return;
        ultimoEstado = estado;

        foreach (GameObject fila in filas)
            Destroy(fila);
        filas.Clear();

        if (!haySesion || filaModelo == null)
            return;

        Transform destino = contenedor != null ? contenedor : transform;

        for (int i = 0; i < lobby.Players.Count; i++)
        {
            ulong clientId = lobby.Players[i].ClientId;
            bool esHost = clientId == NetworkManager.ServerClientId;
            bool soyYo = manager != null && clientId == manager.LocalClientId;

            GameObject fila = Instantiate(filaModelo, destino);
            fila.SetActive(true);
            filas.Add(fila);

            TMP_Text nombre = BuscarHijo<TMP_Text>(fila, "Nombre");
            if (nombre != null)
            {
                nombre.text = NetworkLobbySession.NombreParaMostrar(clientId) +
                    (esHost ? " (host)" : "") +
                    (soyYo ? " (vos)" : "");
            }

            Button expulsar = BuscarHijo<Button>(fila, "BtnExpulsar");
            if (expulsar != null)
            {
                bool puedeExpulsar = soyHost && enLobby && !esHost;
                expulsar.gameObject.SetActive(puedeExpulsar);

                if (puedeExpulsar)
                    expulsar.onClick.AddListener(() => lobby.ExpulsarJugador(clientId));
            }
        }
    }

    // Un texto con todo lo que se muestra; si no cambia, no hace falta rearmar.
    private static string DescribirEstado(
        NetworkLobbySession lobby, bool haySesion, bool soyHost, bool enLobby)
    {
        if (!haySesion)
            return "sin sesion";

        StringBuilder texto = new StringBuilder();
        texto.Append(soyHost).Append('|').Append(enLobby);

        for (int i = 0; i < lobby.Players.Count; i++)
        {
            ulong clientId = lobby.Players[i].ClientId;
            texto.Append('|').Append(clientId).Append(':')
                .Append(NetworkLobbySession.NombreParaMostrar(clientId));
        }

        return texto.ToString();
    }

    private static T BuscarHijo<T>(GameObject fila, string nombreHijo) where T : Component
    {
        Transform hijo = fila.transform.Find(nombreHijo);
        return hijo != null
            ? hijo.GetComponent<T>()
            : fila.GetComponentInChildren<T>(true);
    }
}
