using System;
using System.Collections;
using UnityEngine;

public class Diana : MonoBehaviour
{
    public static event Action<string> OnTargetDestroyed;
    public float duracion = 0.5f;
    public float angulo = -70f;

    private bool tumbada = false;

    public void Derribar()
    {
        Debug.Log("TUMBAR() fue ejecutado");
        OnTargetDestroyed?.Invoke(gameObject.tag);
        if (!tumbada)
        {
            StartCoroutine(DerribarDiana());
        }
    }

    private IEnumerator DerribarDiana()
    {
        Debug.Log("CORRUTINA iniciada");

        tumbada = true;

        Quaternion inicial = transform.rotation;
        Quaternion final = inicial * Quaternion.Euler(angulo, 0, 0);

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

        Debug.Log("Diana derribada");
    }
}
