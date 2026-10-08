public static class CombatValidationRules
{
    public static bool IsCombatMode(GameModeId mode) =>
        mode == GameModeId.TDM || mode == GameModeId.CTF;

    public static bool CanFire(
        MatchPhase phase,
        PlayerLifeState life,
        int ammunition,
        double reloadEndsAt,
        double nextShotAt,
        double now) =>
        phase == MatchPhase.Playing &&
        life == PlayerLifeState.Alive &&
        ammunition > 0 &&
        reloadEndsAt <= 0d &&
        now >= nextShotAt;

    public static bool CanReload(
        MatchPhase phase,
        PlayerLifeState life,
        int ammunition,
        int magazineSize,
        double reloadEndsAt) =>
        phase == MatchPhase.Playing &&
        life == PlayerLifeState.Alive &&
        ammunition < magazineSize &&
        reloadEndsAt <= 0d;

    public static bool IsEnemy(TeamId attacker, TeamId victim) =>
        attacker != TeamId.None &&
        victim != TeamId.None &&
        attacker != victim;

    public static int HealthAfterDamage(int health, int damage) =>
        damage <= 0 ? health : System.Math.Max(0, health - damage);

    public static bool IsLethalTransition(int before, int after) =>
        before > 0 && after == 0;
}
