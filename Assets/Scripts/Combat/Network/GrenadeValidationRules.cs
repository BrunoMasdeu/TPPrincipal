using UnityEngine;

/// <summary>
/// Reglas puras del lanzamiento de granadas. Se mantienen separadas del
/// NetworkBehaviour para poder probar la validacion sin iniciar una sesion.
/// </summary>
public static class GrenadeValidationRules
{
    public static bool CanThrow(
        MatchPhase phase,
        PlayerLifeState lifeState,
        int grenadeCount,
        double nextThrowAt,
        double now) =>
        phase == MatchPhase.Playing &&
        lifeState == PlayerLifeState.Alive &&
        grenadeCount > 0 &&
        now >= nextThrowAt;

    public static bool IsAimValid(
        Vector3 aimOrigin,
        Vector3 aimDirection,
        Vector3 playerPosition,
        float maximumOriginDistance = 4f) =>
        IsFinite(aimOrigin) &&
        IsFinite(aimDirection) &&
        Vector3.Distance(aimOrigin, playerPosition) <= maximumOriginDistance &&
        aimDirection.sqrMagnitude >= 0.9f &&
        aimDirection.sqrMagnitude <= 1.1f;

    public static Vector3 CalculateInitialVelocity(
        Vector3 aimDirection,
        float throwForce,
        float upwardForce) =>
        aimDirection.normalized * Mathf.Max(0f, throwForce) +
        Vector3.up * Mathf.Max(0f, upwardForce);

    public static int CountAfterThrow(int currentCount) =>
        Mathf.Max(0, currentCount - 1);

    private static bool IsFinite(Vector3 vector) =>
        IsFinite(vector.x) && IsFinite(vector.y) && IsFinite(vector.z);

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
