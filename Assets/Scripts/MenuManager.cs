using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public GameObject principalMenu;  // Menú con Play y Exit
    public GameObject multiplayerMenu; // Menú con LAN y Online (elección)
    public GameObject multiplayerHostJoinMenu; // Menú donde eliges Host o Join
    public GameObject playerListPanel; // Panel que muestra lista de jugadores y botón "Listo" para host
    public CanvasGroup transitionPanel;

    // Estado runtime
    private bool isLan = true; // true = LAN, false = Online (Relay)
    private bool isHost = true; // true = Host, false = Join

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

    // Selección LAN vs Online
    public void SelectLan()
    {
        isLan = true;
        // Avanza al menú Host/Join
        StartCoroutine(Transition(multiplayerMenu, multiplayerHostJoinMenu));
    }

    public void SelectOnline()
    {
        isLan = false;
        // Avanza al menú Host/Join
        StartCoroutine(Transition(multiplayerMenu, multiplayerHostJoinMenu));
    }

    // Back desde Host/Join -> multiplayerMenu
    public void BackFromHostJoin()
    {
        StartCoroutine(Transition(multiplayerHostJoinMenu, multiplayerMenu));
    }

    // Host / Join selection handlers
    public void ChooseHost()
    {
        isHost = true;
        // Mostrar panel de lista de jugadores para el host
        playerListPanel.SetActive(true);
        // Ejecutar acción de host (LAN o Online)
        StartHostAction();
    }

    public void ChooseJoin()
    {
        isHost = false;
        // Ocultar panel de lista si estaba visible
        if (playerListPanel != null) playerListPanel.SetActive(false);
        // Ejecutar acción de join (LAN o Online)
        StartJoinAction();
    }

    // Acción real para Start Host
    private void StartHostAction()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Starting Host (isLan={isLan})");
#endif
        // LAN: intentar StartHost local; Online: placeholder para Relay
        if (isLan)
        {
            if (Unity.Netcode.NetworkManager.Singleton != null)
            {
                Unity.Netcode.NetworkManager.Singleton.StartHost();
            }
        }
        else
        {
            // TODO: Implementar Relay/UGS start host flow (Create allocation, set transport relay data, StartHost)
            // Puedes llamar a tus helpers existentes (RelayStartHelpers.CreateAndStartHost)
        }
    }

    // Acción real para Start Join
    private void StartJoinAction()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Starting Join (isLan={isLan})");
#endif
        if (isLan)
        {
            if (Unity.Netcode.NetworkManager.Singleton != null)
            {
                Unity.Netcode.NetworkManager.Singleton.StartClient();
            }
        }
        else
        {
            // TODO: Implementar Relay/UGS join flow (Join with join code, set transport relay data, StartClient)
            // Llama a tus helpers para unirte por Relay
        }
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