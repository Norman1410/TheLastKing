using UnityEngine;
using TMPro;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// Shows the host's local IP in a TextMeshPro component.
/// Useful so other players know which IP to connect to in LAN.
/// </summary>
public class ShowLocalIP : MonoBehaviour
{
    [SerializeField] private TMP_Text ipText;
    [SerializeField] private string prefix = "Host IP: ";
    
    private void Start()
    {
        if (ipText != null)
        {
            string localIP = GetLocalIPAddress();
            ipText.text = prefix + localIP;
        }
    }

    private string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                // Buscar la primera IPv4 que no sea loopback
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"No se pudo obtener la IP local: {ex.Message}");
        }
        
        return "127.0.0.1";
    }

    // Para refrescar manualmente si es necesario
    public void RefreshIP()
    {
        if (ipText != null)
        {
            string localIP = GetLocalIPAddress();
            ipText.text = prefix + localIP;
        }
    }
}
