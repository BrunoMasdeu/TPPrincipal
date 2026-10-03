using UnityEngine;
using System.Collections;

public class movimientoLateral : MonoBehaviour
{
    public float velocidad = 3.0f;
    private float limiteIzquierdo = -5.0f;
    private float limiteDerecho = 5.0f;
    public float distanciaMovimiento = 1.0f;
    private int direccion = 1;
    public int health = 100;

void Start()
    {
        limiteIzquierdo = transform.position.z - distanciaMovimiento;
        limiteDerecho = transform.position.z + distanciaMovimiento;
    }

    void Update()
    {
        transform.Translate(Vector3.right * direccion * velocidad * Time.deltaTime);

        if (transform.position.z >= limiteDerecho)
        {
            direccion = -1;
        }
        else if (transform.position.z <= limiteIzquierdo)
        {
            direccion = 1;
        }
    }
    
    public void TakeDamage(int damage) { 
        health -= damage; 
        Debug.Log("Vida del enemigo: " + health); 
        if (health <= 0) 
        { 
            Die(); 
        } 
    }

    private void Die() 
    { 
        Debug.Log("Enemigo muerto"); 
        Destroy(gameObject);
    }
}
