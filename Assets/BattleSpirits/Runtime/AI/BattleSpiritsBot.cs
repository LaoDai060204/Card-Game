using System;
using System.Collections.Generic;
using System.Linq;
using MDPro3.BattleSpirits.Core;

namespace MDPro3.BattleSpirits.AI
{
    public enum BotDifficulty { Novice, Standard, Expert }

    /// <summary>A deterministic decision policy. Only an owner-filtered snapshot, public cards,
    /// and commands already approved by the rules engine may enter this API.</summary>
    public sealed class BattleSpiritsBot
    {
        private readonly Dictionary<string, CardDefinition> cards;
        private readonly string[] publicOpponentPool;
        public BattleSpiritsBot(IReadOnlyList<CardDefinition> publicCatalog, IReadOnlyList<string> announcedOpponentDeck = null)
        {
            cards = publicCatalog.Where(c => c != null).GroupBy(c => c.id).ToDictionary(g => g.Key, g => g.First());
            publicOpponentPool = announcedOpponentDeck?.ToArray() ?? cards.Keys.SelectMany(id => Enumerable.Repeat(id, 3)).ToArray();
        }

        public BattleCommand Choose(BattleSnapshot view, IReadOnlyList<BattleCommand> legalCommands,
            BotDifficulty difficulty = BotDifficulty.Standard, int seed = 0)
        {
            if (view == null || legalCommands == null || legalCommands.Count == 0) return null;
            if (view.DecisionPlayerIndex != view.ViewerIndex) return null;
            var moves = legalCommands.Where(c => c != null && c.action != "concede").ToArray();
            if (moves.Length == 0) return legalCommands[0];
            var random = new Random(seed);
            if (difficulty == BotDifficulty.Novice && random.Next(5) == 0) return moves[random.Next(moves.Length)];
            // Expert estimates possible responses by sampling the public pool, never the real hidden state.
            double responseRisk = difficulty == BotDifficulty.Expert
                ? SampleOpponentHand(view, publicOpponentPool, seed).Count(id => cards.TryGetValue(id, out var possible) && possible.IsMagic) * .6 : 0;
            var best = moves[0];
            double bestScore = double.NegativeInfinity;
            foreach (var move in moves)
            {
                double score = Score(view, move, difficulty, responseRisk) + random.NextDouble() * (difficulty == BotDifficulty.Expert ? .03 : .4);
                if (score > bestScore) { bestScore = score; best = move; }
            }
            return best;
        }

        private double Score(BattleSnapshot v, BattleCommand c, BotDifficulty difficulty, double responseRisk)
        {
            var me = v.Players[v.ViewerIndex]; var foe = v.Players[1 - v.ViewerIndex];
            var source = me.Field.FirstOrDefault(s => s.InstanceId == c.source);
            var target = foe.Field.FirstOrDefault(s => s.InstanceId == c.target);
            CardDefinition card = null;
            if (c.index >= 0 && c.index < me.Hand.Length) cards.TryGetValue(me.Hand[c.index], out card);
            switch (c.action)
            {
                case "opening":
                    int cheap = me.Hand.Count(id => cards.TryGetValue(id, out var h) && h.cost <= 3);
                    return (c.redraw ? cheap == 0 : cheap > 0) ? 20 : 0;
                case "summon":
                    if (card == null) return 1;
                    return 7 + (card.IsNexus ? 1 : 2) + card.symbols + EffectValue(card) + PaymentValue(card, c) +
                        (me.Field.Length < 2 ? 3 : 0) - card.cost * .25;
                case "magic":
                    if (card == null) return 0;
                    // Removal, core gain, recovery and draws are valued from public printed text.
                    if (me.Field.Length == 0 && (card.effectTextEn ?? "").Contains("your Spirits") && !(card.effectTextEn ?? "").Contains("draw")) return -.5;
                    return 1 + EffectValue(card) + PaymentValue(card, c) + (c.mode == 1 && (card.effectTextEn ?? "").Contains("draw") ? 3 : 0) +
                        (target == null ? 0 : target.Bp / 4000.0 + target.NormalCores * .5);
                case "attack":
                    if (source == null) return -30;
                    int symbols = cards.TryGetValue(source.CardId, out var attackCard) ? Math.Max(1, attackCard.symbols) : 1;
                    var blockers = foe.Field.Where(s => !s.Exhausted && !s.HeavyExhausted).ToArray();
                    if (foe.Life <= symbols && blockers.Length == 0) return 1000;
                    double attack = 4 + symbols * 2 + (blockers.Length == 0 ? 4 : 0);
                    if (blockers.Any(b => b.Bp >= source.Bp)) attack -= 7;
                    if (me.Life <= 2 && me.Field.Count(s => !s.Exhausted) <= foe.Field.Count(s => !s.Exhausted)) attack -= 4;
                    return attack - responseRisk * (source.Bp < 7000 ? 1 : .25);
                case "block":
                    var attacker = foe.Field.FirstOrDefault(s => s.InstanceId == v.PendingAttackerId);
                    if (source == null || attacker == null) return -20;
                    if (me.Life <= 1) return 900 + source.Bp / 10000.0;
                    return source.Bp >= attacker.Bp ? 10 + attacker.NormalCores : -3 - source.NormalCores;
                case "take-life": return me.Life <= 1 ? -1000 : me.Life > 3 ? 2 : -2;
                case "transfer":
                    var recipient = me.Field.FirstOrDefault(s => s.InstanceId == c.target);
                    if (recipient == null || c.source != -1 || !cards.TryGetValue(recipient.CardId, out var definition)) return -20;
                    int total = recipient.NormalCores + (recipient.Soul ? 1 : 0) + Math.Max(1, c.mode);
                    if (definition.levels.Any(l => l.cores == total) && total > recipient.NormalCores + (recipient.Soul ? 1 : 0))
                        return 3 + (v.Phase == MatchPhase.Main && me.Hand.Length == 0 ? 2 : 0);
                    return -10;
                case "swap":
                    var swapTarget = me.Field.FirstOrDefault(s => s.InstanceId == c.target);
                    if (swapTarget == null || !cards.TryGetValue(swapTarget.CardId, out var swapCard)) return -5;
                    int gained = LevelWithCores(swapCard, swapTarget.NormalCores - 1, true) - swapTarget.Level;
                    int lost = source != null && cards.TryGetValue(source.CardId, out var sourceCard) ? source.Level - LevelWithCores(sourceCard, source.NormalCores + 1, false) : 0;
                    return gained > lost ? 3 + gained - lost : -5;
                case "activate": return 5;
                case "choose":
                    if (v.PendingChoice == null || c.selection == null) return 0;
                    double choiceScore = 0;
                    foreach (var ix in c.selection)
                        if (ix >= 0 && ix < v.PendingChoice.Options.Length)
                        {
                            var option = v.PendingChoice.Options[ix];
                            var selected = foe.Field.FirstOrDefault(s => s.InstanceId == option.instanceId);
                            if (selected != null) choiceScore += selected.Bp / 1000.0 + selected.NormalCores;
                            if (cards.TryGetValue(option.cardId, out var chosen)) choiceScore += 2 + EffectValue(chosen) - chosen.cost * .1;
                        }
                    return 5 + choiceScore;
                case "pass": return .2;
                case "advance": return .1;
                default: return -100;
            }
        }

        private static int LevelWithCores(CardDefinition card, int normal, bool soul)
        {
            int level = 0;
            for (int i = 0; i < card.levels.Count; i++)
                if (normal + (soul ? 1 : 0) >= card.levels[i].cores || soul && card.levels[i].soulAlternative) level = i + 1;
            return level;
        }

        private static double PaymentValue(CardDefinition card, BattleCommand command)
        {
            double value = (command.alternateCost ? 1.5 : 0) - (command.chooseCores ? .5 : 0) - (command.chooseSuccession ? .5 : 0);
            // All selected succession cards originate in this player's visible Trash, including printed Magic EX.
            if (command.succession != null) value += Math.Min(3, command.succession.Length) * .7;
            if (command.payment != null && command.payment.Any(t => t.soul) &&
                (card.effectTextEn ?? "").IndexOf("pay", StringComparison.OrdinalIgnoreCase) >= 0) value += 1;
            return value;
        }

        private static double EffectValue(CardDefinition c)
        {
            string text = (c.effectTextEn ?? "").ToLowerInvariant();
            double value = 0;
            foreach (var word in new[] { "draw", "destroy", "return", "refresh", "core", "recover", "life", "unblockable", "burst", "super unleash" })
                if (text.Contains(word)) value += .65;
            return value;
        }

        /// <summary>Samples possible hidden cards from public information; never reads actual hands/decks.
        /// Supply an announced deck list when known, otherwise the public legal card pool.</summary>
        public static string[] SampleOpponentHand(BattleSnapshot view, IReadOnlyList<string> publicPool, int seed)
        {
            if (view == null || publicPool == null || publicPool.Count == 0) return Array.Empty<string>();
            var foe = view.Players[1 - view.ViewerIndex]; var pool = new List<string>(publicPool);
            foreach (var id in foe.Discard.Concat(foe.Removed).Concat(foe.Field.Select(s => s.CardId))) pool.Remove(id);
            if (pool.Count == 0) return Array.Empty<string>();
            var random = new Random(seed); var result = new string[Math.Min(foe.HandCount, pool.Count)];
            for (int i = 0; i < result.Length; i++) { int n = random.Next(pool.Count); result[i] = pool[n]; pool.RemoveAt(n); }
            return result;
        }
    }
}
