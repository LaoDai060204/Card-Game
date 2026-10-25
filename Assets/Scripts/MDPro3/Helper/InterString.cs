using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using MDPro3.Utility;
using System.Diagnostics;

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
        private static Dictionary<string, string> translationsForRender = new();
        private static Dictionary<string, string> translationsForPrerelease = new();

        private static string path;
        private static string pathForRender;
        private static string pathForPrerelease;

        public static void Initialize()
        {
            translations.Clear();
            translationsForRender.Clear();
            translationsForPrerelease.Clear();
            englishTranslations.Clear();

            var englishPath = Program.PATH_LOCALES + Language.English + PATH_CONF_FILE;
            if (File.Exists(englishPath))
                InitializeContent(File.ReadAllText(englishPath), englishTranslations);

            path = Program.PATH_LOCALES + Language.GetConfig() + PATH_CONF_FILE;
            if (!File.Exists(path))
                File.Create(path).Close();
            InitializeContent(File.ReadAllText(path), 0);
            // Keep the new locale selectable while another interface language
            // is active, without changing the existing translation files.
            translations[Language.Vietnamese] = "Tiếng Việt";

            if (Language.GetCardConfig() == Language.GetConfig())
            {
                pathForRender = path;
                translationsForRender = translations;
            }
            else
            {
                pathForRender = Program.PATH_LOCALES + Language.GetCardConfig() + PATH_CONF_FILE;
                translationsForRender = new();
                if (!File.Exists(pathForRender))
                    File.Create(pathForRender).Close();
                InitializeContent(File.ReadAllText(pathForRender), 1);
            }

            if (Language.GetPrereleaseConfig() == Language.GetConfig())
            {
                pathForPrerelease = path;
                translationsForPrerelease = translations;
            }
            else if(Language.GetPrereleaseConfig() == Language.GetCardConfig())
            {
                pathForPrerelease = pathForRender;
                translationsForPrerelease = translationsForRender;
            }
            else
            {
                pathForPrerelease = Program.PATH_LOCALES + Language.GetPrereleaseConfig() + PATH_CONF_FILE;
                translationsForPrerelease = new();
                if (!File.Exists(pathForPrerelease))
                    File.Create(pathForPrerelease).Close();
                InitializeContent(File.ReadAllText(pathForPrerelease), 2);
            }
        }

        private static void InitializeContent(string text, int type)
        {
            InitializeContent(text, GetTranslations(type));
        }

        private static void InitializeContent(string text, Dictionary<string, string> dic)
        {
            var lines = text.Replace("\r", string.Empty).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var mats = Regex.Split(lines[i], SEPARATOR);
                if (mats.Length == 2)
                    if (!dic.ContainsKey(mats[0]))
                        dic.Add(mats[0], mats[1]);
            }
        }

        public static string Get(string original, int type = 0)
        {
            var returnValue = original;
            var targetTranslations = GetTranslations(type);
            if (targetTranslations.TryGetValue(original, out returnValue))
                return returnValue.Replace(CONFIG_LINE_BREAK, Program.STRING_LINE_BREAK).Replace(STRING_EMPTY, string.Empty);

            returnValue = original;
            if (Language.GetConfig() == Language.Vietnamese
                && englishTranslations.TryGetValue(original, out var english))
                returnValue = english;

            if (original != string.Empty)
            {
                UnityEngine.Debug.Log($"Undefined translation {targetTranslations.Count}: {original}");

                try
                {
                    var savePath = GetSavePath(type);
                    if (!string.IsNullOrEmpty(savePath))
                        File.AppendAllText(savePath, original + SEPARATOR + returnValue + Program.STRING_LINE_BREAK);
                }
                catch
                {
                    Program.noAccess = true;
                }

                targetTranslations.Add(original, returnValue);
                return returnValue.Replace(CONFIG_LINE_BREAK, Program.STRING_LINE_BREAK).Replace(STRING_EMPTY, string.Empty);
            }
            return original;
        }

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
