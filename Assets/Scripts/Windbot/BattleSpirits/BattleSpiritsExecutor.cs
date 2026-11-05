using System;
using System.Collections.Generic;
using System.Linq;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Networking;

namespace WindBot.BattleSpirits
{
    public enum BotDifficulty { Easy, Normal, Hard }

    /// <summary>
    /// WindBot's Battle Spirits rules backend. Decisions consume exactly the
    /// filtered player view that a human client receives, never MatchState.
    /// The YGO executor/packet types deliberately do not cross this boundary.
    /// </summary>
    public sealed class BattleSpiritsExecutor
    {
        public const int MaximumZoneCards = 256;
        private readonly Dictionary<string, CardDefinition> catalog;
        private readonly Random random;
        private BattleRulesFormat rulesFormat;
        public BotDifficulty Difficulty { get; }
        public int LastInspectionCount { get; private set; }

        public BattleSpiritsExecutor(IReadOnlyList<CardDefinition> cards,
            BotDifficulty difficulty = BotDifficulty.Normal, int randomSeed = 0)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            catalog = cards.Where(card => card != null && !string.IsNullOrEmpty(card.id))
                .ToDictionary(card => card.id, StringComparer.Ordinal);
            Difficulty = difficulty;
            random = new Random(randomSeed);
        }

        public BattleCommand ChooseAction(FilteredMatchView view)
        {
            LastInspectionCount = 0;
            if (!CanDecide(view)) return null;
            if (string.IsNullOrEmpty(view.rulesFormat) || view.rulesFormat == "Eternal") rulesFormat = BattleRulesFormat.Eternal;
            else if (view.rulesFormat == "Standard") rulesFormat = BattleRulesFormat.Standard;
            else return null;
            var me = view.players.First(player => player.playerIndex == view.viewer);
            var opponent = view.players.First(player => player.playerIndex != view.viewer);
            switch (view.phase)
            {
                case "Opening":
                    return new BattleCommand { action = "opening", redraw = ShouldRedraw(view, me) };
                case "Main":
                case "SecondMain":
                    return ChooseMain(view, me, opponent);
                case "Attack":
                    return ChooseAttack(view, me, opponent);
                case "FlashBeforeBlock":
                case "FlashAfterBlock":
                    return ChooseFlash(view, me, opponent);
                case "Block":
                    return ChooseBlock(view, me, opponent);
                default:
                    return null;
            }
        }

        private static bool CanDecide(FilteredMatchView view)
        {
            return view != null && view.viewer >= 0 && view.viewer < 2
                && view.winner < 0 && view.decisionPlayer == view.viewer
                && view.ownHand != null && view.ownHand.Length <= MaximumZoneCards
                && view.players != null && view.players.Length == 2
                && view.players.All(player => player != null && player.field != null
                    && player.field.Length <= MaximumZoneCards && player.field.All(spirit => spirit != null))
                && view.players.Count(player => player.playerIndex == view.viewer) == 1
                && view.players.Count(player => player.playerIndex == 1 - view.viewer) == 1;
        }

        private bool ShouldRedraw(FilteredMatchView view, PublicPlayerView me)
        {
            if (Difficulty == BotDifficulty.Easy) return false;
            foreach (var id in view.ownHand)
            {
                LastInspectionCount++;
                if (TryCard(id, out var card) && IsSpirit(card)
                    && card.cost + card.levels[0].cores <= Reserve(me)) return false;
            }
            return true;
        }

        private BattleCommand ChooseMain(FilteredMatchView view, PublicPlayerView me, PublicPlayerView opponent)
        {
            int[] reductions = new int[6];
            int spare = 0;
            foreach (var spirit in me.field)
            {
                LastInspectionCount++;
                if (!TryCard(spirit.cardId, out var definition) || !IsPermanent(definition)) continue;
                reductions[(int)definition.color] += definition.symbols;
                spare += Math.Max(0, Cores(spirit) - definition.levels[0].cores);
            }
            int selected = -1;
            double bestScore = double.NegativeInfinity;
            for (int index = 0; index < view.ownHand.Length; index++)
            {
                LastInspectionCount++;
                if (!TryCard(view.ownHand[index], out var card) || !IsPermanent(card)) continue;
                int cost = Math.Max(0, card.cost - Math.Min(card.reductionSymbols, reductions[(int)card.color]));
                // Preserve at least Lv1 on established Spirits. The engine may
                // legally sacrifice them, but sacrificing blindly loses tempo.
                if ((long)cost + card.levels[0].cores > (long)Reserve(me) + spare) continue;
                double score = card.levels[0].bp / 1000.0 + 2.5 * card.symbols
                    + reductions[(int)card.color] * 0.25 - cost * 0.75;
                if (Difficulty == BotDifficulty.Easy) score = random.NextDouble();
                if (score > bestScore) { bestScore = score; selected = index; }
            }
            if (selected >= 0) return new BattleCommand { action = "summon", index = selected };

            if (Difficulty != BotDifficulty.Easy)
            {
                for (int index = 0; index < view.ownHand.Length; index++)
                {
                    LastInspectionCount++;
                    if (!TryCard(view.ownHand[index], out var card) || !card.IsPlayableFor(rulesFormat)
                        || !string.Equals(card.type, "Magic", StringComparison.OrdinalIgnoreCase)) continue;
                    int cost = Math.Max(0, card.cost - Math.Min(card.reductionSymbols, reductions[(int)card.color]));
                    if (cost > Reserve(me)) continue;
                    if (card.id == AbilityRegistry.DoubleDraw && me.deckCount > 2 && view.ownHand.Length <= 3)
                        return new BattleCommand { action = "playMagic", index = index };
                    if (card.id == AbilityRegistry.AwakenMagic)
                    {
                        var target = me.field.Where(spirit => TryCard(spirit.cardId, out var definition)
                            && IsSpirit(definition) && definition.levels.Any(level => level.bp > spirit.bp
                                && level.cores > Cores(spirit) && level.cores - Cores(spirit) <= 3
                                && level.cores - Cores(spirit) <= Math.Max(0, me.reserveNormal - cost)))
                            .OrderByDescending(spirit => spirit.bp).FirstOrDefault();
                        if (target != null)
                            return new BattleCommand { action = "playMagic", index = index, target = target.instanceId };
                        if (me.deckCount > 1 && view.ownHand.Length <= 3)
                            return new BattleCommand { action = "playMagic", index = index, transfers = Array.Empty<CoreTransfer>() };
                    }
                }
            }

            // Level allocation is monotonic: only Reserve -> Spirit and only
            // when that Reserve can reach a strictly higher printed BP level.
            // Every returned move consumes a core; no redistribution cycles.
            if (Reserve(me) > 0 && Difficulty != BotDifficulty.Easy)
            {
                SpiritView target = null;
                bool targetSoul = false;
                double best = double.NegativeInfinity;
                int opposingBp = opponent.field.Where(other => !other.exhausted)
                    .Select(other => BlockingBp(other)).DefaultIfEmpty(0).Max();
                LastInspectionCount += opponent.field.Length;
                foreach (var spirit in me.field)
                {
                    LastInspectionCount++;
                    if (!TryCard(spirit.cardId, out var card) || !IsPermanent(card)) continue;
                    int total = Cores(spirit);
                    var next = card.levels.FirstOrDefault(level => level.cores > total);
                    bool useSoul = rulesFormat == BattleRulesFormat.Standard && card.superUnleash
                        && !spirit.soulCore && me.reserveSoul && spirit.level < 2 && card.levels.Count >= 2;
                    // Printed Super Unleash alone makes one Soul Core enough
                    // for Lv2. Ordinary Spirits retain their printed thresholds.
                    if (useSoul) next = card.levels[1];
                    int required = useSoul ? 1 : next == null ? int.MaxValue : next.cores - total;
                    if (next == null || required > Reserve(me)) continue;
                    if (card.IsNexus)
                    {
                        // Silver World's second level matters when defending
                        // several Spirits; do not drain an empty board's core
                        // budget into a Nexus that cannot attack or block.
                        if (card.id != AbilityRegistry.SilverWorld
                            || me.field.Count(item => IsAttackingSpirit(item)) < 2) continue;
                        double nexusScore = 1500.0 / (next.cores - total);
                        if (nexusScore > best) { best = nexusScore; target = spirit; targetSoul = false; }
                        continue;
                    }
                    if (next.bp <= spirit.bp) continue;
                    double score = (next.bp - spirit.bp) / (double)required;
                    if (spirit.bp <= opposingBp && next.bp > opposingBp) score += 10000;
                    if (score > best) { best = score; target = spirit; targetSoul = useSoul; }
                }
                if (target != null)
                    return new BattleCommand { action = "moveCore", source = -1,
                        target = target.instanceId, soul = targetSoul || me.reserveNormal == 0 && me.reserveSoul };
            }
            return new BattleCommand { action = view.turn <= 1 || view.phase == "SecondMain" ? "endTurn" : "attackStep" };
        }

        private BattleCommand ChooseAttack(FilteredMatchView view, PublicPlayerView me, PublicPlayerView opponent)
        {
            var attackers = me.field.Where(spirit => !spirit.exhausted && IsAttackingSpirit(spirit)).ToArray();
            var blockers = opponent.field.Where(spirit => !spirit.exhausted && IsAttackingSpirit(spirit)).ToArray();
            LastInspectionCount += me.field.Length + opponent.field.Length;
            if (attackers.Length == 0) return new BattleCommand { action = "endTurn" };
            if (Difficulty == BotDifficulty.Easy)
                return new BattleCommand { action = "attack", index = attackers[random.Next(attackers.Length)].instanceId };

            int maxBlockingBp = blockers.Select(spirit => BlockingBp(spirit)).DefaultIfEmpty(0).Max();
            int strongestCounter = opponent.field.Select(spirit => spirit.bp).DefaultIfEmpty(0).Max();
            bool lethalPressure = attackers.Length - blockers.Length >= opponent.life;
            SpiritView selected = null;
            double best = double.NegativeInfinity;
            foreach (var attacker in attackers)
            {
                LastInspectionCount++;
                if (!TryCard(attacker.cardId, out var card)) continue;
                // Keep one useful defender at critically low Life unless this
                // attack can force a win or clear every possible blocker.
                if (!lethalPressure && me.life <= 1 && attackers.Length == 1
                    && attacker.bp > strongestCounter && blockers.Length > 0
                    && attacker.bp <= maxBlockingBp) continue;
                double score = attacker.bp / 1000.0 + 3 * card.symbols;
                if (attacker.bp > maxBlockingBp) score += 20;
                else if (attacker.bp == maxBlockingBp) score += 3;
                else score -= 15;
                if (blockers.Length == 0) score += 100;
                if (lethalPressure) score += 100;
                if (score > best) { best = score; selected = attacker; }
            }
            if (selected == null || best < 0) return new BattleCommand { action = "endTurn" };
            return new BattleCommand { action = "attack", index = selected.instanceId };
        }

        private BattleCommand ChooseBlock(FilteredMatchView view, PublicPlayerView me, PublicPlayerView opponent)
        {
            var attacker = opponent.field.FirstOrDefault(spirit => spirit.instanceId == view.attacker);
            LastInspectionCount += opponent.field.Length;
            if (attacker == null) return new BattleCommand { action = "takeLife" };
            int damage = TryCard(attacker.cardId, out var attackCard) ? attackCard.symbols : 1;
            var ready = me.field.Where(spirit => !spirit.exhausted && IsAttackingSpirit(spirit)).ToArray();
            LastInspectionCount += me.field.Length;
            if (ready.Length == 0) return new BattleCommand { action = "takeLife" };
            if (Difficulty == BotDifficulty.Easy)
                return random.Next(2) == 0 || damage >= me.life
                    ? new BattleCommand { action = "block", index = ready[random.Next(ready.Length)].instanceId }
                    : new BattleCommand { action = "takeLife" };
            var winning = ready.Where(spirit => BlockingBp(spirit) > attacker.bp).OrderBy(spirit => BlockingBp(spirit)).FirstOrDefault();
            if (winning != null) return new BattleCommand { action = "block", index = winning.instanceId };
            var trading = ready.Where(spirit => BlockingBp(spirit) == attacker.bp).OrderBy(spirit => Cores(spirit)).FirstOrDefault();
            if (trading != null) return new BattleCommand { action = "block", index = trading.instanceId };
            if (damage >= me.life)
                return new BattleCommand { action = "block", index = ready.OrderBy(spirit => spirit.bp).First().instanceId };
            return new BattleCommand { action = "takeLife" };
        }

        private BattleCommand ChooseFlash(FilteredMatchView view, PublicPlayerView me, PublicPlayerView opponent)
        {
            if (Difficulty == BotDifficulty.Easy) return new BattleCommand { action = "passFlash" };
            int ownId = view.activePlayer == view.viewer ? view.attacker : view.blocker;
            int otherId = view.activePlayer == view.viewer ? view.blocker : view.attacker;
            var other = opponent.field.FirstOrDefault(spirit => spirit.instanceId == otherId);
            var target = me.field.FirstOrDefault(spirit => spirit.instanceId == ownId);
            int opposingBp = other?.bp ?? opponent.field.Where(spirit => !spirit.exhausted && IsAttackingSpirit(spirit))
                .Select(spirit => view.phase == "FlashBeforeBlock" ? BlockingBp(spirit) : spirit.bp).DefaultIfEmpty(0).Max();
            LastInspectionCount += me.field.Length + opponent.field.Length;
            // Before block declaration, the defender can strengthen a ready
            // Spirit. Include printed block bonuses when comparing candidates.
            var candidates = target != null ? new[] { target }
                : view.activePlayer != view.viewer && view.phase == "FlashBeforeBlock"
                    ? me.field.Where(spirit => !spirit.exhausted && IsAttackingSpirit(spirit))
                        .OrderByDescending(spirit => BlockingBp(spirit)).ToArray()
                    : Array.Empty<SpiritView>();
            foreach (var candidate in candidates)
            {
                LastInspectionCount++;
                int battleBp = view.activePlayer != view.viewer && view.phase == "FlashBeforeBlock"
                    ? BlockingBp(candidate) : candidate.bp;
                if (battleBp > opposingBp) return new BattleCommand { action = "passFlash" };
                var boost = ChooseFlashBoost(view, me, candidate, battleBp, opposingBp);
                if (boost != null) return boost;
            }
            return new BattleCommand { action = "passFlash" };
        }

        private BattleCommand ChooseFlashBoost(FilteredMatchView view, PublicPlayerView me,
            SpiritView target, int battleBp, int opposingBp)
        {
            if (!TryCard(target.cardId, out var definition)) return null;
            int printedBp = target.level > 0 && target.level <= definition.levels.Count
                ? definition.levels[target.level - 1].bp : target.bp;
            long existingBonus = (long)battleBp - printedBp;

            if (target.cardId == AbilityRegistry.Siegfried && TryCard(target.cardId, out var boss))
            {
                var next = boss.levels.FirstOrDefault(level => level.cores > Cores(target));
                if (next != null && next.bp + existingBonus > opposingBp)
                {
                    var donors = me.field.Where(spirit => spirit.instanceId != target.instanceId
                        && TryCard(spirit.cardId, out var definition) && IsSpirit(definition)
                        && Cores(spirit) > definition.levels[0].cores).ToArray();
                    int spare = donors.Sum(spirit => Cores(spirit) - catalog[spirit.cardId].levels[0].cores);
                    if (spare >= next.cores - Cores(target))
                    {
                        int required = next.cores - Cores(target);
                        var transfers = new List<CoreTransfer>();
                        foreach (var donor in donors)
                        {
                            int amount = Math.Min(required, Cores(donor) - catalog[donor.cardId].levels[0].cores);
                            int ordinary = Math.Min(amount, donor.normalCores);
                            transfers.Add(new CoreTransfer { source = donor.instanceId, normal = ordinary,
                                soul = amount > ordinary && donor.soulCore });
                            required -= amount;
                            if (required == 0) break;
                        }
                        return new BattleCommand { action = "awaken", target = target.instanceId,
                            transfers = transfers.ToArray() };
                    }
                }
            }
            int redSymbols = me.field.Where(spirit => TryCard(spirit.cardId, out var card)
                && IsPermanent(card) && card.color == CardColor.Red).Sum(spirit => catalog[spirit.cardId].symbols);
            for (int index = 0; index < view.ownHand.Length; index++)
            {
                LastInspectionCount++;
                if (!TryCard(view.ownHand[index], out var card) || !card.IsPlayableFor(rulesFormat) || !card.IsMagic) continue;
                int cost = Math.Max(0, card.cost - Math.Min(redSymbols, card.reductionSymbols));
                if (Reserve(me) < cost) continue;
                if (card.id == AbilityRegistry.AwakenMagic)
                {
                    // The authoritative payment consumes Reserve's normal
                    // cores first. Only the remaining ordinary cores are moved
                    // by the default Awaken Magic selection; Soul stays put.
                    int available = Math.Min(3, Math.Max(0, me.reserveNormal - cost));
                    if (definition.levels.Any(level => level.cores > Cores(target)
                        && level.cores - Cores(target) <= available && level.bp + existingBonus > opposingBp))
                        return new BattleCommand { action = "playMagic", index = index, target = target.instanceId };
                }
                if (card.id == AbilityRegistry.DoubleDraw && (long)battleBp + 2000 > opposingBp)
                    return new BattleCommand { action = "playMagic", index = index, target = target.instanceId };
            }
            return null;
        }

        private bool TryCard(string id, out CardDefinition card)
        {
            card = null;
            return id != null && catalog.TryGetValue(id, out card);
        }

        private bool IsSpirit(CardDefinition card) => card.IsPlayableFor(rulesFormat)
            && string.Equals(card.type, "Spirit", StringComparison.OrdinalIgnoreCase);
        private bool IsPermanent(CardDefinition card) => card.IsPlayableFor(rulesFormat)
            && (string.Equals(card.type, "Spirit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(card.type, "Nexus", StringComparison.OrdinalIgnoreCase));
        private bool IsAttackingSpirit(SpiritView spirit) => TryCard(spirit.cardId, out var card) && IsSpirit(card);
        private static int BlockingBp(SpiritView spirit) => spirit.cardId == AbilityRegistry.Valkyrius
            ? (int)Math.Min(int.MaxValue, (long)spirit.bp + 6000) : spirit.bp;
        private static int Reserve(PublicPlayerView player) => player.reserveNormal + (player.reserveSoul ? 1 : 0);
        private static int Cores(SpiritView spirit) => spirit.normalCores + (spirit.soulCore ? 1 : 0);
    }
}
