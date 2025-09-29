using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;   // necesario para RelayServerData (UTP 2.x)
using UnityEngine;

public static class RelayTransportBootstrap
{
    static bool inited;

    public static async Task EnsureUGSAsync()
    {
        if (inited) return;

        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        inited = true;

        // (Opcional) log simple si el transport falla (evento sin argumentos en tu versión)
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnTransportFailure += () =>
                Debug.LogError("[TransportFailure] UnityTransport reportó un fallo.");
        }
    }

    /// <summary>Arranca HOST por Relay. Devuelve el Join Code.</summary>
    public static async Task<string> StartHostWithRelayAsync(int maxConns = 10 /*, string region = null*/)
    {
        await EnsureUGSAsync();

        // Si quieres forzar región, añade el parámetro y pásalo aquí (p.ej. "us-east1")
        Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxConns /*, region*/);
        string code      = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

        var nm  = NetworkManager.Singleton;
        var utp = nm.GetComponent<UnityTransport>();
        if (utp == null)
        {
            Debug.LogError("[Relay] Falta UnityTransport en NetworkManager.");
            return null;
        }

        // Usar UDP para evitar bloqueos de DTLS por firewall
        var serverData = new RelayServerData(alloc, "udp");
        utp.SetRelayServerData(serverData);

        nm.StartHost();
        Debug.Log("[Relay] Host listo. Code: " + code);
        return code;
    }

    /// <summary>Arranca CLIENT por Relay con un Join Code.</summary>
    public static async Task<bool> StartClientWithRelayAsync(string code /*, string region = null*/)
    {
        await EnsureUGSAsync();

        JoinAllocation join = await RelayService.Instance.JoinAllocationAsync(code /*, region*/);

        var nm  = NetworkManager.Singleton;
        var utp = nm.GetComponent<UnityTransport>();
        if (utp == null)
        {
            Debug.LogError("[Relay] Falta UnityTransport en NetworkManager.");
            return false;
        }

        // Usar UDP
        var serverData = new RelayServerData(join, "udp");
        utp.SetRelayServerData(serverData);

        return nm.StartClient();
    }
}
