using System;
using System.Collections.Generic;

namespace LaoDai.BattleSpirits.Core
{
    public enum MatchPhase
    {
        Opening, Main, Attack, FlashBeforeBlock, Block, FlashAfterBlock, GameOver
    }

    [Serializable]
    public sealed class CorePool
    {
        public int Normal;
        public bool Soul;
        public int Total => Normal + (Soul ? 1 : 0);
    }

    [Serializable]
    public sealed class SpiritState
    {
        public int InstanceId;
        public CardDefinition Card;
        public CorePool Cores = new CorePool();
        public bool Exhausted;

        public int Level
        {
            get
            {
                int level = 0;
                for (int i = 0; i < Card.levels.Count; i++)
                    if (Cores.Total >= Card.levels[i].cores) level = i + 1;
                return level;
            }
        }

        public int Bp => Level == 0 ? 0 : Card.levels[Level - 1].bp;
    }

    [Serializable]
    public sealed class PlayerState
    {
        public int Index;
        public string Name = string.Empty;
        public List<string> Deck = new List<string>();
        public List<string> Hand = new List<string>();
        public List<SpiritState> Field = new List<SpiritState>();
        public List<string> Discard = new List<string>();
        public CorePool Reserve = new CorePool { Normal = 3, Soul = true };
        public CorePool Trash = new CorePool();
        public int Life = 5;
        public int CoreGains;
        public bool MulliganUsed;
    }

    [Serializable]
    public sealed class MatchState
    {
        public PlayerState[] Players = Array.Empty<PlayerState>();
        public int ActivePlayerIndex;
        public int TurnNumber;
        public MatchPhase Phase = MatchPhase.Opening;
        public int WinnerIndex = -1;
        public int PendingAttackerId = -1;
        public int PendingBlockerId = -1;
        public int FlashPlayerIndex = -1;
    }

    public sealed class BattleEvent
    {
        public string Kind = string.Empty;
        public int Actor;
        public int SpiritId = -1;
        public int TargetId = -1;
        public string CardId = string.Empty;
        public int Amount;
    }
}
