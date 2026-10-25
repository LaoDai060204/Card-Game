using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CardGame.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CardGame
{
    [Serializable]
    public class SettingData
    {
        public int[] FinalAttackBlueEyes;
        public int[] FinalAttackDarkM;
        public int[] FinalAttackRedEyes;
        public int[] FinalAttackObelisk;
        public int[] FinalAttackRa;
        public int[] FinalAttackSlifer;

        public string PrereleasePackUrl;
        public string PrereleasePackVersionUrl;
        public string PrereleasePackUrl_DE;
        public string PrereleasePackVersionUrl_DE;
        public string PrereleasePackUrl_EN;
        public string PrereleasePackVersionUrl_EN;
        public string PrereleasePackUrl_ES;
        public string PrereleasePackVersionUrl_ES;
        public string PrereleasePackUrl_FR;
        public string PrereleasePackVersionUrl_FR;
        public string PrereleasePackUrl_IT;
        public string PrereleasePackVersionUrl_IT;
        public string PrereleasePackUrl_JP;
        public string PrereleasePackVersionUrl_JP;
        public string PrereleasePackUrl_KR;
        public string PrereleasePackVersionUrl_KR;
        public string PrereleasePackUrl_PT;
        public string PrereleasePackVersionUrl_PT;
        public string PrereleasePackUrl_TW;
        public string PrereleasePackVersionUrl_TW;

        // Keep this legacy JSON field name for existing Settings.json files.
        public string MDPro3VersionUrl;
        public bool CardRenderPassword;
        public int[] SavedCardSize;
        public string SavedCardFormat;
        public bool BatchMove;
        public string DiySymbol;

        public SettingData()
        {
            FinalAttackBlueEyes = new int[]
            {
                89631139,   // Blue-Eyes White Dragon
                53347303,   // Blue-Eyes Shining Dragon
                22804410,   // Deep-Eyes White Dragon
                38517737,   // Blue-Eyes Alternative White Dragon
                30576089,   // Blue-Eyes Jet Dragon
                9433350,    // Malefic Blue-Eyes White Dragon
                53183600,   // Blue-Eyes Toon Dragon
                23995346,   // Blue-Eyes Ultimate Dragon
                43228023,   // Blue-Eyes Alternative Ultimate Dragon
                56532353,   // Neo Blue-Eyes Ultimate Dragon
                2129638,    // Blue-Eyes Twin Burst Dragon
                11443677    // Blue-Eyes Tyrant Dragon
            };

            FinalAttackDarkM = new int[]
            {
                46986414,   // Dark Magician
                92377303,   // Dark Sage
                342673,       // Dark Magician the Magician of Black Magic
                21296502,   // Toon Dark Magician
                29436665,   // Dark Eradicator Warlock
                35191415,   // Magician of Dark Illusion
                38033121,   // Dark Magician Girl
                90960358,   // Toon Dark Magician Girl
                50237654    // The Dark Magicians
            };

            FinalAttackRedEyes = new int[]
            {
                74677422,   // Red-Eyes Black Dragon
                96561011,   // Red-Eyes Darkness Dragon
                64335804,   // Red-Eyes Black Metal Dragon
                18491580,   // Red-Eyes Alternative Black Dragon
                55343236,   // Malefic Red-Eyes Black Dragon
                6556909,     // Red-Eyes Soul
            };

            FinalAttackObelisk = new int[]
            {
                10000000   // Obelisk the Tormentor
            };

            FinalAttackRa = new int[]
            {
                10000010,   // The Winged Dragon of Ra
                10000080,   // The Winged Dragon of Ra - Sphere Mode
                10000090,   // The Winged Dragon of Ra - Immortal Phoenix
            };

            FinalAttackSlifer = new int[]
            {
                10000020   // Slifer the Sky Dragon
            };

            PrereleasePackUrl = "https://cdn02.moecube.com:444/ygopro-super-pre/archive/ygopro-super-pre.ypk";
            PrereleasePackVersionUrl = "https://cdn02.moecube.com:444/ygopro-super-pre/data/version.txt";

            PrereleasePackUrl_DE = "https://github.com/ElderLich/TransSuperpre/raw/main/DE/ygopro-super-pre.ypk";
            PrereleasePackVersionUrl_DE = "https://github.com/ElderLich/TransSuperpre/raw/main/DE/version.txt";
            PrereleasePackUrl_EN = "https://github.com/ElderLich/TransSuperpre/raw/main/EN/ygopro-super-pre.ypk";
            PrereleasePackVersionUrl_EN = "https://github.com/ElderLich/TransSuperpre/raw/main/EN/version.txt";
            PrereleasePackUrl_ES = "https://github.com/ElderLich/TransSuperpre/raw/main/ES/ygopro-super-pre.ypk";
            PrereleasePackVersionUrl_ES = "https://github.com/ElderLich/TransSuperpre/raw/main/ES/version.txt";
            PrereleasePackUrl_FR = "https://github.com/ElderLich/TransSuperpre/raw/main/FR/ygopro-super-pre.ypk";
            PrereleasePackVersionUrl_FR = "https://github.com/ElderLich/TransSuperpre/raw/main/FR/version.txt";
            PrereleasePackUrl_IT = "https://github.com/ElderLich/TransSuperpre/raw/main/IT/ygopro-super-pre.ypk";
            PrereleasePackVersionUrl_IT = "https://github.com/ElderLich/TransSuperpre/raw/main/IT/version.txt";
            PrereleasePackUrl_JP = "https://github.com/ElderLich/TransSuperpre/raw/main/JP/ygopro-super-pre.ypk";
            PrereleasePackVersionUrl_JP = "https://github.com/ElderLich/TransSuperpre/raw/main/JP/version.txt";
            PrereleasePackUrl_KR = "https://github.com/ElderLich/TransSuperpre/raw/main/KR/ygopro-super-pre.ypk";
            PrereleasePackVersionUrl_KR = "https://github.com/ElderLich/TransSuperpre/raw/main/KR/version.txt";
            PrereleasePackUrl_PT = "https://github.com/ElderLich/TransSuperpre/raw/main/PT/ygopro-super-pre.ypk";
            PrereleasePackVersionUrl_PT = "https://github.com/ElderLich/TransSuperpre/raw/main/PT/version.txt";
            PrereleasePackUrl_TW = "https://github.com/ElderLich/TransSuperpre/raw/main/ZH-TW/ygopro-super-pre.ypk";
            PrereleasePackVersionUrl_TW = "https://github.com/ElderLich/TransSuperpre/raw/main/ZH-TW/version.txt";

            MDPro3VersionUrl = "https://cdn02.moecube.com:444/mdpro3-data/Version.txt";
            CardRenderPassword = true;
            SavedCardSize = new int[] { 704, 1024 };
            SavedCardFormat = Program.EXPANSION_JPG;
            BatchMove = true;
            DiySymbol = "DIY by";
        }
    }

    public static class Settings
    {
        private const string PATH_JSON = Program.PATH_DATA + "Settings.json";
        private const string MDPRO3_VERSION_URL_OLD = "https://code.moenext.com/sherry_chaos/MDPro3/-/raw/master/Version.txt";
        private const string MDPRO3_VERSION_URL_FALSE = "https://code.moenext.com/sherry_chaos/MDPro3/-/raw/master/version.txt";
        private const string MDPRO3_VERSION_URL_DEFAULT = "https://cdn02.moecube.com:444/mdpro3-data/Version.txt";

        private static SettingData _data;
        public static SettingData Data
        {
            get
            {
                if(_data == null)
                    Initialize();
                return _data;
            }
        }

        public static void Initialize()
        {
            if (!File.Exists(PATH_JSON))
            {
                _data = new SettingData();
                SaveSettings(_data);
                return;
            }

            var json = File.ReadAllText(PATH_JSON);
            try
            {
                _data = EnsureDefaultValues(json);
            }
            catch(JsonReaderException ex)
            {
                MessageManager.Cast("Failed to parse Settings.json: " + ex.Message);
                _data = new SettingData();
            }
        }

        public static string GetPrereleasePackUrl()
        {
            return Language.GetDataConfig() switch
            {
                Language.German => Data.PrereleasePackUrl_DE,
                Language.English => Data.PrereleasePackUrl_EN,
                Language.Spanish => Data.PrereleasePackUrl_ES,
                Language.French => Data.PrereleasePackUrl_FR,
                Language.Italian => Data.PrereleasePackUrl_IT,
                Language.Japanese => Data.PrereleasePackUrl_JP,
                Language.Korean => Data.PrereleasePackUrl_KR,
                Language.Portuguese => Data.PrereleasePackUrl_PT,
                Language.TraditionalChinese => Data.PrereleasePackUrl_TW,
                _ => Data.PrereleasePackUrl
            };
        }

        public static string GetPrereleasePackVersionUrl()
        {
            return Language.GetDataConfig() switch
            {
                Language.German => Data.PrereleasePackVersionUrl_DE,
                Language.English => Data.PrereleasePackVersionUrl_EN,
                Language.Spanish => Data.PrereleasePackVersionUrl_ES,
                Language.French => Data.PrereleasePackVersionUrl_FR,
                Language.Italian => Data.PrereleasePackVersionUrl_IT,
                Language.Japanese => Data.PrereleasePackVersionUrl_JP,
                Language.Korean => Data.PrereleasePackVersionUrl_KR,
                Language.Portuguese => Data.PrereleasePackVersionUrl_PT,
                Language.TraditionalChinese => Data.PrereleasePackVersionUrl_TW,
                _ => Data.PrereleasePackVersionUrl
            };
        }

        private static void SaveSettings(SettingData data)
        {
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(PATH_JSON, json);
        }

        private static SettingData EnsureDefaultValues(string json)
        {
            var data = JsonConvert.DeserializeObject<SettingData>(json);

            bool needOverwrite = false;
            var jObject = JObject.Parse(json);
            var missingFields = GetMissingFields<SettingData>(jObject);
            if (missingFields.Count > 0)
                needOverwrite = true;

            if(data.MDPro3VersionUrl == MDPRO3_VERSION_URL_OLD
                || data.MDPro3VersionUrl == MDPRO3_VERSION_URL_FALSE)
            {
                data.MDPro3VersionUrl = MDPRO3_VERSION_URL_DEFAULT;
                needOverwrite = true;
            }

            if (needOverwrite)
                SaveSettings(data);

            return data;
        }

        private static List<string> GetMissingFields<T>(JObject jObject)
        {
            var missingFields = new List<string>();
            var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
                if (!jObject.ContainsKey(field.Name))
                    missingFields.Add(field.Name);
            return missingFields;
        }
    }
}
