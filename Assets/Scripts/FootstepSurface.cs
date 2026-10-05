using UnityEngine;

/// <summary>
/// Colocar este componente en cualquier objeto de piso (suelo, plataforma,
/// escalera, etc.) para indicar qué tipo de sonido de pasos le corresponde.
/// FootstepAudio lo busca automáticamente con un raycast hacia abajo.
///
/// Si un objeto de piso no tiene este componente, FootstepAudio usa el
/// sonido "Default" configurado en el propio jugador, así no hace falta
/// marcar absolutamente todo para que el sistema funcione.
/// </summary>
public class FootstepSurface : MonoBehaviour
{
    public SurfaceType surfaceType = SurfaceType.Tile;
}

public enum SurfaceType
{
    Tile,
    Metal,
    Wood,
    Grass,
    Gravel,
    Sand,
    Snow,
    Water,
    Mud,
    Rock,
    Leaves,
    DirtyGround
}
