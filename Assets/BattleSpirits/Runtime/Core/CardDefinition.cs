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

    // Public fields are deliberate: the same catalog loads with Unity JsonUtility
    // and the standalone rules harness, without a third-party serializer.
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

        public string DisplayName => string.IsNullOrWhiteSpace(nameVi) ? nameJa : nameVi;
    }
}
