using System;

namespace MDPro3.BattleSpirits.Visuals
{
    /// <summary>A presentation snapshot. Rendering never changes the match.</summary>
    [Serializable]
    public sealed class BattlePresentationState
    {
        public int viewer;
        public int currentPlayer;
        public BattleCardPresentation[] cards = Array.Empty<BattleCardPresentation>();
        public BattlePlayerPresentation[] players = Array.Empty<BattlePlayerPresentation>();
    }

    [Serializable]
    public sealed class BattleCardPresentation
    {
        public int instanceId;
        public string cardId = "";
        public int owner;
        public bool exhausted;
        public int level;
        public int bp;
        public int normalCores;
        public bool soul;
    }

    [Serializable]
    public sealed class BattlePlayerPresentation
    {
        public int playerIndex;
        public int life;
        public int reserveNormal;
        public int trashNormal;
        public int deckCount;
        public bool reserveSoul;
        public bool trashSoul;
    }
}
