public enum MatchEndReason : byte
{
    None,
    TimeExpired,
    ScoreLimit,
    Cancelled,
    RaceFinish,
    // [COG-599] Un equipo se quedó sin jugadores porque abandonaron.
    TeamAbandoned
}
