using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

public class PlayableDirectorCleaner : EditorWindow
{
    private static GameObject targetPrefab;
    private static string originalPrefabPath;

    [MenuItem("Assets/Clean PlayableDirector References", false, 105)]
    private static void CleanPlayableDirectorReferences()
    {
        // Get the selected prefab.
        targetPrefab = Selection.activeGameObject;
        if (targetPrefab == null)
        {
            EditorUtility.DisplayDialog("Error", "Select a prefab asset.", "OK");
            return;
        }

        originalPrefabPath = AssetDatabase.GetAssetPath(targetPrefab);
        if (string.IsNullOrEmpty(originalPrefabPath))
        {
            EditorUtility.DisplayDialog("Error", "The selected object is not a prefab asset.", "OK");
            return;
        }

        // Show the save options window.
        ShowSaveOptionWindow();
    }

    [MenuItem("Assets/Clean PlayableDirector References", true)]
    private static bool Validate()
    {
        return Selection.activeObject != null &&
               !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(Selection.activeObject));
    }

    private static void ShowSaveOptionWindow()
    {
        if (EditorUtility.DisplayDialog("Cleanup Options",
            "Choose how to save the result", "Overwrite Original", "Save as New File"))
        {
            // Overwrite Original
            CleanAndSavePrefab(originalPrefabPath);
        }
        else
        {
            // Save as New File
            string newPath = GetNewPrefabPath(originalPrefabPath);
            CleanAndSavePrefab(newPath);
        }
    }

    private static string GetNewPrefabPath(string path)
    {
        string directory = Path.GetDirectoryName(path);
        string fileName = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);

        string newFileName = fileName + "_Cleaned";
        string newPath = Path.Combine(directory, newFileName + extension);

        // Ensure the path is unique.
        return AssetDatabase.GenerateUniqueAssetPath(newPath);
    }

    private static void CleanAndSavePrefab(string savePath)
    {
        Debug.Log($"[PlayableDirector Cleanup] Starting cleanup: {originalPrefabPath}");

        // Load the prefab contents for editing.
        GameObject prefabInstance = PrefabUtility.LoadPrefabContents(originalPrefabPath);

        bool cleaned = false;
        int directorsCleaned = 0;
        PlayableDirector[] directors = prefabInstance.GetComponentsInChildren<PlayableDirector>(true);

        Debug.Log($"[PlayableDirector Cleanup] Found {directors.Length} PlayableDirector components");

        foreach (PlayableDirector director in directors)
        {
            if (CleanDirectorReferences(director))
            {
                cleaned = true;
                directorsCleaned++;
                Debug.Log($"[PlayableDirector Cleanup] Cleaned Director: {director.gameObject.name}", director);
            }
        }

        if (cleaned)
        {
            // Save the prefab
            PrefabUtility.SaveAsPrefabAsset(prefabInstance, savePath, out bool success);

            if (success)
            {
                Debug.Log($"[PlayableDirector Cleanup] Cleaned and saved to: {savePath}\nRemoved invalid references from {directorsCleaned} Directors");
                EditorUtility.DisplayDialog("Success", $"PlayableDirector references cleaned and saved to: {savePath}\nRemoved invalid references from {directorsCleaned} Directors", "OK");
            }
            else
            {
                Debug.LogError("[PlayableDirector Cleanup] Failed to save the prefab");
                EditorUtility.DisplayDialog("Error", "Failed to save the prefab", "OK");
            }
        }
        else
        {
            Debug.Log("[PlayableDirector Cleanup] No PlayableDirector references require cleanup");
            EditorUtility.DisplayDialog("Information", "No PlayableDirector references require cleanup", "OK");
        }

        // Unload the prefab contents.
        PrefabUtility.UnloadPrefabContents(prefabInstance);
    }

    private static bool CleanDirectorReferences(PlayableDirector director)
    {
        bool modified = false;
        SerializedObject so = new SerializedObject(director);

        // Get the TimelineAsset reference.
        PlayableAsset timelineAsset = director.playableAsset;
        SerializedProperty playableAssetProp = so.FindProperty("m_PlayableAsset");

        if (timelineAsset == null)
        {
            Debug.LogWarning($"[PlayableDirector Cleanup] Director {director.gameObject.name} has no TimelineAsset; all bindings will be cleared", director);

            // Clean all bindings and exposed references.
            SerializedProperty sceneBindingsProp2 = so.FindProperty("m_SceneBindings");
            if (sceneBindingsProp2 != null && sceneBindingsProp2.isArray && sceneBindingsProp2.arraySize > 0)
            {
                Debug.Log($"[PlayableDirector Cleanup] Clear {sceneBindingsProp2.arraySize} scene bindings");
                sceneBindingsProp2.ClearArray();
                modified = true;
            }

            SerializedProperty exposedReferencesProp2 = so.FindProperty("m_ExposedReferences");
            if (exposedReferencesProp2 != null)
            {
                SerializedProperty referencesProp = exposedReferencesProp2.FindPropertyRelative("m_References");
                if (referencesProp != null && referencesProp.isArray && referencesProp.arraySize > 0)
                {
                    Debug.Log($"[PlayableDirector Cleanup] Clear {referencesProp.arraySize} exposed references");
                    referencesProp.ClearArray();
                    modified = true;
                }
            }

            if (modified)
            {
                so.ApplyModifiedProperties();
            }

            return modified;
        }

        Debug.Log($"[PlayableDirector Cleanup] Processing Director: {director.gameObject.name}, Timeline: {timelineAsset.name}");

        // Get all track bindings in the current TimelineAsset.
        Dictionary<Object, bool> validBindings = new Dictionary<Object, bool>();
        SerializedObject timelineSo = new SerializedObject(timelineAsset);
        SerializedProperty tracksProp = timelineSo.FindProperty("m_Tracks");

        if (tracksProp != null && tracksProp.isArray)
        {
            for (int i = 0; i < tracksProp.arraySize; i++)
            {
                SerializedProperty trackProp = tracksProp.GetArrayElementAtIndex(i);
                Object trackObject = trackProp.objectReferenceValue;
                if (trackObject != null)
                {
                    validBindings[trackObject] = true;
                }
            }
        }

        Debug.Log($"[PlayableDirector Cleanup] Timeline {timelineAsset.name} contains {validBindings.Count} valid tracks");

        // Clean m_SceneBindings.
        SerializedProperty sceneBindingsProp = so.FindProperty("m_SceneBindings");
        if (sceneBindingsProp != null && sceneBindingsProp.isArray)
        {
            int initialCount = sceneBindingsProp.arraySize;
            for (int i = sceneBindingsProp.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty bindingProp = sceneBindingsProp.GetArrayElementAtIndex(i);
                SerializedProperty keyProp = bindingProp.FindPropertyRelative("key");
                SerializedProperty valueProp = bindingProp.FindPropertyRelative("value");

                Object keyObject = keyProp.objectReferenceValue;

                if (keyObject == null)
                {
                    Debug.Log($"[PlayableDirector Cleanup: {i}");
                    sceneBindingsProp.DeleteArrayElementAtIndex(i);
                    modified = true;
                }
            }

            if (modified)
            {
                Debug.Log($"[PlayableDirector Cleanup] Scene binding cleanup: {initialCount} → {sceneBindingsProp.arraySize}");
            }
        }

        // Clean m_ExposedReferences.
        SerializedProperty exposedReferencesProp = so.FindProperty("m_ExposedReferences");
        if (exposedReferencesProp != null)
        {
            SerializedProperty referencesProp = exposedReferencesProp.FindPropertyRelative("m_References");
            if (referencesProp != null && referencesProp.isArray)
            {
                int initialCount = referencesProp.arraySize;
                int removedCount = 0;

                for (int i = referencesProp.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty referenceProp = referencesProp.GetArrayElementAtIndex(i);

                    // Check both serialized formats:
                    // 1. - fileID: {fileID: 0}
                    // 2. - 903425b99fe89ab4ea3d8f4394881b4a: {fileID: 0}

                    bool shouldRemove = false;

                    // First format: inspect the fileID property directly.
                    SerializedProperty fileIDProp = referenceProp.FindPropertyRelative("fileID");
                    if (fileIDProp != null)
                    {
                        if (fileIDProp.intValue == 0)
                        {
                            shouldRemove = true;
                            removedCount++;
                        }
                    }
                    else
                    {
                        // Second format: inspect all child properties.
                        var iterator = referenceProp.Copy();
                        var endProperty = referenceProp.GetEndProperty();

                        bool hasNonZeroValue = false;
                        while (iterator.NextVisible(true) && !SerializedProperty.EqualContents(iterator, endProperty))
                        {
                            if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                            {
                                if (iterator.objectReferenceValue != null)
                                {
                                    hasNonZeroValue = true;
                                    break;
                                }
                            }
                            else if (iterator.propertyType == SerializedPropertyType.Integer)
                            {
                                if (iterator.intValue != 0)
                                {
                                    hasNonZeroValue = true;
                                    break;
                                }
                            }
                        }

                        if (!hasNonZeroValue)
                        {
                            shouldRemove = true;
                            removedCount++;
                        }
                    }

                    if (shouldRemove)
                    {
                        referencesProp.DeleteArrayElementAtIndex(i);
                        modified = true;
                    }
                }

                if (removedCount > 0)
                {
                    Debug.Log($"[PlayableDirector Cleanup] Remove {removedCount} empty exposed references");
                }
            }
        }

        if (modified)
        {
            so.ApplyModifiedProperties();
            Debug.Log($"[PlayableDirector Cleanup] Director {director.gameObject.name} reference cleanup complete", director);
        }
        else
        {
            Debug.Log($"[PlayableDirector Cleanup] Director {director.gameObject.name} requires no cleanup", director);
        }

        return modified;
    }
}