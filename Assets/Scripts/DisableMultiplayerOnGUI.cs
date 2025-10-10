using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Disables all OnGUI multiplayer systems when MenuManager is handling networking.
/// This prevents conflicts between different networking UI systems.
/// </summary>
public class DisableMultiplayerOnGUI : MonoBehaviour
{
    private void Awake()
    {
        // Disable all conflicting OnGUI systems when MenuManager is active
        DisableOnGUISystems();
    }

    private void DisableOnGUISystems()
    {
        // Find and disable NetModeSelector (causes popup in center)
        var netModeSelector = FindObjectOfType<NetModeSelector>();
        if (netModeSelector != null)
        {
            netModeSelector.enabled = false;
            Debug.Log("[DisableMultiplayerOnGUI] Disabled NetModeSelector");
        }

        // Find and disable LobbyController (OnGUI with LAN/Relay buttons)
        var lobbyController = FindObjectOfType<LobbyController>();
        if (lobbyController != null)
        {
            lobbyController.enabled = false;
            Debug.Log("[DisableMultiplayerOnGUI] Disabled LobbyController");
        }

        // Find and disable QuickJoinLocalUI (LAN harness)
        var quickJoinUI = FindObjectOfType<QuickJoinLocalUI>();
        if (quickJoinUI != null)
        {
            quickJoinUI.enabled = false;
            Debug.Log("[DisableMultiplayerOnGUI] Disabled QuickJoinLocalUI");
        }

        // Find and disable QuickRelayOverlay (Relay harness)
        var relayOverlay = FindObjectOfType<QuickRelayOverlay>();
        if (relayOverlay != null)
        {
            relayOverlay.enabled = false;
            Debug.Log("[DisableMultiplayerOnGUI] Disabled QuickRelayOverlay");
        }

        // Set NetRuntime.Mode to None to prevent automatic systems from activating
        NetRuntime.Mode = NetMode.None;
        Debug.Log("[DisableMultiplayerOnGUI] Set NetRuntime.Mode to None");
    }

    private void Start()
    {
        // Ensure cursor is unlocked and visible for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}