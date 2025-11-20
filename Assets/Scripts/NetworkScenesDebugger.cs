using UnityEngine;
using Unity.Netcode;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Debug helper: imprime información útil sobre NetworkManager(s) y las escenas registradas.
/// Se crea automáticamente al cargar la escena en el Editor/Playmode.
/// </summary>
public class NetworkScenesDebugger : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateInstance()
    {
        var go = new GameObject("NetworkScenesDebugger");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<NetworkScenesDebugger>();
    }

    void Start()
    {
        Debug.Log("[NetworkScenesDebugger] Start: recopilando información de red y escenas...");

        var managers = FindObjectsOfType<NetworkManager>(true);
        Debug.Log($"[NetworkScenesDebugger] NetworkManager instances found: {managers.Length}");
        for (int i = 0; i < managers.Length; i++)
        {
            var m = managers[i];
            Debug.Log($"[NetworkScenesDebugger] [{i}] name='{m.gameObject.name}' isSingleton={(m == NetworkManager.Singleton)} isServer={m.IsServer} isClient={m.IsClient}");
        }

        if (NetworkManager.Singleton != null)
        {
            var nm = NetworkManager.Singleton;
            Debug.Log($"[NetworkScenesDebugger] Singleton NM object: {nm.gameObject.name}");

            // Print a few useful NetworkManager properties
            try
            {
                Debug.Log($"[NetworkScenesDebugger] NetworkConfig PlayerPrefab: {nm.NetworkConfig?.PlayerPrefab}");
            }
            catch { }
        }

#if UNITY_EDITOR
        // List scenes in Editor Build Settings (editor-only)
        var scenes = EditorBuildSettings.scenes;
        Debug.Log($"[NetworkScenesDebugger] EditorBuildSettings has {scenes.Length} scenes:");
        for (int i = 0; i < scenes.Length; i++)
        {
            Debug.Log($"[NetworkScenesDebugger]   [{i}] path={scenes[i].path} enabled={scenes[i].enabled}");
        }
#else
        Debug.Log("[NetworkScenesDebugger] EditorBuildSettings not available (not running in Editor).");
#endif

        Debug.Log("[NetworkScenesDebugger] Done.");
    }
}
