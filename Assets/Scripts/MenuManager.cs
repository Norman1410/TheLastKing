using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    // Nombre de la escena de lobby (ajústalo si tiene otro nombre exacto)
    [SerializeField] private string lobbySceneName = "LobbyNet";

    // Botón PLAY del menú principal
    public void StartGame()
    {
        // Por si NetRuntime se estaba usando antes, lo limpiamos
        try { NetRuntime.Mode = NetMode.None; } catch { }

        // Cargamos SOLO la escena de lobby
        SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
    }

    // Botón EXIT del menú principal
    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
