using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using UnityEngine.Playables;

public class MissingReferencesFinder : EditorWindow
{
    private Vector2 scrollPosition;
    private List<MissingReference> missingReferences = new List<MissingReference>();
    private bool includeScenes = true;
    private bool includePrefabs = true;
    private bool includeScriptableObjects = true;

    [MenuItem("Tools/Missing Reference Finder")]
    public static void ShowWindow()
    {
        GetWindow<MissingReferencesFinder>("Missing Reference Finder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Missing Reference Finder", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // Configuration options
        includeScenes = EditorGUILayout.Toggle("Include Scenes", includeScenes);
        includePrefabs = EditorGUILayout.Toggle("Include Prefabs", includePrefabs);
        includeScriptableObjects = EditorGUILayout.Toggle("Include ScriptableObjects", includeScriptableObjects);

        GUILayout.Space(10);

        if (GUILayout.Button("Scan Entire Project", GUILayout.Height(30)))
        {
            ScanEntireProject();
        }

        if (GUILayout.Button("Scan Selected Assets", GUILayout.Height(30)))
        {
            ScanSelectedAssets();
        }

        GUILayout.Space(10);

        // Display results.
        GUILayout.Label($"Found {missingReferences.Count} missing references");

        if (missingReferences.Count > 0)
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

            foreach (var missingRef in missingReferences)
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Asset: {missingRef.assetPath}", EditorStyles.boldLabel);
                if (GUILayout.Button("Locate", GUILayout.Width(60)))
                {
                    Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(missingRef.assetPath);
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField($"Component: {missingRef.componentType}");
                EditorGUILayout.LabelField($"Property: {missingRef.propertyName}");
                EditorGUILayout.LabelField($"Hierarchy path: {missingRef.gameObjectPath}");

                EditorGUILayout.EndVertical();
                GUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void ScanEntireProject()
    {
        missingReferences.Clear();
        int totalScanned = 0;

        try
        {
            // Scan Prefabs
            if (includePrefabs)
            {
                string[] prefabPaths = AssetDatabase.GetAllAssetPaths()
                    .Where(path => path.EndsWith(".prefab") 
                    && path.StartsWith("Assets/AssetBundles")
                    && !path.Contains("Mat_002")
                    )
                    .ToArray();

                foreach (string path in prefabPaths)
                {
                    EditorUtility.DisplayProgressBar("Scan Prefabs", path, (float)totalScanned / prefabPaths.Length);
                    ScanAssetAtPath(path);
                    totalScanned++;
                }
            }

            // Scan Scenes
            if (includeScenes)
            {
                string[] scenePaths = AssetDatabase.GetAllAssetPaths()
                    .Where(path => path.EndsWith(".unity"))
                    .ToArray();

                foreach (string path in scenePaths)
                {
                    EditorUtility.DisplayProgressBar("Scan Scenes", path, (float)totalScanned / (totalScanned + scenePaths.Length));
                    ScanAssetAtPath(path);
                    totalScanned++;
                }
            }

            // Scan ScriptableObjects
            if (includeScriptableObjects)
            {
                string[] soPaths = AssetDatabase.GetAllAssetPaths()
                    .Where(path =>
                        path.EndsWith(".asset") &&
                        !path.Contains("/Resources/") &&
                        AssetDatabase.GetMainAssetTypeAtPath(path)?.IsSubclassOf(typeof(ScriptableObject)) == true)
                    .ToArray();

                foreach (string path in soPaths)
                {
                    EditorUtility.DisplayProgressBar("Scan ScriptableObjects", path, (float)totalScanned / (totalScanned + soPaths.Length));
                    ScanScriptableObjectAtPath(path);
                    totalScanned++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"Scan complete. Missing references found: {missingReferences.Count} missing references");
    }

    private void ScanSelectedAssets()
    {
        missingReferences.Clear();
        var selectedObjects = Selection.objects;

        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("Select assets to scan first.");
            return;
        }

        foreach (var obj in selectedObjects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path))
            {
                ScanAssetAtPath(path);
            }
        }

        Debug.Log($"Scan complete. Missing references found: {missingReferences.Count} missing references");
    }

    private void ScanAssetAtPath(string assetPath)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null) return;

        // Check GameObjects in the prefab or scene.
        Component[] components = asset.GetComponentsInChildren<Component>(true);

        foreach (var component in components)
        {
            if (component == null)
            {
                // Missing components, such as deleted scripts.
                missingReferences.Add(new MissingReference
                {
                    assetPath = assetPath,
                    componentType = "Missing Script",
                    propertyName = "Component",
                    //gameObjectPath = GetGameObjectPath(component.gameObject, asset.transform)
                });
                continue;
            }

            CheckComponentForMissingReferences(component, assetPath, asset.transform);
        }
    }

    private void ScanScriptableObjectAtPath(string assetPath)
    {
        ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
        if (so == null) return;

        SerializedObject serializedObject = new SerializedObject(so);
        var property = serializedObject.GetIterator();

        while (property.NextVisible(true))
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                if (property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                {
                    missingReferences.Add(new MissingReference
                    {
                        assetPath = assetPath,
                        componentType = so.GetType().Name,
                        propertyName = property.name,
                        gameObjectPath = "ScriptableObject"
                    });
                }
            }
        }
    }

    private void CheckComponentForMissingReferences(Component component, string assetPath, Transform rootTransform)
    {
        if (component is PlayableDirector)
            return;

        SerializedObject serializedObject = new SerializedObject(component);
        var property = serializedObject.GetIterator();

        // Use reflection to inspect object references more accurately.
        var objRefValueMethod = typeof(SerializedProperty).GetProperty("objectReferenceStringValue",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        while (property.NextVisible(true))
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                string objectReferenceStringValue = string.Empty;
                if (objRefValueMethod != null)
                {
                    objectReferenceStringValue = (string)objRefValueMethod.GetGetMethod(true)
                        .Invoke(property, new object[0]);
                }

                // Check whether the reference is missing.
                if (property.objectReferenceValue == null &&
                    (property.objectReferenceInstanceIDValue != 0 ||
                     (objectReferenceStringValue != null && objectReferenceStringValue.StartsWith("Missing"))))
                {
                    missingReferences.Add(new MissingReference
                    {
                        assetPath = assetPath,
                        componentType = component.GetType().Name,
                        propertyName = property.name,
                        gameObjectPath = GetGameObjectPath(component.gameObject, rootTransform)
                    });
                }
            }
        }
    }

    private string GetGameObjectPath(GameObject gameObject, Transform rootTransform)
    {
        List<string> path = new List<string>();
        Transform current = gameObject.transform;

        while (current != null && current != rootTransform)
        {
            path.Insert(0, current.name);
            current = current.parent;
        }

        return string.Join("/", path);
    }

    [MenuItem("Assets/Find Missing References", false, 20)]
    private static void FindMissingReferencesInSelection()
    {
        var window = GetWindow<MissingReferencesFinder>();
        window.ScanSelectedAssets();
    }

    [MenuItem("CONTEXT/Component/Check Missing References")]
    private static void FindMissingReferencesInComponent(MenuCommand command)
    {
        Component component = command.context as Component;
        if (component != null)
        {
            var window = GetWindow<MissingReferencesFinder>();
            window.CheckSingleComponent(component);
        }
    }

    private void CheckSingleComponent(Component component)
    {
        missingReferences.Clear();

        string assetPath = AssetDatabase.GetAssetPath(component.gameObject);
        if (string.IsNullOrEmpty(assetPath))
        {
            assetPath = "Current Scene";
        }

        CheckComponentForMissingReferences(component, assetPath,
            component.transform.root ?? component.transform);

        Debug.Log($"Check complete. Missing references found: {missingReferences.Count} missing references");
    }
}

[System.Serializable]
public class MissingReference
{
    public string assetPath;
    public string componentType;
    public string propertyName;
    public string gameObjectPath;
}