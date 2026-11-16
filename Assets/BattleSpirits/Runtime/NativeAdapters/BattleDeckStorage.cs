using System;
using System.IO;
using System.Linq;
using System.Text;
using MDPro3.BattleSpirits.Core;
using MDPro3.Duel.YGOSharp;
using UnityEngine;

namespace MDPro3.BattleSpirits
{
    /// <summary>Device-local BS decks; the previous product's deck directory is never used.</summary>
    public static class BattleDeckStorage
    {
        private static bool initialized;
        public static string DeckDirectory => Path.Combine(Application.persistentDataPath, "BattleSpirits", "Decks") + Path.DirectorySeparatorChar;
        public static string DefaultDeckName
        {
            get
            {
                EnsureInitialized();
                var files = Directory.GetFiles(DeckDirectory, "*.ydk", SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    string relative = Path.GetRelativePath(DeckDirectory, files.OrderBy(path => path, StringComparer.Ordinal).First());
                    return relative.Substring(0, relative.Length - 4);
                }
                return SafeName(BattleCatalog.StarterDecks.FirstOrDefault()?.name ?? "Battle Spirits Deck");
            }
        }

        public static void EnsureInitialized()
        {
            if (initialized) return;
            BattleCatalog.Initialize(Application.streamingAssetsPath, Application.persistentDataPath);
            Directory.CreateDirectory(DeckDirectory);
            // The marker makes deleting or renaming a starter deck permanent.
            var marker = Path.Combine(DeckDirectory, ".starters-initialized");
            if (!File.Exists(marker))
            {
                foreach (var template in BattleCatalog.StarterDecks)
                {
                    var path = ResolveDeckPath(SafeName(template.name));
                    if (File.Exists(path)) continue;
                    var text = new StringBuilder("#Battle Spirits Standard deck\n#main");
                    foreach (var id in template.cards)
                    {
                        if (!BattleCatalog.TryGet(id, out _)) throw new InvalidDataException("Unknown starter card: " + id);
                        text.Append('\n').Append(BattleCatalog.GetNumericId(id));
                    }
                    text.Append("\n#extra\n!side\n#pickup\n#0\n#0\n#0");
                    File.WriteAllText(path, text.ToString(), Encoding.UTF8);
                }
                File.WriteAllText(marker, "Battle Spirits catalog starter decks\n", Encoding.UTF8);
            }
            initialized = true;
        }

        public static string ResolveDeckPath(string deckPathOrName)
        {
            if (string.IsNullOrWhiteSpace(deckPathOrName)) throw new ArgumentException("A Battle Spirits deck name is required.");
            var requested = deckPathOrName.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            if (!requested.EndsWith(".ydk", StringComparison.OrdinalIgnoreCase)) requested += ".ydk";
            var root = Path.GetFullPath(DeckDirectory);
            var path = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(root, requested));
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!path.StartsWith(root, comparison)) throw new ArgumentException("Battle Spirits decks must stay in the Battle Spirits deck directory.");
            return path;
        }

        public static DeckDefinition LoadDefinition(string deckPathOrName)
        {
            EnsureInitialized();
            var path = ResolveDeckPath(deckPathOrName);
            // Preserve unknown entries so validation reports corruption instead of silently repairing it.
            bool main = false;
            var ids = new System.Collections.Generic.List<string>();
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim().TrimStart('\uFEFF');
                if (line == "#main") { main = true; continue; }
                if (line == "#extra" || line == "!side" || line == "#pickup") { main = false; continue; }
                if (!main || line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                ids.Add(int.TryParse(line, out int numeric) ? BattleCatalog.GetCardId(numeric) ?? "Unknown:" + line : line);
            }
            return new DeckDefinition { name = Path.GetFileNameWithoutExtension(path), format = "Standard", cards = ids.ToArray() };
        }

        public static DeckDefinition ToDefinition(Deck deck, string name = "Battle Spirits Deck") => new DeckDefinition
        {
            name = name,
            format = "Standard",
            cards = deck.Main.Select(id => BattleCatalog.GetCardId(id) ?? "Unknown:" + id).ToArray()
        };

        public static DeckValidationResult Validate(Deck deck)
        {
            var result = DeckValidator.Validate(BattleCatalog.Cards, ToDefinition(deck).cards);
            if (deck.Extra.Count > 0 || deck.Side.Count > 0) result.Errors.Add("Battle Spirits uses one deck. Extra and Side must be empty.");
            return result;
        }

        public static bool SameName(int first, int second)
        {
            if (!BattleCatalog.TryGet(first, out var a) || !BattleCatalog.TryGet(second, out var b)) return false;
            string aName = string.IsNullOrWhiteSpace(a.nameJa) ? a.DisplayName : a.nameJa;
            string bName = string.IsNullOrWhiteSpace(b.nameJa) ? b.DisplayName : b.nameJa;
            return string.Equals(aName, bName, StringComparison.Ordinal);
        }

        public static int CopyLimit(int id)
        {
            if (!BattleCatalog.TryGet(id, out var card) || !card.IsStandardEligible) return 0;
            return BattleCatalog.Cards.Where(other => SameName(id, BattleCatalog.GetNumericId(other.id)))
                .Select(other => Math.Min(3, other.EffectiveCopyLimit)).DefaultIfEmpty(3).Min();
        }

        private static string SafeName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string((name ?? "Battle Spirits Deck").Select(c => invalid.Contains(c) || c == '/' || c == '\\' ? '_' : c).ToArray());
        }
    }
}
