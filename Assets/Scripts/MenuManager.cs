using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public GameObject principalMenu;  // Menú con Play y Exit
    public GameObject multiplayerMenu; // Menú con LAN y Online
    public CanvasGroup transitionPanel;

    private void Start()
    {
        principalMenu.SetActive(true);  // Menú principal activo
        multiplayerMenu.SetActive(false); // Menú multijugador desactivado
        transitionPanel.alpha = 0;
    }

    public void GoToMultiplayerMenu()
    {
        StartCoroutine(Transition(principalMenu, multiplayerMenu));
    }

    public void GoBackToMainMenu()
    {
        StartCoroutine(Transition(multiplayerMenu, principalMenu));
    }

    public void StartGame()
    {
        SceneManager.LoadScene("Game");
    }

    public void ExitGame()
    {
        #if UNITY_EDITOR

            UnityEditor.EditorApplication.isPlaying = false;
        #else

            Application.Quit();
        #endif
    }

    IEnumerator Transition(GameObject fromMenu, GameObject toMenu)
    {
        // Fade in
        for (float t = 0; t < 1; t += Time.deltaTime * 2)
        {
            transitionPanel.alpha = t;
            yield return null;
        }

        transitionPanel.alpha = 1;

        // Change menus
        fromMenu.SetActive(false);
        toMenu.SetActive(true);

        // Fade out
        for (float t = 1; t > 0; t -= Time.deltaTime * 2)
        {
            transitionPanel.alpha = t;
            yield return null;
        }

        transitionPanel.alpha = 0;
    }
}