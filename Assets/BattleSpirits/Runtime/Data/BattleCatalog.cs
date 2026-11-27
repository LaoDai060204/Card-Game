using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using MDPro3.BattleSpirits.Core;
using Newtonsoft.Json;

namespace MDPro3.BattleSpirits
{
    [Serializable]
    public sealed class BattleCardVisual
    {
        public string id = "";
        public int width, height, left, top, right, bottom;
        public bool isBoss;
    }

    /// <summary>Reviewed Battle Spirits cards and byte-preserved local artwork.</summary>
    public static class BattleCatalog
    {
        [Serializable] private sealed class DeckFile { public DeckDefinition[] decks = Array.Empty<DeckDefinition>(); }
        [Serializable] private sealed class VisualFile { public BattleCardVisual[] visuals = Array.Empty<BattleCardVisual>(); }
        [Serializable] private sealed class NumericEntry { public string id = ""; public int numericId; }
        [Serializable] private sealed class NumericFile { public NumericEntry[] entries = Array.Empty<NumericEntry>(); }
        private static readonly object Gate = new object();
        private static IReadOnlyList<CardDefinition> cards = Array.Empty<CardDefinition>();
        private static IReadOnlyList<DeckDefinition> decks = Array.Empty<DeckDefinition>();
        private static Dictionary<string, CardDefinition> byId = new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<int, CardDefinition> byNumber = new Dictionary<int, CardDefinition>();
        private static Dictionary<string, BattleCardVisual> visuals = new Dictionary<string, BattleCardVisual>(StringComparer.OrdinalIgnoreCase);
        private static string assetRoot;

        public static IReadOnlyList<CardDefinition> Cards => cards;
        public static IReadOnlyList<DeckDefinition> StarterDecks => decks;

        public static void Initialize(string streamingAssetsRoot, string persistentRoot = null)
        {
            if (string.IsNullOrWhiteSpace(streamingAssetsRoot)) throw new ArgumentException("A local streaming assets root is required.", nameof(streamingAssetsRoot));
            lock (Gate)
            {
                string root = Directory.Exists(Path.Combine(streamingAssetsRoot, "BattleSpirits"))
                    ? Path.Combine(streamingAssetsRoot, "BattleSpirits") : streamingAssetsRoot;
                root = Path.GetFullPath(root);
                if (string.Equals(assetRoot, root, StringComparison.Ordinal) && cards.Count > 0) return;
                var loaded = Read<CardsDatabase>(Path.Combine(root, "cards.json")).cards;
                var loadedDecks = Read<DeckFile>(Path.Combine(root, "starter-decks.json")).decks;
                var idMap = Read<NumericFile>(Path.Combine(root, "numeric-ids.json")).entries;
                var loadedVisuals = Read<VisualFile>(Path.Combine(root, "visuals.json")).visuals;
                var numeric = idMap.ToDictionary(e => e.id, e => e.numericId, StringComparer.OrdinalIgnoreCase);
                var newById = new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase);
                var newByNumber = new Dictionary<int, CardDefinition>();
                foreach (var card in loaded)
                {
                    if (!numeric.TryGetValue(card.id, out int number) || number < 900000000 || number != card.numericId)
                        throw new InvalidDataException("Missing or inconsistent persistent numeric ID: " + card.id);
                    newById.Add(card.id, card);
                    newByNumber.Add(number, card);
                    if (!File.Exists(Path.Combine(root, card.artPath)))
                        throw new FileNotFoundException("Card artwork is missing: " + card.id);
                }
                foreach (var deck in loadedDecks)
                    if (deck.cards.Length != 40 || deck.cards.Any(id => !newById.ContainsKey(id)))
                        throw new InvalidDataException("Starter deck has missing cards or an invalid size: " + deck.name);
                assetRoot = root;
                byId = newById;
                byNumber = newByNumber;
                visuals = loadedVisuals.ToDictionary(v => v.id, StringComparer.OrdinalIgnoreCase);
                cards = new ReadOnlyCollection<CardDefinition>(loaded);
                decks = new ReadOnlyCollection<DeckDefinition>(loadedDecks);
                // The shipped explicit map is authoritative; user decks store these IDs unchanged.
                // persistentRoot is accepted for callers which keep user configuration separately.
            }
        }

        private static T Read<T>(string path) where T : class =>
            JsonConvert.DeserializeObject<T>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid catalog file: " + path);

        private static string Canonical(string id)
        {
            if (string.Equals(id, "ritenia", StringComparison.OrdinalIgnoreCase)) return "26RBS03-X01";
            if (string.Equals(id, "pisketona", StringComparison.OrdinalIgnoreCase)) return "26RBS03-022";
            return id;
        }

        public static bool TryGet(string id, out CardDefinition card)
        {
            id = Canonical(id);
            if (id == null) { card = null; return false; }
            return byId.TryGetValue(id, out card);
        }
        public static bool TryGet(int numericId, out CardDefinition card) => byNumber.TryGetValue(numericId, out card);
        public static int GetNumericId(string id) => TryGet(id, out var card) ? card.numericId : 0;
        public static string GetCardId(int numericId) => TryGet(numericId, out var card) ? card.id : null;
        public static string CardImagePath(string id) => TryGet(id, out var card) ? Path.Combine(assetRoot, card.artPath) : null;
        public static string StandeePath(string id)
        {
            if (!TryGet(id, out var card) || !card.IsSpirit) return null;
            string path = Path.Combine(assetRoot, "standees", card.id + ".png");
            return File.Exists(path) ? path : null;
        }
        public static BattleCardVisual GetVisual(string id)
        {
            id = Canonical(id);
            return id != null && visuals.TryGetValue(id, out var value) ? value : null;
        }
        public static bool IsBoss(string id) => GetVisual(id)?.isBoss ?? false;
    }
}
