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
    public static class AbilityRegistry
    {
        public const string Version = "standard-reviewed-20261115";
        private static readonly HashSet<string> Supported = new HashSet<string>(StringComparer.Ordinal)
        {
            "26RSD09-001","26RBS02-002","26RBS01-003","26RSD09-004","26RBS03-004","26RBS03-X01","26RBS01-X03","26RBS03-X03","26RSD09-011","26RSD01-012","26RSD09-012","26RBS02-087","26RBS03-087","26RSD01-014",
            "26RBS03-009","26RSD02-001","26RBS03-010","26RBS03-012","26RBS01-019","26RBS03-014","26RSD02-005","26RBS01-X04","26RBS01-024","26RBS03-X04","26RBS03-X05","26RSD02-X01","26RBS01-X06","26RBS03-X06","26RSD02-014","26RBS03-089","26RBS03-097",
            "26RSD03-002","26RSD03-003","26RSD10-003","26RSD10-004","26RSD03-005","26RSD03-X02","26RBS02-034","26RBS02-033","26RBS02-X09","26RSD03-010","26RSD03-012","26RSD03-013","26RSD03-014","26RBS03-091",
            "26RCB01-039","26RCB01-040","26RCB01-041","26RCB01-043","26RCB01-044","26RCB01-046","26RCB01-047","26RCB01-051","26RCB01-052","26RCB01-X06","26RCB01-X07","26RCB01-060","26RSD06-012","26RSD06-013","26RCB01-071","26RBS01-126","26RBS03-022"
        };
        public static bool Contains(string id) => id != null && Supported.Contains(id);
        public static bool IsRegistered(CardDefinition card) => card != null && Contains(card.id);
    }
}
