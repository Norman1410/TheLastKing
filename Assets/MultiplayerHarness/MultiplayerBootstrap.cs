using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public static class MultiplayerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureNetworkManager()
    {
        // En algunas versiones usa FindObjectOfType, en otras FindFirst/AnyObjectByType.
        var existing = Object.FindObjectOfType<NetworkManager>();
        if (existing != null) return;

        var go = new GameObject("NetworkManager_BOOT");
        Object.DontDestroyOnLoad(go);

        var nm = go.AddComponent<NetworkManager>();
        var utp = go.AddComponent<UnityTransport>();

        // ⚠️ En algunas versiones NetworkConfig puede venir null => créala.
        if (nm.NetworkConfig == null)
            nm.NetworkConfig = new NetworkConfig();

        nm.NetworkConfig.NetworkTransport = utp;

        // Datos para pruebas locales (LAN/loopback)
        utp.SetConnectionData("127.0.0.1", 7777);
    }
}
