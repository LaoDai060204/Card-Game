using System;
namespace MDPro3.BattleSpirits.Core
{
    /// <summary>Detached, viewer-filtered data. No deck order or opposing hand identities leave the engine.</summary>
    [Serializable] public sealed class BattleSnapshot
    {
        public int ViewerIndex, ActivePlayerIndex, DecisionPlayerIndex, TurnNumber, WinnerIndex, PendingAttackerId, PendingBlockerId, FlashPlayerIndex;
        public MatchPhase Phase;
        public PlayerSnapshot[] Players = Array.Empty<PlayerSnapshot>();
        public PendingChoice PendingChoice;
    }
    [Serializable] public sealed class PlayerSnapshot
    {
        public int Index, Life, ReserveNormal, TrashNormal, DeckCount, HandCount;
        public string Name = "";
        public bool ReserveSoul, TrashSoul;
        public string[] Hand = Array.Empty<string>(), Discard = Array.Empty<string>(), Removed = Array.Empty<string>(), Revealed = Array.Empty<string>();
        public SpiritSnapshot[] Field = Array.Empty<SpiritSnapshot>();
    }
    [Serializable] public sealed class SpiritSnapshot
    {
        public int InstanceId, Owner, NormalCores, Level, Bp;
        public string CardId = "";
        public bool Soul, Exhausted, HeavyExhausted;
    }
}
