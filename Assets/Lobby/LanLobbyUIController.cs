using UnityEngine;

public class LanLobbyUIController : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] GameObject panelNetMode;     // Panel con botones LAN / ONLINE
    [SerializeField] GameObject panelLanLobby;    // Panel del Lobby LAN
    [SerializeField] GameObject panelRelayLobby;  // Panel del Lobby ONLINE (crear / unirse)
    [SerializeField] GameObject panelLanHUD;      // HUD moderno LAN (dentro del lobby LAN)
    [SerializeField] GameObject panelRelayHUD;    // HUD moderno ONLINE (dentro del lobby Relay)

    void Awake()
    {
        // Estado inicial: solo se ve la selección de modo.
        // Todo lo demás empieza apagado, sin importar cómo esté en el inspector.
        if (panelNetMode    != null) panelNetMode.SetActive(true);
        if (panelLanLobby   != null) panelLanLobby.SetActive(false);
        if (panelRelayLobby != null) panelRelayLobby.SetActive(false);
        if (panelLanHUD     != null) panelLanHUD.SetActive(false);
        if (panelRelayHUD   != null) panelRelayHUD.SetActive(false);
    }

    public void OnSelectLan()
    {
        // Entramos al flujo LAN: solo mostramos el panel de lobby LAN (IP/nombre).
        if (panelNetMode    != null) panelNetMode.SetActive(false);
        if (panelLanLobby   != null) panelLanLobby.SetActive(true);
        if (panelRelayLobby != null) panelRelayLobby.SetActive(false);
        if (panelLanHUD     != null) panelLanHUD.SetActive(false);
        if (panelRelayHUD   != null) panelRelayHUD.SetActive(false);
    }

    public void OnSelectRelay()
    {
        // Entramos al flujo ONLINE: solo mostramos el panel de lobby ONLINE (código/nombre).
        if (panelNetMode    != null) panelNetMode.SetActive(false);
        if (panelLanLobby   != null) panelLanLobby.SetActive(false);
        if (panelRelayLobby != null) panelRelayLobby.SetActive(true);
        if (panelLanHUD     != null) panelLanHUD.SetActive(false);
        if (panelRelayHUD   != null) panelRelayHUD.SetActive(false);
    }

    // 🔙 Botón "Volver" desde LAN o desde ONLINE
    public void OnBackToModeSelect()
    {
        // Volvemos al menú de selección de modo y apagamos TODO lo demás.
        if (panelNetMode    != null) panelNetMode.SetActive(true);
        if (panelLanLobby   != null) panelLanLobby.SetActive(false);
        if (panelRelayLobby != null) panelRelayLobby.SetActive(false);
        if (panelLanHUD     != null) panelLanHUD.SetActive(false);
        if (panelRelayHUD   != null) panelRelayHUD.SetActive(false);
    }
}
