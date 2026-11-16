using System;
using System.Collections.Generic;
using System.Linq;
namespace MDPro3.BattleSpirits.Core
{
    [Serializable] public sealed class DeckDefinition { public string name = "Battle Spirits Deck", format = "Standard"; public string[] cards = Array.Empty<string>(); }
    public sealed class DeckValidationResult { public int Count; public List<string> Errors = new List<string>(); public bool IsValid => Errors.Count == 0; public string Summary => string.Join("\n", Errors); }
    public static class DeckValidator
    {
        public static DeckValidationResult Validate(IReadOnlyList<CardDefinition> catalog, IReadOnlyList<string> ids, bool requireImplemented = true, BattleRulesFormat format = BattleRulesFormat.Standard)
        {
            var result = new DeckValidationResult { Count = ids?.Count ?? 0 };
            if (catalog == null || ids == null) { result.Errors.Add("A card catalog and deck are required."); return result; }
            if (ids.Count < 40) result.Errors.Add("A Standard deck requires at least 40 cards.");
            var cards = catalog.Where(c => c != null && c.id != null).GroupBy(c => c.id).ToDictionary(g => g.Key, g => g.First());
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (id == null || !cards.TryGetValue(id, out var card)) { result.Errors.Add("Unknown card: " + id); continue; }
                if (!card.IsEligibleFor(format)) result.Errors.Add("Card is not legal in " + format + ": " + card.DisplayName);
                if (requireImplemented && !card.IsPlayableFor(format)) result.Errors.Add("Card rules are unavailable: " + card.DisplayName);
                string key = string.IsNullOrEmpty(card.nameJa) ? card.id : card.nameJa;
                counts.TryGetValue(key, out int count); counts[key] = ++count;
                int limit = catalog.Where(c => c != null && (string.IsNullOrEmpty(c.nameJa) ? c.id : c.nameJa) == key && c.standardBlock == "A").Select(c => c.EffectiveCopyLimit).DefaultIfEmpty(card.EffectiveCopyLimit).Min();
                if (count > limit) result.Errors.Add("At most " + limit + " copies of " + card.DisplayName + " are permitted.");
            }
            return result;
        }
    }
    public static class StarterDecks
    {
        public static DeckDefinition Create(IReadOnlyList<CardDefinition> cards, BattleRulesFormat format = BattleRulesFormat.Standard)
        {
            var ids = cards.Where(c => c.IsPlayableFor(format)).SelectMany(c => Enumerable.Repeat(c.id, c.EffectiveCopyLimit)).Take(40).ToArray();
            return new DeckDefinition { cards = ids, format = format.ToString() };
        }
        public static DeckDefinition[] CreatePair(IReadOnlyList<CardDefinition> cards, BattleRulesFormat format = BattleRulesFormat.Standard) => new[] { Create(cards, format), Create(cards, format) };
    }
}
