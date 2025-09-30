using UnityEngine;
using System;

public class PowerUp : MonoBehaviour
{
    public Action onPicked;    // Evento que el manager usar�
    public float duration = 5f;

    public virtual void Activate(GameObject player)
    {
        // Aqu� se define qu� hace el power-up; cada hijo lo sobrescribir�
        
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
