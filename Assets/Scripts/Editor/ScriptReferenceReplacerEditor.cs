using System.IO;
using UnityEditor;
using UnityEngine;

public class ScriptReferenceReplacerEditor : EditorWindow
{
    private ScriptReplacementConfig replacementConfig;
    private Vector2 scrollPosition;

    [MenuItem("Assets/Replace Script References", false, 102)]
    private static void ReplaceScriptReferences()
    {
        // Show a confirmation dialog first.
        bool userConfirmed = EditorUtility.DisplayDialog(
            "Confirm Destructive Operation", // Title
            $"Replace script references? This operation cannot be undone!\n\nBack up the project before continuing.", // Message
            "Continue", // Confirm button
            "Cancel" // Cancel button
        );

        // Stop if the user selects Cancel.
        if (!userConfirmed)
        {
            Debug.Log("The operation was canceled.");
            return;
        }

        // Try to find an existing configuration.
        string[] configGUIDs = AssetDatabase.FindAssets("t:ScriptReplacementConfig");
        ScriptReplacementConfig config = null;
        if (configGUIDs.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(configGUIDs[0]);
            config = AssetDatabase.LoadAssetAtPath<ScriptReplacementConfig>(path);
        }

        if (config == null)
        {
            Debug.LogError("Please create a ScriptReplacementConfig first and assign the replacement pairs.");
            return;
        }

        // Process assets.
        var assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        ProcessAsset(assetPath, config);
    }

    [MenuItem("Assets/Replace Script References", true)]
    private static bool ValidateReplaceStringsInAsset()
    {
        return Selection.activeObject != null &&
               !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(Selection.activeObject));
    }

    private static void ProcessAsset(string assetPath, ScriptReplacementConfig config)
    {
        // Read the asset as text.
        string assetContent = File.ReadAllText(assetPath);
        bool modified = false;

        // Process each configured replacement.
        foreach (var pair in config.replacementPairs)
        {
            // Build the old m_Script line.
            string oldScriptLine = $"m_Script: {{fileID: {pair.oldFileId}, guid: {pair.oldGuid}, type: 3}}";
            // Build the new m_Script line.
            string newScriptLine = $"m_Script: {{fileID: {pair.newFileId}, guid: {pair.newGuid}, type: 3}}";

            // Find and replace matching text.
            if (assetContent.Contains(oldScriptLine))
            {
                assetContent = assetContent.Replace(oldScriptLine, newScriptLine);
                modified = true;
                Debug.Log($"Replaced script reference in {assetPath}: {oldScriptLine} -> {newScriptLine}");
            }
        }

        // Write the modified content and reimport the asset.
        if (modified)
        {
            File.WriteAllText(assetPath, assetContent);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"Successfully processed and reimported: {assetPath}");
        }
        else
        {
            Debug.Log($"No matching script references found in: {assetPath}");
        }
    }

    // Optional editor window for viewing and editing the configuration.
    [MenuItem("Tools/Script Reference Replacer")]
    public static void ShowWindow()
    {
        GetWindow<ScriptReferenceReplacerEditor>("Script Replacer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Script Reference Replacer Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Allow drag-and-drop assignment.
        replacementConfig = (ScriptReplacementConfig)EditorGUILayout.ObjectField("Replacement Config", replacementConfig, typeof(ScriptReplacementConfig), false);

        if (replacementConfig == null)
        {
            EditorGUILayout.HelpBox("Please assign a ScriptReplacementConfig asset.", MessageType.Warning);
            if (GUILayout.Button("Create New Configuration"))
            {
                CreateNewConfig();
            }
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        SerializedObject serializedConfig = new SerializedObject(replacementConfig);
        SerializedProperty pairsProperty = serializedConfig.FindProperty("replacementPairs");
        EditorGUILayout.PropertyField(pairsProperty, true);
        serializedConfig.ApplyModifiedProperties();
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (GUILayout.Button("Process Selected Asset"))
        {
            if (Selection.activeObject != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                ProcessAsset(assetPath, replacementConfig);
            }
            else
            {
                Debug.LogWarning("Please select an asset first.");
            }
        }
    }

    private void CreateNewConfig()
    {
        string path = EditorUtility.SaveFilePanelInProject("Create Replacement Config", "ScriptReplacementConfig", "asset", "Please enter a file name for the configuration");
        if (!string.IsNullOrEmpty(path))
        {
            ScriptReplacementConfig newConfig = CreateInstance<ScriptReplacementConfig>();
            AssetDatabase.CreateAsset(newConfig, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            replacementConfig = newConfig;
        }
    }
}