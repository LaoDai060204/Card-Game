
using CardGame.Duel.YGOSharp;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Collections.Generic;

namespace CardGame
{
    public static class SpecialCards
    {
        [Serializable]
        private class SpecialCardLists
        {
            public int[] FinalAttackBlueEyes;
            public int[] FinalAttackDarkM;
            public int[] FinalAttackRedEyes;
            public int[] FinalAttackObelisk;
            public int[] FinalAttackRa;
            public int[] FinalAttackSlifer;

            public int[] FrameObelisk;
            public int[] FrameRa;
            public int[] FrameSlifer;
            public int[] FrameRaviel;
            public int[] FrameUria;
            public int[] FrameHamon;

            public int[] LevelZeroMonsters;
            public Dictionary<int, int[]> SameCutins;

            public SpecialCardLists()
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

                FrameObelisk = new int[]
                {
                    10000000   // Obelisk the Tormentor
                };

                FrameRa = new int[]
                {
                    10000010   // The Winged Dragon of Ra
                };

                FrameSlifer = new int[]
                {
                    10000020   // Slifer the Sky Dragon
                };

                FrameRaviel = new int[]
                {
                    69890967   // Raviel, Lord of Phantasms
                };

                FrameUria = new int[]
                {
                    6007213    // Uria, Lord of Searing Flames
                };

                FrameHamon = new int[]
                {
                    32491822   // Hamon, Lord of Striking Thunder
                };

                LevelZeroMonsters = new int[]
                {
                    1686814,
                    90884403,
                    26973555,
                    43490025,
                    65305468,
                    52653092,
                    41522092,
                };

                SameCutins = new Dictionary<int, int[]>
                {
                    { 89631139, new int[] { 89631155 } },   // Blue-Eyes White Dragon (variant 1)
                    { 89631141, new int[] { 89631142, 89631148, 89631151, 89631154 } },   // Blue-Eyes White Dragon (variant 2)
                    { 89943723, new int[] { 89943725 } },   // Elemental HERO Neos
                    { 46986417, new int[] { 46986424, 46986426, 46986430 } },   // Dark Magician
                    { 74677424, new int[] { 74677425 } },   // Red-Eyes Black Dragon
                    { 44508094, new int[] { 44508096 } },   // Stardust Dragon
                    { 84013237, new int[] { 84013240 } },   // Number 39: Utopia
                    { 16178681, new int[] { 16178684 } },   // Odd-Eyes Pendulum Dragon
                    { 5043010, new int[] { 5043013 } },   // Firewall Dragon
                    { 35405755, new int[] { 35405756 } },   // Kurikara Divincarnate
                    { 32828466, new int[] { 32828467 } },   // Wake Up Your Elemental HERO
                    { 37818794, new int[] { 37818795 } },   // Red-Eyes Dark Dragoon
                    { 44146295, new int[] { 44146296 } },   // Mirrorjade the Iceblade Dragon
                    { 35952884, new int[] { 35952885 } },   // Shooting Quasar Dragon
                    { 6218704, new int[] { 6218705 } },   // Odd-Eyes Arcray Dragon
                    { 10000000, new int[] { 10000004 } },   // Obelisk the Tormentor
                    { 10000010, new int[] { 10000014 } },   // The Winged Dragon of Ra
                    { 10000020, new int[] { 10000024 } },   // Slifer the Sky Dragon
                    { 12381100, new int[] { 12381101 } },   // Dragon Master Magia
                    { 13331639, new int[] { 13331640 } },   // Supreme King Z-ARC
                    { 22850702, new int[] { 22850703 } },   // Chaos Angel
                    { 31801517, new int[] { 31801518 } },   // Number 62: Galaxy-Eyes Prime Photon Dragon
                    { 98127546, new int[] { 98127547 } },   // Underworld Goddess of the Closed World
                    { 98630720, new int[] { 98630721 } },   // Borrelend Dragon
                    { 99585850, new int[] { 99585851 } },   // Red Supernova Dragon
                };
            }

            public bool ApplyDefaults()
            {
                var defaults = new SpecialCardLists();
                bool changed = false;

                changed |= ApplyDefault(ref FinalAttackBlueEyes, defaults.FinalAttackBlueEyes);
                changed |= ApplyDefault(ref FinalAttackDarkM, defaults.FinalAttackDarkM);
                changed |= ApplyDefault(ref FinalAttackRedEyes, defaults.FinalAttackRedEyes);
                changed |= ApplyDefault(ref FinalAttackObelisk, defaults.FinalAttackObelisk);
                changed |= ApplyDefault(ref FinalAttackRa, defaults.FinalAttackRa);
                changed |= ApplyDefault(ref FinalAttackSlifer, defaults.FinalAttackSlifer);
                changed |= ApplyDefault(ref FrameObelisk, defaults.FrameObelisk);
                changed |= ApplyDefault(ref FrameRa, defaults.FrameRa);
                changed |= ApplyDefault(ref FrameSlifer, defaults.FrameSlifer);
                changed |= ApplyDefault(ref FrameRaviel, defaults.FrameRaviel);
                changed |= ApplyDefault(ref FrameUria, defaults.FrameUria);
                changed |= ApplyDefault(ref FrameHamon, defaults.FrameHamon);
                changed |= ApplyDefault(ref LevelZeroMonsters, defaults.LevelZeroMonsters);

                return changed;
            }

            private static bool ApplyDefault(ref int[] target, int[] defaultValue)
            {
                if (target != null || target.Length == 0)
                    return false;
                target = defaultValue;
                return true;
            }
        }

        private const string PATH_JSON = Program.PATH_DATA + "SpecialCards.json";
        private static SpecialCardLists lists;
        private static SpecialCardLists Lists
        {
            get
            {
                if (lists == null)
                    Initialize();
                return lists;
            }
        }

        private static void Initialize()
        {
            if (!File.Exists(PATH_JSON))
            {
                lists = new SpecialCardLists();
                SaveJson(lists);
                return;
            }

            var json = File.ReadAllText(PATH_JSON);
            bool parsedFromFile = false;
            bool shouldWriteBack = false;
            try
            {
                lists = JsonConvert.DeserializeObject<SpecialCardLists>(json);
                parsedFromFile = true;
                if (lists == null)
                {
                    lists = new SpecialCardLists();
                    shouldWriteBack = true;
                }
            }
            catch (JsonReaderException ex)
            {
                MessageManager.Cast("Failed to parse SpecialCards.json: " + ex.Message);
                lists = new SpecialCardLists();
            }
            finally
            {
                lists ??= new SpecialCardLists();
                shouldWriteBack |= lists.ApplyDefaults();
                if (parsedFromFile && shouldWriteBack)
                    SaveJson(lists);
            }
        }

        private static void SaveJson(SpecialCardLists lists)
        {
            var json = JsonConvert.SerializeObject(lists, Formatting.Indented);
            File.WriteAllText(PATH_JSON, json);
        }

        public static bool IsLevelZeroMonster(this Card data)
        {
            var code = data.GetOriginalID();
            return Contains(Lists.LevelZeroMonsters, code);
        }

        public static int GetOriginalCutinCode(int code)
        {
            foreach (var pair in Lists.SameCutins)
            {
                if (Contains(pair.Value, code))
                    return pair.Key;
            }
            return code;
        }

        public static FinalAttackType GetFinalAttackType(int code)
        {
            var data = CardsManager.Get(code);
            var id = data.GetOriginalID();

            if (Contains(Lists.FinalAttackBlueEyes, id))
                return FinalAttackType.BlueEyes;
            if (Contains(Lists.FinalAttackDarkM, id))
                return FinalAttackType.DarkM;
            if (Contains(Lists.FinalAttackRedEyes, id))
                return FinalAttackType.RedEyes;
            if (Contains(Lists.FinalAttackObelisk, id))
                return FinalAttackType.Obelisk;
            if (Contains(Lists.FinalAttackRa, id))
                return FinalAttackType.Ra;
            if (Contains(Lists.FinalAttackSlifer, id))
                return FinalAttackType.Slifer;

            return FinalAttackType.Normal;
        }

        public static CardFrameType GetFrameType(Card data)
        {
            var id = data.Id;

            if (Contains(Lists.FrameObelisk, id))
                return CardFrameType.Obelisk;
            if (Contains(Lists.FrameRa, id))
                return CardFrameType.Ra;
            if (Contains(Lists.FrameSlifer, id))
                return CardFrameType.Slifer;
            if (Contains(Lists.FrameRaviel, id))
                return CardFrameType.Raviel;
            if (Contains(Lists.FrameUria, id))
                return CardFrameType.Uria;
            if (Contains(Lists.FrameHamon, id))
                return CardFrameType.Hamon;

            return CardFrameType.Normal;
        }

        public enum FinalAttackType
        {
            Normal,
            BlueEyes,
            DarkM,
            RedEyes,
            Obelisk,
            Ra,
            Slifer,
        }

        public enum CardFrameType
        {
            Normal,
            Obelisk,
            Ra,
            Slifer,
            Raviel,
            Uria,
            Hamon,
        }

        private static bool Contains(int[] list, int code)
        {
            return list != null && Array.Exists(list, c => c == code);
        }

    }
}
