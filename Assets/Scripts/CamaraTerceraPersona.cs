using UnityEngine;
using UnityEngine.InputSystem;

public class CamaraTerceraPersona : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform target; // Acá vas a arrastrar a tu Player

    [Header("Ajustes de Cámara")]
    public float distancia = 4f; // Qué tan lejos está la cámara de la espalda
    public float altura = 1.5f; // A qué altura apunta (ideal a los hombros/cabeza)
    public float sensibilidad = 15f; // Velocidad del mouse

    private float rotacionX = 0f;
    private float rotacionY = 0f;

    void Start()
    {
        // Oculta el cursor del mouse y lo traba en el centro de la pantalla
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Usamos LateUpdate en lugar de Update para que la cámara se mueva DESPUÉS del personaje, evitando temblores.
    void LateUpdate()
    {
        if (target == null) return;

        // Leer el movimiento del mouse
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        // Calcular la rotación en base al movimiento del mouse
        rotacionY += mouseDelta.x * sensibilidad * Time.deltaTime;
        rotacionX -= mouseDelta.y * sensibilidad * Time.deltaTime;

        // Limitar la cámara para que no dé una vuelta entera por arriba de la cabeza o por abajo del piso
        rotacionX = Mathf.Clamp(rotacionX, -30f, 60f);

        // Calcular la nueva rotación de la cámara
        Quaternion rotacionCamara = Quaternion.Euler(rotacionX, rotacionY, 0);
        transform.rotation = rotacionCamara;

        // Calcular a dónde apuntamos (sumándole la altura para no mirar a los pies)
        Vector3 puntoDeMira = target.position + new Vector3(0, altura, 0);

        // Posicionar la cámara a la distancia correcta hacia atrás
        transform.position = puntoDeMira - rotacionCamara * Vector3.forward * distancia;

        // ¡CLAVE! Rotar el cuerpo del personaje (eje Y) para que siempre mire hacia donde apunta la cámara.
        // Así las teclas WASD funcionan en la dirección correcta.
        target.rotation = Quaternion.Euler(0, rotacionY, 0);
    }
}