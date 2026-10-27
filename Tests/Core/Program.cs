using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LaoDai.BattleSpirits.Core;

internal static class Program
{
    private static int passed;

    private static void Main(string[] args)
    {
        Run("Opening, first turn, and second player's first Core Step", OpeningAndFirstTurn);
        Run("Eternal mulligan is once, replaces all four, and preserves deck size", Mulligan);
        Run("Cost reduction counts field colors and respects printed cap", CostReduction);
        Run("Last-core field payment retains the discount fixed before payment", FieldPaymentDiscount);
        Run("Summoning reserves enough core for Lv1 and fails atomically", SummonValidation);
        Run("Soul Core follows payment, trash, and Refresh", SoulPaymentAndRefresh);
        Run("Moving cores raises BP and distinguishes vanish from destroy", MovingAndVanish);
        Run("Newly summoned attacker; defender-first Flash and two passes", FlashPriorityAndFreshAttack);
        Run("Exhausted blockers are illegal; equal BP destroys both", BlockingAndTie);
        Run("Higher BP survives without dealing Life damage through a block", BlockedBattle);
        Run("Unblocked symbols transfer Life cores to Reserve; lethal wins", LifeAndVictory);
        Run("Drawing last card is safe; empty deck loses only at owner Start", DeckOutTiming);
        Run("Deck validation uses card names, not merely printing IDs", DeckAndCardValidation);
        Run("Deterministic shuffles and hundreds of complete legal actions", DeterministicSimulation);
        if (args.Length > 0)
            Run("Shipped official card catalog builds legal decks and completes matches", () => CatalogIntegration(args[0]));
        Console.WriteLine("PASS: " + passed + " independent rules scenarios.");
    }

    private static void Run(string name, Action test)
    {
        test();
        passed++;
        Console.WriteLine("PASS " + name);
    }

    private static CardDefinition Card(string id = "TEST-001", int cost = 0, int reduction = 0,
        CardColor color = CardColor.Red, int bp = 1000, int symbols = 1)
    {
        return new CardDefinition
        {
            id = id, nameJa = "Test Spirit " + id, nameVi = "Spirit mẫu " + id,
            color = color, cost = cost, reductionSymbols = reduction, symbols = symbols,
            implemented = true, effectTextJa = "-",
            levels = new List<CardLevel>
            {
                new CardLevel { cores = 1, bp = bp },
                new CardLevel { cores = 3, bp = bp + 3000 }
            }
        };
    }

    // Tiny/same-name fixture decks intentionally bypass deckbuilding validation;
    // the separate validation test and shipped starter decks exercise legal decks.
    private static BattleMatch Match(CardDefinition card = null, int deckSize = 40, int firstPlayer = 0)
    {
        card = card ?? Card();
        return BattleMatch.Create(new[] { card }, Enumerable.Repeat(card.id, deckSize).ToArray(),
            Enumerable.Repeat(card.id, deckSize).ToArray(), 42, firstPlayer, false);
    }

    private static void Keep(BattleMatch match)
    {
        int first = match.DecisionPlayerIndex;
        Yes(match.TryConfirmOpening(first, false, out var error), error);
        Yes(match.TryConfirmOpening(1 - first, false, out error), error);
    }

    private static int Summon(BattleMatch match, int player)
    {
        Yes(match.TrySummon(player, 0, out var error), error);
        return match.State.Players[player].Field.Last().InstanceId;
    }

    private static void EndTurn(BattleMatch match)
    {
        Yes(match.TryEndTurn(match.State.ActivePlayerIndex, out var error), error);
    }

    private static void PassBoth(BattleMatch match)
    {
        Yes(match.TryPassFlash(match.DecisionPlayerIndex, out var error), error);
        Yes(match.TryPassFlash(match.DecisionPlayerIndex, out error), error);
    }

    private static void BeginAttack(BattleMatch match, int attacker)
    {
        Yes(match.TryBeginAttackStep(match.State.ActivePlayerIndex, out var error), error);
        Yes(match.TryAttack(match.State.ActivePlayerIndex, attacker, out error), error);
    }

    private static void OpeningAndFirstTurn()
    {
        var match = Match(firstPlayer: 1);
        Equal(1, match.DecisionPlayerIndex);
        Equal(4, match.State.Players[0].Hand.Count);
        Equal(4, match.State.Players[1].Hand.Count);
        Equal(3, match.State.Players[1].Reserve.Normal);
        Yes(match.State.Players[1].Reserve.Soul);
        No(match.TrySummon(1, 0, out _));
        Keep(match);
        Equal(1, match.State.ActivePlayerIndex);
        Equal(1, match.State.TurnNumber);
        Equal(5, match.State.Players[1].Hand.Count);
        Equal(0, match.State.Players[1].CoreGains);
        No(match.TryBeginAttackStep(1, out _));
        EndTurn(match);
        Equal(0, match.State.ActivePlayerIndex);
        Equal(1, match.State.Players[0].CoreGains);
        Equal(5, match.State.Players[0].Reserve.Total);
        Yes(match.TryBeginAttackStep(0, out var error), error);
        match.AssertInvariants();
    }

    private static void Mulligan()
    {
        var match = Match();
        No(match.TryConfirmOpening(1, true, out _));
        Yes(match.TryConfirmOpening(0, true, out var error), error);
        Equal(4, match.State.Players[0].Hand.Count);
        Equal(36, match.State.Players[0].Deck.Count);
        Yes(match.State.Players[0].MulliganUsed);
        No(match.TryConfirmOpening(0, true, out _));
        Yes(match.TryConfirmOpening(1, false, out error), error);
        No(match.TryConfirmOpening(1, true, out _));
        Equal(5, match.State.Players[0].Hand.Count);
        match.AssertInvariants();
    }

    private static void CostReduction()
    {
        var match = Match();
        Keep(match);
        int first = Summon(match, 0);
        var red = Card("COST", 5, 2);
        var blue = Card("BLUE", 5, 2, CardColor.Blue);
        Equal(4, match.GetSummonCost(0, red));
        Equal(5, match.GetSummonCost(0, blue));
        Summon(match, 0);
        Summon(match, 0);
        Equal(3, match.GetSummonCost(0, red));
        Yes(match.TryMoveCore(0, first, -1, false, out var error), error);
        Equal(3, match.GetSummonCost(0, red));
        match.AssertInvariants();
    }

    private static void FieldPaymentDiscount()
    {
        var source = Card("PAYMENT-SOURCE");
        var target = Card("PAYMENT-TARGET", cost: 2, reduction: 1);
        var deck = Enumerable.Repeat(source.id, 20).Concat(Enumerable.Repeat(target.id, 20)).ToArray();
        var match = BattleMatch.Create(new[] { source, target }, deck, deck, 41, 0, false);
        Keep(match);
        var owner = match.State.Players[0];
        // Arrange an exact public rule example: one red symbol with its last
        // normal core on field and one ordinary core in Reserve. Other cores
        // have already been used for payments and are in Trash.
        owner.Hand.RemoveAt(0);
        owner.Field.Add(new SpiritState
        {
            InstanceId = 500, Card = source, Cores = new CorePool { Normal = 1 }
        });
        owner.Reserve.Normal = 1;
        owner.Reserve.Soul = false;
        owner.Trash.Normal = 1;
        owner.Trash.Soul = true;
        int targetIndex = owner.Hand.FindIndex(id => id == target.id);
        if (targetIndex < 0)
        {
            int inDeck = owner.Deck.FindIndex(id => id == target.id);
            string swap = owner.Hand[0];
            owner.Hand[0] = target.id;
            owner.Deck[inDeck] = swap;
            targetIndex = 0;
        }
        match.AssertInvariants();
        Equal(1, match.GetSummonCost(0, target));
        Yes(match.TrySummon(0, targetIndex, out var error), error);
        Yes(match.FindSpirit(500) == null);
        Equal(1, owner.Field.Count);
        Equal(target.id, owner.Field[0].Card.id);
        Equal(1, owner.Field[0].Cores.Total);
        Equal(0, owner.Reserve.Total);
        Equal(3, owner.Trash.Total);
        Yes(match.LastEvents.Any(e => e.Kind == "Vanished"));
        match.AssertInvariants();
    }

    private static void SummonValidation()
    {
        var match = Match(Card(cost: 4));
        Keep(match);
        int hand = match.State.Players[0].Hand.Count;
        No(match.TrySummon(0, 0, out _));
        Equal(hand, match.State.Players[0].Hand.Count);
        Equal(4, match.State.Players[0].Reserve.Total);
        Equal(0, match.State.Players[0].Trash.Total);
        No(match.TrySummon(0, -1, out _));
        No(match.TrySummon(0, int.MaxValue, out _));
        No(match.TrySummon(1, 0, out _));
        match.AssertInvariants();
    }

    private static void SoulPaymentAndRefresh()
    {
        var match = Match();
        Keep(match);
        int spirit = Summon(match, 0);
        Yes(match.TryMoveCore(0, -1, spirit, true, out var error), error);
        No(match.State.Players[0].Reserve.Soul);
        Yes(match.FindSpirit(spirit).Cores.Soul);
        Yes(match.TryMoveCore(0, spirit, -1, true, out error), error);
        // Put all normal Reserve cores on the existing Spirit: the next Lv1
        // allocation must use Soul Core, proving it is a usable real core.
        while (match.State.Players[0].Reserve.Normal > 0)
            Yes(match.TryMoveCore(0, -1, spirit, false, out error), error);
        int soulSpirit = Summon(match, 0);
        Yes(match.FindSpirit(soulSpirit).Cores.Soul);
        Yes(match.TryMoveCore(0, soulSpirit, -1, true, out error), error);
        Yes(match.State.Players[0].Reserve.Soul);
        Equal(1, match.State.Players[0].Discard.Count);
        match.AssertInvariants();

        // At cost3, ordinary cores go to Trash while Soul sustains Lv1.
        var payment = Match(Card(cost: 3));
        Keep(payment);
        int paidSpirit = Summon(payment, 0);
        Equal(3, payment.State.Players[0].Trash.Normal);
        Yes(payment.FindSpirit(paidSpirit).Cores.Soul);
        EndTurn(payment);
        Equal(3, payment.State.Players[0].Trash.Normal); // not opponent Refresh
        EndTurn(payment);
        Equal(0, payment.State.Players[0].Trash.Total);
        Equal(4, payment.State.Players[0].Reserve.Normal); // +1 own Core Step
        payment.AssertInvariants();
    }

    private static void MovingAndVanish()
    {
        var match = Match();
        Keep(match);
        int spirit = Summon(match, 0);
        Equal(1, match.FindSpirit(spirit).Level);
        Yes(match.TryMoveCore(0, -1, spirit, false, out var error), error);
        Yes(match.TryMoveCore(0, -1, spirit, false, out error), error);
        Equal(2, match.FindSpirit(spirit).Level);
        Equal(4000, match.FindSpirit(spirit).Bp);
        Yes(match.TryMoveCore(0, spirit, -1, false, out error), error);
        Equal(1, match.FindSpirit(spirit).Level);
        Yes(match.TryMoveCore(0, spirit, -1, false, out error), error);
        Yes(match.TryMoveCore(0, spirit, -1, false, out error), error);
        Yes(match.FindSpirit(spirit) == null);
        Yes(match.LastEvents.Any(e => e.Kind == "Vanished"));
        No(match.LastEvents.Any(e => e.Kind == "Destroyed"));
        Equal(4, match.State.Players[0].Reserve.Total);
        No(match.TryMoveCore(0, -1, int.MaxValue, false, out _));
        match.AssertInvariants();
    }

    private static void FlashPriorityAndFreshAttack()
    {
        var match = Match();
        Keep(match);
        EndTurn(match);
        int attacker = Summon(match, 1);
        BeginAttack(match, attacker);
        Yes(match.FindSpirit(attacker).Exhausted);
        Equal(MatchPhase.FlashBeforeBlock, match.State.Phase);
        Equal(0, match.DecisionPlayerIndex);
        No(match.TryPassFlash(1, out _));
        No(match.TryTakeLife(0, out _));
        No(match.TryEndTurn(1, out _));
        Yes(match.TryPassFlash(0, out var error), error);
        Equal(1, match.DecisionPlayerIndex);
        Equal(MatchPhase.FlashBeforeBlock, match.State.Phase);
        Yes(match.TryPassFlash(1, out error), error);
        Equal(MatchPhase.Block, match.State.Phase);
        Yes(match.TryTakeLife(0, out error), error);
        Equal(MatchPhase.Attack, match.State.Phase); // no second Flash if unblocked
        No(match.TryAttack(1, attacker, out _));
        match.AssertInvariants();
    }

    private static void BlockingAndTie()
    {
        var match = Match();
        Keep(match);
        int blocker = Summon(match, 0);
        EndTurn(match);
        int attacker = Summon(match, 1);
        BeginAttack(match, attacker);
        PassBoth(match);
        No(match.TryBlock(1, attacker, out _));
        match.FindSpirit(blocker).Exhausted = true;
        No(match.TryBlock(0, blocker, out _));
        match.FindSpirit(blocker).Exhausted = false;
        Yes(match.TryBlock(0, blocker, out var error), error);
        Yes(match.FindSpirit(blocker).Exhausted);
        Equal(MatchPhase.FlashAfterBlock, match.State.Phase);
        Equal(0, match.DecisionPlayerIndex);
        PassBoth(match);
        Yes(match.FindSpirit(attacker) == null);
        Yes(match.FindSpirit(blocker) == null);
        Equal(2, match.LastEvents.Count(e => e.Kind == "Destroyed"));
        Equal(5, match.State.Players[0].Life);
        Equal(5, match.State.Players[1].Life);
        Equal(4, match.State.Players[0].Reserve.Total);
        Equal(5, match.State.Players[1].Reserve.Total);
        match.AssertInvariants();
    }

    private static void BlockedBattle()
    {
        var match = Match();
        Keep(match);
        int blocker = Summon(match, 0);
        EndTurn(match);
        int attacker = Summon(match, 1);
        Yes(match.TryMoveCore(1, -1, attacker, false, out var error), error);
        Yes(match.TryMoveCore(1, -1, attacker, false, out error), error);
        BeginAttack(match, attacker);
        PassBoth(match);
        Yes(match.TryBlock(0, blocker, out error), error);
        PassBoth(match);
        Yes(match.FindSpirit(attacker) != null);
        Yes(match.FindSpirit(blocker) == null);
        Equal(5, match.State.Players[0].Life);
        Equal(MatchPhase.Attack, match.State.Phase);
        match.AssertInvariants();
    }

    private static void LifeAndVictory()
    {
        var match = Match(Card(symbols: 2));
        Keep(match);
        EndTurn(match);
        int attacker = Summon(match, 1);
        BeginAttack(match, attacker);
        PassBoth(match);
        Yes(match.TryTakeLife(0, out var error), error);
        Equal(3, match.State.Players[0].Life);
        Equal(6, match.State.Players[0].Reserve.Total);
        Equal(-1, match.State.WinnerIndex);
        // Arrange a valid one-Life position while keeping all cores accounted for.
        match.State.Players[0].Life = 1;
        match.State.Players[0].Reserve.Normal += 2;
        int finisher = SummonAfterMainCycle(match, 1);
        BeginAttack(match, finisher);
        PassBoth(match);
        Yes(match.TryTakeLife(0, out error), error);
        Equal(0, match.State.Players[0].Life);
        Equal(1, match.State.WinnerIndex);
        Equal(MatchPhase.GameOver, match.State.Phase);
        No(match.TryEndTurn(1, out _));
        match.AssertInvariants();
    }

    private static int SummonAfterMainCycle(BattleMatch match, int player)
    {
        EndTurn(match);
        EndTurn(match);
        Equal(player, match.State.ActivePlayerIndex);
        return Summon(match, player);
    }

    private static void DeckOutTiming()
    {
        var match = Match(deckSize: 5);
        Keep(match);
        Equal(0, match.State.Players[0].Deck.Count);
        Equal(-1, match.State.WinnerIndex);
        Equal(MatchPhase.Main, match.State.Phase);
        EndTurn(match);
        Equal(0, match.State.Players[1].Deck.Count);
        Equal(-1, match.State.WinnerIndex);
        EndTurn(match);
        Equal(1, match.State.WinnerIndex);
        Equal(MatchPhase.GameOver, match.State.Phase);
        // A deck-out at Start Step must not produce a Core Step gain.
        Equal(0, match.State.Players[0].CoreGains);
        match.AssertInvariants();
    }

    private static void DeckAndCardValidation()
    {
        var cards = Enumerable.Range(0, 14).Select(i => Card("DECK-" + i)).ToArray();
        var legal = BattleMatch.CreateDefault(cards);
        Equal(36, legal.State.Players[0].Deck.Count);
        Equal(4, legal.State.Players[0].Hand.Count);
        Throws<ArgumentException>(() => BattleMatch.CreateDefault(cards.Take(13).ToArray()));
        Throws<ArgumentException>(() => BattleMatch.Create(cards, Array.Empty<string>(), Array.Empty<string>()));
        var alias = Card("ALIAS");
        alias.nameJa = cards[0].nameJa;
        var withAlias = cards.Concat(new[] { alias }).ToArray();
        var tooMany = cards.SelectMany(c => Enumerable.Repeat(c.id, 3)).Take(39).ToList();
        tooMany.Add(alias.id);
        Throws<ArgumentException>(() => BattleMatch.Create(withAlias, tooMany, tooMany));
        var effect = Card("EFFECT");
        effect.effectTextJa = "このスピリットの召喚時";
        Throws<ArgumentException>(() => BattleMatch.Create(new[] { effect },
            Enumerable.Repeat(effect.id, 40).ToArray(), Enumerable.Repeat(effect.id, 40).ToArray(), validateDecks: false));
    }

    private static void DeterministicSimulation()
    {
        var cards = Enumerable.Range(0, 16).Select(i => Card("SIM-" + i, color: (CardColor)(i % 6))).ToArray();
        var first = BattleMatch.CreateDefault(cards, 718);
        var second = BattleMatch.CreateDefault(cards, 718);
        Equal(string.Join(",", first.State.Players[0].Deck), string.Join(",", second.State.Players[0].Deck));
        Equal(string.Join(",", first.State.Players[1].Hand), string.Join(",", second.State.Players[1].Hand));
        int totalCommands = 0;
        for (int run = 0; run < 64; run++)
            totalCommands += Simulate(BattleMatch.CreateDefault(cards, 718 + run));
        Yes(totalCommands > 1000, "Repeated complete matches should cover more than a thousand legal commands.");
        Console.WriteLine("    Simulated 64 complete matches / " + totalCommands + " legal commands.");
    }

    private static int Simulate(BattleMatch first)
    {
        Keep(first);
        int commands = 0;
        while (first.State.Phase != MatchPhase.GameOver && commands++ < 2000)
        {
            int actor = first.DecisionPlayerIndex;
            switch (first.State.Phase)
            {
                case MatchPhase.Main:
                    var owner = first.State.Players[actor];
                    int usable = owner.Reserve.Total + owner.Field.Sum(s => s.Cores.Total);
                    for (int i = 0; i < owner.Hand.Count; i++)
                    {
                        var card = first.GetCard(owner.Hand[i]);
                        if (first.GetSummonCost(actor, card) + card.levels[0].cores > usable) continue;
                        Yes(first.TrySummon(actor, i, out var summonError), summonError);
                        break;
                    }
                    if (first.State.TurnNumber == 1) EndTurn(first);
                    else Yes(first.TryBeginAttackStep(actor, out var beginError), beginError);
                    break;
                case MatchPhase.Attack:
                    var ready = first.State.Players[actor].Field.FirstOrDefault(s => !s.Exhausted);
                    if (ready == null) EndTurn(first);
                    else Yes(first.TryAttack(actor, ready.InstanceId, out var attackError), attackError);
                    break;
                case MatchPhase.FlashBeforeBlock:
                case MatchPhase.FlashAfterBlock:
                    Yes(first.TryPassFlash(actor, out var passError), passError);
                    break;
                case MatchPhase.Block:
                    // Always block if possible, exercising repeat simultaneous
                    // destructions, core reuse and Refresh until a real win.
                    var blocker = first.State.Players[actor].Field.FirstOrDefault(s => !s.Exhausted);
                    if (blocker == null) Yes(first.TryTakeLife(actor, out var lifeError), lifeError);
                    else Yes(first.TryBlock(actor, blocker.InstanceId, out var blockError), blockError);
                    break;
                default: throw new Exception("Unexpected simulation phase.");
            }
            first.AssertInvariants();
        }
        Yes(first.State.Phase == MatchPhase.GameOver, "Simulation must finish.");
        Yes(commands > 20, "Simulation should cover many legal actions.");
        return commands;
    }

    private static void CatalogIntegration(string path)
    {
        var catalog = JsonSerializer.Deserialize<CardsDatabase>(File.ReadAllText(path),
            new JsonSerializerOptions { IncludeFields = true });
        Yes(catalog != null && catalog.cards.Length >= 14);
        int commands = 0;
        for (int seed = 0; seed < 32; seed++)
        {
            var match = BattleMatch.CreateDefault(catalog.cards, seed);
            foreach (var player in match.State.Players)
            {
                var allIds = player.Deck.Concat(player.Hand).ToArray();
                Equal(40, allIds.Length);
                Yes(allIds.GroupBy(id => match.GetCard(id).nameJa).All(group => group.Count() <= 3));
                Equal(catalog.cards.Length, allIds.Distinct().Count());
            }
            commands += Simulate(match);
        }
        Console.WriteLine("    Official catalog: " + catalog.cards.Length + " cards / 32 complete matches / " + commands + " commands.");
    }

    private static void Yes(bool condition, string message = "Expected true")
    {
        if (!condition) throw new Exception(message);
    }

    private static void No(bool condition) => Yes(!condition, "Expected rejection.");

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception("Expected " + expected + "; got " + actual + ".");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name + ".");
    }
}
