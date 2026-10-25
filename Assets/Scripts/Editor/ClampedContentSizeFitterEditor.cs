using CardGame.UI;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

[CustomEditor(typeof(ClampedContentSizeFitter))]
public class ClampedContentSizeFitterEditor : ContentSizeFitterEditor
{
    SerializedProperty m_MinWidth;
    SerializedProperty m_MaxWidth;

    protected override void OnEnable()
    {
        base.OnEnable();
        m_MinWidth = serializedObject.FindProperty("m_MinWidth");
        m_MaxWidth = serializedObject.FindProperty("m_MaxWidth");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(m_MinWidth, new GUIContent("Min Width"));
        EditorGUILayout.PropertyField(m_MaxWidth, new GUIContent("Max Width"));
        serializedObject.ApplyModifiedProperties();

        base.OnInspectorGUI(); // Draw the built-in inspector settings.

    }
}