using MDPro3.BattleSpirits;
using System;
using System.Collections.Generic;
using System.IO;
using MDPro3.Duel.YGOSharp;
using MDPro3.Net;
using System.Text;
using System.Linq;

namespace MDPro3.Duel.YGOSharp
{
    public class Deck
    {
        public List<int> Main;
        public List<int> Extra;
        public List<int> Side;
        public List<int> Pickup;
        public int Protector;
        public int Case;
        public int Field;
        public int Grave;
        public int Stand;
        public int Mate;

        public const string deckHint = "#Battle Spirits Standard deck";
        public string userId;
        public string deckId;

        public string type = string.Empty;

        public Deck()
        {
            Main = new List<int>();
            Extra = new List<int>();
            Side = new List<int>();
            Pickup = new List<int>() { 0, 0, 0 };
            Protector = 1079001;
            Case = 0;
            Field = 1099001;
            Grave = 0;
            Stand = 0;
            Mate = 0;
        }

        public Deck(string path) : this(File.ReadAllText(path), string.Empty, string.Empty)
        {
            var relative = Path.GetRelativePath(BattleDeckStorage.DeckDirectory, Path.GetFullPath(path));
            type = Path.GetDirectoryName(relative) ?? string.Empty;
        }

        public Deck(string ydk, string deckID = "", string userID = "")
        {
            deckId = deckID;
            userId = userID;

            Main = new List<int>();
            Extra = new List<int>();
            Side = new List<int>();
            Pickup = new List<int>();
            Protector = 1079001;
            Case = 0;
            Field = 1099001;
            Grave = 0;
            Stand = 0;
            Mate = 0;
            string st = ydk.Replace("\r", string.Empty).Replace("\\r", string.Empty).Replace("\\n", "\n");
            string[] lines = st.Split("\n", StringSplitOptions.RemoveEmptyEntries);
            int flag = -1;

            foreach (string line in lines)
            {
                if (line.StartsWith("###") && userId == string.Empty)
                {
                    userId = line.Replace("###", string.Empty);
                    continue;
                }
                if (line.StartsWith("##") && deckId == string.Empty)
                {
                    deckId = line.Replace("##", string.Empty);
                    continue;
                }

                if (line.StartsWith("#main"))
                    flag = 1;
                else if (line.StartsWith( "#extra"))
                    flag = 2;
                else if (line.StartsWith("!side"))
                    flag = 3;
                else if (line.StartsWith("#pickup"))
                    flag = 4;
                else if (line.StartsWith("#protector"))
                    flag = 5;
                else if (line.StartsWith("#case"))
                    flag = 6;
                else if (line.StartsWith("#field"))
                    flag = 7;
                else if (line.StartsWith("#grave"))
                    flag = 8;
                else if (line.StartsWith("#stand"))
                    flag = 9;
                else if (line.StartsWith("#mate"))
                    flag = 10;
                else
                {
                    int code = 0;
                    try
                    {
                        code = int.Parse(line.Replace("#", ""));
                    }
                    catch
                    {
                        continue;
                    }

                    if (code > 100)
                    {
                        // Replace pre-release ids with official ids only for playable deck sections.
                        if (flag >= 1 && flag <= 3)
                            code = BattleCatalog.TryGet(code, out _) ? code : 0;

                        switch (flag)
                        {
                            case 1:
                                Main.Add(code);
                                break;
                            case 2:
                                Extra.Add(code);
                                break;
                            case 3:
                                Side.Add(code);
                                break;
                            case 4:
                                Pickup.Add(code);
                                break;
                            case 5:
                                Protector = code;
                                break;
                            case 6:
                                Case = code;
                                break;
                            case 7:
                                Field = code;
                                break;
                            case 8:
                                Grave = code;
                                break;
                            case 9:
                                Stand = code;
                                break;
                            case 10:
                                Mate = code;
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            Main = Main.Where(id => BattleCatalog.TryGet(id, out _)).ToList();
            Extra.Clear();
            Side.Clear();

            if (Pickup.Count < 3)
                Pickup.AddRange(Enumerable.Repeat(0, 3 - Pickup.Count));
        }

        public Deck(List<int> main, List<int> extra, List<int> side)
        {
            Main = main != null ? new List<int>(main) : new List<int>();
            Extra = new List<int>();
            Side = new List<int>();
            Pickup = new List<int>() { 0, 0, 0 };
            Protector = 1079001;
            Case = 0;
            Field = 1099001;
            Grave = 0;
            Stand = 0;
            Mate = 0;

            // Normalize any pre-release ids that may be in the provided lists.
            Main = Main.Where(id => BattleCatalog.TryGet(id, out _)).ToList();
            Extra.Clear();
            Side.Clear();
        }

        public int Check(Banlist ban, bool ocg, bool tcg)
        {
            var validation = BattleDeckStorage.Validate(this);
            return validation.IsValid ? 0 : 1;
        }

        public int GetCardCount(int code)
        {
            return Main.Count(id => BattleDeckStorage.SameName(code, id));
        }

        public bool Check(Deck deck)
        {
            if (deck.Main.Count != Main.Count || deck.Extra.Count != Extra.Count)
                return false;

            Dictionary<int, int> cards = new Dictionary<int, int>();
            Dictionary<int, int> ncards = new Dictionary<int, int>();
            List<int>[] stacks = { Main, Extra, Side };
            foreach (IList<int> stack in stacks)
            {
                foreach (int id in stack)
                {
                    if (!cards.ContainsKey(id))
                        cards.Add(id, 1);
                    else
                        cards[id]++;
                }
            }
            stacks = new[] { deck.Main, deck.Extra, deck.Side };
            foreach (var stack in stacks)
            {
                foreach (int id in stack)
                {
                    if (!ncards.ContainsKey(id))
                        ncards.Add(id, 1);
                    else
                        ncards[id]++;
                }
            }
            foreach (var pair in cards)
            {
                if (!ncards.ContainsKey(pair.Key))
                    return false;
                if (ncards[pair.Key] != pair.Value)
                    return false;
            }
            return true;
        }

        private static void AddToCards(Dictionary<int, int> cards, Card card)
        {
            int id = card.Id;
            if (card.Alias != 0)
                id = card.Alias;
            if (cards.ContainsKey(id))
                cards[id]++;
            else
                cards.Add(id, 1);
        }

        /// <summary>
        /// Save directly; deckid is added automatically.
        /// </summary>
        /// <param name="deckName"></param>
        /// <param name="saveTime"></param>
        /// <param name="upload"></param>
        /// <returns></returns>
        public bool Save(string deckName, DateTime saveTime, bool upload = true, bool showHint = true)
        {
            try
            {
                BattleDeckStorage.EnsureInitialized();
                deckName = NormalizeDeckFileName(deckName);
                if (!IsValidDeckFileName(deckName)) return false;
                var relativeName = string.IsNullOrEmpty(type) ? deckName : type + "/" + deckName;
                var path = BattleDeckStorage.ResolveDeckPath(relativeName);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, GetYDK(), Encoding.UTF8);
                File.SetLastWriteTimeUtc(path, saveTime);
                return true;
            }
            catch { return false; }
        }

        public static string NormalizeDeckFileName(string deckName)
        {
            deckName = deckName?.Trim() ?? string.Empty;
            if (deckName.EndsWith(Program.EXPANSION_YDK, StringComparison.OrdinalIgnoreCase))
                deckName = deckName[..^Program.EXPANSION_YDK.Length];
            return deckName;
        }

        public static bool IsValidDeckFileName(string deckName)
        {
            if (string.IsNullOrWhiteSpace(deckName))
                return false;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (deckName.EndsWith(" ", StringComparison.Ordinal) || deckName.EndsWith(".", StringComparison.Ordinal))
                return false;
#endif
            return deckName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        public string GetYDK()
        {
            var value = new StringBuilder(deckHint + "\n#main");
            foreach (var id in Main)
                if (BattleCatalog.TryGet(id, out _)) value.Append('\n').Append(id);
            value.Append("\n#extra\n!side\n#pickup");
            for (int i = 0; i < 3; i++)
                value.Append("\n#").Append(i < Pickup.Count && BattleCatalog.TryGet(Pickup[i], out _) ? Pickup[i] : 0);
            value.Append("\n#protector\n#").Append(Protector)
                .Append("\n#case\n#").Append(Case)
                .Append("\n#field\n#").Append(Field)
                .Append("\n#grave\n#").Append(Grave)
                .Append("\n#stand\n#").Append(Stand)
                .Append("\n#mate\n#").Append(Mate);
            return value.ToString();
        }
    }
}
