using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using System.Threading.Tasks;

public class MenuManager : MonoBehaviour
{
    [Header("Transition Panels")]
    public CanvasGroup transitionPanel;
    
    [Header("Main Menus")]
    public GameObject principalMenu;
    public GameObject multiplayerMenu;
    public GameObject multiplayerHostJoinMenu;
    
    [Header("LAN Lobby Menus")]
    public GameObject multiplayerLanHostMenu;
    public GameObject multiplayerReadyLanMenu;
    
    [Header("Online Lobby Menus")]
    public GameObject multiplayerOnlineHostMenu;
    public GameObject multiplayerJoinReadyOnlineMenu;
    
    [Header("Online Join/Ready Panels")]
    public GameObject onlineJoinPanel;
    public GameObject onlineReadyPanel;
    
    [Header("Online Input Fields")]
    public TMP_InputField joinCodeInput;
    public TMP_Text hostJoinCodeDisplay;

    [Header("Prefabs (Host Only)")]
    [Tooltip("Prefab with NetworkObject + LanLobbyState. Must be registered in NetworkManager -> Network Prefabs.")]
    [SerializeField] private NetworkObject lanLobbyStatePrefab;

    [Header("Player Name (optional)")]
    public TMP_InputField playerNameInput;

    [Header("LAN Join (optional)")]
    public TMP_InputField lanIpInput; // If empty, defaults to 127.0.0.1

    private void Awake()
    {
        // Ensure cursor is available for UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // CRITICAL: Disable conflicting OnGUI systems FIRST
        DisableConflictingSystems();
    }

    private void Start()
    {
        principalMenu.SetActive(true);
        multiplayerMenu.SetActive(false);
        multiplayerHostJoinMenu.SetActive(false);
        
        if (multiplayerLanHostMenu) multiplayerLanHostMenu.SetActive(false);
        if (multiplayerOnlineHostMenu) multiplayerOnlineHostMenu.SetActive(false);
        if (multiplayerReadyLanMenu) multiplayerReadyLanMenu.SetActive(false);
        if (multiplayerJoinReadyOnlineMenu) multiplayerJoinReadyOnlineMenu.SetActive(false);
        
        if (onlineJoinPanel) onlineJoinPanel.SetActive(true);
        if (onlineReadyPanel) onlineReadyPanel.SetActive(false);
        
        if (transitionPanel) transitionPanel.alpha = 0;
    }

    public void GoToMultiplayerMenu()
    {
        principalMenu.SetActive(false);
        multiplayerMenu.SetActive(true);
    }

    public void BackToPrincipalFromMultiplayer()
    {
        multiplayerMenu.SetActive(false);
        principalMenu.SetActive(true);
    }

    public void GoToHostJoinMenu()
    {
        multiplayerMenu.SetActive(false);
        multiplayerHostJoinMenu.SetActive(true);
    }

    public void BackToMultiplayerFromHostJoin()
    {
        multiplayerHostJoinMenu.SetActive(false);
        multiplayerMenu.SetActive(true);
    }

    // ===== SELECCIÓN LAN/ONLINE =====
    // Llamar desde botones "LAN" y "Online" en Multiplayer Menu
    
    public void SelectLan()
    {
        NetRuntime.Mode = NetMode.LAN;
        GoToHostJoinMenu();
    }

    public void SelectOnline()
    {
        NetRuntime.Mode = NetMode.Relay;
        GoToHostJoinMenu();
    }

    // ===== GENERIC HOST/JOIN BUTTONS =====
    // Call from "Host" and "Join" buttons in Host/Join Menu
    // Auto-detect LAN or Online based on NetRuntime.Mode

    public void HostButtonPressed()
    {
        if (NetRuntime.Mode == NetMode.LAN)
        {
            StartLanHost();
        }
        else if (NetRuntime.Mode == NetMode.Relay)
        {
            StartOnlineHost();
        }
        else
        {
            Debug.LogWarning("No se ha seleccionado LAN o Online. Usa SelectLan() o SelectOnline() primero.");
        }
    }

    public void JoinButtonPressed()
    {
        if (NetRuntime.Mode == NetMode.LAN)
        {
            JoinLanGame();
        }
        else if (NetRuntime.Mode == NetMode.Relay)
        {
            // For Online, just show the Join/Ready menu where user can enter code
            ShowOnlineJoinMenu();
        }
        else
        {
            Debug.LogWarning("LAN or Online mode not selected. Use SelectLan() or SelectOnline() first.");
        }
    }

    // Show Online Join menu (user will enter code here)
    private void ShowOnlineJoinMenu()
    {
        multiplayerHostJoinMenu.SetActive(false);
        multiplayerJoinReadyOnlineMenu.SetActive(true);
        
        if (onlineJoinPanel) onlineJoinPanel.SetActive(true);
        if (onlineReadyPanel) onlineReadyPanel.SetActive(false);
    }

    // ===== LAN/ONLINE HOST/JOIN =====

    public void StartLanHost()
    {
        // CRITICAL: Shutdown any existing network session first
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[MenuManager] Shutting down existing network session before starting new host...");
            NetworkManager.Singleton.Shutdown();
            
            // Wait for shutdown to complete, then start
            StartCoroutine(StartLanHostDelayed());
            return;
        }

        StartLanHostImmediately();
    }

    private System.Collections.IEnumerator StartLanHostDelayed()
    {
        // Wait one frame for shutdown to complete
        yield return null;
        
        Debug.Log("[MenuManager] Starting LAN host after shutdown...");
        StartLanHostImmediately();
    }

    private void StartLanHostImmediately()
    {
        NetRuntime.Mode = NetMode.LAN;

        // Save chosen player name (if provided via input or from PlayerName)
        ApplyPlayerName();

        // Prevent PlayerObject auto-spawn while in lobby
        ConfigureLobbyConnectionApproval();

        // Start LAN host
        LanStartHelpers.StartLanHost();
        
        // NOW create lobby state AFTER network is active
        EnsureLobbyState();
        
        multiplayerHostJoinMenu.SetActive(false);
        multiplayerLanHostMenu.SetActive(true);

        // Ensure cursor is free in lobby
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void LeaveLanHost()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        multiplayerLanHostMenu.SetActive(false);
        multiplayerHostJoinMenu.SetActive(true);
    }

    public void JoinLanGame()
    {
        // CRITICAL: Shutdown any existing network session first
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[MenuManager] Shutting down existing network session before joining...");
            NetworkManager.Singleton.Shutdown();
            
            // Wait for shutdown to complete, then join
            StartCoroutine(JoinLanGameDelayed());
            return;
        }

        JoinLanGameImmediately();
    }

    private System.Collections.IEnumerator JoinLanGameDelayed()
    {
        // Wait one frame for shutdown to complete
        yield return null;
        
        Debug.Log("[MenuManager] Joining LAN game after shutdown...");
        JoinLanGameImmediately();
    }

    private void JoinLanGameImmediately()
    {
        NetRuntime.Mode = NetMode.LAN;

        // Save chosen player name (if provided via input or from PlayerName)
        ApplyPlayerName();

        // Prevent PlayerObject auto-spawn while in lobby
        ConfigureLobbyConnectionApproval();

        // Start LAN client (use IP from input or 127.0.0.1 by default)
        var ip = GetLanIpOrDefault();
        LanStartHelpers.StartLanClient(ip);
        
        multiplayerHostJoinMenu.SetActive(false);
        multiplayerReadyLanMenu.SetActive(true);

        // Ensure cursor is free in lobby
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void LeaveLanReady()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        multiplayerReadyLanMenu.SetActive(false);
        multiplayerHostJoinMenu.SetActive(true);
    }

    public async void StartOnlineHost()
    {
        // Don't start if already running
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("[MenuManager] Network already running, cannot start host");
            return;
        }

        NetRuntime.Mode = NetMode.Relay;

        // Save chosen player name (if provided via input or from PlayerName)
        ApplyPlayerName();

        // Prevent PlayerObject auto-spawn while in lobby
        ConfigureLobbyConnectionApproval();

    await RelayStartHelpers.CreateAndStartHost();
        
    // Ensure LobbyState exists on host via prefab
    EnsureLobbyStateHost();
        
        string joinCode = RelayStartHelpers.LastJoinCode;
        
        if (!string.IsNullOrEmpty(joinCode))
        {
            if (hostJoinCodeDisplay != null)
            {
                hostJoinCodeDisplay.text = joinCode;
            }
            
            multiplayerHostJoinMenu.SetActive(false);
            multiplayerOnlineHostMenu.SetActive(true);

            // Ensure cursor is free in lobby
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Debug.LogError("Failed to create Relay host");
        }
    }

    public void LeaveOnlineHost()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        multiplayerOnlineHostMenu.SetActive(false);
        multiplayerHostJoinMenu.SetActive(true);
    }

    public async void JoinOnlineGame()
    {
        if (joinCodeInput == null || string.IsNullOrEmpty(joinCodeInput.text))
        {
            // UX: If no code yet, just navigate to the Online Join/Ready menu (Join panel visible)
            if (multiplayerHostJoinMenu) multiplayerHostJoinMenu.SetActive(false);
            if (multiplayerJoinReadyOnlineMenu) multiplayerJoinReadyOnlineMenu.SetActive(true);
            if (onlineJoinPanel) onlineJoinPanel.SetActive(true);
            if (onlineReadyPanel) onlineReadyPanel.SetActive(false);
            return;
        }

        // Don't start if already running
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("[MenuManager] Network already running, cannot join");
            return;
        }

        NetRuntime.Mode = NetMode.Relay;

        // Save chosen player name (if provided via input or from PlayerName)
        ApplyPlayerName();

        // Prevent PlayerObject auto-spawn while in lobby
        ConfigureLobbyConnectionApproval();

        try
        {
            Debug.Log($"[MenuManager] Attempting to join Relay with code: {joinCodeInput.text}");
            await RelayStartHelpers.JoinAsClient(joinCodeInput.text);
            
            // Switch from Join panel to Ready panel (already in multiplayerJoinReadyOnlineMenu)
            if (onlineJoinPanel) onlineJoinPanel.SetActive(false);
            if (onlineReadyPanel) onlineReadyPanel.SetActive(true);

            // Ensure cursor is free in lobby
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            Debug.Log("[MenuManager] Successfully joined Relay game");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to join Relay game: {ex.Message}");
        }
    }

    public void LeaveOnlineReady()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        multiplayerJoinReadyOnlineMenu.SetActive(false);
        multiplayerHostJoinMenu.SetActive(true);
        
        if (onlineJoinPanel) onlineJoinPanel.SetActive(true);
        if (onlineReadyPanel) onlineReadyPanel.SetActive(false);
    }

    public void StartGameAsHost()
    {
        if (LanLobbyState.Instance != null)
        {
            Debug.Log("[MenuManager] Starting game as host...");
            LanLobbyState.Instance.StartMatchAsHost();
        }
        else
        {
            Debug.LogError("[MenuManager] Cannot start game: LanLobbyState.Instance is null!");
        }
    }

    // Host-only: ensure lobby state exists by instantiating a registered prefab and spawning it
    private void EnsureLobbyStateHost()
    {
        if (LanLobbyState.Instance != null)
            return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return; // only server/host creates it

        if (lanLobbyStatePrefab == null)
        {
            Debug.LogError("[MenuManager] lanLobbyStatePrefab is not assigned. Create a prefab with NetworkObject + LanLobbyState and assign it here, and register it in NetworkManager -> Network Prefabs.");
            return;
        }

        var instance = Instantiate(lanLobbyStatePrefab);
        var netObj = instance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[MenuManager] The assigned lanLobbyStatePrefab has no NetworkObject component.");
            Destroy(instance);
            return;
        }

        if (!netObj.IsSpawned)
            netObj.Spawn();

        Debug.Log("[MenuManager] Spawned LanLobbyState from prefab on host.");
    }

    // Close active connections and exit game (Exit button)
    public void ExitGame()
    {
        // Shutdown network if running
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

#if UNITY_EDITOR
        // Stop Play Mode in the Editor
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Quit application in build
        Application.Quit();
#endif
    }

    public void ToggleReady()
    {
        if (LanLobbyState.Instance != null && NetworkManager.Singleton != null)
        {
            bool currentReady = GetCurrentPlayerReadyState();
            LanLobbyState.Instance.ToggleReadyServerRpc(!currentReady);
        }
    }

    private bool GetCurrentPlayerReadyState()
    {
        if (LanLobbyState.Instance == null || NetworkManager.Singleton == null)
            return false;

        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        var players = LanLobbyState.Instance.Players;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].ClientId == localClientId)
                return players[i].Ready;
        }

        return false;
    }

    // ===== Helpers =====
    private void ApplyPlayerName()
    {
        // Priority 1: Use playerNameInput if provided
        if (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text))
        {
            PlayerName.Set(playerNameInput.text.Trim());
            Debug.Log($"[MenuManager] Player name set from input: {PlayerName.Get()}");
            return;
        }

        // Priority 2: PlayerName is already set (from NicknameManager or previous save)
        // Just make sure it's loaded - PlayerName.Get() will return the saved name or "Jugador"
        string currentName = PlayerName.Get();
        Debug.Log($"[MenuManager] Using existing player name: {currentName}");
    }

    private void ConfigureLobbyConnectionApproval()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Enable ConnectionApproval to control PlayerObject creation
        nm.NetworkConfig.ConnectionApproval = true;
        nm.ConnectionApprovalCallback = (req, resp) =>
        {
            resp.Approved = true;
            // Key: do NOT create PlayerObject in the lobby
            resp.CreatePlayerObject = false;
            resp.Pending = false;
        };
    }

    private string GetLanIpOrDefault()
    {
        var ip = lanIpInput != null ? lanIpInput.text : null;
        if (string.IsNullOrWhiteSpace(ip)) ip = "127.0.0.1";
        return ip.Trim();
    }

    private void EnsureLobbyState()
    {
        // Backward compatibility: call the host-specific path
        EnsureLobbyStateHost();
    }

    private void DisableConflictingSystems()
    {
        // Set NetRuntime to None to prevent automatic activation
        NetRuntime.Mode = NetMode.None;

        // Find and DESTROY (not just disable) conflicting OnGUI systems
        var netModeSelector = FindFirstObjectByType<NetModeSelector>();
        if (netModeSelector != null)
        {
            Destroy(netModeSelector.gameObject);
            Debug.Log("[MenuManager] Destroyed NetModeSelector (gray popup box)");
        }

        var lobbyController = FindFirstObjectByType<LobbyController>();
        if (lobbyController != null)
        {
            lobbyController.enabled = false;
            Debug.Log("[MenuManager] Disabled LobbyController");
        }

        var quickJoinUI = FindFirstObjectByType<QuickJoinLocalUI>();
        if (quickJoinUI != null)
        {
            quickJoinUI.enabled = false;
            Debug.Log("[MenuManager] Disabled QuickJoinLocalUI");
        }

        var relayOverlay = FindFirstObjectByType<QuickRelayOverlay>();
        if (relayOverlay != null)
        {
            relayOverlay.enabled = false;
            Debug.Log("[MenuManager] Disabled QuickRelayOverlay");
        }

        Debug.Log("[MenuManager] Disabled/destroyed conflicting multiplayer systems");
    }

    // Emergency cleanup method - call this if you get "port already in use" errors
    public void ForceNetworkCleanup()
    {
        Debug.Log("[MenuManager] Force cleanup: Shutting down all network connections...");
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        // Destroy lobby state if it exists
        if (LanLobbyState.Instance != null)
        {
            Destroy(LanLobbyState.Instance.gameObject);
        }
        
        // Reset mode
        NetRuntime.Mode = NetMode.None;
        
        Debug.Log("[MenuManager] Force cleanup complete. You can now start a new session.");
    }
}
