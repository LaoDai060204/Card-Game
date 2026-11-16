using System;
using System.Collections.Generic;

namespace MDPro3.BattleSpirits.Core
{
    public enum MatchPhase { Opening, Start, Core, Draw, Refresh, Main, Attack, FlashBeforeBlock, Block, FlashAfterBlock, SecondMain, End, GameOver }
    [Serializable] public sealed class CorePool { public int Normal; public bool Soul; public int Total => Normal + (Soul ? 1 : 0); }
    [Serializable]
    public sealed class SpiritState
    {
        public int InstanceId, Owner;
        public CardDefinition Card;
        public CorePool Cores = new CorePool();
        public bool Exhausted, HeavyExhausted;
        public bool PendingRemoval;
        public int TurnBpBonus, BattleBpBonus, BattleLevelOverride;
        internal Func<SpiritState, int> ContinuousBp;
        public int Level
        {
            get
            {
                int result = 0;
                if (Card?.levels == null) return result;
                for (int i = 0; i < Card.levels.Count; i++) if (Cores.Total >= Card.levels[i].cores || Cores.Soul && Card.levels[i].soulAlternative) result = i + 1;
                
                return Math.Max(result, BattleLevelOverride);
            }
        }
        public int Bp => Level <= 0 || !Card.IsSpirit ? 0 : (int)Math.Max(0, Math.Min(int.MaxValue, (long)Card.levels[Math.Min(Level, Card.levels.Count) - 1].bp + TurnBpBonus + BattleBpBonus + (ContinuousBp?.Invoke(this) ?? 0)));
    }
    [Serializable]
    public sealed class PlayerState
    {
        public int Index;
        public string Name = "Player";
        public List<string> Deck = new List<string>(), Hand = new List<string>(), Discard = new List<string>(), Removed = new List<string>(), Revealed = new List<string>();
        public List<SpiritState> Field = new List<SpiritState>();
        public CorePool Reserve = new CorePool { Normal = 3, Soul = true }, Trash = new CorePool();
        public int Life = 5, CoreGains;
        public bool MulliganUsed, LifeReducedThisTurn, OpponentDepletedThisTurn;
        public int AttacksOrBlocksThisTurn;
    }
    [Serializable] public sealed class ChoiceOption { public string key = "", label = "", cardId = ""; public int instanceId = -1, index = -1; }
    [Serializable]
    public sealed class PendingChoice
    {
        public int Id, Owner, Min, Max;
        public string Prompt = "";
        public bool Ordered;
        public ChoiceOption[] Options = Array.Empty<ChoiceOption>();
    }
    [Serializable]
    public sealed class MatchState
    {
        public string Format = "Standard";
        public PlayerState[] Players = Array.Empty<PlayerState>();
        public int ActivePlayerIndex, TurnNumber, WinnerIndex = -1, PendingAttackerId = -1, PendingBlockerId = -1, FlashPlayerIndex = -1;
        public MatchPhase Phase = MatchPhase.Opening;
        public PendingChoice PendingChoice;
    }
    [Serializable] public sealed class BattleEvent { public string Kind = "", CardId = ""; public int Actor, SpiritId = -1, TargetId = -1, Amount; }
    public sealed class PaymentContext { public bool SoulPaid; public int SuccessionEx, FamilyEx; public List<string> RemovedCards = new List<string>(); public List<int> RemovedCardIndices = new List<int>(); }
}
