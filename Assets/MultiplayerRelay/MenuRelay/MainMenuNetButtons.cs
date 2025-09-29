using UnityEngine;
// Alias para evitar conflicto con tu propio SceneManager:
using SceneManagerU = UnityEngine.SceneManagement.SceneManager;

public class MainMenuNetButtons : MonoBehaviour
{
    [SerializeField] string gameplayScene = "SampleScene"; // cambia al nombre real

    public void PlayLAN()
    {
        NetRuntime.Mode = NetMode.LAN;
        SceneManagerU.LoadScene(gameplayScene);
    }

    public void PlayRelay()
    {
        NetRuntime.Mode = NetMode.Relay;
        SceneManagerU.LoadScene(gameplayScene);
    }
}
