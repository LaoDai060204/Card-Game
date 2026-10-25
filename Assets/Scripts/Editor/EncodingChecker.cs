using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

public class EncodingChecker : EditorWindow
{
    private List<string> nonUTF8Files = new List<string>();
    private Vector2 scrollPos;

    [MenuItem("Tools/Encoding Check/Find Non-UTF-8 Scripts")]
    private static void ShowWindow() => GetWindow<EncodingChecker>("Script Encoding Checker");

    private void OnGUI()
    {
        if (GUILayout.Button("Scan .cs Files (Exclude Packages)", GUILayout.Height(30)))
            ScanScripts();

        if (nonUTF8Files.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.Label($"Found {nonUTF8Files.Count} non-UTF-8 files:");
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            foreach (var file in nonUTF8Files)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(file);
                if (GUILayout.Button("Open in VS", GUILayout.Width(80)))
                    OpenInVS(file);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Convert All to UTF-8 without BOM", GUILayout.Height(30)))
                ConvertAllToUTF8();
        }
        else
        {
            GUILayout.Label("✅ All scripts use UTF-8 without BOM");
        }
    }

    // Scan while excluding the Packages directory.
    private void ScanScripts()
    {
        nonUTF8Files.Clear();
        string[] allScripts = AssetDatabase.FindAssets("t:Script");
        int total = allScripts.Length;

        for (int i = 0; i < total; i++)
        {
            string guid = allScripts[i];
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // Exclude the Packages directory.
            if (path.StartsWith("Packages/") || path.EndsWith(".dll"))
                continue;

            if (path.Contains("/Demigiant/"))
                continue;

            if (path.Contains("/Windbot/"))
                continue;

            if (path.StartsWith("Assets/Tools/"))
                continue;

            if (path.StartsWith("Assets/Scripts/YGOSharp"))
                continue;

            if (EditorUtility.DisplayCancelableProgressBar(
                "Scanning...",
                $"Checking: {Path.GetFileName(path)} ({i + 1}/{total})",
                (float)i / total))
                break;

            if (!IsUTF8WithoutBOM(path))
                nonUTF8Files.Add(path);
        }
        EditorUtility.ClearProgressBar();
    }

    // Detect UTF-8 without BOM.
    private bool IsUTF8WithoutBOM(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);

            // Reject files that contain a BOM.
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return false;

            // Check whether the byte sequence is valid UTF-8.
            return IsValidUTF8(bytes);
        }
        catch { return false; }
    }

    // UTF-8 byte-sequence validation.
    private bool IsValidUTF8(byte[] bytes)
    {
        int i = 0;
        while (i < bytes.Length)
        {
            if ((bytes[i] & 0x80) == 0x00) i++; // One-byte sequence
            else if ((bytes[i] & 0xE0) == 0xC0) // Two-byte sequence
            {
                if (i + 1 >= bytes.Length || (bytes[i + 1] & 0xC0) != 0x80)
                    return false;
                i += 2;
            }
            else if ((bytes[i] & 0xF0) == 0xE0) // Three-byte sequence
            {
                if (i + 2 >= bytes.Length ||
                    (bytes[i + 1] & 0xC0) != 0x80 ||
                    (bytes[i + 2] & 0xC0) != 0x80)
                    return false;
                i += 3;
            }
            else if ((bytes[i] & 0xF8) == 0xF0) // Four-byte sequence
            {
                if (i + 3 >= bytes.Length ||
                    (bytes[i + 1] & 0xC0) != 0x80 ||
                    (bytes[i + 2] & 0xC0) != 0x80 ||
                    (bytes[i + 3] & 0xC0) != 0x80)
                    return false;
                i += 4;
            }
            else return false;
        }
        return true;
    }

    // Open a file in Visual Studio or VS Code.
    private void OpenInVS(string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);

        // Try Visual Studio first.
        if (File.Exists(@"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe"))
            Process.Start("devenv.exe", $"/Edit \"{fullPath}\"");
        // Fall back to VS Code.
        else
            Process.Start("code", $"\"{fullPath}\"");
    }

    // Convert encoding while preserving the content.
    private void ConvertAllToUTF8()
    {
        foreach (string path in nonUTF8Files)
        {
            // Read using the system encoding to support legacy text.
            string content = File.ReadAllText(path, Encoding.Default);
            // Write UTF-8 without a BOM.
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }
        AssetDatabase.Refresh();
        UnityEngine.Debug.Log($"Converted {nonUTF8Files.Count} files");
        nonUTF8Files.Clear();
    }
}