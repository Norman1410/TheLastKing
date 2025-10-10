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
    


