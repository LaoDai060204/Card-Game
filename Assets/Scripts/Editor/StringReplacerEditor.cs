// StringReplacerEditor.cs
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class StringReplacerEditor : EditorWindow
{
    private StringReplacementConfig config;
    private Vector2 scrollPosition;
    private bool processTextFiles = true;
    private bool processSerializedAssets = true;

    [MenuItem("Assets/Replace Strings", false, 103)]
    private static void ReplaceStringsInAsset()
    {
        // Show a confirmation dialog first.
        bool userConfirmed = EditorUtility.DisplayDialog(
            "Confirm Destructive Operation", // Title
            $"Replace strings? This operation cannot be undone!\n\nBack up the project before continuing.", // Message
            "Continue", // Confirm button
            "Cancel" // Cancel button
        );

        // Stop if the user selects Cancel.
        if (!userConfirmed)
        {
            Debug.Log("The operation was canceled.");
            return;
        }

        ProcessSelectedAsset(AssetDatabase.GetAssetPath(Selection.activeObject));
    }

    [MenuItem("Assets/Replace Strings", true)]
    private static bool ValidateReplaceStringsInAsset()
    {
        return Selection.activeObject != null &&
               !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(Selection.activeObject));
    }

    [MenuItem("Tools/String Replacer")]
    public static void ShowWindow()
    {
        GetWindow<StringReplacerEditor>("String Replacer");
    }

    private static void ProcessSelectedAsset(string assetPath)
    {
        // Find the configuration asset.
        StringReplacementConfig config = FindConfig();
        if (config == null || !config.HasRules())
        {
            Debug.LogError("Create a StringReplacementConfig and configure replacement rules first.");
            return;
        }

        // Choose the processing method based on the file type.
        string extension = Path.GetExtension(assetPath).ToLower();
        bool isTextFile = IsTextFileExtension(extension);
        bool isSerializedAsset = IsSerializedAssetExtension(extension);

        bool processed = false;

        if (isTextFile)
        {
            processed = ProcessTextAsset(assetPath, config);
        }
        else if (isSerializedAsset)
        {
            processed = ProcessSerializedAsset(assetPath, config);
        }
        else
        {
            Debug.LogWarning($"Unsupported file type: {extension}");
            return;
        }

        if (processed)
        {
            Debug.Log($"Processed successfully: {assetPath}");
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }
        else
        {
            Debug.Log($"No matching strings found: {assetPath}");
        }
    }

    private static bool ProcessTextAsset(string assetPath, StringReplacementConfig config)
    {
        string content = File.ReadAllText(assetPath);
        bool modified = false;

        foreach (var rule in config.replacementRules)
        {
            StringComparison comparison = rule.caseSensitive ?
                StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            if (content.IndexOf(rule.findString, comparison) >= 0)
            {
                content = content.Replace(rule.findString, rule.replaceString);
                modified = true;
                Debug.Log($"Replace text: {rule.findString} -> {rule.replaceString}");
            }
        }

        if (modified)
        {
            File.WriteAllText(assetPath, content, Encoding.UTF8);
            return true;
        }

        return false;
    }

    private static bool ProcessSerializedAsset(string assetPath, StringReplacementConfig config)
    {
        // Serialized assets require more careful handling.
        // Use Unity's serialization API to modify assets safely.
        bool modified = false;

        SerializedObject serializedObject = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(assetPath)[0]);
        SerializedProperty property = serializedObject.GetIterator();

        while (property.Next(true))
        {
            if (property.propertyType == SerializedPropertyType.String)
            {
                string originalValue = property.stringValue;
                string newValue = originalValue;
                
                foreach (var rule in config.replacementRules)
                {
                    if (ShouldReplace(originalValue, rule))
                    {
                        newValue = newValue.Replace(rule.findString, rule.replaceString);
                    }
                }

                if (newValue != originalValue)
                {
                    property.stringValue = newValue;
                    modified = true;
                    Debug.Log($"Replace property {property.name}: {originalValue} -> {newValue}");
                }
            }
        }

        if (modified)
        {
            serializedObject.ApplyModifiedProperties();
            return true;
        }

        return false;
    }

    private static bool ShouldReplace(string text, StringReplacementConfig.ReplacementRule rule)
    {
        if (rule.caseSensitive)
        {
            return text.Contains(rule.findString);
        }
        else
        {
            return text.IndexOf(rule.findString, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    private static StringReplacementConfig FindConfig()
    {
        string[] guids = AssetDatabase.FindAssets("t:StringReplacementConfig");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<StringReplacementConfig>(path);
        }
        return null;
    }

    private static bool IsTextFileExtension(string extension)
    {
        return true;
        return extension == ".txt" || extension == ".xml" || extension == ".json" ||
               extension == ".csv" || extension == ".html" || extension == ".htm" ||
               extension == ".yaml" || extension == ".yml" || extension == ".cs" ||
               extension == ".js" || extension == ".shader" || extension == ".cginc";
    }

    private static bool IsSerializedAssetExtension(string extension)
    {
        return extension == ".asset" || extension == ".prefab" || extension == ".unity" ||
               extension == ".mat" || extension == ".controller" || extension == ".anim" ||
               extension == ".playable";
    }

    private void OnGUI()
    {
        GUILayout.Label("Batch String Replacement", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Configuration selection
        config = (StringReplacementConfig)EditorGUILayout.ObjectField("Replacement Configuration", config, typeof(StringReplacementConfig), false);

        if (config == null)
        {
            EditorGUILayout.HelpBox("Create or assign a StringReplacementConfig asset first.", MessageType.Warning);
            if (GUILayout.Button("Create Configuration"))
            {
                CreateNewConfig();
            }
            return;
        }

        // Processing Options
        EditorGUILayout.Space();
        GUILayout.Label("Processing Options", EditorStyles.boldLabel);
        processTextFiles = EditorGUILayout.Toggle("Process Text Files", processTextFiles);
        processSerializedAssets = EditorGUILayout.Toggle("Process Serialized Assets", processSerializedAssets);

        // Display and edit the configuration.
        EditorGUILayout.Space();
        GUILayout.Label("Replacement Rules", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        SerializedObject serializedConfig = new SerializedObject(config);
        SerializedProperty rulesProperty = serializedConfig.FindProperty("replacementRules");
        EditorGUILayout.PropertyField(rulesProperty, true);
        serializedConfig.ApplyModifiedProperties();
        EditorGUILayout.EndScrollView();

        // Action buttons
        EditorGUILayout.Space();
        if (GUILayout.Button("Process Selected Assets"))
        {
            if (Selection.activeObject != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                ProcessSelectedAsset(assetPath);
            }
            else
            {
                Debug.LogWarning("Select an asset first.");
            }
        }

        if (GUILayout.Button("Process All Assets in Folder"))
        {
            if (Selection.activeObject != null)
            {
                string folderPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (AssetDatabase.IsValidFolder(folderPath))
                {
                    ProcessAllAssetsInFolder(folderPath);
                }
                else
                {
                    Debug.LogWarning("Select a folder");
                }
            }
        }
    }

    private void CreateNewConfig()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Replacement Configuration",
            "StringReplacementConfig",
            "asset",
            "Enter a configuration filename");

        if (!string.IsNullOrEmpty(path))
        {
            StringReplacementConfig newConfig = CreateInstance<StringReplacementConfig>();
            AssetDatabase.CreateAsset(newConfig, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            config = newConfig;
        }
    }

    private void ProcessAllAssetsInFolder(string folderPath)
    {
        StringReplacementConfig config = FindConfig();
        if (config == null || !config.HasRules())
        {
            Debug.LogError("Configure replacement rules first.");
            return;
        }

        // Get all assets in the folder.
        string[] allFiles = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
        int processedCount = 0;

        foreach (string file in allFiles)
        {
            if (file.EndsWith(".meta")) continue;

            string assetPath = file.Replace(Application.dataPath, "Assets");
            string extension = Path.GetExtension(assetPath).ToLower();

            bool isTextFile = processTextFiles && IsTextFileExtension(extension);
            bool isSerializedAsset = processSerializedAssets && IsSerializedAssetExtension(extension);

            if (isTextFile || isSerializedAsset)
            {
                if (ProcessTextAsset(assetPath, config) || ProcessSerializedAsset(assetPath, config))
                {
                    processedCount++;
                }
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"Processing complete. Files processed: {processedCount} files");
    }
}