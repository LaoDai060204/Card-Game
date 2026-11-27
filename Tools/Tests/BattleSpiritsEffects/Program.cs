using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MDPro3.BattleSpirits.Core;

// Production-source harness: no copied effect implementation and no Unity dependency.
static partial class EffectsTests
{
    static CardDefinition[] Catalog;
    static int assertions, instance = 1000;
    static readonly HashSet<string> covered = new();
    static void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
    static BattleMatch Fresh()
    {
        var deck = Enumerable.Repeat("26RBS01-003", 120).ToArray();
        var m = BattleMatch.Create(Catalog, deck, deck, validateDecks: false);
        m.State.Phase = MatchPhase.Main; m.State.TurnNumber = 2;
        foreach (var p in m.State.Players) { p.Reserve.Normal += 200; p.CoreGains += 200; }
        return m;
    }
    static SpiritState Put(BattleMatch m, string id, int owner = 0, int normal = 1, bool soul = false)
    {
        var p = m.State.Players[owner]; p.Deck.RemoveAt(0); p.Reserve.Normal -= normal;
        if (soul) { Check(p.Reserve.Soul, "Fixture has only one Soul Core"); p.Reserve.Soul = false; }
        var s = new SpiritState { InstanceId = instance++, Owner = owner, Card = m.GetCard(id), Cores = new CorePool { Normal = normal, Soul = soul }, ContinuousBp = m.EffectContinuousBp };
        p.Field.Add(s); return s;
    }
    static void Hand(BattleMatch m, string id, int owner = 0) { var p = m.State.Players[owner]; p.Deck.RemoveAt(0); p.Hand.Add(id); }
    static void Trash(BattleMatch m, string id, int owner = 0) { var p = m.State.Players[owner]; p.Deck.RemoveAt(0); p.Discard.Add(id); }
    static void Choose(BattleMatch m, params int[] indices)
    {
        var c = m.State.PendingChoice;
        Check(c != null, "Expected an explicit PendingChoice");
        Check(m.TryExecute(c.Owner, new BattleCommand { action = "choose", selection = indices }, out var error), "Choice rejected: " + error);
    }
    static void Drain(BattleMatch m, bool optional = true)
    {
        m.ProcessEffects(); int guard = 0;
        while (m.State.PendingChoice != null)
        {
            Check(++guard < 80, "Choices terminate");
            var c = m.State.PendingChoice;
            int n = optional ? c.Max : c.Min;
            Choose(m, Enumerable.Range(0, n).ToArray());
        }
        m.AssertInvariants();
    }
    static void Test(string id, Action<BattleMatch> action)
    {
        var m = Fresh(); action(m); m.AssertInvariants(); covered.Add(id);
    }
    static void Summon(BattleMatch m, SpiritState s, PaymentContext p = null) { m.QueueEffect(() => m.EffectSummoned(s, p ?? new PaymentContext())); m.ProcessEffects(); }
    static void Attack(BattleMatch m, SpiritState s)
    {
        m.State.Phase = MatchPhase.Attack; m.State.PendingAttackerId = s.InstanceId; s.Exhausted = true;
        m.State.Players[s.Owner].AttacksOrBlocksThisTurn++; m.QueueEffect(() => m.EffectAttacked(s)); m.ProcessEffects();
    }
    static void Magic(BattleMatch m, string id, int mode = 0) { m.QueueEffect(() => m.EffectPlayMagic(0, m.GetCard(id), new BattleCommand { mode = mode }, new PaymentContext())); m.ProcessEffects(); }
    static int Total(BattleMatch m, int owner) { var p = m.State.Players[owner]; return p.Life + p.Reserve.Total + p.Trash.Total + p.Field.Sum(s => s.Cores.Total); }

    static void Red()
    {
        Test("26RSD09-001", m => { var s = Put(m, "26RSD09-001"); Check(!m.EffectCanAttack(s), "Raraia cannot attack without Soul"); s.Cores.Soul = true; m.State.Players[0].Reserve.Soul = false; Check(m.EffectCanAttack(s), "Raraia can attack with Soul"); });
        foreach (var id in new[] { "26RBS02-002", "26RBS01-003" }) Test(id, m => { var s = Put(m, id); int n = m.State.Players[0].Hand.Count; Summon(m, s); Attack(m, s); Check(n == m.State.Players[0].Hand.Count && m.State.PendingChoice == null, id + " has no printed effects"); Check(s.Card.ExSymbols == 1, id + " has printed EX"); });
        Test("26RSD09-004", m => { var s = Put(m, "26RSD09-004", soul: true); int before = s.Bp, hand = m.State.Players[0].Hand.Count; Attack(m, s); Check(s.Bp == before + 2000 && m.State.Players[0].Hand.Count == hand + 1, "Farutekkusu BP and draw"); m.State.Players[0].Hand.AddRange(Enumerable.Repeat("26RBS01-003", 2)); m.State.Players[0].Deck.RemoveRange(0, 2); hand = m.State.Players[0].Hand.Count; m.EffectAttacked(s); Check(m.State.Players[0].Hand.Count == hand, "Draw requires at most five cards"); });
        Test("26RBS03-004", m => { var s = Put(m, "26RBS03-004"); var enemy = Put(m, "26RBS01-003", 1); enemy.TurnBpBonus = 0; Summon(m, s); Check(m.State.PendingChoice == null && m.FindSpirit(enemy.InstanceId) != null, "Lumius normal 3000 limit"); Summon(m, s, new PaymentContext { SoulPaid = true }); Drain(m); Check(m.FindSpirit(enemy.InstanceId) == null, "Lumius Soul-paid 4000 limit"); });
        Test("26RBS03-X01", m => { var s = Put(m, "26RBS03-X01", soul: true); var enemy = Put(m, "26RBS01-003", 1); enemy.TurnBpBonus = 2000; Summon(m, s, new PaymentContext { FamilyEx = 1 }); Drain(m); Check(m.FindSpirit(enemy.InstanceId) == null, "Ritenia scales by family EX"); m.State.Players[0].Reserve.Normal--; m.State.Players[0].Trash.Normal++; int n = s.Cores.Normal; m.EffectStep(MatchPhase.End, 0); Drain(m); Check(s.Cores.Normal == n + 1 && m.State.Players[0].Trash.Normal == 0, "Ritenia End trash core"); });
        Test("26RBS01-X03", m => { var s = Put(m, "26RBS01-X03", soul: true); var b = Put(m, "26RBS01-003", 1); Attack(m, s); Check(m.EffectMustBlock(s), "Clash forces a legal blocker"); m.State.PendingBlockerId = b.InstanceId; m.EffectBlocked(b); Drain(m); Check(!s.Exhausted, "Varudigaiya refreshes against at most 5000 BP blocker"); });
        Test("26RBS03-X03", m => { var s = Put(m, "26RBS03-X03"); m.State.Players[0].Reserve.Soul = false; m.State.Players[0].Trash.Soul = true; Attack(m, s); Choose(m, 0); Check(s.Cores.Soul && !s.Exhausted, "Soul Awakening moves Soul and refreshes"); int life = m.State.Players[1].Life; m.EffectBattleEnded(s); Drain(m); m.EffectBattleEnded(s); Drain(m); Check(m.State.Players[1].Life == life - 1, "Zekuga name once per turn"); });
        Test("26RSD09-011", m => { var n = Put(m, "26RSD09-011", normal: 3); var s = Put(m, "26RBS01-003"); Attack(m, s); Check(m.EffectContinuousBp(s) == 2000, "Gorge continuous red attack BP"); var e = Put(m, "26RBS01-003", 1); m.DestroySpirit(n); Drain(m); Check(m.FindSpirit(e.InstanceId) == null, "Gorge destruction trigger"); });
        Test("26RSD01-012", m => { var s = Put(m, "26RBS01-003"); s.Exhausted = true; var n = Put(m, "26RSD09-011", 1); m.State.Players[0].Reserve.Normal--; m.State.Players[0].Trash.Normal++; Magic(m, "26RSD01-012", 1); Drain(m); Check(m.FindSpirit(n.InstanceId) == null && m.State.Players[0].Trash.Normal == 0, "Break Claw Nexus and recovery"); int bp = s.Bp; Magic(m, "26RSD01-012"); Drain(m); Check(s.Bp == bp + 3000, "Break Claw flash BP"); });
        Test("26RSD09-012", m => { int h = m.State.Players[0].Hand.Count; Magic(m, "26RSD09-012", 1); Check(m.State.Players[0].Hand.Count == h + 2, "Blaze Draw main draw2"); var s = Put(m, "26RBS01-003"); int bp = s.Bp; Magic(m, "26RSD09-012"); Drain(m); Check(s.Bp == bp + 2000 && AbilityRegistry.HasSuccession("26RSD09-012"), "Blaze Draw flash and Succession"); });
        Test("26RBS02-087", m => { var s = Put(m, "26RBS01-003"); s.Exhausted = true; int bp = s.Bp; Magic(m, "26RBS02-087"); Drain(m); Check(s.Bp == bp + 3000, "Flame Summoning counts exhausted Spirits"); var n = Put(m, "26RSD09-011", 1); m.EffectAttackLifeLost(s, 1); Drain(m); Check(m.FindSpirit(n.InstanceId) == null, "Flame Summoning Life loss Nexus destruction"); });
        Test("26RBS03-087", m => { var a = Put(m, "26RSD09-004", 1); var b = Put(m, "26RSD09-004", 1); var c = Put(m, "26RSD09-004", 1); Magic(m, "26RBS03-087"); Check(m.State.PendingChoice.Options.Length == 6, "Flame Storm excludes 9000 BP triple at base limit"); Choose(m); m.State.Players[0].LifeReducedThisTurn = true; Magic(m, "26RBS03-087"); Check(m.State.PendingChoice.Options.Length == 7, "Flame Storm includes triple at increased limit"); Choose(m, 2); Drain(m); });
        Test("26RSD01-014", m => { var s = Put(m, "26RBS01-003", 1); s.TurnBpBonus = 4000; Magic(m, "26RSD01-014"); Check(m.State.PendingChoice == null, "Flame Hurricane base limit7000"); m.State.Players[0].LifeReducedThisTurn = true; Magic(m, "26RSD01-014"); Drain(m); Check(m.FindSpirit(s.InstanceId) == null, "Flame Hurricane life-reduced limit10000"); });
    }

    static void PurpleEffects()
    {
        Test("26RBS03-009", m => { var s = Put(m, "26RBS03-009", soul: true); var e = Put(m, "26RBS01-003", 1); m.State.PendingAttackerId = s.InstanceId; m.State.PendingBlockerId = e.InstanceId; m.DestroySpirit(s); Drain(m); Check(m.FindSpirit(e.InstanceId) == null, "Nidori destroys its blocker"); });
        Test("26RSD02-001", m => { var s = Put(m, "26RSD02-001", normal: 3); var e = Put(m, "26RBS01-003", 1, 2); m.DestroySpirit(s); Drain(m); Check(e.Cores.Normal == 1, "Sukurusu destruction normal core removal"); });
        Test("26RBS03-010", m => { var s = Put(m, "26RBS03-010"); int n = m.State.Players[0].Hand.Count; Summon(m, s); Check(m.State.Players[0].Hand.Count == n + 1, "Gasha Hound draw"); });
        Test("26RBS03-012", m => { var s = Put(m, "26RBS03-012"); var e = Put(m, "26RBS01-003", 1, 2, true); Summon(m, s); Choose(m, 0); Check(m.State.PendingChoice.Owner == 1, "Garudi core choice belongs to opponent"); Choose(m, 0); Check(e.Cores.Total == 1 && e.Cores.Soul, "Opponent may preserve Soul"); });
        foreach (var id in new[] { "26RBS01-019", "26RSD02-005" }) Test(id, m => { var s = Put(m, id); Summon(m, s); Check(m.State.PendingChoice == null && s.Card.ExSymbols == 1, id + " vanilla EX"); });
        Test("26RBS03-014", m => { var s = Put(m, "26RBS03-014", soul: true); var e = Put(m, "26RBS01-003", 1); var a = Put(m, "26RBS01-019"); Attack(m, a); Drain(m); Check(e.Cores.Total == 1, "Irurion cannot remove last core"); int h = m.State.Players[0].Hand.Count; m.DestroySpirit(s); Drain(m); Check(m.State.Players[0].Hand.Count == h + 1, "Irurion destruction draw"); });
        Test("26RBS01-X04", m => { var s = Put(m, "26RBS01-X04", soul: true); var p = m.State.Players[0]; p.Deck[0] = "26RBS01-019"; p.Deck[1] = "26RBS03-010"; p.Deck[2] = "26RBS01-003"; Summon(m, s); Choose(m, 1); Check(m.State.PendingChoice.Ordered, "Shi Rezen bottom order is explicit"); Choose(m, 1, 0); Check(p.Discard.Contains("26RBS03-010") && p.Deck.TakeLast(2).SequenceEqual(new[] { "26RBS01-003", "26RBS01-019" }), "Shi Rezen discard and ordered remainder"); Put(m, "26RBS01-024"); Trash(m, "26RBS03-010"); Attack(m, s); Drain(m); Check(OwnCount(m, "26RBS03-010") == 1, "Shi Rezen activates Spirit Manipulation"); });
        Test("26RBS01-024", m => { var s = Put(m, "26RBS01-024", normal: 3); Trash(m, "26RBS03-010"); int before = m.State.Players[0].Trash.Total; Summon(m, s); Drain(m); Check(OwnCount(m, "26RBS03-010") == 1 && m.State.Players[0].Trash.Total > before, "Papirioni revival pays summon cost"); var e = Put(m, "26RBS01-019", 1); e.Exhausted = true; Attack(m, s); Drain(m); Check(m.FindSpirit(e.InstanceId) == null, "Papirioni attacks destroy exhausted cost3"); });
        Test("26RBS03-X04", m => { var s = Put(m, "26RBS03-X04", soul: true); var e = Put(m, "26RBS01-003", 1); var p = m.State.Players[0]; p.Deck[0] = "26RBS03-010"; Summon(m, s); Drain(m); Check(m.FindSpirit(e.InstanceId) == null, "Rindorin discarded Dark Purple removes last core"); Trash(m, "26RBS03-X04"); Trash(m, "26RBS03-X04"); int index = p.Discard.Count - 2; Check(m.EffectActivate(0, new BattleCommand { source = s.InstanceId }, out _), "Rindorin Main activation"); Choose(m, m.State.PendingChoice.Options.ToList().FindIndex(o => o.index == index)); Check(m.EffectExSymbols(0, "26RBS03-X04", index) == 1 && m.EffectExSymbols(0, "26RBS03-X04", index + 1) == 0, "Rindorin converts only selected physical copy"); Check(!m.EffectCanActivate(0, new BattleCommand { source = s.InstanceId }), "Rindorin once per turn"); });
        Test("26RBS03-X05", m => { var s = Put(m, "26RBS03-X05", soul: true); Trash(m, "26RBS03-010"); int n = m.State.Players[0].Trash.Total; Summon(m, s); Drain(m); Check(OwnCount(m, "26RBS03-010") == 1 && m.State.Players[0].Trash.Total == n, "Kuramagurama free revival"); var e = Put(m, "26RBS01-003", 1); m.State.PendingAttackerId = e.InstanceId; m.EffectBlocked(s); Drain(m); Check(m.FindSpirit(e.InstanceId) == null, "Kuramagurama blocking removes normal core"); });
        Test("26RSD02-X01", m => { var s = Put(m, "26RSD02-X01", normal: 4); var e = Put(m, "26RBS01-003", 1, 3); Summon(m, s); Drain(m); Check(e.Cores.Total == 1, "Nefira summon leaves one core"); int n = m.State.Players[0].Hand.Count; Attack(m, s); Drain(m); Check(m.FindSpirit(e.InstanceId) == null && m.State.Players[0].Hand.Count == n + 1, "Nefira depletion draws"); });
        Test("26RBS01-X06", m => { var s = Put(m, "26RBS01-X06", normal: 4); for (int i = 0; i < 3; i++) Put(m, "26RBS01-003", 1); Summon(m, s); Drain(m); Check(m.State.Players[1].Field.Count == 0 && m.EffectAttackDamage(s, 1) == 2, "Danpirion depletion and extra Life loss"); });
        Test("26RBS03-X06", m => { var s = Put(m, "26RBS03-X06", soul: true); for (int i = 0; i < 3; i++) Put(m, "26RBS01-003", 1); int n = s.Cores.Normal; Summon(m, s); Drain(m); Check(s.Cores.Normal == n + 1, "Desperado depletion earns exactly one Void core"); var e = Put(m, "26RBS01-003", 1, 3, true); Attack(m, s); Drain(m); Check(e.Cores.Normal == 1 && e.Cores.Soul, "Desperado preserves Soul and one non-Soul"); });
        Test("26RSD02-014", m => { var e = Put(m, "26RBS01-003", 1, 2, true); int n = Total(m, 1); Magic(m, "26RSD02-014"); Choose(m, 0); Check(m.State.PendingChoice.Owner == 1, "Soul Bite owner chooses cores"); Choose(m, 1); Check(e.Cores.Total == 1 && !e.Cores.Soul && Total(m, 1) == n, "Soul Bite Soul choice conserves cores"); });
        Test("26RBS03-089", m => { var a = Put(m, "26RBS01-003", 1); var b = Put(m, "26RBS01-003", 1); m.State.Players[0].LifeReducedThisTurn = true; Magic(m, "26RBS03-089"); Check(m.State.PendingChoice.Min == 2, "Grand Theft life reduced requires two if present"); Drain(m); Check(m.State.Players[1].Field.Count == 0, "Grand Theft destroys two single-core Spirits"); });
        Test("26RBS03-097", m => { var e = Put(m, "26RBS03-X05", 1); Magic(m, "26RBS03-097"); Check(m.FindSpirit(e.InstanceId) != null && m.State.PendingChoice == null, "Sharp Thorn normal limit4"); m.State.Players[0].LifeReducedThisTurn = true; Magic(m, "26RBS03-097"); Drain(m); Check(m.FindSpirit(e.InstanceId) == null, "Sharp Thorn life reduced limit7"); });
        Test("26RBS03-022", m => { var s = Put(m, "26RBS03-022"); Trash(m, "26RBS01-019"); Check(m.EffectExtraReduction(0, s.Card) == m.State.Players[1].Reserve.Total, "Pisketona purple-only reduction counts Soul"); Trash(m, "26RBS01-003"); Check(m.EffectExtraReduction(0, s.Card) == 0, "Pisketona rejects mixed Trash"); Put(m, "26RBS01-003", 1); int n = m.State.Players[0].Hand.Count; Attack(m, s); Drain(m); Check(m.State.Players[0].Hand.Count == n + 1, "Pisketona removes core then checks Reserve for draw"); });
    }
    static int OwnCount(BattleMatch m, string id) => m.State.Players[0].Field.Count(s => s.Card.id == id);

    static void Green()
    {
        Test("26RSD03-002", m => { var s = Put(m, "26RSD03-002"); int n = m.State.Players[0].Trash.Normal; Summon(m, s); Check(m.State.Players[0].Trash.Normal == n + 1, "Puffer Void core to Trash"); });
        Test("26RSD03-003", m => { var s = Put(m, "26RSD03-003", soul: true); var e = Put(m, "26RBS01-003", 1); Summon(m, s); Drain(m); Check(e.Exhausted, "Rashead exhaust"); int n = s.Bp; Attack(m, s); Check(s.Bp == n + 2000, "Rashead attack BP"); });
        Test("26RSD10-003", m => { var s = Put(m, "26RSD10-003"); int n = s.Cores.Total; Summon(m, s); Choose(m, 0); Check(s.Cores.Total == n + 1, "Sharky Void core onto itself"); Summon(m, s); int r = m.State.Players[0].Reserve.Normal; Choose(m, 1); Check(m.State.Players[0].Reserve.Normal == r + 1, "Sharky alternate Reserve destination"); });
        Test("26RSD10-004", m => { var s = Put(m, "26RSD10-004", soul: true); int n = s.Bp; Attack(m, s); Check(s.Bp == n + 2000, "Monocaul attack BP"); });
        Test("26RSD03-005", m => { var s = Put(m, "26RSD03-005"); Summon(m, s); Check(m.State.PendingChoice == null && s.Card.ExSymbols == 1, "Mola Mola vanilla EX"); });
        Test("26RSD03-X02", m => { var s = Put(m, "26RSD03-X02", soul: true); var a = Put(m, "26RSD03-005"); int n = a.Bp; Summon(m, s); Drain(m); Check(a.Bp == n + 3000, "Dantecreo buff two Armored Fish"); a.Exhausted = true; m.State.Phase = MatchPhase.FlashBeforeBlock; Check(m.EffectActivate(0, new BattleCommand { source = s.InstanceId }, out _), "Dantecreo flash activation"); Drain(m); Check(!a.Exhausted && !m.EffectCanActivate(0, new BattleCommand { source = s.InstanceId }), "Dantecreo refresh once per turn"); });
        Test("26RBS02-034", m => { var s = Put(m, "26RBS02-034", normal: 3); int n = s.Cores.Total; Summon(m, s); Check(s.Cores.Total == n + 1, "Buzzsorn summon Void core"); var e = Put(m, "26RBS01-003", 1); m.DestroySpirit(s); Drain(m); Check(e.Exhausted, "Buzzsorn destruction exhaust"); });
        Test("26RBS02-033", m => { var s = Put(m, "26RBS02-033", soul: true); var e = Put(m, "26RBS01-003", 1); Summon(m, s); Drain(m); Check(e.Exhausted && !e.HeavyExhausted, "Daisy refresh to exhausted"); Summon(m, s); Drain(m); Check(e.HeavyExhausted, "Daisy exhausted to heavy"); e.HeavyExhausted = false; m.EffectStep(MatchPhase.End, 0); m.ProcessEffects(); Check(m.State.PendingChoice.Owner == 1, "Daisy End choice owned by opponent"); Drain(m); Check(e.HeavyExhausted, "Daisy End heavy"); });
        Test("26RBS02-X09", m => { var s = Put(m, "26RBS02-X09", normal: 4); var e = Put(m, "26RBS01-003", 1); Attack(m, s); Drain(m); Check(!s.Exhausted && e.HeavyExhausted, "Leviaclan attack exhaust/refresh and level2 heavy trigger"); e.HeavyExhausted = false; e.Exhausted = false; Attack(m, s); Drain(m); Check(!e.Exhausted, "Leviaclan attack once per turn"); });
        Test("26RSD03-010", m => { var n = Put(m, "26RSD03-010", soul: true); var s = Put(m, "26RSD03-005"); Attack(m, s); Check(m.EffectContinuousBp(s) == 1000, "Green Abyss continuous Armored Fish BP"); m.State.Phase = MatchPhase.Main; Hand(m, "26RSD03-005"); int h = m.State.Players[0].Hand.Count; Check(m.EffectActivate(0, new BattleCommand { source = n.InstanceId }, out _), "Green Abyss activate"); Drain(m); Check(m.State.Players[0].Hand.Count == h && !m.EffectCanActivate(0, new BattleCommand { source = n.InstanceId }), "Green Abyss return/draw once"); });
        Test("26RSD03-012", m => { int expected = m.State.Players[1].Hand.Count; Magic(m, "26RSD03-012", 1); Drain(m); Check(m.State.Players[0].Hand.Count == expected, "Hand Molt replaces Hand with opponent hand count"); var s = Put(m, "26RSD03-005"); int n = s.Bp; Magic(m, "26RSD03-012"); Drain(m); Check(s.Bp == n + 2000, "Hand Molt flash buff"); });
        Test("26RSD03-013", m => { var s = Put(m, "26RBS01-003", 1); Magic(m, "26RSD03-013"); Drain(m); Check(s.Exhausted && !s.HeavyExhausted, "Vortex Shave exhaust"); });
        Test("26RSD03-014", m => { var s = Put(m, "26RBS01-003", 1); Magic(m, "26RSD03-014"); Drain(m); Check(s.HeavyExhausted, "Tentacle Attack heavy exhaust"); });
        Test("26RBS03-091", m => { var a = Put(m, "26RBS01-003", 1); var b = Put(m, "26RBS01-019", 1); m.State.ActivePlayerIndex = 1; m.State.Phase = MatchPhase.FlashBeforeBlock; Magic(m, "26RBS03-091"); Choose(m, 0); Choose(m, 1); Drain(m); Check(a.Exhausted && b.Exhausted, "Bubble Shield opposing Attack Step second exhaust"); });
    }

    static void ExAid()
    {
        foreach (var id in new[] { "26RCB01-039", "26RCB01-043" }) Test(id, m => { var s = Put(m, id); Attack(m, s); Check(s.Level == 2, id + " first attack Level Up"); s.BattleLevelOverride = 0; Attack(m, s); Check(s.Level == 1, id + " second attack no Level Up"); });
        foreach (var id in new[] { "26RCB01-040", "26RCB01-044" }) Test(id, m => { var s = Put(m, id); Summon(m, s); Check(m.State.PendingChoice == null && s.Card.ExSymbols == 1, id + " vanilla EX"); });
        Test("26RCB01-041", m => { var s = Put(m, "26RCB01-041"); var p = m.State.Players[0]; p.Deck[0] = "26RCB01-041"; p.Deck[1] = "26RCB01-040"; p.Deck[2] = "26RBS01-003"; int h = p.Hand.Count; Summon(m, s); Check(m.State.PendingChoice.Options.Length == 1, "Action Gamer excludes its own name and non EX-AID"); Drain(m); Check(p.Hand.Count == h + 1 && p.Hand.Contains("26RCB01-040"), "Action Gamer adds eligible Spirit"); });
        Test("26RCB01-046", m => { Put(m, "26RCB01-046"); Put(m, "26RCB01-046"); var s = Put(m, "26RCB01-040"); m.State.Phase = MatchPhase.Attack; int h = m.State.Players[0].Hand.Count; m.EffectAttackLifeLost(s, 1); Drain(m); Check(m.State.Players[0].Hand.Count == h + 1, "Poppy unstackable draw2 return1"); m.EffectAttackLifeLost(s, 1); Drain(m); Check(m.State.Players[0].Hand.Count == h + 1, "Poppy once per turn across copies"); });
        Test("26RCB01-047", m => { var s = Put(m, "26RCB01-047"); Put(m, "26RCB01-040"); int h = m.State.Players[0].Hand.Count; Attack(m, s); Check(s.Level == 2, "Brave Level Up before attack Lv2 clause"); Drain(m); Check(m.State.Players[0].Hand.Count == h, "Brave draw2 ordered return2"); });
        Test("26RCB01-051", m => { var s = Put(m, "26RCB01-051"); var a = Put(m, "26RCB01-040"); Put(m, "26RBS01-003", 1); Put(m, "26RBS01-003", 1); Attack(m, a); Drain(m); Check(s.Exhausted && m.State.Players[1].Field.Count == 0, "Simulation exhaust self destroys two"); });
        Test("26RCB01-052", m => { var s = Put(m, "26RCB01-052", soul: true); s.Exhausted = true; Hand(m, "26RCB01-X07"); Check(m.EffectActivate(0, new BattleCommand { source = s.InstanceId }, out _), "Maximum F Change legal with replacement"); Drain(m); Check(m.FindSpirit(s.InstanceId) == null && OwnCount(m, "26RCB01-X07") == 1 && m.State.Players[0].Hand.Contains(s.Card.id), "F Change returns and summons without cost"); var s2 = Put(m, "26RCB01-052", normal: 4); var enemy = Put(m, "26RBS01-019", 1); // Return Soul from replacement for this independent clause.
            var muteki = m.State.Players[0].Field.Single(x => x.Card.id == "26RCB01-X07"); if (muteki.Cores.Soul) { muteki.Cores.Soul = false; m.State.Players[0].Reserve.Soul = true; }
            s2.Cores.Soul = true; m.State.Players[0].Reserve.Soul = false; Attack(m, s2); Drain(m); Check(m.FindSpirit(enemy.InstanceId) == null, "Maximum attack destruction"); });
        Test("26RCB01-X06", m => { var s = Put(m, "26RCB01-X06"); var e = Put(m, "26RBS01-003", 1); Attack(m, s); Drain(m); Check(s.Level == 2 && m.FindSpirit(e.InstanceId) == null, "Para-DX first attack Level Up and destroy"); Check(m.EffectFChangeCost(0, s.Card) == s.Card.cost, "Para-DX ordinary F Change cost"); m.DamageLife(0, 2); Drain(m); Check(m.EffectFChangeCost(0, s.Card) == 5, "Para-DX low life optional cost5"); });
        Test("26RCB01-X07", m => { var s = Put(m, "26RCB01-X07", soul: true); var other = Put(m, "26RCB01-040"); m.State.Phase = MatchPhase.Attack; Check(!m.EffectCanBeMagicTarget(1, s), "Muteki all EX-AID protection"); var outsider = Put(m, "26RBS01-003"); Check(m.EffectCanBeMagicTarget(1, s), "Muteki protection needs all Spirits EX-AID"); m.ReturnSpirit(outsider); Attack(m, s); Drain(m); Check(!s.Exhausted && other.Exhausted, "Muteki exhaust another refresh self"); });
        Test("26RCB01-060", m => { var n = Put(m, "26RCB01-060", soul: true); Hand(m, "26RCB01-040"); Hand(m, "26RCB01-044"); int h = m.State.Players[0].Hand.Count; Summon(m, n); Drain(m); Check(m.State.Players[0].Hand.Count == h, "Genm revealreturn then draw equal count"); var s = Put(m, "26RCB01-040"); s.Exhausted = true; m.EffectStep(MatchPhase.End, 0); Drain(m); Check(!s.Exhausted, "Genm End refresh"); });
        Test("26RSD06-012", m => { int h = m.State.Players[0].Hand.Count; Magic(m, "26RSD06-012", 1); Drain(m); Check(m.State.Players[0].Hand.Count == h + 1, "Feeding Draw draw3return2"); var s = Put(m, "26RCB01-040"); int n = s.Bp; Magic(m, "26RSD06-012"); Drain(m); Check(s.Bp == n + 3000, "Feeding Draw Flash BP"); });
        Test("26RSD06-013", m => { var n = Put(m, "26RCB01-060"); var s = Put(m, "26RBS03-X05", 1); Magic(m, "26RSD06-013"); Choose(m, 0); Drain(m); Check(m.FindSpirit(n.InstanceId) == null && m.FindSpirit(s.InstanceId) == null, "Stem Lance adds destroyed Nexus cost"); });
        Test("26RCB01-071", m => { Put(m, "26RCB01-040"); Put(m, "26RCB01-044"); var e = Put(m, "26RBS01-024", 1); Magic(m, "26RCB01-071"); Drain(m); Check(m.FindSpirit(e.InstanceId) == null, "Mighty Critical Strike scales with all EX-AID permanents"); });
        Test("26RBS01-126", m => { Put(m, "26RBS01-003", 1); Put(m, "26RBS01-019", 1); m.State.Players[0].LifeReducedThisTurn = true; Magic(m, "26RBS01-126"); Drain(m); Check(m.State.Players[1].Field.Count == 0, "Merciless Blow second destruction after life decrease"); });
    }

    static void NegativeAndPayment()
    {
        var m = Fresh(); var own = Put(m, "26RCB01-X07", soul: true); m.State.Phase = MatchPhase.FlashBeforeBlock; m.State.FlashPlayerIndex = 1;
        Hand(m, "26RSD01-014", 1); int hand = m.State.Players[1].Hand.Count;
        Check(!m.TryExecute(1, new BattleCommand { action = "magic", index = hand - 1, target = own.InstanceId }, out _), "Opponent Magic cannot select protected Muteki"); Check(m.State.Players[1].Hand.Count == hand, "Illegal Magic does not pay/discard");
        m = Fresh(); var target = Put(m, "26RBS01-003", 1, 1, true); Magic(m, "26RSD02-014"); Choose(m, 0); var choice = m.State.PendingChoice;
        Check(choice == null, "Removing all cores has a unique Soul/normal combination"); Drain(m); Check(m.FindSpirit(target.InstanceId) == null && m.State.Players[1].Reserve.Soul, "Depletion returns Soul to correct owner");
        m = Fresh(); var e = Put(m, "26RBS01-003", 1, 2, true); Magic(m, "26RSD02-014"); Choose(m, 0); int before = e.Cores.Total;
        Check(!m.TryExecute(0, new BattleCommand { action = "choose", selection = new[] { 0 } }, out _), "Cannot resolve opponent-owned core choice"); Check(e.Cores.Total == before, "Rejected choice has no mutation");
        Choose(m, 1); Drain(m);
        foreach (var id in AbilityRegistry.CardIds) Check(covered.Contains(id), "Missing meaningful profile test: " + id);
        Check(AbilityRegistry.CardIds.Count == 62 && Catalog.Length == 62, "Exactly62 registered review cards");
    }
    static void ContinuationsAndPrintedClauses()
    {
        var granted=Fresh(); var phoenix=Put(granted,"26RBS03-X03",soul:true); Put(granted,"26RCB01-060",1); Put(granted,"26RCB01-060",1);
        Magic(granted,"26RBS02-087"); Drain(granted); Magic(granted,"26RBS02-087"); Drain(granted);
        int opposingLife=granted.State.Players[1].Life; granted.EffectBattleEnded(phoenix); Drain(granted);
        Check(granted.State.Players[1].Life==opposingLife-1&&granted.State.Players[1].Field.Count==0,"Repeated Flame Summoning grants stack and trigger from Zekuga effect Life reduction");
        var m=Fresh(); Put(m,"26RCB01-060",soul:true); var rider=Put(m,"26RCB01-040"); rider.Exhausted=true; rider.HeavyExhausted=true;
        m.EffectStep(MatchPhase.End,0); Drain(m);
        Check(rider.Exhausted&&!rider.HeavyExhausted,"A card-effect refresh advances heavy exhaustion by only one state");
        m=Fresh(); var rindorin=Put(m,"26RBS03-X04",soul:true); Trash(m,"26RBS03-X04"); Trash(m,"26RBS03-X04"); Hand(m,"26RSD02-X01"); m.State.Phase=MatchPhase.SecondMain;
        Check(m.GetLegalCommands(0).Any(c=>c.action=="activate"&&c.source==rindorin.InstanceId),"Activate Main is offered during Standard Second Main");
        Check(m.TryExecute(0,new BattleCommand {action="activate",source=rindorin.InstanceId},out var error),"Second Main activation executes: "+error); Choose(m,1);
        Check(m.EffectExSymbols(0,"26RBS03-X04",0)==0&&m.EffectExSymbols(0,"26RBS03-X04",1)==1,"Second Main Rindorin converts the chosen physical duplicate");
        int handIndex=m.State.Players[0].Hand.Count-1;
        Check(m.TryExecute(0,new BattleCommand {action="summon",index=handIndex,succession=new[]{"26RBS03-X04"},successionIndices=new[]{1}},out error),"The converted second copy can pay Succession: "+error); Drain(m);
        Check(m.State.Players[0].Discard.SequenceEqual(new[]{"26RBS03-X04"})&&m.State.Players[0].Removed.SequenceEqual(new[]{"26RBS03-X04"})&&m.EffectExSymbols(0,"26RBS03-X04",0)==0,"Succession removes the selected copy and preserves the unconverted duplicate");
        m=Fresh(); var abyss=Put(m,"26RSD03-010",soul:true); Hand(m,"26RSD03-005"); m.State.Phase=MatchPhase.SecondMain;
        Check(m.GetLegalCommands(0).Any(c=>c.action=="activate"&&c.source==abyss.InstanceId),"Green Abyss Main effect is reachable in Second Main");
        m=Fresh(); var maximum=Put(m,"26RCB01-052"); maximum.Exhausted=true; Hand(m,"26RCB01-X06"); m.DamageLife(0,2); Drain(m);
        Check(!m.GetLegalCommands(0).Any(c=>c.action=="activate"&&c.source==maximum.InstanceId),"Maximum Gamer cannot F Change into Para-DX because its name lacks Ex-Aid");
        Hand(m,"26RCB01-X07");
        Check(m.TryExecute(0,new BattleCommand {action="activate",source=maximum.InstanceId},out error),"Eligible cost7 F Change executes at low Life: "+error); Drain(m);
        Check(OwnCount(m,"26RCB01-X07")==1&&m.FindSpirit(maximum.InstanceId)==null,"Maximum Gamer summons its eligible Ex-Aid replacement at low Life");
        m=Fresh(); var pending=Put(m,"26RCB01-051"); var attacker=Put(m,"26RCB01-040"); var enemy=Put(m,"26RBS01-003",1);
        m.DestroySpirit(pending); m.EffectAttacked(attacker);
        Check(m.State.PendingChoice==null,"A pending-destruction Spirit cannot activate its ordinary triggered effect"); Drain(m);
        Check(m.FindSpirit(enemy.InstanceId)!=null,"Pending Simulation Gamer never destroys an opposing Spirit");
    }
    static void SimultaneousTriggerOrder()
    {
        foreach (bool simulationFirst in new[]{true,false})
        {
            var m=Fresh(); var muteki=Put(m,"26RCB01-X07",soul:true); var simulation=Put(m,"26RCB01-051"); Put(m,"26RBS01-003",1); Put(m,"26RBS01-003",1);
            Attack(m,muteki); var order=m.State.PendingChoice;
            Check(order!=null&&order.Owner==0&&order.Options.Length==2&&order.Prompt.Contains("simultaneous"),"Turn player chooses between simultaneous Muteki and Simulation effects");
            int chosen=Array.FindIndex(order.Options,o=>o.instanceId==(simulationFirst?simulation.InstanceId:muteki.InstanceId)); Choose(m,chosen);
            if (simulationFirst)
            {
                Choose(m,0); Choose(m,0,1); Drain(m);
                Check(m.State.Players[1].Field.Count==0&&muteki.Exhausted&&simulation.Exhausted,"Simulation can resolve first, destroy both targets, and leave no refreshed Spirit to pay Muteki");
            }
            else
            {
                Choose(m,0); Drain(m);
                Check(m.State.Players[1].Field.Count==2&&!muteki.Exhausted&&simulation.Exhausted,"Muteki can exhaust Simulation first; its simultaneous effect then cannot pay its exhaustion condition");
            }
        }
        var destroyed=Fresh(); var irurion=Put(destroyed,"26RBS03-014"); var buzz=Put(destroyed,"26RBS02-034",1,3); var survivor=Put(destroyed,"26RBS01-003"); int hand=destroyed.State.Players[0].Hand.Count;
        destroyed.DestroySpirits(new[]{irurion,buzz}); destroyed.ProcessEffects();
        Check(irurion.PendingRemoval&&buzz.PendingRemoval&&destroyed.State.PendingChoice.Owner==0,"Both destroyed cards remain pending while turn player orders both owners' triggers");
        Choose(destroyed,Array.FindIndex(destroyed.State.PendingChoice.Options,o=>o.instanceId==buzz.InstanceId));
        Check(destroyed.State.PendingChoice.Owner==1,"After trigger ordering, Buzzsorn target choice returns to its owner"); Choose(destroyed,Array.FindIndex(destroyed.State.PendingChoice.Options,o=>o.instanceId==survivor.InstanceId)); Drain(destroyed);
        Check(survivor.Exhausted&&destroyed.State.Players[0].Hand.Count==hand+1&&destroyed.FindSpirit(irurion.InstanceId)==null&&destroyed.FindSpirit(buzz.InstanceId)==null,"Both ordered destruction effects resolve before final card/core removal");
        var ending=Fresh(); var first=Put(ending,"26RBS03-X01",normal:3); var second=Put(ending,"26RBS03-X01",normal:3); ending.State.Players[0].Reserve.Normal--; ending.State.Players[0].Trash.Normal++;
        ending.EffectStep(MatchPhase.End,0); ending.ProcessEffects(); Choose(ending,Array.FindIndex(ending.State.PendingChoice.Options,o=>o.instanceId==second.InstanceId)); Choose(ending,0); Drain(ending);
        Check(first.Cores.Normal==3&&second.Cores.Normal==4,"Turn player chooses which simultaneous End effect receives the only available Trash core");
    }
    static int Main(string[] args)
    {
        try
        {
            string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
            var options = new JsonSerializerOptions { IncludeFields = true }; options.Converters.Add(new JsonStringEnumConverter());
            Catalog = JsonSerializer.Deserialize<CardsDatabase>(File.ReadAllText(Path.Combine(root, "Assets/StreamingAssets/BattleSpirits/cards.json")), options).cards;
            Check(Catalog.All(c=>c.implemented&&c.IsPlayable),"The actual shipped card flags expose every profile");
            Red(); PurpleEffects(); Green(); ExAid(); NegativeAndPayment(); ContinuationsAndPrintedClauses(); SimultaneousTriggerOrder();
            PublicCommandReachability(); LevelUpDepletionThroughCommands();
            Console.WriteLine($"PASS {assertions} assertions; {covered.Count} exact card profiles; production Core/*.cs linked."); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
