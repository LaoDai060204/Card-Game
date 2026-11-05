using System;
using System.Collections.Generic;
using System.Linq;

namespace LaoDai.BattleSpirits.Core
{
    [Serializable]
    public sealed class DeckDefinition
    {
        public string name = "Bộ bài Battle Spirits";
        public string format = "Eternal";
        public string[] cards = Array.Empty<string>();
    }

    public sealed class DeckValidationResult
    {
        public int Count;
        public List<string> Errors = new List<string>();
        public bool IsValid => Errors.Count == 0;
        public string Summary => string.Join("\n", Errors);
    }

    public static class DeckValidator
    {
        public static DeckValidationResult Validate(IReadOnlyList<CardDefinition> catalog,
            IReadOnlyList<string> ids, bool requireImplemented = true, BattleRulesFormat format = BattleRulesFormat.Eternal)
        {
            var result = new DeckValidationResult { Count = ids?.Count ?? 0 };
            if (format != BattleRulesFormat.Eternal && format != BattleRulesFormat.Standard)
            {
                result.Errors.Add("Thể thức Battle Spirits không hợp lệ.");
                return result;
            }
            if (catalog == null)
            {
                result.Errors.Add("Chưa có dữ liệu thẻ Battle Spirits.");
                return result;
            }
            var cards = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
            var limits = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var card in catalog)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.id) || string.IsNullOrWhiteSpace(card.nameJa))
                {
                    AddUnique(result, "Dữ liệu thẻ thiếu ID hoặc tên gốc.");
                    continue;
                }
                if (cards.ContainsKey(card.id))
                {
                    AddUnique(result, "ID thẻ bị trùng: " + card.id);
                    continue;
                }
                cards.Add(card.id, card);
                // A restriction on an Eternal-only printing must not alter a
                // Standard deck's limit. Banned Standard prints still count.
                if (format == BattleRulesFormat.Eternal || IsStandardPrinting(card))
                {
                    int limit = card.EffectiveCopyLimit;
                    if (!limits.TryGetValue(card.nameJa, out var previous) || limit < previous)
                        limits[card.nameJa] = limit;
                }
            }
            if (ids == null)
            {
                result.Errors.Add("Chưa chọn bộ bài.");
                return result;
            }
            if (ids.Count < 40)
                result.Errors.Add("Bộ bài " + format + " cần ít nhất 40 lá.");
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (id == null || !cards.TryGetValue(id, out var card))
                {
                    AddUnique(result, "Không có dữ liệu cho thẻ: " + (id ?? "(trống)"));
                    continue;
                }
                if (!card.IsEligibleFor(format))
                    AddUnique(result, "Thẻ chưa được xác nhận dùng trong " + format + " hoặc đang bị cấm: " + card.DisplayName);
                if (requireImplemented && !card.IsPlayableFor(format))
                    AddUnique(result, "Thẻ hiện chỉ để xem, chưa hỗ trợ hiệu ứng trong trận: " + card.DisplayName);
                counts.TryGetValue(card.nameJa, out int count);
                counts[card.nameJa] = count + 1;
                int limit = limits.TryGetValue(card.nameJa, out var verifiedLimit) ? verifiedLimit : card.EffectiveCopyLimit;
                if (count + 1 > limit)
                    AddUnique(result, "Tối đa " + limit + " lá cùng tên: " + card.DisplayName);
            }
            return result;
        }

        private static void AddUnique(DeckValidationResult result, string error)
        {
            if (!result.Errors.Contains(error)) result.Errors.Add(error);
        }

        internal static bool IsStandardPrinting(CardDefinition card) => card.standardLegalityVerified
            && card.standardBlock == "A"
            && (string.Equals(card.format, "Standard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(card.format, "Both", StringComparison.OrdinalIgnoreCase));
    }

    public static class StarterDecks
    {
        public static DeckDefinition Create(IReadOnlyList<CardDefinition> catalog,
            string name = "Bộ bài luyện tập", BattleRulesFormat format = BattleRulesFormat.Eternal)
        {
            return Build(catalog, name, false, format);
        }

        public static DeckDefinition[] CreatePair(IReadOnlyList<CardDefinition> catalog, BattleRulesFormat format = BattleRulesFormat.Eternal)
        {
            return new[]
            {
                Build(catalog, "Luyện tập " + format + " 1", false, format),
                Build(catalog, "Luyện tập " + format + " 2", true, format)
            };
        }

        private static DeckDefinition Build(IReadOnlyList<CardDefinition> catalog, string name, bool reverse, BattleRulesFormat format)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var all = catalog.Where(c => c != null && !string.IsNullOrWhiteSpace(c.nameJa)).ToArray();
            var limits = all.Where(c => format == BattleRulesFormat.Eternal || DeckValidator.IsStandardPrinting(c))
                .GroupBy(c => c.nameJa, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Min(c => c.EffectiveCopyLimit), StringComparer.Ordinal);
            var eligible = all.Where(c => c.IsPlayableFor(format) && c.IsSpirit).OrderBy(c => c.cost)
                .ThenBy(c => c.id, StringComparer.Ordinal).GroupBy(c => c.nameJa, StringComparer.Ordinal)
                .Select(g => g.First()).ToList();
            if (reverse) eligible.Reverse();
            var ids = new List<string>();
            for (int copy = 0; copy < 40 && ids.Count < 40; copy++)
                foreach (var card in eligible)
                {
                    if (ids.Count == 40) break;
                    if (copy < limits[card.nameJa]) ids.Add(card.id);
                }
            if (ids.Count < 40)
                throw new ArgumentException("Chưa đủ tên thẻ đã hỗ trợ để tạo bộ bài " + format + " 40 lá hợp lệ.");
            var deck = new DeckDefinition { name = name, format = format.ToString(), cards = ids.ToArray() };
            var result = DeckValidator.Validate(catalog, deck.cards, format: format);
            if (!result.IsValid) throw new ArgumentException(result.Summary);
            return deck;
        }
    }
}
