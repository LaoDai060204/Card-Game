using System;
using System.Collections.Generic;

namespace LaoDai.BattleSpirits.Core
{
    public enum CardColor { Red, Purple, Green, White, Yellow, Blue }

    [Serializable]
    public sealed class CardsDatabase
    {
        public CardDefinition[] cards = Array.Empty<CardDefinition>();
    }

    [Serializable]
    public sealed class CardLevel
    {
        public int cores;
        public int bp;
    }

    [Serializable]
    public sealed class Reduction
    {
        public string color = string.Empty;
        public int count;
    }

    [Serializable]
    public sealed class CardSymbol
    {
        public string color = string.Empty;
        public int count;
    }

    // Catalog fields describe the original cards, including cards whose rules
    // have not been implemented. Such entries remain available for browsing.
    [Serializable]
    public sealed class CardDefinition
    {
        public string id = string.Empty;
        public string nameJa = string.Empty;
        public string nameVi = string.Empty;
        public CardColor color;
        public int cost;
        public int reductionSymbols;
        public int symbols = 1;
        public List<CardLevel> levels = new List<CardLevel>();
        public string effectTextJa = string.Empty;
        public string sourceUrl = string.Empty;
        public string imageUrl = string.Empty;
        public bool implemented;

        // Defaults retain compatibility with the previously verified vanilla
        // Spirit catalog. New imports explicitly state their observed metadata.
        public string type = "Spirit";
        public string[] families = Array.Empty<string>();
        public string rarity = string.Empty;
        public string setId = string.Empty;
        public string setName = string.Empty;
        public string releaseDate = string.Empty;
        public string format = "Eternal";
        public bool released = true;
        public bool eternalLegal = true;
        public int maxCopies = 3;
        public string restrictionSourceUrl = string.Empty;
        public bool restrictionVerified;
        public string[] colors = Array.Empty<string>();
        public Reduction[] reductions = Array.Empty<Reduction>();
        public CardSymbol[] symbolDetails = Array.Empty<CardSymbol>();
        public bool symbolsVerified = true;
        public string artPath = string.Empty;

        public string DisplayName => string.IsNullOrWhiteSpace(nameVi) ? nameJa : nameVi;

        // Larger copy exceptions require proof in the trusted server catalog.
        // Clients submit card IDs only and cannot provide these definitions.
        public int EffectiveCopyLimit => Math.Max(0, maxCopies > 3 && !restrictionVerified ? 3 : maxCopies);

        public bool IsEternalEligible => released && eternalLegal && maxCopies > 0
            && (string.Equals(format, "Eternal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(format, "Both", StringComparison.OrdinalIgnoreCase));

        public bool IsPlayable
        {
            get
            {
                string effect = (effectTextJa ?? string.Empty).Trim();
                if (!implemented || !IsEternalEligible || !symbolsVerified
                    || !string.Equals(type, "Spirit", StringComparison.OrdinalIgnoreCase)
                    || !(effect.Length == 0 || effect == "-" || effect == "－" || effect == "―")
                    || string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nameJa)
                    || cost < 0 || reductionSymbols < 0 || reductionSymbols > cost
                    || symbols < 1 || color < CardColor.Red || color > CardColor.Blue
                    || levels == null || levels.Count == 0
                    || colors == null || colors.Length > 1
                    || reductions == null || reductions.Length > 1
                    || symbolDetails == null || symbolDetails.Length > 1)
                    return false;
                if (colors.Length == 1 && (!TryGetColor(colors[0], out var printedColor) || printedColor != color))
                    return false;
                if (reductions.Length == 1 && (reductions[0] == null
                    || !TryGetColor(reductions[0].color, out var reductionColor) || reductionColor != color
                    || reductions[0].count != reductionSymbols))
                    return false;
                if (symbolDetails.Length == 1 && (symbolDetails[0] == null
                    || !TryGetColor(symbolDetails[0].color, out var symbolColor) || symbolColor != color
                    || symbolDetails[0].count != symbols))
                    return false;
                int previous = 0;
                foreach (var level in levels)
                {
                    if (level == null || level.cores <= previous || level.bp < 0)
                        return false;
                    previous = level.cores;
                }
                return true;
            }
        }

        private static bool TryGetColor(string value, out CardColor parsed)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "red": case "赤": parsed = CardColor.Red; return true;
                case "purple": case "紫": parsed = CardColor.Purple; return true;
                case "green": case "緑": parsed = CardColor.Green; return true;
                case "white": case "白": parsed = CardColor.White; return true;
                case "yellow": case "黄": parsed = CardColor.Yellow; return true;
                case "blue": case "青": parsed = CardColor.Blue; return true;
                default: parsed = default; return false;
            }
        }
    }
}
