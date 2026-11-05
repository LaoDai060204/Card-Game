using System;
using System.Collections.Generic;
using System.Linq;
using LaoDai.BattleSpirits.Core;

namespace WindBot.BattleSpirits
{
    public sealed class BattleSpiritsBotProfile
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public string Description { get; internal set; }
        public BotDifficulty Difficulty { get; internal set; }
        public DeckDefinition Deck { get; internal set; }
    }

    /// <summary>WindBot deck registration for the supported Battle Spirits card pool.</summary>
    public static class BattleSpiritsBotProfiles
    {
        public static BattleSpiritsBotProfile[] Create(IReadOnlyList<CardDefinition> catalog,
            BattleRulesFormat format = BattleRulesFormat.Eternal)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            return new[] {
                Profile(catalog, "training", "Luyện tập — Xung kích", BotDifficulty.Easy,
                    "WindBot luyện tập: triệu hồi đơn giản, tấn công và chặn để làm quen luật " + format + ".", format),
                Profile(catalog, "guardian", "Chiến thuật — Giữ sân", BotDifficulty.Normal,
                    "WindBot cân đối Core, nâng Level/BP và chọn Spirit chặn có lợi.", format),
                Profile(catalog, "boss", "Thử thách — Boss", BotDifficulty.Hard,
                    "WindBot ưu tiên Spirit mạnh trong nhóm thẻ đã được hỗ trợ.", format)
            };
        }

        private static BattleSpiritsBotProfile Profile(IReadOnlyList<CardDefinition> catalog,
            string id, string name, BotDifficulty difficulty, string description, BattleRulesFormat format)
        {
            var limits = catalog.Where(card => card != null && !string.IsNullOrEmpty(card.nameJa)
                && (format == BattleRulesFormat.Eternal || IsStandardPrinting(card)))
                .GroupBy(card => card.nameJa, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Min(card => card.EffectiveCopyLimit), StringComparer.Ordinal);
            var ordered = catalog.Where(card => card != null && card.IsPlayableFor(format))
                .GroupBy(card => card.nameJa, StringComparer.Ordinal)
                .Select(group => group.OrderBy(card => card.id, StringComparer.Ordinal).First())
                .OrderByDescending(card => Score(card, difficulty)).ThenBy(card => card.id, StringComparer.Ordinal).ToArray();
            var ids = new List<string>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            void Add(string cardId, int wanted)
            {
                var card = ordered.FirstOrDefault(item => item.id == cardId);
                if (card == null) return;
                counts.TryGetValue(card.nameJa, out int current);
                for (int copy = current; copy < Math.Min(wanted + current, limits[card.nameJa]) && ids.Count < 40; copy++)
                { ids.Add(card.id); counts[card.nameJa] = copy + 1; }
            }
            // Deck-specific registration mirrors WindBot's named executor/deck
            // profiles. Only catalog cards whose abilities are enabled enter.
            if (format == BattleRulesFormat.Eternal && difficulty == BotDifficulty.Normal)
            { Add(AbilityRegistry.Valkyrius, 1); Add(AbilityRegistry.SilverWorld, 2); Add(AbilityRegistry.DoubleDraw, 2); }
            if (format == BattleRulesFormat.Eternal && difficulty == BotDifficulty.Hard)
            { Add(AbilityRegistry.Siegfried, 3); Add(AbilityRegistry.AwakenMagic, 3); Add(AbilityRegistry.DoubleDraw, 3); Add(AbilityRegistry.Valkyrius, 1); }
            foreach (var card in ordered)
            {
                counts.TryGetValue(card.nameJa, out int current);
                Add(card.id, limits[card.nameJa] - current);
            }
            var deck = new DeckDefinition { name = name, format = format.ToString(), cards = ids.ToArray() };
            var validation = DeckValidator.Validate(catalog, deck.cards, format: format);
            if (!validation.IsValid) throw new ArgumentException("WindBot deck " + id + ": " + validation.Summary);
            return new BattleSpiritsBotProfile { Id = id, Name = name, Description = description,
                Difficulty = difficulty, Deck = deck };
        }

        private static bool IsStandardPrinting(CardDefinition card) => card.standardLegalityVerified
            && card.standardBlock == "A"
            && (string.Equals(card.format, "Standard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(card.format, "Both", StringComparison.OrdinalIgnoreCase));

        private static double Score(CardDefinition card, BotDifficulty difficulty)
        {
            int bp = card.levels?.Count > 0 ? card.levels[0].bp : 0;
            if (difficulty == BotDifficulty.Easy)
                return (card.color == CardColor.Red ? 20 : card.color == CardColor.Green ? 10 : 0) - card.cost * 2 + bp / 2000.0;
            if (difficulty == BotDifficulty.Hard)
                return (card.id == AbilityRegistry.Siegfried ? 100 : 0) + card.symbols * 10 + bp / 1000.0 - card.cost * 0.3;
            return (card.color == CardColor.White ? 20 : card.color == CardColor.Green ? 10 : 0) + bp / (double)Math.Max(1, card.cost + 1);
        }
    }
}
