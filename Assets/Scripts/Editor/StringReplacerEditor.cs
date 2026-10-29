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
        // 首先显示确认弹窗
        bool userConfirmed = EditorUtility.DisplayDialog(
            "Confirm Destructive Operation", // 标题
            $"Replace strings? This operation cannot be undone.\n\nBack up the project before continuing.", // 消息
            "Continue", // 确定按钮
            "Cancel" // 取消按钮
        );

        // 如果用户点击了“取消”，则终止操作
        if (!userConfirmed)
        {
            Debug.Log("Operation canceled by the user.");
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
        // 查找配置文件
        StringReplacementConfig config = FindConfig();
        if (config == null || !config.HasRules())
        {
            Debug.LogError("Create a StringReplacementConfig and configure replacement rules first.");
            return;
        }

        // 根据文件类型选择处理方法
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
            Debug.Log($"Successfully processed: {assetPath}");
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }
        else
        {
            Debug.Log($"No matching strings found in: {assetPath}");
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
                Debug.Log($"Replaced text: {rule.findString} -> {rule.replaceString}");
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
        // 对于序列化资产，我们需要更谨慎的处理
        // 这里使用Unity的序列化API来安全地修改资产
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
                    Debug.Log($"Replaced property {property.name}: {originalValue} -> {newValue}");
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
        GUILayout.Label("Batch String Replacer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 配置选择
        config = (StringReplacementConfig)EditorGUILayout.ObjectField("Replacement Config", config, typeof(StringReplacementConfig), false);

        if (config == null)
        {
            EditorGUILayout.HelpBox("Create or assign a StringReplacementConfig asset first.", MessageType.Warning);
            if (GUILayout.Button("Create New Config"))
            {
                CreateNewConfig();
            }
            return;
        }

        // 处理选项
        EditorGUILayout.Space();
        GUILayout.Label("Processing Options", EditorStyles.boldLabel);
        processTextFiles = EditorGUILayout.Toggle("Process Text Files", processTextFiles);
        processSerializedAssets = EditorGUILayout.Toggle("Process Serialized Assets", processSerializedAssets);

        // 显示和编辑配置
        EditorGUILayout.Space();
        GUILayout.Label("Replacement Rules", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        SerializedObject serializedConfig = new SerializedObject(config);
        SerializedProperty rulesProperty = serializedConfig.FindProperty("replacementRules");
        EditorGUILayout.PropertyField(rulesProperty, true);
        serializedConfig.ApplyModifiedProperties();
        EditorGUILayout.EndScrollView();

        // 操作按钮
        EditorGUILayout.Space();
        if (GUILayout.Button("Process Selected Asset"))
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
                    Debug.LogWarning("Select a folder.");
                }
            }
        }
    }

    private void CreateNewConfig()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Replacement Config",
            "StringReplacementConfig",
            "asset",
            "Enter a configuration file name.");

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

        // 获取文件夹中的所有资产
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
        Debug.Log($"Processing complete. Processed {processedCount} files.");
    }
}