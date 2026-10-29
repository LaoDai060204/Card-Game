using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class AssetReferenceTool
{
    private static bool _isProcessing = false;

    private static readonly HashSet<string> AssetExtensions = new HashSet<string>
    {
        ".prefab", ".unity", ".asset", ".mat",
        ".controller", ".anim", ".overrideController",
        ".physicMaterial", ".physicsMaterial2D", ".flare",
        ".fontsettings", ".guiskin", ".renderTexture",
        ".lighting", ".terrainlayer", ".signal", ".mixer"
    };

    // -------------------- 菜单：合并重复资产（基于文件名） --------------------
    [MenuItem("Assets/Merge Duplicate Assets", true)]
    static bool ValidateMergeDuplicate()
    {
        return Selection.objects.Length > 1;
    }

    [MenuItem("Assets/Merge Duplicate Assets")]
    static void MergeDuplicate()
    {
        _isProcessing = true;
        try { MergeDuplicateInternal(); }
        finally { _isProcessing = false; }
    }

    static void MergeDuplicateInternal()
    {
        var assetPaths = Selection.objects
            .Select(AssetDatabase.GetAssetPath)
            .Where(p => !AssetDatabase.IsValidFolder(p))
            .ToList();

        if (assetPaths.Count < 2)
        {
            EditorUtility.DisplayDialog("Error", "Select at least two asset files, not folders.", "OK");
            return;
        }

        var fileNames = assetPaths.Select(p => Path.GetFileNameWithoutExtension(p)).ToList();
        string baseName = fileNames.OrderBy(n => n.Length).First();
        var keepCandidates = new List<string>();
        var deletePaths = new List<string>();

        foreach (var path in assetPaths)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (name == baseName)
                keepCandidates.Add(path);
            else if (IsNumberedVariant(baseName, name))
                deletePaths.Add(path);
            else
            {
                EditorUtility.DisplayDialog("Error",
                    $"File \"{name}\" does not match the expected format.\nAll files must share the base name \"{baseName}\"; duplicate names must use \"baseName_number\".",
                    "OK");
                return;
            }
        }

        if (keepCandidates.Count != 1 || deletePaths.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "No unique base file or duplicate copies were found.", "OK");
            return;
        }

        string keepPath = keepCandidates[0];
        string keepName = Path.GetFileName(keepPath);
        string deleteNames = string.Join("\n", deletePaths.Select(Path.GetFileName));
        if (!EditorUtility.DisplayDialog("Confirm Merge",
            $"The following duplicates will be deleted, and all references will point to the retained file:\n\nKeep: {keepName}\n\nTo delete:\n{deleteNames}\n\nThis cannot be undone!",
            "Confirm", "Cancel"))
            return;

        string keepGuid = AssetDatabase.AssetPathToGUID(keepPath);
        var deleteGuids = deletePaths.Select(AssetDatabase.AssetPathToGUID).ToList();
        ReplaceGUIDReferences(deleteGuids, keepGuid);

        foreach (var path in deletePaths) AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Done", "Duplicate assets have been merged.", "OK");
    }

    static bool IsNumberedVariant(string baseName, string name)
    {
        if (!name.StartsWith(baseName + "_")) return false;
        string suffix = name.Substring(baseName.Length + 1);
        return Regex.IsMatch(suffix, @"^\d+$");
    }

    // -------------------- 菜单：替换单个资产的 GUID --------------------
    [MenuItem("Assets/Replace Asset GUID", true)]
    static bool ValidateReplaceGUID()
    {
        if (Selection.objects.Length != 1) return false;
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return !string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path);
    }

    [MenuItem("Assets/Replace Asset GUID")]
    static void ShowReplaceGUIDWindow()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        string guid = AssetDatabase.AssetPathToGUID(path);
        ReplaceGUIDWindow.OpenWindow(path, guid);
    }

    // -------------------- 菜单：查看资产引用 --------------------
    [MenuItem("Assets/View Asset References", true)]
    static bool ValidateShowReferences()
    {
        if (Selection.objects.Length != 1) return false;
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return !string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path);
    }

    [MenuItem("Assets/View Asset References")]
    static void ShowReferencesWindow()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        ReferenceViewerWindow.OpenWindow(path);
    }

    // -------------------- 菜单：查看资产被引用 --------------------
    [MenuItem("Assets/View Asset Referencers", true)]
    static bool ValidateShowDependents()
    {
        if (Selection.objects.Length != 1) return false;
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return !string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path);
    }

    [MenuItem("Assets/View Asset Referencers")]
    static void ShowDependentsWindow()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        DependentsViewerWindow.OpenWindow(path);
    }

    // -------------------- 菜单：通过 GUID 查找资产 --------------------
    [MenuItem("Assets/Find Asset by GUID", true)]
    static bool ValidateFindByGuid() { return true; }

    [MenuItem("Assets/Find Asset by GUID")]
    static void ShowFindByGuidWindow()
    {
        FindAssetByGuidWindow.OpenWindow();
    }

    // -------------------- 菜单：合并文件夹内的重复资产（增强版） --------------------
    [MenuItem("Assets/Merge Duplicate Assets in Folder", true)]
    static bool ValidateMergeFolderDuplicates()
    {
        if (Selection.objects.Length != 1) return false;
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return AssetDatabase.IsValidFolder(path);
    }

    [MenuItem("Assets/Merge Duplicate Assets in Folder")]
    static void MergeFolderDuplicates()
    {
        string folderPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            EditorUtility.DisplayDialog("Error", "Select a valid folder.", "OK");
            return;
        }

        _isProcessing = true;
        try { MergeFolderDuplicatesInternal(folderPath); }
        finally { _isProcessing = false; }
    }

    static void MergeFolderDuplicatesInternal(string folderPath)
    {
        string[] allPaths = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith(folderPath) && !AssetDatabase.IsValidFolder(p))
            .ToArray();

        if (allPaths.Length == 0)
        {
            EditorUtility.DisplayDialog("Notice", "No asset files were found in the selected folder or its subfolders.", "OK");
            return;
        }

        // 第一阶段：主对象名称匹配
        var mainNameGroups = new Dictionary<string, List<string>>();
        foreach (string path in allPaths)
        {
            Object mainObj = AssetDatabase.LoadMainAssetAtPath(path);
            if (mainObj == null) continue;
            string mainName = mainObj.name;
            if (!mainNameGroups.ContainsKey(mainName))
                mainNameGroups[mainName] = new List<string>();
            mainNameGroups[mainName].Add(path);
        }

        var mergeGroups = new Dictionary<string, List<string>>();
        var processedPaths = new HashSet<string>();

        foreach (var kvp in mainNameGroups)
        {
            string mainName = kvp.Key;
            List<string> paths = kvp.Value;
            var baseFiles = paths.Where(p => Path.GetFileNameWithoutExtension(p) == mainName).ToList();
            if (baseFiles.Count != 1) continue;

            string basePath = baseFiles[0];
            var duplicates = paths.Where(p => p != basePath && Path.GetFileNameWithoutExtension(p) != mainName).ToList();
            if (duplicates.Count > 0)
            {
                mergeGroups[basePath] = duplicates;
                processedPaths.Add(basePath);
                foreach (var d in duplicates) processedPaths.Add(d);
            }
        }

        // 第二阶段：内容哈希匹配
        var remainingPaths = allPaths.Where(p => !processedPaths.Contains(p)).ToList();
        if (remainingPaths.Count > 1)
        {
            var extGroups = remainingPaths.GroupBy(p => Path.GetExtension(p).ToLower());
            foreach (var group in extGroups)
            {
                var paths = group.ToList();
                if (paths.Count < 2) continue;

                var hashToPaths = new Dictionary<string, List<string>>();
                foreach (var path in paths)
                {
                    string hash = GetFileMD5(path);
                    if (string.IsNullOrEmpty(hash)) continue;
                    if (!hashToPaths.ContainsKey(hash))
                        hashToPaths[hash] = new List<string>();
                    hashToPaths[hash].Add(path);
                }

                foreach (var hashGroup in hashToPaths)
                {
                    if (hashGroup.Value.Count < 2) continue;
                    var sorted = hashGroup.Value.OrderBy(p => Path.GetFileNameWithoutExtension(p).Length).ToList();
                    string keepPath = sorted[0];
                    var duplicates = sorted.Skip(1).ToList();
                    if (duplicates.Count > 0)
                        mergeGroups[keepPath] = duplicates;
                }
            }
        }

        if (mergeGroups.Count == 0)
        {
            EditorUtility.DisplayDialog("Notice", "No duplicate assets were found to merge.", "OK");
            return;
        }

        var sb = new StringBuilder("The following duplicate groups were detected and will be merged:\n\n");
        foreach (var group in mergeGroups)
        {
            sb.AppendLine($"Keep: {Path.GetFileName(group.Key)}");
            foreach (var dup in group.Value)
                sb.AppendLine($"  Delete: {Path.GetFileName(dup)}");
            sb.AppendLine();
        }
        sb.Append("This will replace all references and delete duplicate files. This cannot be undone!");

        if (!EditorUtility.DisplayDialog("Confirm Merge", sb.ToString(), "Merge All", "Cancel"))
            return;

        EnsureCacheReady();

        foreach (var group in mergeGroups)
        {
            string keepPath = group.Key;
            List<string> deletePaths = group.Value;

            string keepGuid = AssetDatabase.AssetPathToGUID(keepPath);
            var deleteGuids = deletePaths.Select(AssetDatabase.AssetPathToGUID).ToList();
            ReplaceGUIDReferences(deleteGuids, keepGuid);

            foreach (string del in deletePaths)
                AssetDatabase.DeleteAsset(del);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Done", "Duplicate assets in the folder have been merged.", "OK");
    }

    private static string GetFileMD5(string filePath)
    {
        try
        {
            byte[] data = File.ReadAllBytes(filePath);
            using (var md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(data);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }
        catch
        {
            return null;
        }
    }

    // -------------------- 菜单：Prefab中Sprite引用type改为3（简化版） --------------------
    [MenuItem("Assets/Set Sprite Reference Type to 3", true)]
    static bool ValidateFixSpriteRefType()
    {
        if (Selection.objects.Length != 1) return false;
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return !string.IsNullOrEmpty(path) && Path.GetExtension(path).ToLower() == ".prefab";
    }

    [MenuItem("Assets/Set Sprite Reference Type to 3")]
    static void FixSpriteRefType()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            EditorUtility.DisplayDialog("Error", "No valid prefab file was found.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Confirm Change",
            $"In prefab \"{Path.GetFileName(path)}\" all m_Sprite references will be changed from type: 2 to type: 3.\n\nThis changes how assets are loaded. Back up the prefab before continuing.",
            "Continue", "Cancel"))
            return;

        string[] lines = File.ReadAllLines(path);
        bool modified = false;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("m_Sprite:"))
            {
                if (lines[i].Contains("type: 2"))
                {
                    lines[i] = lines[i].Replace("type: 2", "type: 3");
                    modified = true;
                }
            }
        }

        if (!modified)
        {
            EditorUtility.DisplayDialog("Notice", "No m_Sprite references with type: 2 were found. No changes are needed.", "OK");
            return;
        }

        File.WriteAllLines(path, lines);
        AssetDatabase.ImportAsset(path);
        EditorUtility.DisplayDialog("Done", "Sprite reference types in the prefab have been changed to type: 3.", "OK");
    }

    // -------------------- 替换 GUID 窗口 --------------------
    internal class ReplaceGUIDWindow : EditorWindow
    {
        private string assetPath, currentGuid, newGuidInput = "";
        private Object assetObject;

        public static void OpenWindow(string path, string guid)
        {
            var window = GetWindow<ReplaceGUIDWindow>(true, "Replace Asset GUID");
            window.assetPath = path;
            window.currentGuid = guid;
            window.newGuidInput = "";
            window.assetObject = AssetDatabase.LoadMainAssetAtPath(path);
            window.minSize = new Vector2(450, 220);
            window.maxSize = new Vector2(600, 260);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Asset Path:");
            EditorGUILayout.ObjectField(assetObject, typeof(Object), false);

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Current GUID:");
            EditorGUILayout.SelectableLabel(currentGuid, EditorStyles.textField, GUILayout.Height(18));

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("New GUID:");
            newGuidInput = EditorGUILayout.TextField(newGuidInput);

            EditorGUILayout.Space(15);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Replace", GUILayout.Width(100))) Replace();
            if (GUILayout.Button("Cancel", GUILayout.Width(100))) Close();
            EditorGUILayout.EndHorizontal();
        }

        private void Replace()
        {
            string newGuid = newGuidInput.Trim();
            if (!Regex.IsMatch(newGuid, @"^[a-fA-F0-9]{32}$"))
            {
                EditorUtility.DisplayDialog("Invalid Format", "GUID must be a 32-character hexadecimal string.", "OK");
                return;
            }
            if (newGuid == currentGuid)
            {
                EditorUtility.DisplayDialog("No Change", "The new GUID is the same as the current GUID.", "OK");
                return;
            }
            string existPath = AssetDatabase.GUIDToAssetPath(newGuid);
            if (!string.IsNullOrEmpty(existPath) && existPath != assetPath)
            {
                EditorUtility.DisplayDialog("Conflict", $"GUID is already in use:\n{existPath}", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Confirm Replacement", $"Change GUID from {currentGuid} to {newGuid}?", "Confirm", "Cancel"))
                return;

            _isProcessing = true;
            try
            {
                string metaPath = assetPath + ".meta";
                if (File.Exists(metaPath))
                {
                    string content = File.ReadAllText(metaPath);
                    content = Regex.Replace(content, @"^guid:\s*\S+", "guid: " + newGuid, RegexOptions.Multiline);
                    File.WriteAllText(metaPath, content);
                }
                EnsureCacheReady();
                ReplaceGUIDReferences(new List<string> { currentGuid }, newGuid);
                AssetDatabase.ImportAsset(assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Done", "The asset GUID has been replaced.", "OK");
            }
            finally { _isProcessing = false; }
            Close();
        }
    }

    // -------------------- 查看资产引用窗口 --------------------
    internal class ReferenceViewerWindow : EditorWindow
    {
        private string assetPath;
        private List<Object> referencedObjects = new List<Object>();
        private Vector2 scroll;

        static readonly HashSet<string> ScriptExts = new HashSet<string> { ".cs", ".js", ".boo" };

        public static void OpenWindow(string path)
        {
            var window = GetWindow<ReferenceViewerWindow>(true, "Asset References");
            window.assetPath = path;
            window.LoadRefs();
            window.minSize = new Vector2(450, 300);
            window.Show();
        }

        void LoadRefs()
        {
            referencedObjects.Clear();
            if (string.IsNullOrEmpty(assetPath)) return;

            foreach (var dep in AssetDatabase.GetDependencies(assetPath, false))
            {
                if (dep == assetPath) continue;
                string ext = Path.GetExtension(dep)?.ToLower();
                if (ext != null && ScriptExts.Contains(ext)) continue;

                Object obj = AssetDatabase.LoadMainAssetAtPath(dep);
                if (obj != null)
                    referencedObjects.Add(obj);
            }
        }

        void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Asset:", assetPath);
            EditorGUILayout.LabelField("Reference Count:", referencedObjects.Count.ToString());
            EditorGUILayout.Space(5);

            if (referencedObjects.Count == 0)
            {
                EditorGUILayout.HelpBox("This asset does not reference any non-script assets.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var obj in referencedObjects)
            {
                EditorGUILayout.ObjectField(obj, typeof(Object), false);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    // -------------------- 查看资产被引用窗口 --------------------
    internal class DependentsViewerWindow : EditorWindow
    {
        private string assetPath;
        private List<Object> dependentObjects = new List<Object>();
        private Vector2 scroll;
        static readonly HashSet<string> ScriptExts = new HashSet<string> { ".cs", ".js", ".boo" };

        public static void OpenWindow(string path)
        {
            var window = GetWindow<DependentsViewerWindow>(true, "Asset Referencers");
            window.assetPath = path;
            window.LoadDependents();
            window.minSize = new Vector2(450, 300);
            window.Show();
        }

        void LoadDependents()
        {
            dependentObjects.Clear();
            if (string.IsNullOrEmpty(assetPath)) return;

            string targetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            EnsureCacheReady();

            if (_guidRefMap != null && _guidRefMap.TryGetValue(targetGuid, out var referencerPaths))
            {
                foreach (string refPath in referencerPaths)
                {
                    string ext = Path.GetExtension(refPath)?.ToLower();
                    if (!string.IsNullOrEmpty(ext) && ScriptExts.Contains(ext))
                        continue;

                    Object obj = AssetDatabase.LoadMainAssetAtPath(refPath);
                    if (obj != null)
                        dependentObjects.Add(obj);
                }
            }
        }

        void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Asset:", assetPath);
            EditorGUILayout.LabelField("Referencer Count:", dependentObjects.Count.ToString());
            EditorGUILayout.Space(5);

            if (dependentObjects.Count == 0)
            {
                EditorGUILayout.HelpBox("This asset is not referenced by any non-script assets.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var obj in dependentObjects)
            {
                EditorGUILayout.ObjectField(obj, typeof(Object), false);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    // -------------------- 通过 GUID 查找资产窗口 --------------------
    internal class FindAssetByGuidWindow : EditorWindow
    {
        private string searchGuid = "";
        private Object foundAsset;

        public static void OpenWindow()
        {
            var window = GetWindow<FindAssetByGuidWindow>(true, "Find Asset by GUID");
            window.minSize = new Vector2(400, 150);
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(10);
            searchGuid = EditorGUILayout.TextField("Enter GUID:", searchGuid);

            if (GUILayout.Button("Find", GUILayout.Width(100)))
            {
                FindAsset();
            }

            EditorGUILayout.Space(10);
            if (foundAsset != null)
            {
                EditorGUILayout.ObjectField("Found Asset:", foundAsset, typeof(Object), false);
            }
            else if (!string.IsNullOrEmpty(searchGuid))
            {
                EditorGUILayout.HelpBox("No matching asset was found.", MessageType.Warning);
            }
        }

        void FindAsset()
        {
            foundAsset = null;
            string guid = searchGuid.Trim();
            if (guid.Length != 32 || !Regex.IsMatch(guid, @"^[a-fA-F0-9]{32}$"))
            {
                EditorUtility.DisplayDialog("Error", "Invalid GUID format.", "OK");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                foundAsset = null;
                Repaint();
                return;
            }

            foundAsset = AssetDatabase.LoadMainAssetAtPath(path);
            if (foundAsset)
            {
                EditorGUIUtility.PingObject(foundAsset);
                Selection.activeObject = foundAsset;
            }
            Repaint();
        }
    }

    // ---- 共用引用缓存系统 ----
    private static Dictionary<string, HashSet<string>> _guidRefMap;
    private static readonly object _lock = new object();

    internal static void EnsureCacheReady()
    {
        lock (_lock)
        {
            if (_guidRefMap != null) return;
            var prevMode = EditorSettings.serializationMode;
            bool switched = false;
            if (prevMode != SerializationMode.ForceText)
            {
                EditorSettings.serializationMode = SerializationMode.ForceText;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                switched = true;
            }
            try { BuildGUIDReferenceMap(); }
            finally
            {
                if (switched)
                {
                    EditorSettings.serializationMode = prevMode;
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }
        }
    }

    private static void BuildGUIDReferenceMap()
    {
        _guidRefMap = new Dictionary<string, HashSet<string>>();
        var files = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/"))
            .Where(p => AssetExtensions.Contains(Path.GetExtension(p)?.ToLower()))
            .ToArray();
        var regex = new Regex(@"[a-fA-F0-9]{32}", RegexOptions.Compiled);
        for (int i = 0; i < files.Length; i++)
        {
            string path = files[i];
            if (Directory.Exists(path)) continue;
            if (i % 100 == 0)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Building Reference Cache", Path.GetFileName(path), (float)i / files.Length))
                {
                    EditorUtility.ClearProgressBar();
                    _guidRefMap = null;
                    return;
                }
            }
            try
            {
                string content = File.ReadAllText(path);
                foreach (Match m in regex.Matches(content))
                {
                    string g = m.Value;
                    if (!_guidRefMap.ContainsKey(g)) _guidRefMap[g] = new HashSet<string>();
                    _guidRefMap[g].Add(path);
                }
            }
            catch { }
        }
        EditorUtility.ClearProgressBar();
    }

    public static void InvalidateCache()
    {
        if (_isProcessing) return;
        lock (_lock) { _guidRefMap = null; }
    }

    internal static void ReplaceGUIDReferences(List<string> oldGuids, string newGuid)
    {
        EnsureCacheReady();
        if (_guidRefMap == null) { Debug.LogError("Failed to build the reference cache."); return; }

        var filesToModify = new HashSet<string>();
        lock (_lock)
        {
            foreach (var g in oldGuids)
                if (_guidRefMap.TryGetValue(g, out var fset))
                    foreach (var f in fset) filesToModify.Add(f);
        }

        var prevMode = EditorSettings.serializationMode;
        bool switched = false;
        if (prevMode != SerializationMode.ForceText)
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            switched = true;
        }

        try
        {
            int total = filesToModify.Count;
            int index = 0;
            foreach (var fp in filesToModify)
            {
                if (!File.Exists(fp)) continue;
                string content = File.ReadAllText(fp);
                bool changed = false;
                foreach (var g in oldGuids)
                {
                    if (content.Contains(g))
                    {
                        content = content.Replace(g, newGuid);
                        changed = true;
                    }
                }
                if (changed)
                {
                    File.WriteAllText(fp, content);
                    AssetDatabase.ImportAsset(fp);
                    lock (_lock)
                    {
                        foreach (var g in oldGuids)
                            if (_guidRefMap.TryGetValue(g, out var refs)) refs.Remove(fp);
                        if (!_guidRefMap.TryGetValue(newGuid, out var newRefs))
                        {
                            newRefs = new HashSet<string>();
                            _guidRefMap[newGuid] = newRefs;
                        }
                        newRefs.Add(fp);
                    }
                }
                if (++index % 20 == 0)
                    EditorUtility.DisplayProgressBar("Replacing References", Path.GetFileName(fp), (float)index / total);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (switched)
            {
                EditorSettings.serializationMode = prevMode;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
    }
}

// 全局监听
internal class DuplicateAssetCacheInvalidator : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        AssetReferenceTool.InvalidateCache();
    }
}