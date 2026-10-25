using Codice.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class FolderDependencyAnalyzer : EditorWindow
{
    private string targetFolderPath;
    private Dictionary<string, List<string>> externalDependencies = new Dictionary<string, List<string>>();
    private Vector2 scrollPosition;
    private bool showInternalDependencies = false;

    [MenuItem("Assets/Analyze Folder External Dependencies", false, 20)]
    static void AnalyzeFolderDependencies()
    {
        // Get the selected folder.
        string folderPath = AssetDatabase.GetAssetPath(Selection.activeObject);

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            EditorUtility.DisplayDialog("Error", "Select a valid folder", "OK");
            return;
        }

        // Create and show the analysis window.
        var window = CreateInstance<FolderDependencyAnalyzer>();
        window.targetFolderPath = folderPath;
        window.titleContent = new GUIContent($"Dependency analysis: {Path.GetFileName(folderPath)}");
        window.ShowUtility();
        window.AnalyzeDependencies();
    }

    [MenuItem("Assets/Analyze Folder External Dependencies", true)]
    static bool ValidateAnalyzeFolderDependencies()
    {
        return Selection.activeObject != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(Selection.activeObject));
    }

    void AnalyzeDependencies()
    {
        externalDependencies.Clear();
        EditorUtility.DisplayProgressBar("Analyze Dependencies", "Collecting assets in the folder...", 0);

        try
        {
            // Get all assets in the folder.
            string[] allAssetPaths = AssetDatabase.FindAssets("", new[] { targetFolderPath })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(path => !path.EndsWith(".meta"))
                .ToArray();

            // Analyze the dependencies of each asset.
            for (int i = 0; i < allAssetPaths.Length; i++)
            {
                string assetPath = allAssetPaths[i];
                EditorUtility.DisplayProgressBar("Analyze Dependencies", $"Analyzing: {Path.GetFileName(assetPath)}", (float)i / allAssetPaths.Length);

                // Get all dependencies of this asset.
                string[] dependencies = AssetDatabase.GetDependencies(assetPath, true);

                // Keep dependencies outside the current folder.
                List<string> externalDeps = dependencies
                    .Where(depPath =>
                        !depPath.StartsWith(targetFolderPath) && // Outside the current folder
                        !depPath.Equals(assetPath) && // Exclude self-references
                        !depPath.EndsWith(".cs") && // Exclude script files
                        !depPath.EndsWith(".dll") && // Exclude DLL files
                        !depPath.EndsWith(".shader")) // Exclude shader files
                    .ToList();

                if (externalDeps.Count > 0)
                {
                    externalDependencies[assetPath] = externalDeps;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Analyzed folder: {targetFolderPath}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Found {externalDependencies.Count} assets with external dependencies");
        EditorGUILayout.Space();

        if (GUILayout.Button("Analyze Again"))
        {
            AnalyzeDependencies();
        }

        showInternalDependencies = EditorGUILayout.Toggle("Show Internal Dependencies", showInternalDependencies);
        EditorGUILayout.Space();

        if (externalDependencies.Count == 0)
        {
            EditorGUILayout.HelpBox("No external dependencies were found, or the folder contains no assets.", MessageType.Info);
            return;
        }

        // Display the external dependency list.
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (var item in externalDependencies)
        {
            string assetPath = item.Key;
            List<string> deps = item.Value;

            EditorGUILayout.BeginVertical("box");

            // Display asset names and paths.
            EditorGUILayout.BeginHorizontal();
            GUIStyle boldStyle = new GUIStyle(EditorStyles.foldout);
            boldStyle.fontStyle = FontStyle.Bold;

            bool isExpanded = EditorGUILayout.Foldout(false, $"{Path.GetFileName(assetPath)} ({deps.Count} external dependencies)", true, boldStyle);

            if (GUILayout.Button("Select", GUILayout.Width(50)))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Path: {assetPath}", EditorStyles.miniLabel);

            // Display external dependency details.
            EditorGUI.indentLevel++;
            foreach (string depPath in deps)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(Path.GetFileName(depPath), GUILayout.Width(200));
                EditorGUILayout.LabelField(depPath, EditorStyles.miniLabel);

                if (GUILayout.Button("Locate", GUILayout.Width(40)))
                {
                    Object depObject = AssetDatabase.LoadAssetAtPath<Object>(depPath);
                    if (depObject != null)
                    {
                        Selection.activeObject = depObject;
                        EditorGUIUtility.PingObject(depObject);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        EditorGUILayout.EndScrollView();
    }

    // Add a menu item for the tool window.
    [MenuItem("Tools/Asset Analysis/Folder Dependency Analyzer")]
    static void ShowWindow()
    {
        var window = CreateInstance<FolderDependencyAnalyzer>();
        window.titleContent = new GUIContent("Folder Dependency Analysis");
        window.Show();
    }
}