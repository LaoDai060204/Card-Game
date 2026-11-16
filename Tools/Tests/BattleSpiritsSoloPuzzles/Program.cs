using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MDPro3.BattleSpirits.Core;
using MDPro3.BattleSpirits.AI;
using MDPro3.BattleSpirits.Puzzles;

static class SoloPuzzleTests
{
    public sealed class DeckFile { public DeckDefinition[] decks = Array.Empty<DeckDefinition>(); }
    static int checks;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static int Main(string[] args)
    {
        string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath(".");
        var json = new JsonSerializerOptions { IncludeFields = true, Converters = { new JsonStringEnumConverter() } };
        var cards = JsonSerializer.Deserialize<CardsDatabase>(File.ReadAllText(Path.Combine(root, "Assets/StreamingAssets/BattleSpirits/cards.json")), json).cards;
        Check(cards.Length == 62 && cards.All(c => c.implemented && c.IsPlayable), "Actual shipped catalog exposes all 62 implemented profiles");
        Check(cards.Count(c => c.ExSymbols > 0) == 23 && cards.Count(c => c.IsMagic && c.ExSymbols > 0) == 8, "Printed EX roster includes eight Magic cards");
        Check(BattlePuzzleCatalog.All.Count >= 10, "At least ten playable Battle Spirits puzzles");
        foreach (var definition in BattlePuzzleCatalog.All)
        {
            string path = Path.Combine(root, "Data/BattleSpirits/Puzzles", definition.id + ".json");
            var disk = JsonSerializer.Deserialize<PuzzleDefinition>(File.ReadAllText(path), json);
            Check(JsonSerializer.Serialize(definition, json) == JsonSerializer.Serialize(disk, json), "JSON and pure built-in match: " + definition.id);
            Check(File.ReadAllText(path) == File.ReadAllText(Path.Combine(root, "Assets/StreamingAssets/BattleSpirits/Puzzles", definition.id + ".json")), "Shipped fixture matches source");
            var session = PuzzleSession.Create(disk, cards);
            Check(session.Status == PuzzleStatus.InProgress, "Puzzle starts active");
            Check(session.ValidateSolution(out var error), definition.id + ": " + error);
            foreach (var step in disk.solution)
                Check(session.TryExecute(step.actor < 0 ? session.Match.DecisionPlayerIndex : step.actor, step.command, out error), "Live solution: " + error);
            Check(session.Status == PuzzleStatus.Solved, "Live goal recorded");
            session.Reset();
            Check(session.Status == PuzzleStatus.InProgress && session.CommandCount == 0, "Reset creates clean playable state");
            Check(!session.TryExecute(0, new BattleCommand { action = "not-a-rule" }, out error) && session.CommandCount == 0, "Invalid input does not consume a puzzle attempt");
            Check(session.TryExecute(0, new BattleCommand { action = "concede" }, out error) && session.Status == PuzzleStatus.Failed, "Conceding fails the challenge");
            Console.WriteLine("PASS " + definition.id);
        }
        var bot = new BattleSpiritsBot(cards);
        var fixture = PuzzleSession.Create(BattlePuzzleCatalog.All.First(p => p.id == "bs-03-level-up"), cards);
        var match = fixture.Match; var view = match.GetSnapshot(0); var legal = match.GetLegalCommands(0);
        Check(view.Players[1].Hand.Length == 0, "Opponent hand is filtered");
        var move = bot.Choose(view, legal, BotDifficulty.Expert, 42);
        Check(legal.Contains(move), "AI only selects engine legal candidates");
        Check(JsonSerializer.Serialize(move, json) == JsonSerializer.Serialize(bot.Choose(view, legal, BotDifficulty.Expert, 42), json), "AI deterministic by seed");
        Check(match.TryExecute(0, move, out var why), "AI chosen command executes: " + why);
        Check(bot.Choose(match.GetSnapshot(1), legal) == null, "AI cannot decide for another seat");
        var pool = cards.SelectMany(c => Enumerable.Repeat(c.id, 3)).ToArray();
        var handA = BattleSpiritsBot.SampleOpponentHand(view, pool, 7);
        match.State.Players[1].Hand.Reverse();
        var handB = BattleSpiritsBot.SampleOpponentHand(match.GetSnapshot(0), pool, 7);
        Check(handA.SequenceEqual(handB), "Hidden hand order cannot affect public sampler");
        var decks = JsonSerializer.Deserialize<DeckFile>(File.ReadAllText(Path.Combine(root, "Assets/StreamingAssets/BattleSpirits/starter-decks.json")), json).decks;
        Check(decks.Length == 4 && decks.All(d => d.cards.Length == 40 && DeckValidator.Validate(cards, d.cards).IsValid), "Four exact 40-card shipped decks pass actual implementation and legality validation");
        foreach (var first in decks) foreach (var second in decks)
        {
            var duel = BattleMatch.Create(cards, first.cards, second.cards, 20261116);
            var policies = new[] { new BattleSpiritsBot(cards, second.cards), new BattleSpiritsBot(cards, first.cards) };
            int steps = 0;
            while (duel.State.WinnerIndex < 0 && steps < 3000)
            {
                int actor = duel.DecisionPlayerIndex;
                var candidates = duel.GetLegalCommands(actor);
                var decision = policies[actor].Choose(duel.GetSnapshot(actor), candidates, BotDifficulty.Expert, steps + 2026);
                Check(decision != null, "Self-play always has a decision");
                Check(duel.TryExecute(actor, decision, out var error), first.name + " vs " + second.name + " step " + steps + ": " + decision.action + " rejected: " + error);
                duel.AssertInvariants(); steps++;
            }
            Check(duel.State.WinnerIndex >= 0, first.name + " vs " + second.name + " completes without a policy stall (phase " + duel.State.Phase + ", turn " + duel.State.TurnNumber + ")");
            Console.WriteLine("PASS self-play " + first.name + " vs " + second.name + " (" + steps + " commands, winner " + duel.State.WinnerIndex + ")");
        }
        Console.WriteLine($"PASS {checks} assertions: 10 deterministic puzzles, reset/fail flow, 16 full AI pairings and hidden-information boundary.");
        return 0;
    }
}
