using UnityEngine;
using Unity.Netcode;   // 👈 IMPORTANTE

public class PlayerPowerCollector : MonoBehaviour
{
    private NetworkObject netObj;

    private void Awake()
    {
        // Buscamos el NetworkObject del jugador dueño de este colector
        netObj = GetComponentInParent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogWarning("[PlayerPowerCollector] No se encontró NetworkObject en los padres.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 🔒 Solo el jugador LOCAL en este cliente puede recoger poderes
        if (netObj != null && !netObj.IsOwner)
            return;

        // Intenta obtener la clase base de tu poder (la que tiene el método Activate)
        PowerUp power = other.GetComponent<PowerUp>();

        if (power != null)
        {
            // Verifica si hay un PowerManager activo (del jugador local)
            if (PowerManager.Instance != null)
            {
                // Aquí el SuperJump (y otros) llaman a PowerManager.AddPower()
                power.Activate(this.gameObject);
            }
            else
            {
                Debug.LogWarning("PowerManager.Instance no encontrado. No se puede recoger el poder.");
            }
        }
    }
}
