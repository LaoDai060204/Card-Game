using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LaoDai.BattleSpirits.Core;

internal static class ConversionTests
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    private static int cases;
    private static CardDefinition[] official;

    public static int Main(string[] args)
    {
        string path = args.Length == 0 ? "Data/BattleSpirits/cards.json" : args[0];
        official = JsonSerializer.Deserialize<CardsDatabase>(File.ReadAllText(path), Json).cards;
        LegacyRules.Run(new[] { path });
        Case("Catalog can contain unsupported Magic and unknown Spirit statistics", BrowseOnlyCatalog);
        Case("Imported flags cannot falsely implement Magic or non-vanilla abilities", SupportedSubset);
        Case("Catalog JSON round trip retains original metadata and symbols verification", MetadataRoundTrip);
        Case("Starter decks contain exactly forty supported cards and differ", StarterValidation);
        Case("Same original Japanese name shares the three-copy limit across printings", SameNameLimit);
        Case("Restriction on one printing applies to every printing of that name", RestrictionAcrossPrintings);
        Case("Unreleased, banned, and unverified Standard cards cannot enter Eternal", FormatRestrictions);
        Case("Draft validation can inspect unsupported Eternal cards without allowing a match", DraftDoesNotEnableRules);
        Case("Unknown/null IDs and duplicate catalog IDs return validation errors", MalformedDecks);
        Case("Printed copy exceptions require verified catalog metadata and allow twenty copies", CopyExceptions);
        Case("Invalid format names cannot bypass Eternal eligibility", ExactFormat);
        Case("Printed color, reduction and symbols must agree with active rule values", PrintedConsistency);
        Case("Extreme statistics cannot overflow a summon-cost check", ExtremeStatistics);
        Case("Catalog and deck inputs are never mutated by validation or starter building", InputPreservation);
        Case("Dispatcher rejects malformed actors and commands without changing match state", InvalidCommands);
        Case("Dispatcher enforces opening player and phase ownership", OpeningAuthority);
        Case("Dispatcher cannot move another player's cores", CoreOwnership);
        Case("Concession can finish opening and an ended match rejects further play", Concession);
        Case("Dispatcher drives complete legal deterministic matches", DispatcherMatches);
        IndependentAudit.Run(new[] { path });
        Console.WriteLine(JsonSerializer.Serialize(new { result = "PASS", conversionScenarios = cases,
            catalogCards = official.Length, supportedCards = official.Count(c => c.IsPlayable),
            completeDispatcherGames = 24, unityPlayTested = false }));
        return 0;
    }

    private static void Case(string name, Action body)
    {
        body(); cases++; Console.WriteLine("PASS " + name);
    }
    private static void Assert(bool result, string message = "Assertion failed")
    {
        if (!result) throw new Exception(message);
    }
    private static void Throws(Action body)
    {
        bool threw = false;
        try { body(); } catch (ArgumentException) { threw = true; }
        Assert(threw, "Expected a validation exception");
    }
    private static string Snapshot(BattleMatch match) => JsonSerializer.Serialize(match.State, Json);
    private static CardDefinition Clone(CardDefinition card) => JsonSerializer.Deserialize<CardDefinition>(JsonSerializer.Serialize(card, Json), Json);
    private static CardDefinition New(string id)
    {
        var card = Clone(official.First(c => c.IsPlayable));
        card.id = id; card.nameJa = id; card.nameVi = id;
        card.cost = 0; card.reductionSymbols = 0;
        return card;
    }
    private static BattleMatch Match()
    {
        return BattleMatch.CreateDefault(official, 871, 0);
    }
    private static void Confirm(BattleMatch match)
    {
        Assert(BattleCommandDispatcher.TryExecute(match, 0, new BattleCommand { action = "opening" }, out var error), error);
        Assert(BattleCommandDispatcher.TryExecute(match, 1, new BattleCommand { action = "opening" }, out error), error);
    }
    private static bool Dispatch(BattleMatch match, int actor, BattleCommand command)
    {
        string before = Snapshot(match);
        bool success = BattleCommandDispatcher.TryExecute(match, actor, command, out _);
        if (!success) Assert(before == Snapshot(match), "Rejected command changed authoritative state");
        match.AssertInvariants();
        return success;
    }
    private static void BrowseOnlyCatalog()
    {
        var magic = new CardDefinition { id = "NEW-MAGIC", nameJa = "新しいマジック", type = "Magic", cost = 5,
            symbols = 0, levels = null, implemented = false, effectTextJa = "フラッシュ：未対応の効果" };
        var unknown = new CardDefinition { id = "NEW-SPIRIT", nameJa = "新しいスピリット", type = "Spirit",
            symbols = 0, levels = null, implemented = false, symbolsVerified = false };
        var catalog = official.Concat(new[] { magic, unknown }).ToArray();
        var decks = StarterDecks.CreatePair(catalog);
        var match = BattleMatch.Create(catalog, decks[0].cards, decks[1].cards);
        Assert(match.GetCard(magic.id).type == "Magic");
        Assert(match.GetCard(unknown.id).levels == null);
        match.AssertInvariants();
    }
    private static void SupportedSubset()
    {
        var fake = New("FAKE"); fake.type = "Magic"; fake.implemented = true;
        Assert(!fake.IsPlayable);
        fake.type = "Spirit"; fake.effectTextJa = "このスピリットの召喚時、1枚ドローする。";
        Assert(!fake.IsPlayable);
        fake.effectTextJa = "-"; fake.symbolsVerified = false; Assert(!fake.IsPlayable);
        fake.symbolsVerified = true; fake.colors = new[] { "red", "green" }; Assert(!fake.IsPlayable);
        fake.colors = Array.Empty<string>(); fake.levels[0].cores = 0; Assert(!fake.IsPlayable);
    }
    private static void MetadataRoundTrip()
    {
        var item = New("METADATA"); item.families = new[] { "緋炎", "古竜" };
        item.type = "Nexus"; item.setId = "BS76"; item.setName = "永皇の輝き"; item.rarity = "X";
        item.releaseDate = "2026-05-30"; item.format = "Eternal"; item.released = true;
        item.colors = new[] { "red" }; item.reductions = new[] { new Reduction { color = "red", count = 2 } };
        item.symbolDetails = new[] { new CardSymbol { color = "red", count = 1 } };
        item.artPath = "Picture/BattleSpirits/METADATA.png"; item.symbolsVerified = false;
        var restored = Clone(item);
        Assert(restored.nameJa == item.nameJa && restored.families[1] == "古竜");
        Assert(restored.reductions[0].count == 2 && restored.symbolDetails[0].count == 1);
        Assert(restored.type == "Nexus" && !restored.symbolsVerified && restored.setId == "BS76");
    }
    private static void StarterValidation()
    {
        var decks = StarterDecks.CreatePair(official);
        Assert(decks.Length == 2 && decks.All(d => d.cards.Length == 40));
        Assert(decks.All(d => DeckValidator.Validate(official, d.cards).IsValid));
        Assert(!decks[0].cards.SequenceEqual(decks[1].cards));
        Assert(decks.All(d => d.cards.GroupBy(id => official.First(c => c.id == id).nameJa).All(g => g.Count() <= 3)));
        Throws(() => StarterDecks.Create(official.Where(c => c.IsPlayable).Take(13).ToArray()));
    }
    private static void SameNameLimit()
    {
        var original = official.First(c => c.IsPlayable);
        var alias = Clone(original); alias.id = "ALTERNATE-PRINT"; alias.nameVi = "Different translated display name";
        var catalog = official.Concat(new[] { alias }).ToArray();
        var ids = StarterDecks.Create(official).cards.ToList();
        ids.RemoveAll(id => id == original.id);
        ids.AddRange(new[] { original.id, original.id, alias.id, alias.id });
        Assert(!DeckValidator.Validate(catalog, ids).IsValid);
        Assert(!DeckValidator.Validate(catalog, ids, false).IsValid);
    }
    private static void RestrictionAcrossPrintings()
    {
        var original = official.First(c => c.IsPlayable);
        var restrictedPrint = Clone(original); restrictedPrint.id = "RESTRICTED-PRINT"; restrictedPrint.maxCopies = 1;
        var catalog = official.Concat(new[] { restrictedPrint }).ToArray();
        var ids = StarterDecks.Create(official).cards.ToList();
        ids.RemoveAll(id => id == original.id); ids.AddRange(new[] { original.id, original.id });
        Assert(!DeckValidator.Validate(catalog, ids).IsValid);
    }
    private static void FormatRestrictions()
    {
        var baseDeck = StarterDecks.Create(official).cards;
        foreach (string kind in new[] { "unreleased", "banned", "standard" })
        {
            var item = New("RESTRICT-" + kind);
            if (kind == "unreleased") item.released = false;
            if (kind == "banned") item.maxCopies = 0;
            if (kind == "standard") { item.format = "Standard"; item.eternalLegal = false; }
            var catalog = official.Concat(new[] { item }).ToArray();
            Assert(!item.IsEternalEligible && !item.IsPlayable);
            Assert(!DeckValidator.Validate(catalog, baseDeck.Skip(1).Concat(new[] { item.id }).ToArray(), false).IsValid);
        }
    }
    private static void DraftDoesNotEnableRules()
    {
        var item = New("BROWSE-ONLY"); item.implemented = false; item.effectTextJa = "未対応の効果";
        var catalog = official.Concat(new[] { item }).ToArray();
        var ids = StarterDecks.Create(official).cards.Skip(1).Concat(new[] { item.id }).ToArray();
        Assert(DeckValidator.Validate(catalog, ids, false).IsValid);
        Assert(!DeckValidator.Validate(catalog, ids).IsValid);
        Throws(() => BattleMatch.Create(catalog, ids, ids));
    }
    private static void MalformedDecks()
    {
        Assert(!DeckValidator.Validate(null, null).IsValid);
        Assert(!DeckValidator.Validate(official, null).IsValid);
        var ids = StarterDecks.Create(official).cards.ToArray(); ids[0] = null; ids[1] = "NOT-A-CARD";
        Assert(!DeckValidator.Validate(official, ids).IsValid);
        Assert(!DeckValidator.Validate(official.Concat(new[] { official[0] }).ToArray(), StarterDecks.Create(official).cards).IsValid);
        var invalid = new CardDefinition { id = "", nameJa = "" };
        Assert(!DeckValidator.Validate(official.Concat(new[] { invalid }).ToArray(), ids).IsValid);
    }
    private static void CopyExceptions()
    {
        var exception = New("VERIFIED-COPY-EXCEPTION"); exception.maxCopies = 20;
        var catalog = official.Concat(new[] { exception }).ToArray();
        var baseIds = StarterDecks.Create(official).cards;
        var ids = Enumerable.Repeat(exception.id, 20).Concat(baseIds.Take(20)).ToArray();
        Assert(!DeckValidator.Validate(catalog, ids, false).IsValid);
        exception.restrictionVerified = true;
        exception.restrictionSourceUrl = "https://www.battlespirits.com/rule/limited.php";
        Assert(DeckValidator.Validate(catalog, ids, false).IsValid);
        Assert(DeckValidator.Validate(catalog, ids).IsValid);
        var match = BattleMatch.Create(catalog, ids, ids);
        match.AssertInvariants();
        Assert(!DeckValidator.Validate(catalog,
            Enumerable.Repeat(exception.id, 21).Concat(baseIds.Take(19)).ToArray(), false).IsValid);
    }
    private static void ExactFormat()
    {
        foreach (string format in new[] { "NonEternal", "NotEternal", "", null, "Eternal-unknown" })
        {
            var item = New("INVALID-FORMAT"); item.format = format;
            Assert(!item.IsEternalEligible && !item.IsPlayable);
        }
        var both = New("BOTH"); both.format = "Both"; Assert(both.IsPlayable);
    }
    private static void PrintedConsistency()
    {
        var item = New("CONSISTENT"); item.color = CardColor.Red;
        item.colors = new[] { "赤" };
        item.reductions = new[] { new Reduction { color = "red", count = item.reductionSymbols } };
        item.symbolDetails = new[] { new CardSymbol { color = "red", count = item.symbols } };
        Assert(item.IsPlayable);
        item.colors[0] = "blue"; Assert(!item.IsPlayable); item.colors[0] = "red";
        item.reductions[0].color = "green"; Assert(!item.IsPlayable); item.reductions[0].color = "red";
        item.reductions[0].count++; Assert(!item.IsPlayable); item.reductions[0].count--;
        item.symbolDetails[0].count++; Assert(!item.IsPlayable); item.symbolDetails[0].count--;
        item.symbolDetails[0].color = "purple"; Assert(!item.IsPlayable);
        item.symbolDetails[0].color = "red"; Assert(item.IsPlayable);
    }
    private static void ExtremeStatistics()
    {
        var item = New("EXTREME"); item.cost = int.MaxValue;
        var deck = Enumerable.Repeat(item.id, 40).ToArray();
        var match = BattleMatch.Create(new[] { item }, deck, deck, 44, 0, false);
        Confirm(match);
        Assert(!Dispatch(match, 0, new BattleCommand { action = "summon", index = 0 }));
        Assert(match.State.Players[0].Hand.Count == 5);
    }
    private static void InputPreservation()
    {
        string before = JsonSerializer.Serialize(official, Json);
        var starter = StarterDecks.Create(official); string[] ids = starter.cards.ToArray();
        DeckValidator.Validate(official, ids); StarterDecks.CreatePair(official);
        Assert(before == JsonSerializer.Serialize(official, Json));
        Assert(ids.SequenceEqual(starter.cards));
    }
    private static void InvalidCommands()
    {
        var match = Match();
        foreach (int actor in new[] { -1, 2, int.MaxValue })
            foreach (string action in new[] { "opening", "summon", "concede", "moveCore" })
                Assert(!Dispatch(match, actor, new BattleCommand { action = action }));
        Assert(!Dispatch(match, 0, null));
        Assert(!Dispatch(match, 0, new BattleCommand { action = "setLife" }));
        Assert(!BattleCommandDispatcher.TryExecute(null, 0, new BattleCommand(), out _));
    }
    private static void OpeningAuthority()
    {
        var match = Match();
        Assert(!Dispatch(match, 1, new BattleCommand { action = "opening" }));
        Assert(!Dispatch(match, 0, new BattleCommand { action = "summon", index = 0 }));
        Confirm(match);
        Assert(!Dispatch(match, 1, new BattleCommand { action = "summon", index = 0 }));
        Assert(!Dispatch(match, 0, new BattleCommand { action = "passFlash" }));
        Assert(!Dispatch(match, 0, new BattleCommand { action = "summon", index = -1 }));
        Assert(!Dispatch(match, 0, new BattleCommand { action = "summon", index = int.MaxValue }));
        Assert(!Dispatch(match, 0, new BattleCommand { action = "takeLife" }));
    }
    private static void CoreOwnership()
    {
        var first = New("CORE-TEST");
        var deck = Enumerable.Repeat(first.id, 40).ToArray();
        var match = BattleMatch.Create(new[] { first }, deck, deck, 32, 0, false);
        Confirm(match);
        Assert(Dispatch(match, 0, new BattleCommand { action = "summon", index = 0 }));
        int spirit0 = match.State.Players[0].Field[0].InstanceId;
        Assert(Dispatch(match, 0, new BattleCommand { action = "endTurn" }));
        Assert(Dispatch(match, 1, new BattleCommand { action = "summon", index = 0 }));
        int spirit1 = match.State.Players[1].Field[0].InstanceId;
        Assert(!Dispatch(match, 1, new BattleCommand { action = "moveCore", source = spirit0, target = spirit1 }));
        Assert(Dispatch(match, 1, new BattleCommand { action = "moveCore", source = -1, target = spirit1 }));
        Assert(!Dispatch(match, 1, new BattleCommand { action = "moveCore", source = spirit1, target = spirit0 }));
    }
    private static void Concession()
    {
        foreach (int loser in new[] { 0, 1 })
        {
            var match = Match();
            Assert(Dispatch(match, loser, new BattleCommand { action = "concede" }));
            Assert(match.State.Phase == MatchPhase.GameOver && match.State.WinnerIndex == 1 - loser);
            Assert(!Dispatch(match, 1 - loser, new BattleCommand { action = "concede" }));
            Assert(!Dispatch(match, 0, new BattleCommand { action = "opening" }));
        }
    }
    private static void DispatcherMatches()
    {
        for (int seed = 0; seed < 24; seed++)
        {
            var left = BattleMatch.CreateDefault(official, seed, seed % 2);
            var right = BattleMatch.CreateDefault(official, seed, seed % 2);
            int safety = 0;
            while (left.State.Phase != MatchPhase.GameOver && safety++ < 2500)
            {
                int actor = left.DecisionPlayerIndex;
                BattleCommand command;
                switch (left.State.Phase)
                {
                    case MatchPhase.Opening: command = new BattleCommand { action = "opening", redraw = seed % 3 == 0 }; break;
                    case MatchPhase.Main:
                        int summon = left.State.Players[actor].Hand.FindIndex(id => {
                            var card = left.GetCard(id);
                            return left.GetSummonCost(actor, card) + card.levels[0].cores <= left.State.Players[actor].Reserve.Total;
                        });
                        command = summon >= 0 ? new BattleCommand { action = "summon", index = summon }
                            : new BattleCommand { action = left.State.TurnNumber == 1 ? "endTurn" : "attackStep" };
                        break;
                    case MatchPhase.Attack:
                        var attacker = left.State.Players[actor].Field.FirstOrDefault(s => !s.Exhausted);
                        command = attacker == null ? new BattleCommand { action = "endTurn" }
                            : new BattleCommand { action = "attack", index = attacker.InstanceId };
                        break;
                    case MatchPhase.FlashBeforeBlock:
                    case MatchPhase.FlashAfterBlock: command = new BattleCommand { action = "passFlash" }; break;
                    case MatchPhase.Block:
                        var blocker = left.State.Players[actor].Field.FirstOrDefault(s => !s.Exhausted);
                        command = blocker != null && seed % 2 == 0
                            ? new BattleCommand { action = "block", index = blocker.InstanceId }
                            : new BattleCommand { action = "takeLife" };
                        break;
                    default: throw new Exception("Unexpected phase");
                }
                Assert(Dispatch(left, actor, command), "Valid command failed: " + command.action);
                Assert(Dispatch(right, actor, command), "Deterministic twin command failed");
                Assert(Snapshot(left) == Snapshot(right), "Same seed/actions diverged");
            }
            Assert(left.State.Phase == MatchPhase.GameOver, "Dispatcher match did not finish");
        }
    }
}
