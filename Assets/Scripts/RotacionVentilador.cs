using UnityEngine;

public class RotacionVentilador : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidadRotacion = 160f; // Ajustá este número para que gire más rápido o más lento

    void Update()
    {
        // Esto hace que la hélice gire constantemente. 
        // Si gira para un lado que no querés, cambiale el eje (ej: transform.Rotate(0, velocidadRotacion * Time.deltaTime, 0); )
        transform.Rotate(0, 0, velocidadRotacion * Time.deltaTime);
    }
}