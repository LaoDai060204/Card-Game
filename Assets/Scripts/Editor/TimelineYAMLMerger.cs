using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;

public class TimelineYAMLMerger
{
    private class FileIDMapper
    {
        private Dictionary<string, Dictionary<long, long>> _fileToOldNewIDMap = new Dictionary<string, Dictionary<long, long>>();
        private long _nextAvailableID = 1140000000000000000; // Use a starting value consistent with Unity IDs.

        public long GetNewID(string assetPath, long oldID)
        {
            // Get or create the ID map for this asset path.
            if (!_fileToOldNewIDMap.TryGetValue(assetPath, out var idMap))
            {
                idMap = new Dictionary<long, long>();
                _fileToOldNewIDMap[assetPath] = idMap;
            }

            // Get or generate the replacement for this old ID.
            if (!idMap.TryGetValue(oldID, out var newID))
            {
                newID = _nextAvailableID++;
                idMap[oldID] = newID;
            }
            return newID;
        }

        // Check whether an ID in this asset was already processed.
        public bool HasMappingFor(string assetPath, long oldID)
        {
            return _fileToOldNewIDMap.TryGetValue(assetPath, out var idMap) && idMap.ContainsKey(oldID);
        }

        // Get all processed asset paths.
        public IEnumerable<string> GetAllProcessedAssetPaths() => _fileToOldNewIDMap.Keys;

        // Find the new ID corresponding to an old ID.
        public long GetNewIDFromOld(string assetPath, long oldID)
        {
            if (_fileToOldNewIDMap.TryGetValue(assetPath, out var idMap) && idMap.TryGetValue(oldID, out var newID))
            {
                return newID;
            }
            return oldID; // Return the original value if no mapping exists.
        }
    }

    [MenuItem("Assets/Merge Timeline and All Dependencies (YAML)", false, 104)]
    private static void MergeTimelineAndDependencies()
    {
        string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);

        // Get the original filename without its extension.
        string originalFileName = Path.GetFileNameWithoutExtension(assetPath);

        // Get all dependencies, including indirect and repeated ones.
        string[] allDependencies = AssetDatabase.GetDependencies(assetPath, true);

        // Initialize the ID mapper.
        FileIDMapper idMapper = new FileIDMapper();

        // Read the main file's YAML header.
        string mainFileHeader = ExtractYamlHeader(assetPath);

        // Store all YAML content to merge.
        List<string> allYamlBlocks = new List<string>();
        // Track processed assets to avoid duplicates, such as shared materials.
        HashSet<string> processedAssets = new HashSet<string>();

        // Record the main asset's fileID for updating m_Name later.
        long mainAssetNewFileID = -1;

        // Phase 1: Collect fileID mappings for all assets.
        CollectFileIDMappings(assetPath, idMapper, processedAssets);
        foreach (var dependencyPath in allDependencies)
        {
            if (dependencyPath == assetPath || dependencyPath.EndsWith(".cs") || processedAssets.Contains(dependencyPath))
                continue;
            CollectFileIDMappings(dependencyPath, idMapper, processedAssets);
        }

        processedAssets.Clear(); // Clear the processed set for phase 2.

        // Phase 2: Process all asset content.
        ProcessAsset(assetPath, idMapper, allYamlBlocks, processedAssets, true, originalFileName, ref mainAssetNewFileID);
        foreach (var dependencyPath in allDependencies)
        {
            if (dependencyPath == assetPath || dependencyPath.EndsWith(".cs") || processedAssets.Contains(dependencyPath))
                continue;
            ProcessAsset(dependencyPath, idMapper, allYamlBlocks, processedAssets, false, originalFileName, ref mainAssetNewFileID);
        }

        // 3. Combine the YAML blocks and prepend the main file's header.
        string combinedYaml = mainFileHeader + string.Join("\n", allYamlBlocks);

        // 4. Write the new file.
        string originalDirectory = Path.GetDirectoryName(assetPath);
        string originalExtension = Path.GetExtension(assetPath);
        string newFileName = $"{originalFileName}_merged{originalExtension}";
        string newFilePath = Path.Combine(originalDirectory, newFileName);

        // Ensure the filename is unique.
        newFilePath = AssetDatabase.GenerateUniqueAssetPath(newFilePath);

        // Get the new filename without its extension.
        string newFileNameWithoutExt = Path.GetFileNameWithoutExtension(newFilePath);

        // Update the main asset's m_Name to match the new filename.
        if (mainAssetNewFileID != -1)
        {
            combinedYaml = UpdateMainAssetName(combinedYaml, mainAssetNewFileID, newFileNameWithoutExt);
        }

        combinedYaml = combinedYaml.Replace("{fileID: 337831424, guid: 1f79187d04fa6fdd168e19d4bafe7dfa, type: 3}", "{fileID: 11500000, guid: bfda56da833e2384a9677cd3c976a436, type: 3}");

        File.WriteAllText(newFilePath, combinedYaml);

        // 5. Refresh the asset database and select the new file.
        AssetDatabase.Refresh();
        EditorUtility.FocusProjectWindow();
        var newAsset = AssetDatabase.LoadAssetAtPath<Object>(newFilePath);
        Selection.activeObject = newAsset;

        Debug.Log($"Merge complete. New file saved to: {newFilePath}");
    }

    [MenuItem("Assets/Merge Timeline and All Dependencies (YAML)", true)]
    private static bool ValidateReplaceStringsInAsset()
    {
        return Selection.activeObject != null &&
               !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(Selection.activeObject));
    }

    // Phase 1: Collect fileID mappings for all assets.
    private static void CollectFileIDMappings(string assetPath, FileIDMapper idMapper, HashSet<string> processedAssets)
    {
        if (processedAssets.Contains(assetPath)) return;

        string fullPath = Path.GetFullPath(assetPath);
        string[] yamlLines = File.ReadAllLines(fullPath);

        // Match a YAML document header and fileID, for example:--- !u!114 &11400000
        Regex headerRegex = new Regex(@"^--- !u!([0-9]+) &([0-9]+)$");

        foreach (var line in yamlLines)
        {
            // Check for the start of a YAML document.
            if (line.StartsWith("---"))
            {
                // Extract the old fileID and type from the document header.
                Match headerMatch = headerRegex.Match(line);
                if (headerMatch.Success && long.TryParse(headerMatch.Groups[2].Value, out long oldID))
                {
                    // Generate new IDs now and apply them during phase 2.
                    idMapper.GetNewID(assetPath, oldID);
                }
            }
        }

        processedAssets.Add(assetPath);
    }

    // Extract the YAML header before the first --- line.
    private static string ExtractYamlHeader(string assetPath)
    {
        string[] lines = File.ReadAllLines(assetPath);
        StringBuilder header = new StringBuilder();

        foreach (string line in lines)
        {
            if (line.StartsWith("---"))
                break;

            header.AppendLine(line);
        }

        return header.ToString();
    }

    // Update the main asset's m_Name to match the new filename.
    private static string UpdateMainAssetName(string yamlContent, long mainAssetFileID, string newName)
    {
        // Build the pattern for the main asset's fileID line.
        string mainAssetHeaderPattern = $"--- !u!114 &{mainAssetFileID}";

        // Split the YAML content into lines.
        string[] lines = yamlContent.Split('\n');
        bool isInsideMainAsset = false;
        bool nameUpdated = false;

        StringBuilder result = new StringBuilder();

        foreach (string line in lines)
        {
            // Check whether the main asset block begins here.
            if (line.StartsWith(mainAssetHeaderPattern))
            {
                isInsideMainAsset = true;
                result.AppendLine(line);
                continue;
            }

            // Check whether the main asset block ends and the next asset begins.
            if (isInsideMainAsset && line.StartsWith("--- !u!"))
            {
                isInsideMainAsset = false;
                result.AppendLine(line);
                continue;
            }

            // Update an m_Name line found inside the main asset block.
            if (isInsideMainAsset && !nameUpdated && line.TrimStart().StartsWith("m_Name:"))
            {
                // Preserve indentation and replace only the name.
                int indentLength = line.IndexOf("m_Name:");
                string indent = new string(' ', indentLength);
                result.AppendLine($"{indent}m_Name: {newName}");
                nameUpdated = true;
            }
            else
            {
                result.AppendLine(line);
            }
        }

        return result.ToString();
    }

    private static void ProcessAsset(string assetPath, FileIDMapper idMapper, List<string> yamlBlocks, HashSet<string> processedAssets, bool isMainAsset, string originalFileName, ref long mainAssetNewFileID)
    {
        if (processedAssets.Contains(assetPath)) return;

        string fullPath = Path.GetFullPath(assetPath);
        string[] yamlLines = File.ReadAllLines(fullPath);

        List<string> currentBlock = new List<string>();
        bool isInsideAsset = false;
        bool skipHeader = !isMainAsset; // Skip the YAML header for non-main assets.

        // Track the original fileID of the current YAML document.
        long currentOldFileID = -1;
        long currentNewFileID = -1;

        // Match a YAML document header and fileID, for example:--- !u!114 &11400000
        Regex headerRegex = new Regex(@"^--- !u!([0-9]+) &([0-9]+)$");
        // Match a simple reference:{fileID: 12345678}
        Regex simpleRefRegex = new Regex(@"{fileID:\s*([0-9]+)\s*}");
        // Match a reference containing an external GUID:{fileID: 7400000, guid: aa54d0da6b3cc424dbdb7130447a5662, type: 2}
        Regex complexRefRegex = new Regex(@"{fileID:\s*([0-9]+)\s*,\s*guid:\s*([0-9a-fA-F]+)\s*,\s*type:\s*([0-9]+)\s*}");
        // Match m_Script lines.
        Regex mScriptRegex = new Regex(@"m_Script:\s*{fileID:\s*([0-9]+)\s*(,\s*guid:\s*[0-9a-fA-F]+\s*,\s*type:\s*[0-9]+\s*)?}");

        foreach (var line in yamlLines)
        {
            // Skip the YAML header for non-main assets.
            if (skipHeader && !line.StartsWith("---"))
                continue;

            skipHeader = false; // Stop skipping the header after the first --- line.

            // Check for the start of a YAML document.
            if (line.StartsWith("---"))
            {
                // Process and save the current block before starting a new document.
                if (isInsideAsset && currentBlock.Count > 0)
                {
                    string processedBlock = ProcessYamlBlock(currentBlock, assetPath, idMapper, simpleRefRegex, complexRefRegex, mScriptRegex);
                    yamlBlocks.Add(processedBlock);
                    currentBlock.Clear();
                }

                // Start a new YAML document block.
                isInsideAsset = true;

                // Extract the old fileID and type from the document header.
                Match headerMatch = headerRegex.Match(line);
                if (headerMatch.Success && long.TryParse(headerMatch.Groups[2].Value, out long oldID))
                {
                    currentOldFileID = oldID;
                    // Get the mapped replacement ID.
                    currentNewFileID = idMapper.GetNewIDFromOld(assetPath, currentOldFileID);
                    // Preserve the !u! type and replace only the fileID after &.
                    string newLine = $"--- !u!{headerMatch.Groups[1].Value} &{currentNewFileID}";
                    currentBlock.Add(newLine);

                    // Record the new fileID for the main asset.
                    if (isMainAsset && mainAssetNewFileID == -1)
                    {
                        mainAssetNewFileID = currentNewFileID;
                    }
                }
                else
                {
                    // Keep the original line if no fileID matches.
                    currentBlock.Add(line);
                    currentOldFileID = -1; // Invalid or missing fileID
                    currentNewFileID = -1;
                }
            }
            else if (isInsideAsset)
            {
                // Process lines inside the document.
                if (!string.IsNullOrEmpty(line) && currentOldFileID != -1)
                {
                    string processedLine = line;

                    // Leave m_Script lines unchanged.
                    if (mScriptRegex.IsMatch(processedLine))
                    {
                        currentBlock.Add(processedLine);
                        continue;
                    }

                    // 1. Process references to external files.
                    processedLine = complexRefRegex.Replace(processedLine, match =>
                    {
                        if (long.TryParse(match.Groups[1].Value, out long referencedOldID))
                        {
                            string guid = match.Groups[2].Value;

                            // Find the asset path for this GUID.
                            string refAssetPath = AssetDatabase.GUIDToAssetPath(guid);
                            if (!string.IsNullOrEmpty(refAssetPath) && refAssetPath != assetPath)
                            {
                                // Get the replacement ID for this external reference.
                                long referencedNewID = idMapper.GetNewIDFromOld(refAssetPath, referencedOldID);
                                return $"{{fileID: {referencedNewID}}}";
                            }
                            else if (!string.IsNullOrEmpty(refAssetPath) && refAssetPath == assetPath)
                            {
                                // This reference points into the same file.
                                long referencedNewID = idMapper.GetNewIDFromOld(assetPath, referencedOldID);
                                return $"{{fileID: {referencedNewID}}}";
                            }
                        }
                        return match.Value; // Keep the original value if parsing fails or no replacement is needed.
                    });

                    // 2. Process references within the same file.
                    processedLine = simpleRefRegex.Replace(processedLine, match =>
                    {
                        if (long.TryParse(match.Groups[1].Value, out long referencedOldID))
                        {
                            // Skip fileID: 0, which represents a null reference.
                            if (referencedOldID == 0)
                                return match.Value;

                            // Check whether the reference is in the same file.
                            long referencedNewID = idMapper.GetNewIDFromOld(assetPath, referencedOldID);
                            if (referencedNewID != referencedOldID)
                            {
                                return $"{{fileID: {referencedNewID}}}";
                            }
                        }
                        return match.Value; // Keep the original value if parsing fails or no replacement is needed.
                    });

                    currentBlock.Add(processedLine);
                }
                else
                {
                    currentBlock.Add(line);
                }
            }
        }

        // Process the last block at the end of the file.
        if (isInsideAsset && currentBlock.Count > 0)
        {
            string processedBlock = ProcessYamlBlock(currentBlock, assetPath, idMapper, simpleRefRegex, complexRefRegex, mScriptRegex);
            yamlBlocks.Add(processedBlock);
        }

        processedAssets.Add(assetPath);
    }

    private static string ProcessYamlBlock(List<string> yamlBlock, string assetPath, FileIDMapper idMapper, Regex simpleRefRegex, Regex complexRefRegex, Regex mScriptRegex)
    {
        // ProcessAsset now performs most of this work line by line.
        // Normalize formatting and ensure the block ends with a newline.
        return string.Join("\n", yamlBlock);
    }

}