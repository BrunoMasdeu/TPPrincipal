using UnityEngine;

public class MovimientoEsferaX : MonoBehaviour
{
    [Header("Configuración del Movimiento")]
    [Tooltip("Distancia máxima que recorrerá hacia cada lado desde el punto inicial")]
    [SerializeField] private float distancia = 2.5f;

    [Tooltip("Velocidad de desplazamiento")]
    [SerializeField] private float velocidad = 2f;

    private Vector3 posicionInicial;

    private void Start()
    {
        // Guardar la posición en la que colocaste la esfera en la escena
        posicionInicial = transform.position;
    }

    private void Update()
    {
        // Movimiento lineal a velocidad constante (estilo ping-pong)
        // PingPong oscila entre 0 y (distancia * 2)
        float desplazamiento = Mathf.PingPong(Time.time * velocidad, distancia * 2f) - distancia;
        transform.position = new Vector3(posicionInicial.x + desplazamiento, posicionInicial.y, posicionInicial.z);
    }
}
