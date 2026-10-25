using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using UnityEngine;
using CardGame.Utility;

namespace CardGame
{
    public static class Config
    {
        public const string LABEL_TIMING = "Timing";
        private const string SEPARATOR = "->";
        public const string EMPTY_STRING = "@ui";
        public const string STRING_YES = "1";
        public const string STRING_NO = "0";
        private const string VIETNAMESE_UI_INITIALIZED = "VietnameseUiInitialized";

        public static uint ClientVersion = 0x1362;

        private static readonly List<OneString> translations = new List<OneString>();
        private static string path;

        public static void Initialize(string path)
        {
            Config.path = path;
            if (!File.Exists(path))
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.Create(path).Close();
            }
            var txtString = File.ReadAllText(path);
            var lines = txtString.Replace("\r", "").Split('\n');
            translations.Clear();
            for (var i = 0; i < lines.Length; i++)
            {
                var mats = Regex.Split(lines[i], SEPARATOR);
                if (mats.Length == 2)
                {
                    var s = new OneString
                    {
                        original = mats[0],
                        translated = mats[1]
                    };
                    translations.Add(s);
                }
            }

            // Apply this edition's defaults once, including to an existing
            // upstream config. Later language choices remain untouched.
            if (!GetBool(VIETNAMESE_UI_INITIALIZED, false))
            {
                Language.SetConfig(Language.Vietnamese);
                Language.SetCardConfig(Language.English);
                Language.SetPrereleaseConfig(Language.English);
                SetBool(VIETNAMESE_UI_INITIALIZED, true);
                Save();
            }
        }

        public static bool Have(string original)
        {
            var found = false;
            for (var i = 0; i < translations.Count; i++)
                if (translations[i].original == original)
                {
                    found = true;
                    break;
                }
            return found;
        }

        public static string Get(string original, string defau)
        {
            var return_value = defau;
            var found = false;
            for (var i = 0; i < translations.Count; i++)
                if (translations[i].original == original)
                {
                    return_value = translations[i].translated;
                    found = true;
                    break;
                }

            if (found == false)
                if (path != null)
                {
                    File.AppendAllText(path, original + SEPARATOR + defau + Program.STRING_LINE_BREAK);
                    var s = new OneString
                    {
                        original = original,
                        translated = defau
                    };
                    return_value = defau;
                    translations.Add(s);
                }

            return return_value.Replace(EMPTY_STRING, string.Empty);
        }

        public static void Set(string original, string setted)
        {
            var found = false;
            for (var i = 0; i < translations.Count; i++)
                if (translations[i].original == original)
                {
                    found = true;
                    translations[i].translated = setted;
                }

            if (found == false)
            {
                var s = new OneString();
                s.original = original;
                s.translated = setted;
                translations.Add(s);
            }
        }

        public static float GetFloat(string v, float defau)
        {
            var getted = 0;
            try
            {
                getted = int.Parse(Get(v, (defau * 1000).ToString()));
            }
            catch { }

            return getted / 1000f;
        }

        public static void SetFloat(string v, float f)
        {
            Set(v, ((int)(f * 1000f)).ToString());
        }

        public static bool GetBool(string original, bool value)
        {
            try
            {
                value = Get(original, value ? STRING_YES : STRING_NO) == STRING_YES;
            }
            catch { }

            return value;
        }

        public static void SetBool(string original, bool value)
        {
            Set(original, value ? STRING_YES : STRING_NO);
        }

        public static void Save()
        {
            var all = "";
            for (var i = 0; i < translations.Count; i++)
                all += translations[i].original + SEPARATOR + translations[i].translated + Program.STRING_LINE_BREAK;

            try
            {
                File.WriteAllText(path, all);
            }
            catch (Exception e)
            {
                Program.noAccess = true;
                Debug.Log(e);
            }
        }

        private class OneString
        {
            public string original = "";
            public string translated = "";
        }

        public static float GetUIScale(float maxUIScale = 1.5f)
        {
            var defau = 1000f;
#if UNITY_ANDROID || UNITY_IOS
            defau = 1500f;
#endif
            var scale = float.Parse(Get("UIScale", defau.ToString())) / 1000;
            return scale > maxUIScale ? maxUIScale : scale;
        }

        public static string GetConfigDeckName(bool containType = true)
        {
            var config = Get("DeckInUse", EMPTY_STRING);
            if(config.StartsWith("/") && config.Length > 1)
                config = config[1..];
            if (!containType && config.Contains("/"))
                config = Path.GetFileName(config);
            return config;
        }

        public static void SetConfigDeck(string deckName, bool needCheck = false)
        {
            if (needCheck)
            {
                var type = Program.instance.deckSelector.DeckType;
                var config = type == string.Empty ? deckName : $"{type}/{deckName}";
                Set("DeckInUse", config);
            }
            else
                Set("DeckInUse", deckName);
        }
    }
}
