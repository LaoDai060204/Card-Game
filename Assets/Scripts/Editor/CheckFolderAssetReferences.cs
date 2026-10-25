using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class CheckFolderAssetReferences : EditorWindow
{
    private string targetFolderPath;
    private Dictionary<string, int> referenceCounts = new Dictionary<string, int>();
    private List<string> zeroReferenceAssets = new List<string>();
    private Vector2 scrollPosition;

    [MenuItem("Assets/Check Folder Asset References", false, 106)]
    private static void CheckFolderReferences()
    {
        string folderPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            EditorUtility.DisplayDialog("Error", "Select a valid folder", "OK");
            return;
        }

        CheckFolderAssetReferences window = GetWindow<CheckFolderAssetReferences>("Asset Reference Inspector");
        window.targetFolderPath = folderPath;
        window.AnalyzeReferences();
    }

    [MenuItem("Assets/Check Folder Asset References", true, 106)]
    private static bool ValidateCheckFolderReferences()
    {
        return Selection.activeObject != null &&
               AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(Selection.activeObject));
    }

    private void AnalyzeReferences()
    {
        referenceCounts.Clear();
        zeroReferenceAssets.Clear();

        // Get all assets in the folder.
        string[] allAssetGUIDs = AssetDatabase.FindAssets("", new[] { targetFolderPath });
        Dictionary<string, List<string>> referenceCache = new Dictionary<string, List<string>>();

        // Build the global reference cache.
        string[] allProjectGUIDs = AssetDatabase.FindAssets("");
        int total = allProjectGUIDs.Length;
        int current = 0;

        foreach (string guid in allProjectGUIDs)
        {
            current++;
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // Skip files outside Assets or the target folder.
            if (!assetPath.StartsWith("Assets/")) continue;

            EditorUtility.DisplayProgressBar("Analyze References",
                $"Scanning... ({current}/{total})", (float)current / total);

            string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);

            foreach (string dependency in dependencies)
            {
                if (dependency == assetPath) continue; // Skip self-references.

                if (!referenceCache.ContainsKey(dependency))
                    referenceCache[dependency] = new List<string>();

                referenceCache[dependency].Add(assetPath);
            }
        }

        // Count references to assets in the target folder.
        foreach (string guid in allAssetGUIDs)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            int count = referenceCache.ContainsKey(assetPath) ? referenceCache[assetPath].Count : 0;
            referenceCounts[assetPath] = count;

            if (count == 0)
                zeroReferenceAssets.Add(assetPath);
        }

        EditorUtility.ClearProgressBar();
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField($"Target folder: {targetFolderPath}", EditorStyles.boldLabel);

        if (GUILayout.Button("Analyze Again"))
            AnalyzeReferences();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Unreferenced assets ({zeroReferenceAssets.Count}):", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (string assetPath in zeroReferenceAssets)
        {
            EditorGUILayout.BeginHorizontal();

            // Display the asset object field.
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            EditorGUILayout.ObjectField(asset, typeof(Object), false, GUILayout.Width(300));

            // Display the path label.
            EditorGUILayout.LabelField(assetPath, GUILayout.Width(400));

            // Delete button
            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog("Confirm Deletion",
                    $"Delete {assetPath}?", "Delete", "Cancel"))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                    zeroReferenceAssets.Remove(assetPath);
                    Repaint();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }
}