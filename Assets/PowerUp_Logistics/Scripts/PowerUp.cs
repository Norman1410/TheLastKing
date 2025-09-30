using UnityEngine;
using System;

public class PowerUp : MonoBehaviour
{
    public Action onPicked;    // Evento que el manager usará
    public float duration = 5f;

    public virtual void Activate(GameObject player)
    {
        // Aquí se define qué hace el power-up; cada hijo lo sobrescribirá
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Activate(other.gameObject);
            onPicked?.Invoke();
            gameObject.SetActive(false);
        }
    }
}
