// ScriptableObjectToConfigReplacements.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ScriptReplacementConfig", menuName = "Scriptable Objects/Script Replacement Configuration")]
public class ScriptReplacementConfig : ScriptableObject
{
    [Serializable]
    public class ScriptReplacementPair
    {
        public string scriptName; // Script name
        public string oldGuid; // Old GUID
        public string newGuid; // New GUID
        public long oldFileId; // Old file ID
        public long newFileId; // New file ID
    }

    public List<ScriptReplacementPair> replacementPairs = new List<ScriptReplacementPair>();
}