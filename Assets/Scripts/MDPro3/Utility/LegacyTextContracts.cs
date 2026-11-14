using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace MDPro3.Utility
{
    // These values describe external CDB text and upstream puzzle filenames,
    // not UI labels. English code refers to semantic keys while the adapter
    // retains the exact data grammar required to read the original resources.
    public static class LegacyTextContracts
    {
        private static readonly Lazy<Dictionary<string, string[]>> Values =
            new Lazy<Dictionary<string, string[]>>(Load);

        private static Dictionary<string, string[]> Load()
        {
            string path = Path.Combine(Program.PATH_LOCALES, "legacy-text-contracts.json");
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string[]>>(File.ReadAllText(path))
                    ?? new Dictionary<string, string[]>(StringComparer.Ordinal);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException)
            {
                UnityEngine.Debug.LogWarning("Legacy text compatibility data could not be loaded: " + error.Message);
                return new Dictionary<string, string[]>(StringComparer.Ordinal);
            }
        }

        public static string[] Get(string key) => Values.Value.TryGetValue(key, out var values)
            ? values : Array.Empty<string>();

        public static string First(string key)
        {
            var values = Get(key);
            return values.Length == 0 ? string.Empty : values[0];
        }

        public static bool StartsWith(string key, string text)
        {
            if (text == null) return false;
            foreach (string prefix in Get(key))
                if (!string.IsNullOrEmpty(prefix) && text.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
