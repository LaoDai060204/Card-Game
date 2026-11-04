using System;
using System.Text;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Networking;

namespace WindBot.BattleSpirits
{
    /// <summary>
    /// Frame-polled WindBot driver. No sleeping, threads, Unity references or
    /// full-state access. At most one command is issued for one observed state.
    /// </summary>
    public sealed class BattleSpiritsBotSession
    {
        private readonly int playerIndex;
        private readonly BattleSpiritsExecutor executor;
        private readonly double delaySeconds;
        private string pendingState;
        private double due;
        private bool delivered;
        private bool fallbackPending;
        public bool IsCancelled { get; private set; }

        public BattleSpiritsBotSession(int playerIndex, BattleSpiritsExecutor executor,
            double decisionDelaySeconds = 0.35)
        {
            if (playerIndex < 0 || playerIndex > 1) throw new ArgumentOutOfRangeException(nameof(playerIndex));
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            if (double.IsNaN(decisionDelaySeconds) || double.IsInfinity(decisionDelaySeconds)
                || decisionDelaySeconds < 0 || decisionDelaySeconds > 60)
                throw new ArgumentOutOfRangeException(nameof(decisionDelaySeconds));
            this.playerIndex = playerIndex;
            this.executor = executor;
            delaySeconds = decisionDelaySeconds;
        }

        public bool TryPoll(FilteredMatchView view, double nowSeconds, out BattleCommand command)
        {
            command = null;
            if (IsCancelled || double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds)) return false;
            if (view == null || view.viewer != playerIndex || view.decisionPlayer != playerIndex
                || view.winner >= 0 || view.ownHand == null
                || view.ownHand.Length > BattleSpiritsExecutor.MaximumZoneCards
                || view.players == null || view.players.Length != 2)
            { pendingState = null; delivered = false; return false; }
            string state = Fingerprint(view);
            if (state == null) return false;
            if (pendingState != state)
            {
                pendingState = state;
                due = nowSeconds + delaySeconds;
                delivered = false;
            }
            if (delivered || nowSeconds < due) return false;
            command = fallbackPending ? Fallback(view.phase) : executor.ChooseAction(view);
            fallbackPending = false;
            delivered = true;
            return command != null;
        }

        public void Cancel() { IsCancelled = true; pendingState = null; delivered = false; fallbackPending = false; }
        public void Reset() { IsCancelled = false; pendingState = null; delivered = false; fallbackPending = false; due = 0; }

        // Call only after the authoritative dispatcher rejects the returned
        // command. The next poll yields a safe phase action, never a retry loop.
        public void ReportRejectedAction()
        {
            if (IsCancelled) return;
            pendingState = null;
            delivered = false;
            fallbackPending = true;
        }

        private static BattleCommand Fallback(string phase)
        {
            switch (phase)
            {
                case "Opening": return new BattleCommand { action = "opening" };
                case "Main":
                case "Attack": return new BattleCommand { action = "endTurn" };
                case "FlashBeforeBlock":
                case "FlashAfterBlock": return new BattleCommand { action = "passFlash" };
                case "Block": return new BattleCommand { action = "takeLife" };
                default: return null;
            }
        }

        private static string Fingerprint(FilteredMatchView view)
        {
            var text = new StringBuilder();
            text.Append(view.viewer).Append('|').Append(view.decisionPlayer).Append('|')
                .Append(view.activePlayer).Append('|').Append(view.turn).Append('|').Append(view.phase)
                .Append('|').Append(view.attacker).Append('|').Append(view.blocker).Append('|').Append(view.flashPlayer);
            foreach (string card in view.ownHand) text.Append('|').Append(card?.Length ?? 0).Append(':').Append(card);
            foreach (var player in view.players)
            {
                if (player == null || player.field == null || player.field.Length > BattleSpiritsExecutor.MaximumZoneCards) return null;
                text.Append('|').Append(player.playerIndex).Append(',').Append(player.life).Append(',')
                    .Append(player.deckCount).Append(',').Append(player.handCount).Append(',')
                    .Append(player.reserveNormal).Append(',').Append(player.reserveSoul).Append(',')
                    .Append(player.trashNormal).Append(',').Append(player.trashSoul);
                foreach (var spirit in player.field)
                {
                    if (spirit == null) return null;
                    text.Append(';').Append(spirit.instanceId).Append(',').Append(spirit.cardId).Append(',')
                        .Append(spirit.normalCores).Append(',').Append(spirit.soulCore).Append(',')
                        .Append(spirit.exhausted).Append(',').Append(spirit.level).Append(',').Append(spirit.bp);
                }
            }
            return text.ToString();
        }
    }
}
