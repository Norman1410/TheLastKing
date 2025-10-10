using UnityEngine;
using Unity.Netcode;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor tool to find all NetworkObjects in the scene and their hashes.
/// This helps diagnose "NetworkPrefab could not be found" errors.
/// </summary>
public class NetworkObjectFinder : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Find NetworkObjects in Scene")]
    static void FindNetworkObjects()
    {
        var networkObjects = FindObjectsOfType<NetworkObject>(true);
        
        Debug.Log($"=== Found {networkObjects.Length} NetworkObjects in current scene ===");
        
        foreach (var netObj in networkObjects)
        {
            string path = GetGameObjectPath(netObj.gameObject);
            bool isPrefab = PrefabUtility.IsPartOfAnyPrefab(netObj);
            
            // Try to get hash - use reflection if direct access doesn't work
            uint hash = 0;
            try
            {
                var hashField = typeof(NetworkObject).GetField("GlobalObjectIdHash", 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
                
                if (hashField != null)
                    hash = (uint)hashField.GetValue(netObj);
            }
            catch { }
            
            Debug.Log($"NetworkObject: {netObj.gameObject.name}\n" +
                     $"  Path: {path}\n" +
                     $"  Hash: {hash}\n" +
                     $"  IsPrefab: {isPrefab}\n" +
                     $"  Active: {netObj.gameObject.activeInHierarchy}");
        }
        
        if (networkObjects.Length == 0)
        {
            Debug.Log("No NetworkObjects found in scene. This is correct for menu scenes!");
        }
    }
    
    static string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }
#endif
}
