/// <summary>
/// [COG-620] Reglas de la votación de revancha, separadas para poder probarlas
/// sin levantar la red.
/// </summary>
public static class RevanchaRules
{
    /// <summary>
    /// Hace falta más de la mitad: con 2 jugadores, 2; con 3, 2; con 4, 3.
    /// </summary>
    public static int VotosNecesarios(int jugadores)
    {
        return jugadores <= 0 ? 0 : jugadores / 2 + 1;
    }

    public static bool HayMayoria(int votos, int jugadores)
    {
        return jugadores > 0 && votos >= VotosNecesarios(jugadores);
    }

    /// <summary>
    /// La revancha se ofrece en TDM y CTF, con la partida terminada y con
    /// todos los jugadores conectados: si alguien se fue, no hay revancha.
    /// </summary>
    public static bool PuedeVotarse(
        GameModeId modo,
        SessionPhase fase,
        MatchPhase fasePartida,
        int jugadoresConectados,
        int jugadoresEsperados)
    {
        return (modo == GameModeId.TDM || modo == GameModeId.CTF) &&
            fase == SessionPhase.InMatch &&
            fasePartida == MatchPhase.Finished &&
            jugadoresEsperados > 0 &&
            jugadoresConectados == jugadoresEsperados;
    }
}
