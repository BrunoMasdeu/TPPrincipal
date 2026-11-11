using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// [ZOMBIES] Carga la malla de navegación de MapaZombies (por donde caminan los zombies).
/// Hay una sola para todo el mapa. La genera el menú
/// Tools > Zombies > Regenerar navegacion de MapaZombies.
///
/// Antes había 30 mallas, una por pieza de calle, y cada una cubría el mapa entero.
/// Como se horneaban en momentos distintos y algunas piezas después se movieron o
/// giraron, quedaban copias corridas y encimadas, y los zombies se desviaban.
/// </summary>
public class NavegacionZombies : MonoBehaviour
{
    public NavMeshData datos;

    private NavMeshDataInstance instancia;

    private void OnEnable()
    {
        if (datos != null)
            instancia = NavMesh.AddNavMeshData(datos);
        else
            Debug.LogWarning("NavegacionZombies: falta la navegación. Usá Tools > Zombies > Regenerar navegacion de MapaZombies.");
    }

    private void OnDisable()
    {
        instancia.Remove();
    }
}
