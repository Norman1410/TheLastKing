using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private string mainMenuSceneName = "PrincipalMenu";
    private bool isPaused = false;

    void Start()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false); 
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (pausePanel == null) return;

        isPaused = !isPaused;
        pausePanel.SetActive(isPaused);
        Cursor.visible = isPaused;
        Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
    }

    public void ReturnToMainMenu()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void ResumeGame()
    {
        if (pausePanel == null) return;

        isPaused = false;
        pausePanel.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}