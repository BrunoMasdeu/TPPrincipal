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
        if (tumbada) return;

        tumbada = true;
        OnTargetDestroyed?.Invoke(gameObject.tag);
        StartCoroutine(DerribarDiana());
    }

    private IEnumerator DerribarDiana()
    {
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
    }
}
