using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public static class NetSetupOnce
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureNetworkManager()
    {
        // 1) Buscar o crear NetworkManager + UnityTransport
        var nm = Object.FindFirstObjectByType<NetworkManager>();
        if (nm == null)
        {
            var go = new GameObject("NetworkManager");
            Object.DontDestroyOnLoad(go);
            nm = go.AddComponent<NetworkManager>();
            if (go.GetComponent<UnityTransport>() == null)
                go.AddComponent<UnityTransport>();
            // Ensure the NetworkConfig has a transport assigned
            var utp = go.GetComponent<UnityTransport>();
            if (utp != null)
            {
                if (nm.NetworkConfig == null) nm.NetworkConfig = new Unity.Netcode.NetworkConfig();
                if (nm.NetworkConfig.NetworkTransport == null)
                {
                    nm.NetworkConfig.NetworkTransport = utp;
                    Debug.Log("[NetSetupOnce] Assigned UnityTransport into NetworkConfig.NetworkTransport.");
                }
            }
        }

        // Ensure NetworkConfig is not null (some versions may have it null)
        if (nm.NetworkConfig == null)
        {
            nm.NetworkConfig = new Unity.Netcode.NetworkConfig();
            Debug.Log("[NetSetupOnce] Created new NetworkConfig on NetworkManager.");
        }

        // 2) Asignar PlayerPrefab si está vacío (no tocamos la lista de Prefabs)
        if (nm.NetworkConfig.PlayerPrefab == null)
        {
            // Carga desde cualquier carpeta .../Resources/
            var p = Resources.Load<GameObject>("PlayerNetwork");
            if (p != null)
            {
                nm.NetworkConfig.PlayerPrefab = p;
            }
            else
            {
                Debug.LogError("[NET] Falta PlayerNetwork.prefab en alguna carpeta 'Resources/'.");
            }
        }

        // Nota: no agregamos a nm.NetworkConfig.Prefabs ni usamos AddNetworkPrefab.
        // Si hay duplicados en el Inspector, los quitas a mano (paso de abajo).
    }

    // Public wrapper so other code (e.g. MenuManager) can ensure a NetworkManager exists on demand
    public static void EnsureNetworkManagerPublic() => EnsureNetworkManager();
}
