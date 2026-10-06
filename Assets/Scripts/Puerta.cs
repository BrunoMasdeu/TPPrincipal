using UnityEngine;
using System.Collections;

public class Puerta : MonoBehaviour
{
    public float duracion = 1f;
    public float angulo = 90f;

    private bool abierta = false;

    public void Abrir()
    {
        Debug.Log("ABRIR() fue ejecutado");

        if (!abierta)
        {
            StartCoroutine(AbrirPuerta());
        }
    }

    private IEnumerator AbrirPuerta()
    {
        Debug.Log("CORRUTINA iniciada");

        abierta = true;

        Quaternion inicial = transform.rotation;
        Quaternion final = inicial * Quaternion.Euler(0, angulo, 0);

        float tiempo = 0f;

        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;

            float progreso = tiempo / duracion;

            transform.rotation = Quaternion.Slerp(
                inicial,
                final,
                progreso
            );

            yield return null;
        }

        transform.rotation = final;

        Debug.Log("PUERTA ABIERTA");
    }
}


