using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MissingScriptsFinder : EditorWindow
{
    private Vector2 scrollPos;
    private List<string> results = new List<string>();

    [MenuItem("Tools/Find Missing Scripts In Project...")]
    public static void ShowWindow()
    {
        GetWindow<MissingScriptsFinder>("Missing Scripts Finder");
    }

    void OnGUI()
    {
        GUILayout.Label("Scan project for missing MonoBehaviour scripts", EditorStyles.boldLabel);

        if (GUILayout.Button("Scan and Report"))
            Scan(false);

        if (GUILayout.Button("Scan and Remove Missing Scripts (Prefabs + Scenes)"))
            Scan(true);

        GUILayout.Space(8);
        GUILayout.Label($"Results ({results.Count})", EditorStyles.label);
        scrollPos = GUILayout.BeginScrollView(scrollPos);
        foreach (var line in results)
            GUILayout.Label(line);
        GUILayout.EndScrollView();
    }

    private void Scan(bool doRemove)
    {
        results.Clear();

        // Scan prefabs
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefabRoot = PrefabUtility.LoadPrefabContents(path);
            int before = CountMissing(prefabRoot);
            if (before > 0)
            {
                results.Add($"Missing in Prefab: {path} -> {before} missing component(s)");
                if (doRemove)
                {
                    int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(prefabRoot);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    results.Add($"  Removed {removed} missing components from prefab: {path}");
                }
            }
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        // Scan scenes
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
        foreach (var guid in sceneGuids)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            int totalMissing = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                int missingHere = CountMissing(root);
                if (missingHere > 0)
                {
                    results.Add($"Missing in Scene: {scenePath} -> {missingHere} missing on {root.name}");
                    totalMissing += missingHere;
                    if (doRemove)
                    {
                        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
                        results.Add($"  Removed {removed} missing components from object {root.name} in scene {scenePath}");
                    }
                }
            }
            if (doRemove && totalMissing > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            EditorSceneManager.CloseScene(scene, true);
        }

        if (results.Count == 0)
            results.Add("No missing scripts found in prefabs or scenes.");
    }

    private int CountMissing(GameObject go)
    {
        int count = 0;
        var comps = go.GetComponentsInChildren<Component>(true);
        foreach (var c in comps)
            if (c == null) count++;
        return count;
    }
}
