using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Ensures LanLobbyState GameObject exists in scene when MenuManager starts networking.
/// This is needed because MenuManager uses LanLobbyState for both LAN and Relay lobbies.
/// </summary>
public class LobbyBootstrap : MonoBehaviour
{
    [Header("Lobby State Prefab")]
    [SerializeField] private GameObject lanLobbyStatePrefab;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void EnsureLobbyState()
    {
        // If LanLobbyState already exists, we're good
        if (LanLobbyState.Instance != null)
            return;

        GameObject lobbyStateGO = null;

        // Try to use prefab first
        if (lanLobbyStatePrefab != null)
        {
            lobbyStateGO = Instantiate(lanLobbyStatePrefab);
        }
        else
        {
            // Create GameObject with required components
            lobbyStateGO = new GameObject("LanLobbyState");
            
            // Add NetworkObject first
            var networkObject = lobbyStateGO.AddComponent<NetworkObject>();
            
            // Add LanLobbyState component
            lobbyStateGO.AddComponent<LanLobbyState>();
            
            Debug.Log("[LobbyBootstrap] Created LanLobbyState GameObject dynamically");
        }

        if (lobbyStateGO != null)
        {
            DontDestroyOnLoad(lobbyStateGO);
        }
    }

    private void Start()
    {
        // Ensure lobby state exists on startup
        EnsureLobbyState();
    }
}