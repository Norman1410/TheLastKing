using UnityEngine;
using UnityEngine.SceneManagement;
public class NewMonoBehaviourScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Play()
    {
        SceneManager.LoadScene("Game");
    }

    public void Exit()
    {
        Application.Quit();
    }

}
