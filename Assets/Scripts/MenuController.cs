using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private string mainMenuSceneName = "PrincipalMenu";
    private bool isPaused = false;
    // Keep track of canvas sorting state so we can restore when hiding
    private Canvas pauseCanvas;
    private bool prevOverrideSorting = false;
    private int prevSortingOrder = 0;

    void Start()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false); 
            pauseCanvas = pausePanel.GetComponent<Canvas>();
            if (pauseCanvas != null)
            {
                prevOverrideSorting = pauseCanvas.overrideSorting;
                prevSortingOrder = pauseCanvas.sortingOrder;
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    // If paused and the player presses Enter, exit to main menu and properly stop multiplayer
    void LateUpdate()
    {
        if (isPaused && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            ExitToMainMenuFromPause();
        }
    }

    public void TogglePause()
    {
        if (pausePanel == null) return;

        isPaused = !isPaused;

        if (isPaused)
        {
            // Ensure the pause panel is on top of other UI
            pausePanel.SetActive(true);
            // Move to last sibling so it's rendered on top within its parent
            pausePanel.transform.SetAsLastSibling();

            // If there's a Canvas on the panel, force its sorting to be on top
            pauseCanvas = pauseCanvas ?? pausePanel.GetComponent<Canvas>();
            if (pauseCanvas != null)
            {
                // store previous state (in case Start didn't run or canvas was added later)
                prevOverrideSorting = pauseCanvas.overrideSorting;
                prevSortingOrder = pauseCanvas.sortingOrder;
                pauseCanvas.overrideSorting = true;
                pauseCanvas.sortingOrder = 1000; // large value to ensure on top
            }
        }
        else
        {
            // Hide and restore canvas sorting
            pausePanel.SetActive(false);
            if (pauseCanvas != null)
            {
                pauseCanvas.overrideSorting = prevOverrideSorting;
                pauseCanvas.sortingOrder = prevSortingOrder;
            }
        }

        Cursor.visible = isPaused;
        Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
    }

    public void ReturnToMainMenu()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    void ExitToMainMenuFromPause()
    {
        // If there's no NetworkManager, just load menu
        var nm = NetworkManager.Singleton;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // First hide pause UI
        if (pausePanel != null) pausePanel.SetActive(false);
        isPaused = false;

        if (nm == null)
        {
            // Clean up any persistent network/bootstrap objects that may have been created
            CleanupPersistentNetworkObjects();
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        if (nm.IsServer)
        {
            // Host: ask NetworkManager to load the menu scene for all clients (server-driven scene change)
            // This will instruct clients to switch scenes. After a short delay, shutdown the network.
            try
            {
                nm.SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
            }
            catch
            {
                // fallback to local load if networked load fails
                SceneManager.LoadScene(mainMenuSceneName);
            }

            // Ask server to load menu for all clients, then shutdown host after a short delay
            try
            {
                nm.SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
            }
            catch { SceneManager.LoadScene(mainMenuSceneName); }

            StartCoroutine(ShutdownNetworkManagerDelayed(nm, 0.5f));
        }
        else
        {
            // Client: just shutdown network and go back to menu locally
            nm.Shutdown();

            try { NetRuntime.Mode = NetMode.None; } catch { }

            if (nm.gameObject != null) Destroy(nm.gameObject);
            // Clean up other persistent bootstrap/network objects
            CleanupPersistentNetworkObjects();

            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    System.Collections.IEnumerator ShutdownNetworkManagerDelayed(Unity.Netcode.NetworkManager nm, float delay)
    {
        yield return new WaitForSeconds(delay);
        try { nm.Shutdown(); } catch { }
        try { NetRuntime.Mode = NetMode.None; } catch { }
        try { if (nm.gameObject != null) Destroy(nm.gameObject); } catch { }
        CleanupPersistentNetworkObjects();
    }

    // Destroy known persistent objects created by the multiplayer/bootstrap subsystems so the main menu
    // returns to the clean initial state seen on first launch.
    void CleanupPersistentNetworkObjects()
    {
        // Known names created by UnifiedBootstrap/NetSetupOnce/Relay helpers
        string[] names = new string[] {
            "BootstrapRunner",
            "BootstrapRunner(Clone)",
            "LanHudSuppressor",
            "NetModeSelector",
            "UnifiedBootstrap_SceneWatcher",
            "LAN_QuickJoin_UI",
            "LAN_QuickJoin_UI(Clone)",
            "NetworkManager_BOOT",
            "NetworkManager",
            "Debug Updater",
            "[Debug Updater]"
        };

        foreach (var n in names)
        {
            try {
                var go = GameObject.Find(n);
                if (go != null)
                {
                    Destroy(go);
                }
            } catch { }
        }

        // Also try destroying by type names (LanLobbyState, QuickJoinLocalUI, NetUIAnchor)
        string[] typeNames = new string[] { "LanLobbyState", "QuickJoinLocalUI", "NetUIAnchor", "LanHudSuppressor" };
        foreach (var tname in typeNames)
        {
            try
            {
                System.Type t = System.Type.GetType(tname);
                if (t == null)
                {
                    // Try scanning assemblies for the type name (non-LINQ loop)
                    var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                    foreach (var asm in assemblies)
                    {
                        System.Type[] types = null;
                        try { types = asm.GetTypes(); } catch { }
                        if (types == null) continue;
                        foreach (var tt in types)
                        {
                            if (tt.Name == tname) { t = tt; break; }
                        }
                        if (t != null) break;
                    }
                }
                if (t != null)
                {
                    var objs = Resources.FindObjectsOfTypeAll(t);
                    foreach (var o in objs)
                    {
                        if (o == null) continue;
                        // Only destroy objects that are scene instances (including DontDestroyOnLoad)
                        if (o is GameObject go)
                        {
                            try {
                                var s = go.scene;
                                if (s.IsValid()) Destroy(go);
                            } catch { }
                        }
                        else if (o is Component c && c.gameObject != null)
                        {
                            try {
                                var s = c.gameObject.scene;
                                if (s.IsValid()) Destroy(c.gameObject);
                            } catch { }
                        }
                        else
                        {
                            // Skip destroying assets (we don't want to delete project assets at runtime)
                        }
                    }
                }
            }
            catch { }
        }

        // Force a GC of lingering singletons (best-effort)
        Resources.UnloadUnusedAssets();
    }

    public void ResumeGame()
    {
        if (pausePanel == null) return;

        isPaused = false;
        pausePanel.SetActive(false);
        if (pauseCanvas != null)
        {
            pauseCanvas.overrideSorting = prevOverrideSorting;
            pauseCanvas.sortingOrder = prevSortingOrder;
        }
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}