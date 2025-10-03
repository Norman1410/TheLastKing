using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public static class LanStartHelpers
{
    public static void StartLanHost(ushort port = 7777)
    {
        var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        utp.SetConnectionData("0.0.0.0", port, "0.0.0.0"); // escucha en todas las interfaces
        NetworkManager.Singleton.StartHost();
    }

    public static void StartLanClient(string hostIp, ushort port = 7777)
    {
        var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        utp.SetConnectionData(hostIp, port);
        NetworkManager.Singleton.StartClient();
    }
}
