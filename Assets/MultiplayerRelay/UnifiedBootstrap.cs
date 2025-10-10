using UnityEngine;

public static class UnifiedBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (NetRuntime.Mode == NetMode.None)
        {
            // Only spawn the selector when the gameplay scene is active or a NetUIAnchor/LanLobbyState exists.
            // This prevents the selector from appearing while the main menu is showing.
            bool hasAnchorOrLobby =
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                Object.FindAnyObjectByType<NetUIAnchor>() != null ||
                Object.FindAnyObjectByType<LanLobbyState>() != null;
#else
                Object.FindObjectOfType<NetUIAnchor>() != null ||
                Object.FindObjectOfType<LanLobbyState>() != null;
#endif

            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool isGameScene = active.name == "Pruebas_TheLastKing";

            if (isGameScene || hasAnchorOrLobby)
            {
                // Crea selector y suprime HUD LAN hasta que elijan
                if (
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                    Object.FindAnyObjectByType<NetModeSelector>() == null
#else
                    Object.FindObjectOfType<NetModeSelector>() == null
#endif
                )
                {
                    var sel = new GameObject("NetModeSelector");
                    Object.DontDestroyOnLoad(sel);
                    sel.AddComponent<NetModeSelector>();
                }
                var sup = new GameObject("LanHudSuppressor");
                Object.DontDestroyOnLoad(sup);
                sup.AddComponent<LanHudSuppressor>();

                CoroutineRunner.Run(DelayedStart()); // espera elección
            }
            else
            {
                // If we're not in the game scene yet, wait for scene changes and then re-run boot logic.
                SceneWatcher.StartWatching();
            }
        }
        else
        {
            // El menú ya eligió → arranca directo
            CoroutineRunner.Run(DirectStart());
        }
    }

    // Allow SceneWatcher to re-run boot logic after scene load
    internal static void TriggerBoot() => Boot();

    static System.Collections.IEnumerator DelayedStart()
    {
        while (NetRuntime.Mode == NetMode.None) yield return null; // esperar elección
        yield return DirectStart();
    }

    static System.Collections.IEnumerator DirectStart()
    {
        if (NetRuntime.Mode == NetMode.Relay)
        {
            RelayTransportBootstrap.EnsureUGSAsync().Forget(); // opcional (pre-login)
            HarnessRelayAddon.TryCreateUI();                   // crea HUD Relay
        }
        else if (NetRuntime.Mode == NetMode.LAN)
        {
            LanHarnessBootstrap.InitIfNeeded();               // crea/activa HUD LAN
        }
        yield break;
    }

    // --- helpers ---
    class CoroutineRunner : MonoBehaviour
    {
        public static void Run(System.Collections.IEnumerator r)
        {
            var go = new GameObject("BootstrapRunner");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<CoroutineRunner>().StartCoroutine(r);
        }
    }
}

// Mientras no se elija modo, mantener el HUD LAN oculto
class LanHudSuppressor : MonoBehaviour
{
    System.Collections.IEnumerator Start()
    {
        while (NetRuntime.Mode == NetMode.None)
        {
            QuickJoinLocalUI hud =
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                Object.FindAnyObjectByType<QuickJoinLocalUI>();
#else
                Object.FindObjectOfType<QuickJoinLocalUI>(true);
#endif
            if (hud != null) hud.gameObject.SetActive(false);
            yield return null;
        }
        Destroy(gameObject);
    }
}

// Helper: watch for scene changes and re-run UnifiedBootstrap.Boot when the gameplay scene loads.
static class SceneWatcher
{
    static bool watching = false;

    public static void StartWatching()
    {
        if (watching) return;
        watching = true;
        var go = new GameObject("UnifiedBootstrap_SceneWatcher");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<WatcherBehaviour>();
    }

    class WatcherBehaviour : MonoBehaviour
    {
        void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }
        void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name == "Pruebas_TheLastKing")
            {
                // Re-run boot logic to possibly spawn selector now that the game scene is active
                UnifiedBootstrap.TriggerBoot();
                Destroy(gameObject);
            }
        }
    }
}

static class TaskExt
{
    public static async void Forget(this System.Threading.Tasks.Task t)
    {
        try { await t; } catch { }
    }
}
