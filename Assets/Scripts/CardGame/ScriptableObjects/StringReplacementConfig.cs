// StringReplacementConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StringReplacementConfig", menuName = "Scriptable Objects/String Replacement Configuration")]
public class StringReplacementConfig : ScriptableObject
{
    [Serializable]
    public class ReplacementRule
    {
        public string findString;    // String to find
        public string replaceString; // Replacement string
        [Tooltip("Match case")]
        public bool caseSensitive = true;
    }

    public List<ReplacementRule> replacementRules = new List<ReplacementRule>();

    // Optional utility methods
    public bool HasRules()
    {
        return replacementRules != null && replacementRules.Count > 0;
    }
}