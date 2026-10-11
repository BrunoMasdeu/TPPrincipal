public static class CombatValidationRules
{
    public static bool IsCombatMode(GameModeId mode) =>
        mode == GameModeId.TDM || mode == GameModeId.CTF;

    // Compatibilidad temporal: Race abre hoy CentroEntrenamiento. Esto permite
    // probar el inventario allí sin convertirlo en un modo competitivo.
    public static bool SupportsInventoryPreview(GameModeId mode) =>
        IsCombatMode(mode) || mode == GameModeId.Race;

    public static bool CanFire(
        MatchPhase phase,
        PlayerLifeState life,
        int ammunition,
        double reloadEndsAt,
        double nextShotAt,
        double now) =>
        CanAttack(
            phase, life, true, ammunition,
            reloadEndsAt, nextShotAt, now);

    public static bool CanAttack(
        MatchPhase phase,
        PlayerLifeState life,
        bool usesAmmunition,
        int ammunition,
        double reloadEndsAt,
        double nextAttackAt,
        double now) =>
        phase == MatchPhase.Playing &&
        life == PlayerLifeState.Alive &&
        (!usesAmmunition || ammunition > 0) &&
        reloadEndsAt <= 0d &&
        now >= nextAttackAt;

    public static bool CanReload(
        MatchPhase phase,
        PlayerLifeState life,
        int ammunition,
        int magazineSize,
        double reloadEndsAt,
        int reserveAmmunition = int.MaxValue) =>
        phase == MatchPhase.Playing &&
        life == PlayerLifeState.Alive &&
        magazineSize > 0 &&
        ammunition < magazineSize &&
        reserveAmmunition > 0 &&
        reloadEndsAt <= 0d;

    public static int CalculateReloadTransfer(
        int ammunition,
        int magazineSize,
        int reserveAmmunition)
    {
        int missing = System.Math.Max(0, magazineSize - ammunition);
        return System.Math.Min(missing, System.Math.Max(0, reserveAmmunition));
    }

    public static bool IsEnemy(TeamId attacker, TeamId victim) =>
        attacker != TeamId.None &&
        victim != TeamId.None &&
        attacker != victim;

    public static int HealthAfterDamage(int health, int damage) =>
        damage <= 0 ? health : System.Math.Max(0, health - damage);

    public static bool IsLethalTransition(int before, int after) =>
        before > 0 && after == 0;
}
