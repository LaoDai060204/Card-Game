using System;
using System.Collections.Generic;
using System.Linq;

namespace MDPro3.BattleSpirits.Core
{
    // Card IDs are deliberate dispatch keys. Printed clauses are never inferred from prose.
    public sealed partial class BattleMatch
    {
        private readonly Dictionary<string, int> effectUses = new Dictionary<string, int>();
        private readonly Dictionary<int, int> flameSummoningTurns = new Dictionary<int, int>();
        private readonly Dictionary<int, int> flameSummoningCopies = new Dictionary<int, int>();
        private readonly Dictionary<string, int> purpleExTurns = new Dictionary<string, int>();

        private bool EffectUsed(string key) => effectUses.TryGetValue(key, out var turn) && turn == State.TurnNumber;
        private void EffectUse(string key) { effectUses[key] = State.TurnNumber; }
        private string Once(SpiritState s, string name) => s.Owner + ":" + name + ":" + s.InstanceId;
        private bool OnField(SpiritState s) => s != null && State.Players[s.Owner].Field.Contains(s);
        private bool CanTrigger(SpiritState s) => OnField(s) && !s.PendingRemoval;
        private IEnumerable<SpiritState> Own(int owner) => State.Players[owner].Field.Where(s => s.Card.IsSpirit);
        private IEnumerable<SpiritState> Opp(int owner) => Own(1 - owner);
        private static bool Purple(CardDefinition c) => c.color == CardColor.Purple || (c.colors != null && c.colors.Any(x => string.Equals(x, "Purple", StringComparison.OrdinalIgnoreCase)));
        private static bool Color(CardDefinition c, CardColor color) => c.color == color || (c.colors != null && c.colors.Any(x => string.Equals(x, color.ToString(), StringComparison.OrdinalIgnoreCase)));
        private void EYes(int owner, string prompt, Action yes)
        {
            QueueChoice(owner, prompt, 0, 1, new[] { new ChoiceOption { key = "yes", label = "Use this effect" } }, a => { if (a.Length != 0) yes(); });
        }
        private void ESelect(int owner, string prompt, IEnumerable<SpiritState> candidates, int min, int max, Action<SpiritState[]> done, bool magic = false)
        {
            var pool = candidates.Where(s => OnField(s) && (!magic || EffectCanBeMagicTarget(owner, s))).ToArray();
            if (pool.Length == 0) { done(Array.Empty<SpiritState>()); return; }
            QueueChoice(owner, prompt, Math.Min(min, pool.Length), Math.Min(max, pool.Length), pool.Select(s => new ChoiceOption { key = "spirit:" + s.InstanceId, label = s.Card.DisplayName + " (" + s.Bp + " BP, " + s.Cores.Total + " cores)", cardId = s.Card.id, instanceId = s.InstanceId }), a => done(a.Select(i => pool[i]).Where(OnField).ToArray()));
        }
        private void EDestroy(int owner, IEnumerable<SpiritState> pool, string prompt, bool magic = false, Action after = null)
        {
            ESelect(owner, prompt, pool, 1, 1, a => { foreach (var s in a) DestroySpirit(s, "Effect"); if (after != null) QueueEffect(after); }, magic);
        }
        private void ECore(SpiritState target, int count, bool nonSoul, int floor, int effectOwner, Action<bool> after = null)
        {
            if (!OnField(target)) { after?.Invoke(false); return; }
            int total = Math.Min(count, Math.Max(0, target.Cores.Total - floor));
            if (nonSoul) total = Math.Min(total, target.Cores.Normal);
            if (total <= 0) { after?.Invoke(false); return; }
            Action<bool> move = soul =>
            {
                int normal = total - (soul ? 1 : 0);
                target.Cores.Normal -= normal;
                State.Players[target.Owner].Reserve.Normal += normal;
                if (soul) { target.Cores.Soul = false; State.Players[target.Owner].Reserve.Soul = true; }
                Emit("EffectCoreRemoved", effectOwner, target.InstanceId, -1, target.Card.id, total);
                bool depleted = target.Level == 0;
                if (depleted) DepleteSpirit(target, effectOwner);
                if (after != null) QueueEffect(() => after(depleted));
            };
            bool maySoul = !nonSoul && target.Cores.Soul;
            bool mayNormal = target.Cores.Normal >= total;
            if (maySoul && mayNormal)
            {
                QueueChoice(target.Owner, "Choose the cores to move to your Reserve", 1, 1, new[] {
                    new ChoiceOption { key = "normal", label = total + " normal core(s)" },
                    new ChoiceOption { key = "soul", label = "Soul Core" + (total > 1 ? " and " + (total - 1) + " normal core(s)" : "") }
                }, a => move(a[0] == 1));
            }
            else move(maySoul && !mayNormal);
        }
        private void ECoreTargets(int owner, SpiritState[] targets, int count, bool normalOnly, int floor, Action<bool> done = null)
        {
            bool any = false;
            Action<int> next = null;
            next = i => { if (i >= targets.Length) { done?.Invoke(any); return; } ECore(targets[i], count, normalOnly, floor, owner, d => { any |= d; next(i + 1); }); };
            next(0);
        }
        private void EBottom(int owner, int count, string prompt, Func<CardDefinition, bool> eligible = null, bool optional = false, Action<int> done = null)
        {
            var p = State.Players[owner];
            var indices = Enumerable.Range(0, p.Hand.Count).Where(i => eligible == null || eligible(GetCard(p.Hand[i]))).ToArray();
            int take = Math.Min(count, indices.Length);
            if (take == 0) { done?.Invoke(0); return; }
            QueueChoice(owner, prompt, optional ? 0 : take, take, indices.Select(i => new ChoiceOption { key = "hand:" + i, index = i, cardId = p.Hand[i], label = GetCard(p.Hand[i]).DisplayName }), selected =>
            {
                var originals = selected.Select(i => indices[i]).ToArray();
                var ids = originals.Select(i => p.Hand[i]).ToArray();
                foreach (int i in originals.OrderByDescending(i => i)) p.Hand.RemoveAt(i);
                p.Deck.AddRange(ids);
                foreach (var id in ids) Emit("RevealedToBottom", owner, -1, -1, id);
                done?.Invoke(ids.Length);
            }, true);
        }
        private void EReveal(int owner, Func<CardDefinition, bool> eligible, bool toHand, bool optional, Action<CardDefinition> after = null)
        {
            var p = State.Players[owner];
            var cards = p.Deck.Take(3).ToArray();
            p.Deck.RemoveRange(0, cards.Length);
            p.Revealed.AddRange(cards);
            foreach (string id in cards) Emit("Reveal", owner, -1, -1, id);
            Action<int> finishPick = chosen =>
            {
                CardDefinition picked = chosen >= 0 ? GetCard(cards[chosen]) : null;
                if (chosen >= 0) { p.Revealed.Remove(cards[chosen]); if (toHand) p.Hand.Add(cards[chosen]); else p.Discard.Add(cards[chosen]); }
                var remaining = Enumerable.Range(0, cards.Length).Where(i => i != chosen).ToArray();
                Action<int[]> finishOrder = order =>
                {
                    p.Deck.AddRange(order.Select(i => cards[remaining[i]]));
                    foreach (var i in remaining) p.Revealed.Remove(cards[i]);
                    after?.Invoke(picked);
                };
                if (remaining.Length <= 1) finishOrder(Enumerable.Range(0, remaining.Length).ToArray());
                else QueueChoice(owner, "Order the revealed cards on the bottom of your Deck (first selected goes first)", remaining.Length, remaining.Length, remaining.Select(i => new ChoiceOption { key = "revealed:" + i, index = i, cardId = cards[i], label = GetCard(cards[i]).DisplayName }), finishOrder, true);
            };
            var choices = Enumerable.Range(0, cards.Length).Where(i => eligible(GetCard(cards[i]))).ToArray();
            if (choices.Length == 0) { finishPick(-1); return; }
            QueueChoice(owner, toHand ? "Add one revealed eligible card to your Hand" : "Discard one revealed eligible card", optional ? 0 : 1, 1, choices.Select(i => new ChoiceOption { key = "revealed:" + i, index = i, cardId = cards[i], label = GetCard(cards[i]).DisplayName }), a => finishPick(a.Length == 0 ? -1 : choices[a[0]]));
        }
        private void ERevive(int owner, bool free)
        {
            var p = State.Players[owner];
            var candidates = p.Discard.Select((id, index) => new { id, index, card = GetCard(id) }).Where(x => x.card.IsSpirit && x.card.cost <= 3 && x.card.HasFamily("Dark Purple")).ToArray();
            if (candidates.Length == 0) return;
            QueueChoice(owner, free ? "Summon a cost 3 or less Dark Purple Spirit without paying its cost" : "Summon a cost 3 or less Dark Purple Spirit (pay its summon cost)", 0, 1, candidates.Select(x => new ChoiceOption { key = "trash:" + x.index, index = x.index, cardId = x.id, label = x.card.DisplayName }), a => { if (a.Length != 0) EffectSummonFromZone(owner, candidates[a[0]].id, "trash", free); });
        }

        internal void EffectSummoned(SpiritState s, PaymentContext payment)
        {
            int o = s.Owner;
            switch (s.Card.id)
            {
                case "26RBS03-004": EDestroy(o, Opp(o).Where(x => x.Bp <= (payment.SoulPaid ? 4000 : 3000)), "Lumius: destroy an opposing Spirit"); break;
                case "26RBS03-X01": EDestroy(o, Opp(o).Where(x => x.Bp <= 4000 + 2000 * payment.FamilyEx), "Ritenia: destroy an opposing Spirit"); break;
                case "26RBS03-010": Draw(o); break;
                case "26RBS03-012": ESelect(o, "Garudi: reduce a Spirit with at most 3 cores to 1 core", Opp(o).Where(x => x.Cores.Total <= 3), 1, 1, a => ECoreTargets(o, a, int.MaxValue, false, 1)); break;
                case "26RBS01-X04": EReveal(o, Purple, false, false); break;
                case "26RBS01-024": ERevive(o, false); break;
                case "26RBS03-X04": EYes(o, "Rindorin: reveal the top three cards?", () => EReveal(o, c => c.IsSpirit, false, false, c => { if (c != null && c.HasFamily("Dark Purple")) ESelect(o, "Rindorin: remove one opposing core", Opp(o), 1, 1, a => ECoreTargets(o, a, 1, false, 0)); })); break;
                case "26RBS03-X05": ERevive(o, true); break;
                case "26RSD02-X01": ESelect(o, "Nefira: leave one core on an opposing Spirit", Opp(o), 1, 1, a => ECoreTargets(o, a, int.MaxValue, false, 1)); break;
                case "26RBS01-X06": ESelect(o, "Danpirion: remove a core from up to three opposing Spirits", Opp(o), 0, 3, a => ECoreTargets(o, a, 1, false, 0)); break;
                case "26RBS03-X06": ESelect(o, "Desperado: remove a core from three other Spirits", Own(o).Concat(Opp(o)).Where(x => x != s), 3, 3, a => ECoreTargets(o, a, 1, false, 0, any => { if (any && OnField(s)) { s.Cores.Normal++; State.Players[o].CoreGains++; Emit("VoidCore", o, s.InstanceId, -1, s.Card.id, 1); } })); break;
                case "26RSD03-002": State.Players[o].Trash.Normal++; State.Players[o].CoreGains++; Emit("VoidCoreToTrash", o, s.InstanceId, -1, s.Card.id, 1); break;
                case "26RSD03-003": ESelect(o, "Rashead: exhaust an opposing Spirit with cost 4 or less", Opp(o).Where(x => x.Card.cost <= 4), 0, 1, a => { foreach (var x in a) ExhaustSpirit(x, o); }); break;
                case "26RSD10-003": QueueChoice(o, "Sharky: place the core from the Void", 1, 1, new[] { new ChoiceOption { key = "self", label = "Onto Sharky", instanceId = s.InstanceId }, new ChoiceOption { key = "reserve", label = "Into your Reserve" } }, a => { if (a[0] == 0 && OnField(s)) { s.Cores.Normal++; State.Players[o].CoreGains++; } else AddReserve(o, 1); }); break;
                case "26RSD03-X02": ESelect(o, "Dantecreo: Armored Fish Spirits gain 3000 BP", Own(o).Where(x => x.Card.HasFamily("Armored Fish")), 0, 2, a => { foreach (var x in a) x.TurnBpBonus += 3000; }); break;
                case "26RBS02-034": s.Cores.Normal++; State.Players[o].CoreGains++; Emit("VoidCore", o, s.InstanceId, -1, s.Card.id, 1); break;
                case "26RBS02-033": ESelect(o, "Daisy: exhaust or heavy exhaust an opposing Spirit", Opp(o), 1, 1, a => { foreach (var x in a) { if (x.Exhausted) EHeavy(x); else ExhaustSpirit(x, o); } }); break;
                case "26RCB01-041": EReveal(o, c => c.IsSpirit && c.HasFamily("EX-AID") && c.DisplayName != s.Card.DisplayName, true, false); break;
                case "26RCB01-060": EBottom(o, 2, "Genm Corporation: reveal and return up to two EX-AID cards", c => c.HasFamily("EX-AID"), true, n => Draw(o, n)); break;
            }
        }

        private sealed class TriggeredEffect
        {
            public string Label, CardId;
            public int InstanceId;
            public Action Resolve;
        }
        private static void AddTrigger(List<TriggeredEffect> effects, SpiritState source, string timing, Action resolve)
            => effects.Add(new TriggeredEffect { Label = source.Card.DisplayName + ": " + timing, CardId = source.Card.id, InstanceId = source.InstanceId, Resolve = resolve });
        private void QueueTriggers(List<TriggeredEffect> triggers)
        {
            if (triggers.Count == 0) return;
            if (triggers.Count == 1) { QueueEffect(triggers[0].Resolve); return; }
            QueueEffect(() => ChooseNextTrigger(triggers));
        }
        private void ChooseNextTrigger(List<TriggeredEffect> triggers)
        {
            if (triggers.Count == 1) { QueueEffect(triggers[0].Resolve); return; }
            QueueChoice(State.ActivePlayerIndex, "Choose the next simultaneous effect to resolve", 1, 1,
                triggers.Select((t, i) => new ChoiceOption { key = "trigger:" + i, label = t.Label, cardId = t.CardId, instanceId = t.InstanceId }), selected =>
                {
                    var next = triggers[selected[0]]; triggers.RemoveAt(selected[0]);
                    QueueEffect(next.Resolve); QueueEffect(() => ChooseNextTrigger(triggers));
                });
        }
        internal void EffectAttacked(SpiritState s)
        {
            int o = s.Owner;
            ELevelUp(s); // Continuous level changes precede triggered effects.
            var triggers = new List<TriggeredEffect>();
            foreach (var supporter in Own(o).Where(x => x.Card.id == "26RBS03-014" && x.Level >= 2 && !x.PendingRemoval).ToArray())
                if (s.Card.HasFamily("Dark Purple")) AddTrigger(triggers, supporter, "Dark Purple attack", () => { if (CanTrigger(supporter)) ESelect(o, "Irurion: remove a core, leaving at least one", Opp(o).Where(x => x.Card.cost <= 6), 1, 1, a => ECoreTargets(o, a, 1, false, 1)); });
            switch (s.Card.id)
            {
                case "26RSD09-004": if (s.Level >= 2 && State.Players[o].Hand.Count <= 5) AddTrigger(triggers, s, "draw", () => Draw(o)); break;
                case "26RBS03-X03": AddTrigger(triggers, s, "Soul Awakening", () => ESoulAwakening(s)); break;
                case "26RBS01-X04": if (s.Level >= 2) AddTrigger(triggers, s, "Spirit Manipulation", () => ESelect(o, "Shi Rezen: activate a Spirit Manipulation effect", Own(o).Where(x => x.Card.id == "26RBS01-024" && !x.PendingRemoval), 0, 1, a => { if (a.Length != 0) ERevive(o, false); })); break;
                case "26RBS01-024": if (s.Level >= 2) AddTrigger(triggers, s, "destroy an exhausted Spirit", () => EDestroy(o, Opp(o).Where(x => x.Exhausted && x.Card.cost <= 3), "Papirioni: destroy an exhausted Spirit with cost 3 or less")); break;
                case "26RSD02-X01": if (s.Level >= 2) AddTrigger(triggers, s, "remove a normal core", () => ESelect(o, "Nefira: remove a normal core", Opp(o), 1, 1, a => ECoreTargets(o, a, 1, true, 0, d => { if (d) Draw(o); }))); break;
                case "26RBS03-X06": if (s.Level >= 2) AddTrigger(triggers, s, "remove opposing normal cores", () => ESelect(o, "Desperado: leave one normal core on a cost 5 or less Spirit", Opp(o).Where(x => x.Card.cost <= 5), 1, 1, a => { foreach (var x in a) ECore(x, Math.Max(0, x.Cores.Normal - 1), true, 0, o); })); break;
                case "26RBS03-022": AddTrigger(triggers, s, "remove a core and check Reserve", () => ESelect(o, "Pisketona: remove one opposing core", Opp(o), 1, 1, a => ECoreTargets(o, a, 1, false, 0, d => { if (State.Players[1 - o].Reserve.Total >= 3) Draw(o); }))); break;
                case "26RBS02-X09": AddTrigger(triggers, s, "exhaust and refresh", () => ELeviaclanAttack(s)); break;
                case "26RCB01-047": if (s.Level >= 2 && Own(o).Any(x => x != s && x.Card.HasFamily("EX-AID"))) AddTrigger(triggers, s, "draw two and return two", () => { Draw(o, 2); EBottom(o, 2, "Brave: return two cards to the bottom of your Deck"); }); break;
                case "26RCB01-052": if (s.Level >= 2 && Own(o).Any(x => x != s && x.Card.HasFamily("EX-AID"))) AddTrigger(triggers, s, "destroy a cost 3 Spirit", () => EDestroy(o, Opp(o).Where(x => x.Card.cost <= 3), "Maximum Gamer: destroy a cost 3 or less Spirit")); break;
                case "26RCB01-X06": if (s.Level >= 2) AddTrigger(triggers, s, "destroy a cost 5 Spirit", () => EDestroy(o, Opp(o).Where(x => x.Card.cost <= 5), "Para-DX: destroy a cost 5 or less Spirit")); break;
                case "26RCB01-X07": if (s.Level >= 2) AddTrigger(triggers, s, "exhaust to refresh", () => EMuteki(s)); break;
            }
            AddSimulationTriggers(s, triggers); QueueTriggers(triggers);
        }
        internal void EffectBlocked(SpiritState blocker)
        {
            ELevelUp(blocker);
            var triggers = new List<TriggeredEffect>();
            var attacker = FindSpirit(State.PendingAttackerId);
            if (attacker != null && attacker.Card.id == "26RBS01-X03" && attacker.Level >= 2 && blocker.Bp <= 5000) AddTrigger(triggers, attacker, "refresh after block", () => ERefresh(attacker));
            if (blocker.Card.id == "26RBS03-X05" && blocker.Level >= 2 && attacker != null) AddTrigger(triggers, blocker, "remove attacker's normal core", () => ECore(attacker, 1, true, 0, blocker.Owner));
            if (blocker.Card.id == "26RCB01-X07" && blocker.Level >= 2) AddTrigger(triggers, blocker, "exhaust to refresh", () => EMuteki(blocker));
            AddSimulationTriggers(blocker, triggers); QueueTriggers(triggers);
        }
        private void ELevelUp(SpiritState s)
        {
            if ((s.Card.id == "26RCB01-039" || s.Card.id == "26RCB01-043" || s.Card.id == "26RCB01-047" || s.Card.id == "26RCB01-X06") && State.Players[s.Owner].AttacksOrBlocksThisTurn == 1) s.BattleLevelOverride = Math.Max(2, s.BattleLevelOverride);
        }
        private void AddSimulationTriggers(SpiritState participant, List<TriggeredEffect> triggers)
        {
            if (!participant.Card.HasFamily("EX-AID")) return;
            foreach (var s in Own(participant.Owner).Where(x => x.Card.id == "26RCB01-051" && !x.Exhausted && !x.PendingRemoval).ToArray())
                AddTrigger(triggers, s, "exhaust to destroy two", () => { if (!CanTrigger(s) || s.Exhausted) return; EYes(s.Owner, "Simulation Gamer: exhaust this Spirit to destroy two cost 4 or less Spirits?", () => { ExhaustSpirit(s, s.Owner); ESelect(s.Owner, "Simulation Gamer: destroy two Spirits", Opp(s.Owner).Where(x => x.Card.cost <= 4), 2, 2, a => DestroySpirits(a, "Effect")); }); });
        }
        private void ESoulAwakening(SpiritState s)
        {
            string key = s.Owner + ":SoulAwakening";
            if (EffectUsed(key) || s.Cores.Soul) return;
            var sources = State.Players[s.Owner].Field.Where(x => x != s && x.Cores.Soul).ToArray();
            bool trash = State.Players[s.Owner].Trash.Soul;
            if (!trash && sources.Length == 0) return;
            var options = sources.Select(x => new ChoiceOption { key = "spirit:" + x.InstanceId, instanceId = x.InstanceId, label = "Move Soul Core from " + x.Card.DisplayName }).ToList();
            if (trash) options.Add(new ChoiceOption { key = "trash", label = "Move Soul Core from Trash" });
            QueueChoice(s.Owner, "Soul Awakening: move your Soul Core onto this Spirit and refresh it", 0, 1, options, a =>
            {
                if (a.Length == 0 || !OnField(s)) return;
                if (a[0] < sources.Length) { sources[a[0]].Cores.Soul = false; if (sources[a[0]].Level == 0) DepleteSpirit(sources[a[0]], s.Owner); }
                else State.Players[s.Owner].Trash.Soul = false;
                s.Cores.Soul = true; ERefresh(s); EffectUse(key);
            });
        }
        private void EMuteki(SpiritState s)
        {
            if (s.Level < 2) return;
            ESelect(s.Owner, "Muteki: exhaust an EX-AID Spirit to refresh this Spirit", Own(s.Owner).Where(x => x.Card.HasFamily("EX-AID") && !x.Exhausted), 0, 1, a => { if (a.Length > 0) { ExhaustSpirit(a[0], s.Owner); ERefresh(s); } });
        }
        private void ELeviaclanAttack(SpiritState s)
        {
            string key = Once(s, "attack");
            if (EffectUsed(key)) return;
            ESelect(s.Owner, "Leviaclan: exhaust an opposing Spirit", Opp(s.Owner), 0, 1, a =>
            {
                // This optional activation is once per turn even when the first selection is skipped.
                if (a.Length == 0) { ESelect(s.Owner, "Leviaclan: refresh one of your Spirits", Own(s.Owner), 0, 1, b => { if (b.Length > 0) { EffectUse(key); ERefresh(b[0]); } }); return; }
                EffectUse(key); ExhaustSpirit(a[0], s.Owner);
                ESelect(s.Owner, "Leviaclan: refresh one of your Spirits", Own(s.Owner), 0, 1, b => { foreach (var x in b) ERefresh(x); });
            });
        }
        private void ERefresh(SpiritState s) { if (!OnField(s)) return; s.Exhausted = s.HeavyExhausted; s.HeavyExhausted = false; Emit("Refreshed", s.Owner, s.InstanceId, -1, s.Card.id); }
        private void EHeavy(SpiritState s) { if (!OnField(s)) return; s.Exhausted = true; s.HeavyExhausted = true; Emit("HeavyExhausted", 1 - s.Owner, s.InstanceId, -1, s.Card.id); }

        private bool EffectHasDestructionTrigger(SpiritState s, int level)
        {
            switch (s.Card.id)
            {
                case "26RBS03-014": return true;
                case "26RSD09-011": case "26RSD02-001": case "26RBS02-034": return level >= 2;
                case "26RBS03-009": return level >= 2 && State.PendingAttackerId == s.InstanceId;
                default: return false;
            }
        }
        internal void EffectDestroyed(SpiritState s, int levelAtDestruction, string reason)
        {
            int o = s.Owner;
            switch (s.Card.id)
            {
                case "26RSD09-011": if (levelAtDestruction >= 2) EDestroy(o, Opp(o).Where(x => x.Bp <= 5000), "Gorge of Mighty Winds: destroy a Spirit with 5000 BP or less"); break;
                case "26RBS03-009": if (levelAtDestruction >= 2 && State.PendingAttackerId == s.InstanceId) EDestroy(o, Opp(o).Where(x => x.InstanceId == State.PendingBlockerId && x.Card.cost <= 6), "Nidori: destroy the Spirit blocking this Spirit"); break;
                case "26RSD02-001": if (levelAtDestruction >= 2) ESelect(o, "Sukurusu: remove one normal core", Opp(o), 1, 1, a => ECoreTargets(o, a, 1, true, 0)); break;
                case "26RBS03-014": Draw(o); break;
                case "26RBS02-034": if (levelAtDestruction >= 2) ESelect(o, "Buzzsorn: exhaust an opposing Spirit", Opp(o), 0, 1, a => { foreach (var x in a) ExhaustSpirit(x, o); }); break;
            }
        }
        internal void EffectStep(MatchPhase phase, int activePlayer)
        {
            purpleExTurns.Clear();
            if (phase != MatchPhase.End) return;
            var triggers = new List<TriggeredEffect>();
            foreach (var s in State.Players[activePlayer].Field.ToArray())
            {
                if (!CanTrigger(s) || s.Level < 2) continue;
                switch (s.Card.id)
                {
                    case "26RBS03-X01": if (State.Players[s.Owner].Trash.Normal > 0) AddTrigger(triggers, s, "recover a Trash core", () => { if (CanTrigger(s)) EYes(s.Owner, "Ritenia: place one normal core from your Trash onto this Spirit?", () => { if (OnField(s) && State.Players[s.Owner].Trash.Normal > 0) { State.Players[s.Owner].Trash.Normal--; s.Cores.Normal++; } }); }); break;
                    case "26RBS02-033": if (Opp(s.Owner).Count(x => !x.Exhausted) <= 1) AddTrigger(triggers, s, "opponent chooses heavy exhaustion", () => { if (CanTrigger(s)) ESelect(1 - s.Owner, "Daisy: choose one of your exhausted Spirits to heavy exhaust", Opp(s.Owner).Where(x => x.Exhausted && !x.HeavyExhausted), 1, 1, a => { foreach (var x in a) EHeavy(x); }); }); break;
                    case "26RCB01-060": AddTrigger(triggers, s, "refresh an EX-AID Spirit", () => { if (CanTrigger(s)) ESelect(s.Owner, "Genm Corporation: refresh one EX-AID Spirit", Own(s.Owner).Where(x => x.Card.HasFamily("EX-AID")), 0, 1, a => { foreach (var x in a) ERefresh(x); }); }); break;
                }
            }
            QueueTriggers(triggers);
        }
        internal int EffectContinuousBp(SpiritState s)
        {
            if (s.InstanceId != State.PendingAttackerId) return 0;
            int result = 0;
            if (s.Card.id == "26RSD09-004") result += 2000;
            if ((s.Card.id == "26RSD03-003" || s.Card.id == "26RSD10-004") && s.Level >= 2) result += 2000;
            if (State.ActivePlayerIndex == s.Owner)
            {
                foreach (var n in State.Players[s.Owner].Field.Where(x => x.Level > 0))
                {
                    if (n.Card.id == "26RSD09-011" && Color(s.Card, CardColor.Red)) result += 2000;
                    if (n.Card.id == "26RSD03-010" && s.Card.HasFamily("Armored Fish")) result += 1000;
                }
            }
            return result;
        }
        internal bool EffectCanAttack(SpiritState s) => s.Card.id != "26RSD09-001" || s.Cores.Soul;
        internal bool EffectMustBlock(SpiritState s) => s != null && s.Card.id == "26RBS01-X03";
        internal bool EffectCanBeMagicTarget(int actor, SpiritState s)
        {
            return s.Owner == actor || s.Card.id != "26RCB01-X07" || !IsAttackStep() || !Own(s.Owner).All(x => x.Card.HasFamily("EX-AID"));
        }
        private bool IsAttackStep() => State.Phase == MatchPhase.Attack || State.Phase == MatchPhase.FlashBeforeBlock || State.Phase == MatchPhase.Block || State.Phase == MatchPhase.FlashAfterBlock;
        internal int EffectAttackDamage(SpiritState attacker, int ordinaryDamage) => ordinaryDamage + (attacker.Card.id == "26RBS01-X06" && attacker.Level >= 2 && State.Players[attacker.Owner].OpponentDepletedThisTurn ? 1 : 0);
        internal void EffectLifeLost(int owner, int count) { if (count > 0) State.Players[owner].LifeReducedThisTurn = true; }
        internal void EffectDepleted(SpiritState s, int effectOwner) { if (effectOwner >= 0 && effectOwner != s.Owner) State.Players[effectOwner].OpponentDepletedThisTurn = true; }
        internal void EffectAttackLifeLost(SpiritState attacker, int count)
            => EffectSpiritLifeLost(attacker, count, true);
        private void EffectSpiritLifeLost(SpiritState attacker, int count, bool byAttack)
        {
            if (count <= 0) return;
            int o = attacker.Owner;
            var triggers = new List<TriggeredEffect>();
            if (flameSummoningTurns.TryGetValue(attacker.InstanceId, out var turn) && turn == State.TurnNumber)
                for (int i = 0; i < flameSummoningCopies[attacker.InstanceId]; i++) AddTrigger(triggers, attacker, "Flame Summoning grant " + (i + 1), () => EDestroy(o, State.Players[1 - o].Field.Where(x => x.Card.IsNexus && x.Card.cost <= 4), "Flame Summoning: destroy a cost 4 or less Nexus"));
            if (byAttack && State.ActivePlayerIndex == o && attacker.Card.HasFamily("EX-AID"))
            {
                var poppy = Own(o).FirstOrDefault(x => x.Card.id == "26RCB01-046" && !x.PendingRemoval && x.Card.DisplayName != attacker.Card.DisplayName);
                string key = o + ":PoppyUnstackable";
                if (poppy != null && !EffectUsed(key)) { EffectUse(key); AddTrigger(triggers, poppy, "draw two and return one", () => { Draw(o, 2); EBottom(o, 1, "Poppy: return one card to the bottom of your Deck"); }); }
            }
            QueueTriggers(triggers);
        }
        internal void EffectBattleEnded(SpiritState attacker)
        {
            if (attacker != null && attacker.Card.id == "26RBS03-X03" && OnField(attacker) && attacker.Level >= 2)
            {
                string key = attacker.Owner + ":ZekugaName";
                if (!EffectUsed(key))
                {
                    EffectUse(key); int before = State.Players[1 - attacker.Owner].Life; DamageLife(1 - attacker.Owner, 1);
                    EffectSpiritLifeLost(attacker, before - State.Players[1 - attacker.Owner].Life, false);
                }
            }
        }
        internal void EffectExhausted(SpiritState target, int effectOwner)
        {
            if (effectOwner < 0 || State.ActivePlayerIndex != effectOwner) return;
            var triggers = new List<TriggeredEffect>();
            foreach (var s in Own(effectOwner).Where(x => x.Card.id == "26RBS02-X09" && x.Level >= 2 && !x.PendingRemoval).ToArray())
            {
                string key = Once(s, "heavy");
                if (!EffectUsed(key)) AddTrigger(triggers, s, "heavy exhaust an opposing Spirit", () => { if (EffectUsed(key) || !CanTrigger(s)) return; ESelect(effectOwner, "Leviaclan: heavy exhaust an opposing exhausted Spirit", Opp(effectOwner).Where(x => x.Exhausted), 0, 1, a => { if (a.Length > 0) { EffectUse(key); EHeavy(a[0]); } }); });
            }
            QueueTriggers(triggers);
        }
        internal int EffectExSymbols(int owner, string cardId, int discardIndex)
        {
            int ex = GetCard(cardId).ExSymbols;
            if (purpleExTurns.TryGetValue(owner + ":" + discardIndex, out var turn) && turn == State.TurnNumber && IsMainPhase(State.Phase)) ex = Math.Min(GetCard(cardId).symbols, ex + 1);
            return ex;
        }
        internal void EffectTrashRemoved(int owner, int index)
        {
            var prefix = owner + ":";
            var shifted = purpleExTurns.Where(p => p.Key.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            foreach (var p in shifted) purpleExTurns.Remove(p.Key);
            foreach (var p in shifted)
            {
                int old = int.Parse(p.Key.Substring(prefix.Length));
                if (old != index) purpleExTurns[prefix + (old > index ? old - 1 : old)] = p.Value;
            }
        }
        internal int EffectExtraReduction(int owner, CardDefinition card)
        {
            return card.id == "26RBS03-022" && State.Players[owner].Discard.All(id => Purple(GetCard(id))) ? State.Players[1 - owner].Reserve.Total : 0;
        }
        internal int EffectFChangeCost(int owner, CardDefinition card) => card.id == "26RCB01-X06" && (State.Players[owner].Life <= 3 || State.Players[1 - owner].Life <= 3) ? 5 : card.cost;

        internal bool EffectCanPlayMagic(int owner, CardDefinition card, BattleCommand command, out string error)
        {
            error = "";
            bool main = State.Phase == MatchPhase.Main || State.Phase == MatchPhase.SecondMain;
            bool hasMain = AbilityRegistry.HasMainMagic(card.id);
            if (command.mode == 1 && (!main || !hasMain)) { error = "This card has no Main effect available now."; return false; }
            if (command.mode != 0 && command.mode != 1) { error = "Select Flash (mode 0) or Main (mode 1)."; return false; }
            return true;
        }
        internal void EffectPlayMagic(int owner, CardDefinition card, BattleCommand command, PaymentContext payment)
        {
            bool main = command.mode == 1;
            switch (card.id)
            {
                case "26RSD01-012": if (main) EDestroy(owner, State.Players[1 - owner].Field.Where(x => x.Card.IsNexus && !(x.Card.superUnleash && x.Level >= 2)), "Break Claw: destroy a Nexus outside Super Unleash", true, () => { if (Own(owner).Any(x => x.Exhausted && Color(x.Card, CardColor.Red)) && State.Players[owner].Trash.Normal > 0) { State.Players[owner].Trash.Normal--; State.Players[owner].Reserve.Normal++; } }); else EBuff(owner, 3000); break;
                case "26RSD09-012": if (main) Draw(owner, 2); else EBuff(owner, 2000); break;
                case "26RBS02-087": ESelect(owner, "Flame Summoning: select a red Spirit", Own(owner).Where(x => Color(x.Card, CardColor.Red)), 1, 1, a => { foreach (var x in a) { x.TurnBpBonus += 3000 * Own(owner).Count(y => y.Exhausted); int copies = flameSummoningTurns.TryGetValue(x.InstanceId, out int turn) && turn == State.TurnNumber ? flameSummoningCopies[x.InstanceId] : 0; flameSummoningTurns[x.InstanceId] = State.TurnNumber; flameSummoningCopies[x.InstanceId] = copies + 1; } }, true); break;
                case "26RBS03-087": EFlameStorm(owner); break;
                case "26RSD01-014": EDestroy(owner, Opp(owner).Where(x => x.Bp <= (State.Players[owner].LifeReducedThisTurn ? 10000 : 7000)), "Flame Hurricane: destroy an opposing Spirit", true); break;
                case "26RSD02-014": ESelect(owner, "Soul Bite: remove two opposing cores", Opp(owner), 1, 1, a => ECoreTargets(owner, a, 2, false, 0), true); break;
                case "26RBS03-089": ESelect(owner, "Grand Theft: destroy Spirits with exactly one core", Opp(owner).Where(x => x.Cores.Total == 1), State.Players[owner].LifeReducedThisTurn ? 2 : 1, State.Players[owner].LifeReducedThisTurn ? 2 : 1, a => DestroySpirits(a, "Magic"), true); break;
                case "26RBS03-097": EDestroy(owner, Opp(owner).Where(x => x.Card.cost <= (State.Players[owner].LifeReducedThisTurn ? 7 : 4)), "Sharp Thorn: destroy an opposing Spirit", true); break;
                case "26RSD03-012": if (main) EBottom(owner, State.Players[owner].Hand.Count, "Hand Molt: return your Hand in the chosen order", null, false, n => { if (n > 0) Draw(owner, State.Players[1 - owner].Hand.Count); }); else EBuff(owner, 2000); break;
                case "26RSD03-013": ESelect(owner, "Vortex Shave: exhaust a Spirit", Opp(owner), 1, 1, a => { foreach (var x in a) ExhaustSpirit(x, owner); }, true); break;
                case "26RSD03-014": ESelect(owner, "Tentacle Attack: heavy exhaust a Spirit", Opp(owner), 1, 1, a => { foreach (var x in a) EHeavy(x); }, true); break;
                case "26RBS03-091": ESelect(owner, "Bubble Shield: exhaust a Spirit", Opp(owner), 1, 1, a => { foreach (var x in a) ExhaustSpirit(x, owner); if (State.ActivePlayerIndex != owner && IsAttackStep()) ESelect(owner, "Bubble Shield: exhaust a cost 3 or less Spirit", Opp(owner).Where(x => x.Card.cost <= 3), 1, 1, b => { foreach (var x in b) ExhaustSpirit(x, owner); }, true); }, true); break;
                case "26RSD06-012": if (main) { Draw(owner, 3); EBottom(owner, 2, "Feeding Draw: return two cards in any order"); } else EBuff(owner, 3000); break;
                case "26RSD06-013": ESelect(owner, "Stem Lance: optionally destroy one of your Nexuses to increase the cost limit", State.Players[owner].Field.Where(x => x.Card.IsNexus), 0, 1, a => { int limit = 4; if (a.Length > 0) { limit += a[0].Card.cost; DestroySpirit(a[0], "Magic"); } EDestroy(owner, Opp(owner).Where(x => x.Card.cost <= limit), "Stem Lance: destroy an opposing Spirit", true); }, true); break;
                case "26RCB01-071": if (State.Players[owner].Field.All(x => x.Card.HasFamily("EX-AID"))) EDestroy(owner, Opp(owner).Where(x => x.Card.cost <= 3 + State.Players[owner].Field.Count), "Mighty Critical Strike: destroy an opposing Spirit", true); break;
                case "26RBS01-126": EDestroy(owner, Opp(owner).Where(x => x.Card.cost <= 5), "Merciless Blow: destroy a cost 5 or less Spirit", true, () => { if (State.Players[owner].LifeReducedThisTurn) EDestroy(owner, Opp(owner).Where(x => x.Card.cost <= 3), "Merciless Blow: destroy another cost 3 or less Spirit", true); }); break;
            }
        }
        private void EBuff(int owner, int amount) => ESelect(owner, "Select a Spirit to gain " + amount + " BP this turn", Own(owner), 1, 1, a => { foreach (var x in a) x.TurnBpBonus += amount; }, true);
        private void EFlameStorm(int owner)
        {
            int limit = State.Players[owner].LifeReducedThisTurn ? 10000 : 7000;
            var pool = Opp(owner).Where(x => EffectCanBeMagicTarget(owner, x) && x.Bp <= limit).ToArray();
            var groups = new List<SpiritState[]>();
            for (int i = 0; i < pool.Length; i++)
            {
                groups.Add(new[] { pool[i] });
                for (int j = i + 1; j < pool.Length; j++)
                {
                    if ((long)pool[i].Bp + pool[j].Bp <= limit) groups.Add(new[] { pool[i], pool[j] });
                    for (int k = j + 1; k < pool.Length; k++) if ((long)pool[i].Bp + pool[j].Bp + pool[k].Bp <= limit) groups.Add(new[] { pool[i], pool[j], pool[k] });
                }
            }
            if (groups.Count == 0) return;
            QueueChoice(owner, "Flame Storm: choose up to three Spirits with combined BP at most " + limit, 0, 1, groups.Select((g, i) => new ChoiceOption { key = "group:" + i, label = string.Join(" + ", g.Select(x => x.Card.DisplayName)) + " (" + g.Sum(x => x.Bp) + " BP)" }), a => { if (a.Length > 0) DestroySpirits(groups[a[0]].Where(OnField), "Magic"); });
        }

        internal bool EffectCanActivate(int owner, BattleCommand command)
        {
            var s = FindSpirit(command.source);
            if (s == null || s.Owner != owner || s.PendingRemoval || EffectUsed(Once(s, "activate"))) return false;
            bool main = State.ActivePlayerIndex == owner && IsMainPhase(State.Phase);
            switch (s.Card.id)
            {
                case "26RBS03-X04": return main && s.Level >= 2 && State.Players[owner].Discard.Any(id => { var c = GetCard(id); return c.IsSpirit && Purple(c) && c.symbolDetails.Any(z => !z.isEx && string.Equals(z.color, "Purple", StringComparison.OrdinalIgnoreCase) && z.count > 0); });
                case "26RSD03-X02": return State.ActivePlayerIndex == owner && (State.Phase == MatchPhase.FlashBeforeBlock || State.Phase == MatchPhase.FlashAfterBlock) && s.Level >= 2 && Own(owner).Any(x => x != s && x.Card.HasFamily("Armored Fish"));
                case "26RSD03-010": return main && s.Level >= 2 && State.Players[owner].Hand.Any(id => GetCard(id).HasFamily("Armored Fish") && GetCard(id).cost <= 4);
                case "26RCB01-052": return main && s.Exhausted && !EffectUsed(owner + ":MaximumFChange") && State.Players[owner].Hand.Any(id => { var c = GetCard(id); return c.IsSpirit && c.DisplayName.IndexOf("Ex-Aid", StringComparison.OrdinalIgnoreCase) >= 0 && c.DisplayName != s.Card.DisplayName && (c.cost == 7 || EffectFChangeCost(owner, c) == 7); });
                default: return false;
            }
        }
        internal bool EffectActivate(int owner, BattleCommand command, out string error)
        {
            error = "";
            if (!EffectCanActivate(owner, command)) { error = "This card's activation is unavailable in the current state."; return false; }
            var s = FindSpirit(command.source);
            if (s == null || s.Owner != owner) { error = "Select one of your cards."; return false; }
            string key = Once(s, "activate");
            bool main = State.ActivePlayerIndex == owner && IsMainPhase(State.Phase);
            switch (s.Card.id)
            {
                case "26RBS03-X04":
                    if (!main || s.Level < 2 || EffectUsed(key)) break;
                    var purple = State.Players[owner].Discard.Select((id, i) => new { id, i, c = GetCard(id) }).Where(x => x.c.IsSpirit && Purple(x.c) && x.c.symbolDetails.Any(z => !z.isEx && string.Equals(z.color, "Purple", StringComparison.OrdinalIgnoreCase) && z.count > 0)).ToArray();
                    if (purple.Length == 0) break;
                    EffectUse(key);
                    QueueChoice(owner, "Rindorin: treat one purple symbol in your Trash as purple EX during this Main Step", 1, 1, purple.Select(x => new ChoiceOption { key = "trash:" + x.i, index = x.i, cardId = x.id, label = x.c.DisplayName }), a => purpleExTurns[owner + ":" + purple[a[0]].i] = State.TurnNumber);
                    return true;
                case "26RSD03-X02":
                    if (State.ActivePlayerIndex != owner || !IsAttackStep() || s.Level < 2 || EffectUsed(key) || !Own(owner).Any(x => x != s && x.Card.HasFamily("Armored Fish"))) break;
                    EffectUse(key); ESelect(owner, "Dantecreo: refresh another Armored Fish Spirit", Own(owner).Where(x => x != s && x.Card.HasFamily("Armored Fish")), 1, 1, a => { foreach (var x in a) ERefresh(x); }); return true;
                case "26RSD03-010":
                    if (!main || s.Level < 2 || EffectUsed(key) || !State.Players[owner].Hand.Any(id => GetCard(id).HasFamily("Armored Fish") && GetCard(id).cost <= 4)) break;
                    EffectUse(key); EBottom(owner, 1, "Green Abyss: reveal and return an Armored Fish with cost 4 or less", c => c.HasFamily("Armored Fish") && c.cost <= 4, false, n => { if (n > 0) Draw(owner); }); return true;
                case "26RCB01-052":
                    string fkey = owner + ":MaximumFChange";
                    if (!main || !s.Exhausted || EffectUsed(fkey)) break;
                    var p = State.Players[owner];
                    var replacements = p.Hand.Select((id, i) => new { id, i, c = GetCard(id) }).Where(x => x.c.IsSpirit && x.c.DisplayName.IndexOf("Ex-Aid", StringComparison.OrdinalIgnoreCase) >= 0 && x.c.DisplayName != s.Card.DisplayName && (x.c.cost == 7 || EffectFChangeCost(owner, x.c) == 7)).ToArray();
                    if (replacements.Length == 0) break;
                    EffectUse(fkey);
                    QueueChoice(owner, "F Change: choose the cost 7 Ex-Aid Spirit you must summon", 1, 1, replacements.Select(x => new ChoiceOption { key = "hand:" + x.i, index = x.i, cardId = x.id, label = x.c.DisplayName }), a => { var id = replacements[a[0]].id; ReturnSpirit(s, "hand"); EffectSummonFromZone(owner, id, "hand", true); }); return true;
            }
            error = "This card's activation is unavailable in the current state."; return false;
        }
    }
}
