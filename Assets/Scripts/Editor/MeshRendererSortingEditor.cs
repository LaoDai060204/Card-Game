using UnityEngine;
using UnityEditor;
using System.Reflection;

[CustomEditor(typeof(MeshRenderer))]
public class MeshRendererSortingEditor : Editor
{
    private SerializedProperty sortingLayerID;
    private SerializedProperty sortingOrder;
    private static MethodInfo sortingLayerFieldMethod;

    private void OnEnable()
    {
        sortingLayerID = serializedObject.FindProperty("m_SortingLayerID");
        sortingOrder = serializedObject.FindProperty("m_SortingOrder");

        // Cache method information to avoid repeated lookups.
        if (sortingLayerFieldMethod == null)
        {
            sortingLayerFieldMethod = typeof(EditorGUILayout).GetMethod(
                "SortingLayerField",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new System.Type[] { typeof(GUIContent), typeof(SerializedProperty), typeof(GUIStyle) },
                null
            );
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sorting", EditorStyles.boldLabel);

        if (sortingLayerFieldMethod != null)
        {
            // Invoke the internal method through reflection.
            sortingLayerFieldMethod.Invoke(
                null,
                new object[] { new GUIContent("Sorting Layer"), sortingLayerID, EditorStyles.popup }
            );
        }
        else
        {
            // Fall back to an integer field if reflection fails.
            EditorGUILayout.PropertyField(sortingLayerID, new GUIContent("Sorting Layer ID"));
        }

        EditorGUILayout.PropertyField(sortingOrder, new GUIContent("Order in Layer"));

        serializedObject.ApplyModifiedProperties();
    }
}