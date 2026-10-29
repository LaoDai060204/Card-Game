#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class MissingShaderFinder : EditorWindow
{
    private List<Material> _materialsWithMissingShader = new List<Material>();

    [MenuItem("Tools/Find Materials with Missing Shaders")]
    public static void ShowWindow()
    {
        GetWindow<MissingShaderFinder>("Materials with Missing Shaders");
    }

    void OnGUI()
    {
        if (GUILayout.Button("Scan Project", GUILayout.Height(40)))
        {
            FindMaterialsWithMissingShader();
        }

        GUILayout.Label($"Found {_materialsWithMissingShader.Count} materials with missing shaders:");
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        foreach (Material mat in _materialsWithMissingShader)
        {
            EditorGUILayout.BeginHorizontal();
            // 显示材质名称和路径
            EditorGUILayout.ObjectField(mat, typeof(Material), false);

            // 定位按钮
            if (GUILayout.Button("Locate", GUILayout.Width(60)))
            {
                Selection.activeObject = mat;
                EditorGUIUtility.PingObject(mat);
            }

            // 修复按钮
            if (GUILayout.Button("Fix Standard", GUILayout.Width(80)))
            {
                FixShader(mat, Shader.Find("Standard"));
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private Vector2 scrollPos;

    private void FindMaterialsWithMissingShader()
    {
        _materialsWithMissingShader.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Material");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat.shader == null || mat.shader.name.Contains("Error"))
            {
                _materialsWithMissingShader.Add(mat);
            }
        }
    }

    private void FixShader(Material mat, Shader replacementShader)
    {
        if (mat != null && replacementShader != null)
        {
            Undo.RecordObject(mat, "Fix Missing Shader");
            mat.shader = replacementShader;
            EditorUtility.SetDirty(mat);
            Debug.Log($"Fixed material: {mat.name}", mat);
        }
    }
}
#endif