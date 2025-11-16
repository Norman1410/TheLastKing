using UnityEngine;

public class LanLobbyUIController : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] GameObject panelNetMode;   // Panel con botones LAN / ONLINE
    [SerializeField] GameObject panelLanLobby;  // Panel nuevo del Lobby LAN

    void Awake()
    {
        // Estado inicial: solo se ve la selección de modo
        if (panelNetMode != null) panelNetMode.SetActive(true);
        if (panelLanLobby != null) panelLanLobby.SetActive(false);
    }

    public void OnSelectLan()
    {
        // Solo nos encargamos de mostrar/ocultar paneles.
        // El cambio de modo (NetMode.LAN) lo hace NetModeSelectorUI.
        if (panelNetMode != null) panelNetMode.SetActive(false);
        if (panelLanLobby != null) panelLanLobby.SetActive(true);
    }

    // Por si algún día quieres un botón de "Volver"
    public void OnBackToModeSelect()
    {
        if (panelNetMode != null) panelNetMode.SetActive(true);
        if (panelLanLobby != null) panelLanLobby.SetActive(false);
    }
}
