using Unity.Netcode;
using UnityEngine;

public enum EstadoCarrera : byte
{
    Esperando,
    Activa,
    Finalizada
}

public enum MotivoFinalizacionCarrera : byte
{
    Ninguno,
    Llegada,
    TiempoAgotado
}

public enum ResultadoCarreraLocal : byte
{
    Pendiente,
    Victoria,
    DerrotaPorLlegadaRival,
    DerrotaPorTiempo
}

public static class RaceStateRules
{
    public static bool CanStart(EstadoCarrera state)
    {
        return state == EstadoCarrera.Esperando;
    }

    public static bool CanFinish(EstadoCarrera state)
    {
        return state == EstadoCarrera.Activa;
    }
}

public static class RaceResultRules
{
    public static ResultadoCarreraLocal GetLocalResult(
        EstadoCarrera state,
        MotivoFinalizacionCarrera finishReason,
        ulong winnerClientId,
        ulong localClientId)
    {
        if (state != EstadoCarrera.Finalizada)
            return ResultadoCarreraLocal.Pendiente;

        if (finishReason == MotivoFinalizacionCarrera.TiempoAgotado)
            return ResultadoCarreraLocal.DerrotaPorTiempo;

        if (finishReason != MotivoFinalizacionCarrera.Llegada ||
            winnerClientId == GameManager.SinGanador)
        {
            return ResultadoCarreraLocal.Pendiente;
        }

        return winnerClientId == localClientId
            ? ResultadoCarreraLocal.Victoria
            : ResultadoCarreraLocal.DerrotaPorLlegadaRival;
    }
}

[RequireComponent(typeof(NetworkObject))]
public class GameManager : NetworkBehaviour
{
    public const ulong SinGanador = ulong.MaxValue;

    [Header("Configuración del Nivel")]
    [Min(0.1f)]
    public float tiempoMaximo = 120f;

    [Header("Estado del Juego (Sincronizado)")]
    public readonly NetworkVariable<float> tiempoActual = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public readonly NetworkVariable<EstadoCarrera> estadoCarrera = new(
        EstadoCarrera.Esperando,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public readonly NetworkVariable<MotivoFinalizacionCarrera> motivoFinalizacion = new(
        MotivoFinalizacionCarrera.Ninguno,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public readonly NetworkVariable<ulong> idGanador = new(
        SinGanador,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool CarreraActiva => estadoCarrera.Value == EstadoCarrera.Activa;

    private NetworkMatchManager commonMatchManager;

    public float TiempoRestante => Mathf.Max(
        0f,
        tiempoMaximo - tiempoActual.Value
    );

    private void Update()
    {
        if (!IsSpawned || !IsServer || !CarreraActiva)
            return;

        tiempoActual.Value = Mathf.Min(
            tiempoMaximo,
            tiempoActual.Value + Time.deltaTime
        );

        if (tiempoActual.Value >= tiempoMaximo)
            PerderJuegoPorTiempo();
    }

    public bool IniciarCarrera()
    {
        if (!IsSpawned || !IsServer ||
            !RaceStateRules.CanStart(estadoCarrera.Value))
        {
            return false;
        }

        commonMatchManager = NetworkMatchManager.Instance;

        bool commonRaceAlreadyPlaying = commonMatchManager != null &&
            commonMatchManager.Phase == MatchPhase.Playing &&
            NetworkLobbySession.Instance != null &&
            NetworkLobbySession.Instance.SelectedGameModeId == GameModeId.Race;

        if (commonMatchManager != null && !commonRaceAlreadyPlaying &&
            !commonMatchManager.TryBeginLegacyRace(tiempoMaximo))
        {
            Debug.LogWarning(
                "La carrera no pudo iniciar el ciclo común de partida."
            );
            return false;
        }

        tiempoActual.Value = 0f;
        motivoFinalizacion.Value = MotivoFinalizacionCarrera.Ninguno;
        idGanador.Value = SinGanador;
        estadoCarrera.Value = EstadoCarrera.Activa;
        return true;
    }

    public void RegistrarLlegada(ulong idGanador)
    {
        if (!IsServer || !RaceStateRules.CanFinish(estadoCarrera.Value))
            return;

        this.idGanador.Value = idGanador;
        motivoFinalizacion.Value = MotivoFinalizacionCarrera.Llegada;
        estadoCarrera.Value = EstadoCarrera.Finalizada;

        FinishCommonMatch(new MatchResultData(
            TeamId.None,
            MatchEndReason.RaceFinish,
            false
        ));

        Debug.Log(
            $"¡El jugador {idGanador} cruzó la meta en " +
            $"{tiempoActual.Value:F2} segundos!"
        );
    }

    public void PerderJuegoPorTiempo()
    {
        if (!IsServer || !RaceStateRules.CanFinish(estadoCarrera.Value))
            return;

        idGanador.Value = SinGanador;
        motivoFinalizacion.Value = MotivoFinalizacionCarrera.TiempoAgotado;
        estadoCarrera.Value = EstadoCarrera.Finalizada;

        FinishCommonMatch(new MatchResultData(
            TeamId.None,
            MatchEndReason.TimeExpired,
            true
        ));

        Debug.Log("¡Derrota global! Se agotó el tiempo límite para ambos.");
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        tiempoActual.Value = 0f;
        motivoFinalizacion.Value = MotivoFinalizacionCarrera.Ninguno;
        idGanador.Value = SinGanador;
        estadoCarrera.Value = EstadoCarrera.Esperando;
        commonMatchManager = NetworkMatchManager.Instance;

        if (NetworkLobbySession.Instance != null &&
            NetworkLobbySession.Instance.SelectedGameModeId == GameModeId.Race &&
            commonMatchManager == null)
        {
            Debug.LogError(
                "La carrera configurada no encontró NetworkMatchManager."
            );
        }

        if (commonMatchManager != null)
        {
            commonMatchManager.PhaseChanged += OnCommonPhaseChanged;
            commonMatchManager.TimeExpired += OnCommonTimeExpired;

            if (commonMatchManager.Phase == MatchPhase.Playing)
                OnCommonPhaseChanged(MatchPhase.Playing);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (commonMatchManager != null)
        {
            commonMatchManager.PhaseChanged -= OnCommonPhaseChanged;
            commonMatchManager.TimeExpired -= OnCommonTimeExpired;
        }

        commonMatchManager = null;
    }

    private void OnCommonPhaseChanged(MatchPhase phase)
    {
        if (phase == MatchPhase.Playing &&
            NetworkLobbySession.Instance != null &&
            NetworkLobbySession.Instance.SelectedGameModeId == GameModeId.Race)
        {
            IniciarCarrera();
        }
    }

    private void OnCommonTimeExpired()
    {
        if (NetworkLobbySession.Instance != null &&
            NetworkLobbySession.Instance.SelectedGameModeId == GameModeId.Race)
        {
            PerderJuegoPorTiempo();
        }
    }

    private void FinishCommonMatch(MatchResultData matchResult)
    {
        if (commonMatchManager == null)
            commonMatchManager = NetworkMatchManager.Instance;

        if (commonMatchManager != null &&
            !commonMatchManager.TryFinishMatch(matchResult))
        {
            Debug.LogWarning(
                "La carrera terminó, pero el ciclo común no aceptó el cierre."
            );
        }
    }
}
