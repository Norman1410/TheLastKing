using UnityEditor;
using UnityEngine;
using Unity.Netcode;

public class NetworkPrefabValidator : EditorWindow
{
    private GameObject prefabToCheck;

    [MenuItem("Tools/Network Prefab Validator")]
    public static void ShowWindow()
    {
        GetWindow<NetworkPrefabValidator>("Network Prefab Validator");
    }

    void OnGUI()
    {
        GUILayout.Label("Validate LanLobbyState prefab before adding to NetworkManager", EditorStyles.boldLabel);
        prefabToCheck = (GameObject)EditorGUILayout.ObjectField("Prefab to check", prefabToCheck, typeof(GameObject), false);

        if (prefabToCheck == null)
        {
            EditorGUILayout.HelpBox("Assign the prefab asset (from Project view) to validate.", MessageType.Info);
            return;
        }

        if (GUILayout.Button("Validate Prefab"))
        {
            ValidatePrefab(prefabToCheck);
        }
    }

    void ValidatePrefab(GameObject prefab)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Selected object is not an asset in the Project. Please select the prefab asset from the Project window.");
            return;
        }

        if (!AssetDatabase.IsMainAsset(prefab))
        {
            Debug.LogWarning("Selected object is not the main asset. Make sure you selected the prefab asset (not a scene instance).");
        }

        // Check NetworkObject
        var netObj = prefab.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("Prefab is missing NetworkObject component. Add a NetworkObject to the prefab.");
        }
        else
        {
            Debug.Log("NetworkObject component: OK");
        }

        // Check for components that are scene-only (eg, references to scene objects)
        var components = prefab.GetComponentsInChildren<Component>(true);
        foreach (var comp in components)
        {
            if (comp == null) continue; // missing script
            var so = new SerializedObject(comp);
            var sp = so.GetIterator();
            while (sp.NextVisible(true))
            {
                if (sp.propertyType == SerializedPropertyType.ObjectReference)
                {
                    var refObj = sp.objectReferenceValue;
                    if (refObj != null)
                    {
                        string refPath = AssetDatabase.GetAssetPath(refObj);
                        if (string.IsNullOrEmpty(refPath))
                        {
                            Debug.LogWarning($"Component {comp.GetType().Name} has a scene reference in property {sp.name}. This can prevent correct prefab behavior.");
                        }
                    }
                }
            }
        }

        Debug.Log("Validation complete. If all required checks passed, try adding the prefab to the NetworkManager again.");
    }
}
