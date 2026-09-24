using UnityEngine;

public class PenduloHacha : MonoBehaviour
{
    [Header("Configuración de Péndulo (Estilo Fall Guys)")]
    [Tooltip("Ángulo máximo de inclinación hacia cada lado")]
    [SerializeField] private float maxAngle = 75f;

    [Tooltip("Velocidad de oscilación")]
    [SerializeField] private float speed = 2.5f;

    [Tooltip("Eje sobre el cual oscilará (X o Z habitualmente)")]
    [SerializeField] private Vector3 rotationAxis = Vector3.forward;

    [Header("Opción: Rotación Continua 360°")]
    [SerializeField] private bool continuousRotation = false;
    [SerializeField] private float rotationSpeed360 = 120f;

    private Quaternion initialRotation;

    private void Start()
    {
        initialRotation = transform.localRotation;
    }

    private void Update()
    {
        if (continuousRotation)
        {
            // Giro continuo de 360 grados como molino
            transform.Rotate(rotationAxis * rotationSpeed360 * Time.deltaTime, Space.Self);
        }
        else
        {
            // Movimiento armónico de vaivén (Péndulo sinusoidal)
            float angle = Mathf.Sin(Time.time * speed) * maxAngle;
            transform.localRotation = initialRotation * Quaternion.AngleAxis(angle, rotationAxis);
        }
    }
}
