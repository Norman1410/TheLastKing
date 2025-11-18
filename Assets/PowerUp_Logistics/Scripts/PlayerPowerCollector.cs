using UnityEngine;

public class PlayerPowerCollector : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Intenta obtener la clase base de tu poder (la que tiene el método Activate)
        PowerUp power = other.GetComponent<PowerUp>();

        if (power != null)
        {
            // 1. Verifica si hay un PowerManager activo
            if (PowerManager.Instance != null)
            {
                // 2. Llama al método Activate de tu script de poder (ej. SuperJump).
                // Es aquí donde el SuperJump llama al PowerManager.AddPower().
                power.Activate(this.gameObject);
                
                // NOTA IMPORTANTE: El script de poder (SuperJump) es responsable de DESTRUIRSE a sí mismo 
                // si PowerManager.AddPower() devuelve true.
            }
            else
            {
                Debug.LogWarning("PowerManager.Instance no encontrado. No se puede recoger el poder.");
            }
        }
    }
}