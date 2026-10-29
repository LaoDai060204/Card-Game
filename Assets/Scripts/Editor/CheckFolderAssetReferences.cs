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
            EditorUtility.DisplayDialog("Error", "Select a valid folder.", "OK");
            return;
        }

        CheckFolderAssetReferences window = GetWindow<CheckFolderAssetReferences>("Asset Reference Checker");
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

        // 获取文件夹内所有资产
        string[] allAssetGUIDs = AssetDatabase.FindAssets("", new[] { targetFolderPath });
        Dictionary<string, List<string>> referenceCache = new Dictionary<string, List<string>>();

        // 构建全局引用缓存
        string[] allProjectGUIDs = AssetDatabase.FindAssets("");
        int total = allProjectGUIDs.Length;
        int current = 0;

        foreach (string guid in allProjectGUIDs)
        {
            current++;
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // 跳过非Assets文件和非目标文件夹内的文件
            if (!assetPath.StartsWith("Assets/")) continue;

            EditorUtility.DisplayProgressBar("Analyzing References",
                $"Scanning... ({current}/{total})", (float)current / total);

            string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);

            foreach (string dependency in dependencies)
            {
                if (dependency == assetPath) continue; // 跳过自身引用

                if (!referenceCache.ContainsKey(dependency))
                    referenceCache[dependency] = new List<string>();

                referenceCache[dependency].Add(assetPath);
            }
        }

        // 计算目标文件夹内资产的引用次数
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
        EditorGUILayout.LabelField($"Target Folder: {targetFolderPath}", EditorStyles.boldLabel);

        if (GUILayout.Button("Analyze Again"))
            AnalyzeReferences();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Unreferenced Assets ({zeroReferenceAssets.Count}):", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (string assetPath in zeroReferenceAssets)
        {
            EditorGUILayout.BeginHorizontal();

            // 显示资产对象字段
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            EditorGUILayout.ObjectField(asset, typeof(Object), false, GUILayout.Width(300));

            // 显示路径标签
            EditorGUILayout.LabelField(assetPath, GUILayout.Width(400));

            // 删除按钮
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