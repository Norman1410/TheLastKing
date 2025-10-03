using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;

public static class RelayStartHelpers
{
    // Guarda el último join code por si tu UI quiere mostrarlo
    public static string LastJoinCode { get; private set; } = "";

    // Host por Relay: crea allocation, configura el transport y arranca Netcode
    public static async Task CreateAndStartHost(int expectedClients = 7)
    {
        // expectedClients = número de clientes (no incluye al host)
        var alloc = await RelayService.Instance.CreateAllocationAsync(expectedClients);

        LastJoinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

        var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        var data = new RelayServerData(alloc, "dtls");
        transport.SetRelayServerData(data);

        NetworkManager.Singleton.StartHost();
    }

    // Cliente por Relay: se une con joinCode, configura el transport y arranca Netcode
    public static async Task JoinAsClient(string joinCode)
    {
        var join = await RelayService.Instance.JoinAllocationAsync(joinCode);

        var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        var data = new RelayServerData(join, "dtls");
        transport.SetRelayServerData(data);

        NetworkManager.Singleton.StartClient();
    }
}
