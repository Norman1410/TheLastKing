using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

/// <summary>
/// Script helper para mostrar/ocultar o habilitar/deshabilitar el botón Start
/// según si el jugador es Host o Cliente.
/// Solo el host puede iniciar la partida.
/// </summary>
public class HostOnlyButton : MonoBehaviour
{
    [SerializeField] private Button targetButton;
    
    [Header("Comportamiento")]
    [Tooltip("Si es true, oculta el botón. Si es false, solo lo deshabilita.")]
    [SerializeField] private bool hideForClients = true;

    private void Update()
    {
        if (targetButton == null) return;
        
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            // No hay conexión aún, ocultar/deshabilitar
            ApplyState(false);
            return;
        }

        bool isHost = NetworkManager.Singleton.IsHost;
        ApplyState(isHost);
    }

    private void ApplyState(bool shouldBeActive)
    {
        if (hideForClients)
        {
            // Ocultar/mostrar el botón
            targetButton.gameObject.SetActive(shouldBeActive);
        }
        else
        {
            // Solo habilitar/deshabilitar
            targetButton.interactable = shouldBeActive;
        }
    }

    // Asignar el botón automáticamente si está en el mismo GameObject
    private void Reset()
    {
        targetButton = GetComponent<Button>();
    }
}
