using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using LaoDai.BattleSpirits.Core;

// Synthetic cards isolate the shared game rules. They are never shipped as
// official cards. Standard legality of imported prints is validated separately.
internal static class StandardRulesTests
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    private static int cases;

    public static void Run()
    {
        Case("Standard requires verified released block-A metadata, not merely a format label", Legality);
        Case("Standard accepts supported Standard-only prints and rejects Eternal-only BS01", DeckEligibility);
        Case("Invalid rules-format values cannot bypass deck or match validation", InvalidFormat);
        Case("Standard opening keeps five Life, three normal plus Soul, four cards and first-player draw", Setup);
        Case("The first player's first turn skips Core, Attack and Second Main", FirstTurn);
        Case("Standard Attack proceeds to Second Main and then the next turn", SecondMainSequence);
        Case("An early end request still stops in Second Main rather than skipping it", EarlyEnd);
        Case("Second Main permits summon, core movement and simultaneous Soul swaps", MainActions);
        Case("Second Main cannot start a second Attack Step or accept the opponent's actions", SecondMainAuthority);
        Case("An unresolved attack or Flash cannot be skipped into Second Main", PendingBattle);
        Case("Soul Core does not grant a vanilla Spirit the Super Unleash level rule", VanillaSoulLevel);
        Case("Only a printed Super Unleash Spirit reaches Lv2 with a single Soul Core", PrintedSuperUnleash);
        Case("Super Unleash is limited to reviewed two-level Standard Spirit metadata", SuperUnleashBoundary);
        Case("Turn-duration BP remains through Second Main and expires at End", SecondMainDuration);
        Case("Standard copy limits do not inherit bans from Eternal-only printings", IndependentLimits);
        Case("Standard starter builders use only eligible implemented cards and preserve format", StarterFormats);
        Case("Dispatcher completes deterministic Standard matches through Second Main", CompleteMatches);
        Console.WriteLine("PASS: " + cases + " Standard v1.0 scenarios.");
    }

    private static void Case(string name, Action body) { body(); cases++; Console.WriteLine("PASS " + name); }
    private static void Yes(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
    private static void Equal<T>(T expected, T actual) { Yes(EqualityComparer<T>.Default.Equals(expected, actual), expected + " != " + actual); }
    private static string Snapshot(BattleMatch match) => JsonSerializer.Serialize(match.State, Json);
    private static void Reject(BattleMatch match, int actor, BattleCommand command)
    {
        string before = Snapshot(match);
        Yes(!BattleCommandDispatcher.TryExecute(match, actor, command, out _), "Illegal command was accepted");
        Equal(before, Snapshot(match)); match.AssertInvariants();
    }
    private static void Command(BattleMatch match, int actor, string action, int index = 0)
    { Yes(BattleCommandDispatcher.TryExecute(match, actor, new BattleCommand { action = action, index = index }, out var error), error); }
    private static CardDefinition Card(string id = "STANDARD-FIXTURE") => new CardDefinition
    {
        id = id, nameJa = id, implemented = true, format = "Standard", eternalLegal = false,
        standardBlock = "A", standardLegal = true, standardLegalityVerified = true,
        cost = 0, color = CardColor.Red, effectTextJa = "-",
        levels = new List<CardLevel> { new CardLevel { cores = 1, bp = 1000 }, new CardLevel { cores = 3, bp = 5000 } }
    };
    private static BattleMatch Match(int first = 0, int seed = 41, CardDefinition card = null)
    {
        card = card ?? Card(); var ids = Enumerable.Repeat(card.id, 40).ToArray();
        return BattleMatch.Create(new[] { card }, ids, ids, seed, first, false, BattleRulesFormat.Standard);
    }
    private static void Keep(BattleMatch match)
    {
        Command(match, match.DecisionPlayerIndex, "opening");
        Command(match, match.DecisionPlayerIndex, "opening");
    }
    private static BattleMatch SecondTurn()
    {
        var match = Match(); Keep(match); Command(match, 0, "endTurn");
        Equal(2, match.State.TurnNumber); Equal(1, match.State.ActivePlayerIndex); return match;
    }

    private static void Legality()
    {
        var card = Card(); Yes(card.IsStandardEligible && card.IsPlayableFor(BattleRulesFormat.Standard));
        Yes(!card.IsEternalEligible && !card.IsPlayable);
        card.standardLegalityVerified = false; Yes(!card.IsStandardEligible);
        card.standardLegalityVerified = true; card.standardBlock = "B"; Yes(!card.IsStandardEligible);
        card.standardBlock = "1"; Yes(!card.IsStandardEligible);
        card.standardBlock = "A"; card.standardLegal = false; Yes(!card.IsStandardEligible);
        card.standardLegal = true; card.released = false; Yes(!card.IsStandardEligible);
        card.released = true; card.maxCopies = 0; Yes(!card.IsStandardEligible);
        card.maxCopies = 3; card.format = "Eternal"; Yes(!card.IsStandardEligible);
        card.format = "Both"; Yes(card.IsStandardEligible);
        card.effectTextJa = "未対応の効果"; Yes(!card.IsPlayableFor(BattleRulesFormat.Standard));
    }
    private static CardDefinition[] Catalog() => Enumerable.Range(0, 16).Select(i => Card("STANDARD-" + i.ToString("D2"))).ToArray();
    private static void DeckEligibility()
    {
        var catalog = Catalog(); var deck = StarterDecks.Create(catalog, format: BattleRulesFormat.Standard);
        Yes(DeckValidator.Validate(catalog, deck.cards, format: BattleRulesFormat.Standard).IsValid);
        Yes(!DeckValidator.Validate(catalog, deck.cards).IsValid);
        var old = Card("BS01-FIXTURE"); old.format = "Eternal"; old.eternalLegal = true;
        old.standardBlock = string.Empty; old.standardLegal = false; old.standardLegalityVerified = false;
        var both = catalog.Concat(new[] { old }).ToArray(); var ids = deck.cards.ToArray(); ids[0] = old.id;
        Yes(!DeckValidator.Validate(both, ids, format: BattleRulesFormat.Standard).IsValid);
        bool rejected = false;
        try { BattleMatch.Create(both, ids, ids, format: BattleRulesFormat.Standard); }
        catch (ArgumentException) { rejected = true; }
        Yes(rejected, "Eternal-only printing entered Standard match");
    }
    private static void InvalidFormat()
    {
        var catalog = Catalog(); var ids = StarterDecks.Create(catalog, format: BattleRulesFormat.Standard).cards;
        Yes(!DeckValidator.Validate(catalog, ids, format: (BattleRulesFormat)99).IsValid);
        Yes(!catalog[0].IsPlayableFor((BattleRulesFormat)99));
        bool rejected = false;
        try { BattleMatch.Create(catalog, ids, ids, format: (BattleRulesFormat)99); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Yes(rejected);
    }
    private static void Setup()
    {
        var match = Match(first: 1); Equal(BattleRulesFormat.Standard, match.RulesFormat); Equal("Standard", match.State.Format);
        foreach (var player in match.State.Players)
        { Equal(5, player.Life); Equal(3, player.Reserve.Normal); Yes(player.Reserve.Soul); Equal(4, player.Hand.Count); }
        Equal(1, match.DecisionPlayerIndex); Keep(match); Equal(5, match.State.Players[1].Hand.Count);
    }
    private static void FirstTurn()
    {
        var match = Match(first: 1); Keep(match);
        Equal(0, match.State.Players[1].CoreGains);
        Reject(match, 1, new BattleCommand { action = "attackStep" });
        Reject(match, 1, new BattleCommand { action = "secondMain" });
        Command(match, 1, "endTurn"); Equal(2, match.State.TurnNumber);
        Equal(MatchPhase.Main, match.State.Phase); Equal(0, match.State.ActivePlayerIndex);
        Equal(1, match.State.Players[0].CoreGains);
    }
    private static void SecondMainSequence()
    {
        var match = SecondTurn(); Command(match, 1, "attackStep"); Command(match, 1, "secondMain");
        Equal(MatchPhase.SecondMain, match.State.Phase); Equal(2, match.State.TurnNumber);
        Equal(1, match.DecisionPlayerIndex); Yes(match.LastEvents.Any(e => e.Kind == "SecondMainStarted"));
        Command(match, 1, "endTurn"); Equal(3, match.State.TurnNumber); Equal(0, match.State.ActivePlayerIndex);
    }
    private static void EarlyEnd()
    {
        var match = SecondTurn(); Command(match, 1, "endTurn");
        Equal(MatchPhase.SecondMain, match.State.Phase); Equal(2, match.State.TurnNumber);
        Command(match, 1, "endTurn"); Equal(3, match.State.TurnNumber);
        var other = SecondTurn(); Command(other, 1, "attackStep"); Command(other, 1, "endTurn");
        Equal(MatchPhase.SecondMain, other.State.Phase);
    }
    private static void MainActions()
    {
        var match = SecondTurn(); Command(match, 1, "endTurn"); Command(match, 1, "summon");
        var owner = match.State.Players[1]; var spirit = owner.Field.Single();
        Yes(BattleCommandDispatcher.TryExecute(match, 1, new BattleCommand { action = "moveCore", target = spirit.InstanceId }, out var error), error);
        Equal(2, spirit.Cores.Total);
        Yes(BattleCommandDispatcher.TryExecute(match, 1, new BattleCommand { action = "swapCore", source = spirit.InstanceId, target = -1 }, out error), error);
        Equal(2, spirit.Cores.Total); Yes(spirit.Cores.Soul); Equal(MatchPhase.SecondMain, match.State.Phase);
        match.AssertInvariants();
    }
    private static void SecondMainAuthority()
    {
        var match = SecondTurn(); Command(match, 1, "endTurn");
        Reject(match, 1, new BattleCommand { action = "attackStep" });
        Reject(match, 1, new BattleCommand { action = "secondMain" });
        Reject(match, 0, new BattleCommand { action = "summon" });
        Reject(match, 0, new BattleCommand { action = "endTurn" });
    }
    private static void PendingBattle()
    {
        var match = SecondTurn(); Command(match, 1, "summon"); var spirit = match.State.Players[1].Field.Single();
        Command(match, 1, "attackStep"); Command(match, 1, "attack", spirit.InstanceId);
        Reject(match, 1, new BattleCommand { action = "secondMain" });
        Reject(match, 1, new BattleCommand { action = "endTurn" });
        Command(match, 0, "passFlash"); Command(match, 1, "passFlash");
        Equal(MatchPhase.Block, match.State.Phase);
        Reject(match, 1, new BattleCommand { action = "secondMain" });
        Command(match, 0, "takeLife"); Equal(MatchPhase.Attack, match.State.Phase);
        Command(match, 1, "secondMain"); Equal(MatchPhase.SecondMain, match.State.Phase);
    }
    private static void VanillaSoulLevel()
    {
        var match = SecondTurn(); Command(match, 1, "summon"); var spirit = match.State.Players[1].Field.Single();
        Yes(match.TrySwapCore(1, spirit.InstanceId, -1, false, out var error), error);
        Yes(spirit.Cores.Soul); Equal(1, spirit.Cores.Total); Equal(1, spirit.Level); Equal(1000, spirit.Bp);
    }
    private static void PrintedSuperUnleash()
    {
        var card = Card(); card.superUnleash = true;
        var match = Match(card: card); Keep(match); Command(match, 0, "summon");
        var spirit = match.State.Players[0].Field.Single(); Equal(1, spirit.Level);
        Yes(match.TrySwapCore(0, spirit.InstanceId, -1, false, out var error), error);
        Equal(1, spirit.Cores.Total); Yes(spirit.Cores.Soul); Equal(2, spirit.Level); Equal(5000, spirit.Bp);
        Yes(match.TrySwapCore(0, spirit.InstanceId, -1, true, out error), error);
        Equal(1, spirit.Level);
        Yes(match.TryMoveCore(0, -1, spirit.InstanceId, false, out error), error);
        Equal(1, spirit.Level);
        Yes(match.TryMoveCore(0, -1, spirit.InstanceId, false, out error), error);
        Equal(3, spirit.Cores.Total); Equal(2, spirit.Level);
        match.AssertInvariants();
    }
    private static void SuperUnleashBoundary()
    {
        var card = Card(); card.superUnleash = true; Yes(card.IsPlayableFor(BattleRulesFormat.Standard));
        card.format = "Both"; card.eternalLegal = true; Yes(!card.IsPlayable, "Unverified Eternal keyword implementation accepted");
        card.standardLegalityVerified = false; Yes(!card.IsPlayableFor(BattleRulesFormat.Standard));
        card.standardLegalityVerified = true; card.type = "Magic"; Yes(!card.IsPlayableFor(BattleRulesFormat.Standard));
        card.type = "Spirit"; card.levels.Add(new CardLevel { cores = 5, bp = 9000 });
        Yes(!card.IsPlayableFor(BattleRulesFormat.Standard), "Unreviewed multi-level variant accepted");
    }
    private static void SecondMainDuration()
    {
        var match = SecondTurn(); Command(match, 1, "summon");
        var spirit = match.State.Players[1].Field.Single();
        // Fixture represents an already resolved duration effect; no unreviewed
        // official ability is being marked implemented by this rules test.
        spirit.TurnBpBonus = 2000; Equal(3000, spirit.Bp);
        Command(match, 1, "attackStep"); Command(match, 1, "secondMain");
        Equal(3000, spirit.Bp); Command(match, 1, "endTurn"); Equal(1000, spirit.Bp);
    }
    private static void IndependentLimits()
    {
        var catalog = Catalog(); var deck = StarterDecks.Create(catalog, format: BattleRulesFormat.Standard);
        var old = Card("ETERNAL-BANNED"); old.nameJa = catalog[0].nameJa;
        old.format = "Eternal"; old.eternalLegal = true; old.maxCopies = 0;
        old.standardBlock = string.Empty; old.standardLegalityVerified = false;
        Yes(DeckValidator.Validate(catalog.Concat(new[] { old }).ToArray(), deck.cards, format: BattleRulesFormat.Standard).IsValid);
        var restricted = Card("STANDARD-RESTRICTED"); restricted.nameJa = catalog[0].nameJa; restricted.maxCopies = 1;
        Yes(!DeckValidator.Validate(catalog.Concat(new[] { restricted }).ToArray(), deck.cards, format: BattleRulesFormat.Standard).IsValid);
        restricted.maxCopies = 0; restricted.standardLegal = false;
        var withBan = catalog.Concat(new[] { restricted }).ToArray();
        Yes(!DeckValidator.Validate(withBan, deck.cards, format: BattleRulesFormat.Standard).IsValid);
        var rebuilt = StarterDecks.Create(withBan, format: BattleRulesFormat.Standard);
        Yes(!rebuilt.cards.Contains(catalog[0].id));
    }
    private static void StarterFormats()
    {
        var catalog = Catalog(); var browse = Card("UNSUPPORTED"); browse.implemented = false;
        var decks = StarterDecks.CreatePair(catalog.Concat(new[] { browse }).ToArray(), BattleRulesFormat.Standard);
        Yes(decks.All(d => d.format == "Standard" && d.cards.Length == 40 && !d.cards.Contains(browse.id)));
        Yes(!decks[0].cards.SequenceEqual(decks[1].cards));
    }
    private static void CompleteMatches()
    {
        for (int seed = 0; seed < 12; seed++)
        {
            var match = Match(first: seed % 2, seed: seed); Keep(match); int secondMains = 0;
            for (int action = 0; action < 2000 && match.State.WinnerIndex < 0; action++)
            {
                int actor = match.DecisionPlayerIndex; var owner = match.State.Players[actor];
                switch (match.State.Phase)
                {
                    case MatchPhase.Main:
                        if (owner.Hand.Count > 0 && owner.Reserve.Total > 1) Command(match, actor, "summon");
                        else if (match.State.TurnNumber > 1) Command(match, actor, "attackStep");
                        else Command(match, actor, "endTurn");
                        break;
                    case MatchPhase.Attack:
                        var attacker = owner.Field.FirstOrDefault(s => !s.Exhausted);
                        if (attacker == null) Command(match, actor, "secondMain");
                        else Command(match, actor, "attack", attacker.InstanceId);
                        break;
                    case MatchPhase.SecondMain:
                        secondMains++; Command(match, actor, "endTurn"); break;
                    case MatchPhase.FlashBeforeBlock: case MatchPhase.FlashAfterBlock:
                        Command(match, actor, "passFlash"); break;
                    case MatchPhase.Block: Command(match, actor, "takeLife"); break;
                    default: throw new Exception("Unexpected phase: " + match.State.Phase);
                }
                match.AssertInvariants();
            }
            Yes(match.State.WinnerIndex >= 0 && secondMains > 0, "Standard match did not complete through Second Main");
        }
    }
}
