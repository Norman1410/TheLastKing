using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Minimal MenuManager: only provides two public methods for UI buttons:
/// - StartGame(): loads the scene "Pruebas_The Last King" with multiplayer mode selector enabled
/// - ExitGame(): quits the application (stops play mode in editor)
///
/// Attach this to any persistent UI object and wire the two methods to buttons.
/// </summary>
public class MenuManager : MonoBehaviour
{
    // Public method to load the target scene. Wire this to your "Start Game" button.
    public void StartGame()
    {
        // Set NetRuntime.Mode to None first to ensure the selector appears
        NetRuntime.Mode = NetMode.None;

        // Load the game scene additively and make it active
        var operation = SceneManager.LoadSceneAsync("Pruebas_TheLastKing", LoadSceneMode.Additive);
        operation.completed += (op) =>
        {
            // After loading, make it the active scene so it's fully visible
            Scene targetScene = SceneManager.GetSceneByName("Pruebas_TheLastKing");
            SceneManager.SetActiveScene(targetScene);

            // Ensure the multiplayer bootstrap runs (spawn NetModeSelector / HUD) now that the game scene is active
            try {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                UnityEngine.Object.FindAnyObjectByType<MonoBehaviour>();
#else
                UnityEngine.Object.FindObjectOfType<MonoBehaviour>();
#endif
            } catch { }
            // Ensure a NetworkManager exists (NetSetupOnce) so the lobby UI can find it
            try { var nsType = System.Type.GetType("NetSetupOnce"); if (nsType != null) {
                var mi = nsType.GetMethod("EnsureNetworkManagerPublic", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (mi != null) mi.Invoke(null, null);
            } } catch { }
            try { // call trigger if available
                var ubType = System.Type.GetType("UnifiedBootstrap");
                if (ubType != null)
                {
                    var mi = ubType.GetMethod("TriggerBoot", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                    if (mi != null) mi.Invoke(null, null);
                }
            }
            catch { }

            // Finally unload the menu scene since we don't need it anymore
            SceneManager.UnloadSceneAsync(gameObject.scene);
        };
    }

    // Public method to exit the game. Wire this to your "Exit" button.
    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
    


