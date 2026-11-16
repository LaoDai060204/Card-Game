using System;
using System.Collections.Generic;
using System.Linq;

namespace MDPro3.BattleSpirits.Core
{
    public enum CardColor { Red, Purple, Green, White, Yellow, Blue }
    public enum BattleRulesFormat { Eternal, Standard }
    [Serializable] public sealed class CardsDatabase { public CardDefinition[] cards = Array.Empty<CardDefinition>(); }
    [Serializable] public sealed class CardLevel { public int cores; public int bp; public bool soulAlternative; }
    [Serializable] public sealed class Reduction { public string color = ""; public int count; }
    [Serializable] public sealed class CardSymbol { public string color = ""; public int count; public bool isEx; }
    [Serializable]
    public sealed class CardDefinition
    {
        public string id = "", nameJa = "", nameEn = "", effectTextJa = "", effectTextEn = "";
        public int numericId;
        public CardColor color;
        public int cost, reductionSymbols, symbols = 1;
        public List<CardLevel> levels = new List<CardLevel>();
        public string type = "Spirit", rulesProfile = "standard-reviewed-20261115", format = "Standard";
        public string[] families = Array.Empty<string>(), colors = Array.Empty<string>();
        public string rarity = "", standardBlock = "A", sourceUrl = "", imageUrl = "", artPath = "";
        public string setId = "", setName = "", releaseDate = "", restrictionSourceUrl = "";
        public bool implemented, standardLegal = true, standardLegalityVerified, symbolsVerified;
        public bool superUnleash, released = true, eternalLegal, restrictionVerified;
        public int maxCopies = 3;
        public Reduction[] reductions = Array.Empty<Reduction>();
        public CardSymbol[] symbolDetails = Array.Empty<CardSymbol>();
        public string DisplayName => string.IsNullOrWhiteSpace(nameEn) ? nameJa : nameEn;
        public bool IsSpirit => string.Equals(type, "Spirit", StringComparison.OrdinalIgnoreCase);
        public bool IsNexus => string.Equals(type, "Nexus", StringComparison.OrdinalIgnoreCase);
        public bool IsMagic => string.Equals(type, "Magic", StringComparison.OrdinalIgnoreCase);
        public int EffectiveCopyLimit => Math.Max(0, maxCopies > 3 && !restrictionVerified ? 3 : maxCopies);
        public bool IsStandardEligible => released && standardLegal && standardLegalityVerified && standardBlock == "A" && maxCopies > 0 && (format == "Standard" || format == "Both");
        public bool IsEternalEligible => released && eternalLegal && maxCopies > 0 && (format == "Eternal" || format == "Both");
        public bool IsEligibleFor(BattleRulesFormat value) => value == BattleRulesFormat.Standard ? IsStandardEligible : value == BattleRulesFormat.Eternal && IsEternalEligible;
        public bool IsPlayable => IsPlayableFor(BattleRulesFormat.Standard);
        public bool IsPlayableFor(BattleRulesFormat value)
        {
            if (!implemented || !IsEligibleFor(value) || !symbolsVerified || string.IsNullOrWhiteSpace(id) || cost < 0 || reductionSymbols < 0 || levels == null) return false;
            if (!AbilityRegistry.Contains(id)) return false;
            if (IsMagic) return levels.Count == 0;
            if ((!IsSpirit && !IsNexus) || levels.Count == 0) return false;
            int previous = -1;
            foreach (var level in levels) { if (level == null || level.cores <= previous || level.bp < 0) return false; previous = level.cores; }
            return true;
        }
        public bool HasFamily(string name) => families != null && families.Any(f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase));
        public int ExSymbols => symbolDetails == null ? 0 : symbolDetails.Where(s => s != null && s.isEx).Sum(s => s.count);
    }
}
