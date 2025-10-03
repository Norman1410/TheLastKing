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
}
