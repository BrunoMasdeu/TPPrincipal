using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class ParkourMovement : MonoBehaviour
{
    [Header("Parámetros de Movimiento")]
    public float velocidadCaminar = 5f;
    public float velocidadCorrer = 10f;
    public float fuerzaSalto = 5f; // Ahora usamos fuerza de impulso en vez de altura matemática

    [Header("Sistema de Caídas")]
    public Transform puntoDeRespawn;

    private Rigidbody rb;
    private bool enElSuelo;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        // 1. Chequeo de suelo mediante un rayo láser invisible hacia abajo
        enElSuelo = Physics.Raycast(transform.position, Vector3.down, 1.1f);

        // 2. Mecánica de Salto
        if (Keyboard.current.spaceKey.wasPressedThisFrame && enElSuelo)
        {
            // Reseteamos la inercia vertical antes de saltar y aplicamos la fuerza
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * fuerzaSalto, ForceMode.Impulse);
        }
    }

    // FixedUpdate es obligatorio cuando movemos un Rigidbody
    void FixedUpdate()
    {
        if (Keyboard.current == null) return;

        // 3. Inputs de movimiento
        float x = 0f;
        float z = 0f;

        if (Keyboard.current.dKey.isPressed) x += 1f;
        if (Keyboard.current.aKey.isPressed) x -= 1f;
        if (Keyboard.current.wKey.isPressed) z += 1f;
        if (Keyboard.current.sKey.isPressed) z -= 1f;

        float velocidadActual = Keyboard.current.leftShiftKey.isPressed ? velocidadCorrer : velocidadCaminar;

        // 4. Aplicar movimiento respetando la gravedad (velocity.y)
        Vector3 direccion = (transform.right * x + transform.forward * z).normalized;
        rb.linearVelocity = new Vector3(direccion.x * velocidadActual, rb.linearVelocity.y, direccion.z * velocidadActual);
    }
}