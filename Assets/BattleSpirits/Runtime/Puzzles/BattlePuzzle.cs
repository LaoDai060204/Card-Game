using System;
using System.Collections.Generic;
using System.Linq;
using MDPro3.BattleSpirits.Core;

namespace MDPro3.BattleSpirits.Puzzles
{
    [Serializable] public sealed class PuzzleFieldCard
    {
        public string cardId = "";
        public int instanceId, normalCores = 1;
        public bool soul, exhausted, heavyExhausted;
    }
    [Serializable] public sealed class PuzzlePlayer
    {
        public int life = 5, reserveNormal = 8, trashNormal;
        public bool reserveSoul = true, trashSoul;
        public string[] hand = Array.Empty<string>(), discard = Array.Empty<string>();
        public PuzzleFieldCard[] field = Array.Empty<PuzzleFieldCard>();
    }
    [Serializable] public sealed class PuzzleGoal
    {
        public string kind = "Win", cardId = "", phase = "";
        public int owner, instanceId = -1, amount;
    }
    [Serializable] public sealed class PuzzleSolutionStep
    {
        public int actor = -1;
        public BattleCommand command = new BattleCommand();
    }
    [Serializable] public sealed class PuzzleDefinition
    {
        public string id = "", title = "", category = "Basics", description = "", hint = "", previewCardId = "";
        public int seed = 20261116, turnNumber = 2, activePlayer, maxCommands = 20, maxTurnNumber = 2;
        public string phase = "Main";
        public int pendingAttackerId = -1, pendingBlockerId = -1, flashPlayerIndex = -1;
        public PuzzlePlayer[] players = { new PuzzlePlayer(), new PuzzlePlayer() };
        public PuzzleGoal goal = new PuzzleGoal();
        public PuzzleSolutionStep[] solution = Array.Empty<PuzzleSolutionStep>();
    }
    public enum PuzzleStatus { InProgress, Solved, Failed }

    /// <summary>A playable deterministic fixture. Every attempt and solution runs through the same rules commands as Solo.</summary>
    public sealed class PuzzleSession
    {
        private readonly IReadOnlyList<CardDefinition> cards;
        private readonly List<Tuple<int, string, int>> commands = new List<Tuple<int, string, int>>();
        public PuzzleDefinition Definition { get; }
        public BattleMatch Match { get; private set; }
        public PuzzleStatus Status { get; private set; }
        public int CommandCount => commands.Count;
        public string ResultMessage => Status == PuzzleStatus.Solved ? "Puzzle complete." : Status == PuzzleStatus.Failed ? "Puzzle failed. Reset to try again." : Definition.description;
        private PuzzleSession(PuzzleDefinition definition, IReadOnlyList<CardDefinition> catalog)
        { Definition = definition ?? throw new ArgumentNullException(nameof(definition)); cards = catalog ?? throw new ArgumentNullException(nameof(catalog)); Reset(); }
        public static PuzzleSession Create(PuzzleDefinition definition, IReadOnlyList<CardDefinition> cards) => new PuzzleSession(definition, cards);
        public void Reset()
        {
            if (Definition.players == null || Definition.players.Length != 2) throw new ArgumentException("Puzzles require two player fixtures.");
            var decks = new string[2][];
            string filler = cards.First(c => c.id == "26RBS02-002").id;
            for (int owner = 0; owner < 2; owner++)
            {
                var p = Definition.players[owner];
                var ids = p.hand.Concat(p.discard).Concat(p.field.Select(s => s.cardId)).ToList();
                while (ids.Count < 40) ids.Add(filler);
                decks[owner] = ids.ToArray();
            }
            Match = BattleMatch.Create(cards, decks[0], decks[1], Definition.seed, Definition.activePlayer, false, BattleRulesFormat.Standard);
            var state = Match.State;
            state.Phase = (MatchPhase)Enum.Parse(typeof(MatchPhase), Definition.phase);
            state.ActivePlayerIndex = Definition.activePlayer; state.TurnNumber = Definition.turnNumber; state.WinnerIndex = -1;
            state.PendingAttackerId = Definition.pendingAttackerId; state.PendingBlockerId = Definition.pendingBlockerId;
            state.FlashPlayerIndex = Definition.flashPlayerIndex;
            for (int owner = 0; owner < 2; owner++)
            {
                var fixture = Definition.players[owner]; var p = state.Players[owner];
                p.Deck.Clear(); p.Deck.AddRange(decks[owner]); p.Hand.Clear(); p.Discard.Clear(); p.Removed.Clear(); p.Revealed.Clear(); p.Field.Clear();
                foreach (var id in fixture.hand) { if (!p.Deck.Remove(id)) throw new ArgumentException("Missing hand card: " + id); p.Hand.Add(id); }
                foreach (var id in fixture.discard) { if (!p.Deck.Remove(id)) throw new ArgumentException("Missing discard card: " + id); p.Discard.Add(id); }
                foreach (var s in fixture.field)
                {
                    if (!p.Deck.Remove(s.cardId)) throw new ArgumentException("Missing field card: " + s.cardId);
                    p.Field.Add(new SpiritState { InstanceId = s.instanceId, Owner = owner, Card = Match.GetCard(s.cardId),
                        Cores = new CorePool { Normal = s.normalCores, Soul = s.soul }, Exhausted = s.exhausted, HeavyExhausted = s.heavyExhausted });
                }
                p.Life = fixture.life; p.Reserve = new CorePool { Normal = fixture.reserveNormal, Soul = fixture.reserveSoul };
                p.Trash = new CorePool { Normal = fixture.trashNormal, Soul = fixture.trashSoul };
                p.CoreGains = p.Life + p.Reserve.Total + p.Trash.Total + p.Field.Sum(s => s.Cores.Total) - 9;
                if (p.CoreGains < 0) throw new ArgumentException("Puzzle removes cores from conservation: " + Definition.id);
            }
            Match.RebasePuzzleState(); commands.Clear(); Status = PuzzleStatus.InProgress;
        }
        public bool TryExecute(int actor, BattleCommand command, out string error)
        {
            if (Status != PuzzleStatus.InProgress) { error = ResultMessage; return false; }
            if (!Match.TryExecute(actor, command, out error)) return false;
            commands.Add(Tuple.Create(actor, command.action, command.source));
            if (GoalReached()) Status = PuzzleStatus.Solved;
            else if (Match.State.WinnerIndex >= 0 || CommandCount >= Definition.maxCommands || Match.State.TurnNumber > Definition.maxTurnNumber) Status = PuzzleStatus.Failed;
            return true;
        }
        private bool GoalReached()
        {
            var g = Definition.goal; var p = Match.State.Players[g.owner];
            var s = p.Field.FirstOrDefault(x => g.instanceId >= 0 ? x.InstanceId == g.instanceId : x.Card.id == g.cardId);
            switch (g.kind)
            {
                case "Win": return Match.State.WinnerIndex == g.owner;
                case "Summon": return s != null;
                case "Level": return s != null && s.Level >= g.amount;
                case "Attack": return commands.Any(c => c.Item1 == g.owner && c.Item2 == "attack" && c.Item3 == g.instanceId);
                case "Block": return commands.Any(c => c.Item1 == g.owner && c.Item2 == "block" && c.Item3 == g.instanceId);
                case "Life": return p.Life == g.amount;
                case "Destroy": return !p.Field.Any(x => x.InstanceId == g.instanceId) && p.Discard.Contains(g.cardId);
                case "TrashCore": return p.Trash.Normal >= g.amount;
                case "Phase": return Match.State.Phase.ToString() == g.phase;
                case "Refresh": return s != null && !s.HeavyExhausted && s.Exhausted;
                default: return false;
            }
        }
        public bool ValidateSolution(out string error)
        {
            var trial = Create(Definition, cards);
            foreach (var step in Definition.solution)
                if (!trial.TryExecute(step.actor < 0 ? trial.Match.DecisionPlayerIndex : step.actor, step.command, out error)) return false;
            error = trial.Status == PuzzleStatus.Solved ? "" : "Recorded solution did not satisfy the goal: " + Definition.id;
            return trial.Status == PuzzleStatus.Solved;
        }
    }
}
