using UnityEngine;

public class NetModeSelectorUI : MonoBehaviour
{
    [Header("Panel de selección de modo")]
    public GameObject netModePanel;

    public void OnLanClicked()
    {
        NetRuntime.Mode = NetMode.LAN;
        Debug.Log("Modo de red seleccionado: LAN");
        if (netModePanel != null)
            netModePanel.SetActive(false);
    }

    public void OnRelayClicked()
    {
        NetRuntime.Mode = NetMode.Relay;
        Debug.Log("Modo de red seleccionado: Relay");
        if (netModePanel != null)
            netModePanel.SetActive(false);
    }
}
