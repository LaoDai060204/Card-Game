using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using MDPro3.Utility;

namespace MDPro3
{
    public static class InterString
    {
        public const string CONFIG_LINE_BREAK = "@n";
        public const string SYSTEM_LINE_BREAK = Program.STRING_LINE_BREAK;
        private const string SEPARATOR = "->";
        private const string STRING_EMPTY = "@ui";
        private const string PATH_CONF_FILE = "/translation.conf";

        private static readonly Dictionary<string, string> translations = new();
        private static readonly Dictionary<string, string> englishTranslations = new();
        private static Dictionary<string, string> legacyKeys = new();
        private static Dictionary<string, string> translationsForRender = new();
        private static Dictionary<string, string> translationsForPrerelease = new();

        private static string path;
        private static string pathForRender;
        private static string pathForPrerelease;

        public static void Initialize()
        {
            translations.Clear();
            englishTranslations.Clear();
            translationsForRender = new();
            translationsForPrerelease = new();
            legacyKeys = new();
            string legacyPath = Path.Combine(Program.PATH_LOCALES, "legacy-ui-keys.json");
            try
            {
                legacyKeys = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(legacyPath))
                    ?? new Dictionary<string, string>();
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException)
            {
                UnityEngine.Debug.LogWarning("Legacy UI key compatibility data could not be loaded: " + error.Message);
            }
            ReadLanguage(Language.English, englishTranslations);
            path = ReadLanguage(Language.GetConfig(), translations);
            if (Language.GetCardConfig() == Language.GetConfig())
            {
                pathForRender = path;
                translationsForRender = translations;
            }
            else pathForRender = ReadLanguage(Language.GetCardConfig(), translationsForRender);
            if (Language.GetPrereleaseConfig() == Language.GetConfig())
            {
                pathForPrerelease = path;
                translationsForPrerelease = translations;
            }
            else if (Language.GetPrereleaseConfig() == Language.GetCardConfig())
            {
                pathForPrerelease = pathForRender;
                translationsForPrerelease = translationsForRender;
            }
            else pathForPrerelease = ReadLanguage(Language.GetPrereleaseConfig(), translationsForPrerelease);
        }

        private static string CanonicalKey(string key) => legacyKeys.TryGetValue(key, out string canonical) ? canonical : key;

        private static string ReadLanguage(string language, Dictionary<string, string> target)
        {
            string file = Program.PATH_LOCALES + language + PATH_CONF_FILE;
            if (File.Exists(file)) InitializeContent(File.ReadAllText(file), target);
            return file;
        }

        private static void InitializeContent(string text, Dictionary<string, string> target)
        {
            foreach (string line in text.Replace("\r", string.Empty).Split('\n'))
            {
                int separator = line.IndexOf(SEPARATOR, StringComparison.Ordinal);
                if (separator < 0 || line.IndexOf(SEPARATOR, separator + SEPARATOR.Length, StringComparison.Ordinal) >= 0) continue;
                string key = CanonicalKey(line.Substring(0, separator));
                if (!target.ContainsKey(key)) target.Add(key, line.Substring(separator + SEPARATOR.Length));
            }
        }

        public static string Get(string original, int type = 0)
        {
            if (string.IsNullOrEmpty(original)) return original ?? string.Empty;
            string key = CanonicalKey(original);
            var target = GetTranslations(type);
            if (target.TryGetValue(key, out string translated)) return Format(translated);
            if (!englishTranslations.TryGetValue(key, out translated)) translated = key;
            UnityEngine.Debug.Log($"Undefined translation {target.Count}: {key}");
            string savePath = GetSavePath(type);
            if (!string.IsNullOrEmpty(savePath))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                    File.AppendAllText(savePath, key + SEPARATOR + translated + Program.STRING_LINE_BREAK);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    Program.noAccess = true;
                }
            }
            target[key] = translated;
            return Format(translated);
        }

        private static string Format(string text) => text.Replace(CONFIG_LINE_BREAK, Program.STRING_LINE_BREAK).Replace(STRING_EMPTY, string.Empty);

        public static string Get(string original, string replace, int type = 0)
        {
            return Get(original, type).Replace("[?]", replace);
        }

        public static string GetOriginal(string value)
        {
            var returnValue = value;
            foreach (var translation in translations)
            {
                if (translation.Value == value)
                {
                    returnValue = translation.Key;
                    break;
                }
            }
            return returnValue;
        }

        private static Dictionary<string, string> GetTranslations(int type)
        {
            return type switch
            {
                1 => translationsForRender,
                2 => translationsForPrerelease,
                _ => translations
            };
        }

        private static string GetSavePath(int type)
        {
            return type switch
            {
                1 => pathForRender,
                2 => pathForPrerelease,
                _ => path
            };
        }
    }
}
