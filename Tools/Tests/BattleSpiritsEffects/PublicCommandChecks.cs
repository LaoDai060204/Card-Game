using System;
using System.Collections.Generic;
using System.Linq;
using MDPro3.BattleSpirits.Core;

static partial class EffectsTests
{
    // These fixtures arrange cards and cores, but every action and effect choice below
    // enters through the same public dispatcher and legal-command list used by the UI/AI.
    static void ExecutePublic(BattleMatch match, int actor, BattleCommand command, string context)
    {
        Check(BattleCommandDispatcher.TryExecute(match, actor, command, out var error), context + ": " + error);
        match.AssertInvariants();
    }

    static void ResolvePublic(BattleMatch match, bool useOptional = true)
    {
        int guard = 0;
        while (match.State.PendingChoice != null)
        {
            Check(++guard < 80, "Public effect choices terminate");
            var choice = match.State.PendingChoice;
            int count = useOptional ? choice.Max : choice.Min;
            var command = match.GetLegalCommands(choice.Owner).FirstOrDefault(c => c.action == "choose" && c.selection.Length == count);
            Check(command != null, "The legal dispatcher exposes the pending effect choice: " + choice.Prompt);
            ExecutePublic(match, choice.Owner, command, "Resolve " + choice.Prompt);
        }
        match.AssertInvariants();
    }

    static BattleCommand PublicAction(BattleMatch match, int actor, Func<BattleCommand, bool> predicate, string context)
    {
        var command = match.GetLegalCommands(actor).FirstOrDefault(predicate);
        Check(command != null, "Legal commands expose " + context);
        return command;
    }

    static void PublicPass(BattleMatch match)
    {
        int actor = match.DecisionPlayerIndex;
        ExecutePublic(match, actor, PublicAction(match, actor, c => c.action == "pass", "Flash pass"), "Flash pass");
    }

    static BattleMatch PublicFixture(string id)
    {
        var match = Fresh();
        Put(match, "26RCB01-040", normal: 3);
        Put(match, "26RCB01-060", normal: 2);
        if (id != "26RCB01-071")
        {
            Put(match, "26RBS01-003", normal: 2);
            Put(match, "26RSD03-005", normal: 2);
        }
        Put(match, "26RBS01-003", 1);
        Put(match, "26RBS01-019", 1, normal: 3, soul: true);
        Put(match, "26RBS01-003", 1);
        Put(match, "26RSD09-011", 1);
        Trash(match, "26RBS03-010");
        Trash(match, "26RBS01-019");
        Hand(match, "26RSD03-005");
        match.State.Players[0].Deck[0] = "26RBS03-010";
        match.State.Players[0].Deck[1] = "26RCB01-040";
        match.RebasePuzzleState();
        return match;
    }

    static void PublicCommandReachability()
    {
        int handPlays = 0, attacks = 0, blocks = 0, magicModes = 0, soulMagics = 0, activations = 0, successions = 0;
        foreach (var card in Catalog)
        {
            foreach (var phase in new[] { MatchPhase.Main, MatchPhase.SecondMain })
            {
                var match = PublicFixture(card.id);
                Hand(match, card.id); int index = match.State.Players[0].Hand.Count - 1;
                match.State.Phase = phase;
                var command = PublicAction(match, 0, c => c.index == index && c.action == (card.IsMagic ? "magic" : "summon") && c.mode == 0 && !c.alternateCost && !c.chooseCores && !c.chooseSuccession && c.payment == null && c.succession == null, card.id + " hand play in " + phase);
                var events = new List<BattleEvent>(); match.EventEmitted += events.Add;
                ExecutePublic(match, 0, command, card.id + " hand play"); ResolvePublic(match);
                Check(events.Any(e => e.CardId == card.id && e.Kind == (card.IsMagic ? "MagicPlayed" : card.IsNexus ? "NexusPlaced" : "Summoned")), card.id + " emits its actual hand-play event");
                handPlays++;
            }

            if (card.IsSpirit)
            {
                foreach (bool blocking in new[] { false, true })
                {
                    var match = Fresh();
                    var participant = Put(match, card.id, normal: Math.Max(1, card.levels.Last().cores), soul: true);
                    Put(match, card.HasFamily("EX-AID") ? "26RCB01-040" : "26RSD03-005");
                    var enemy = Put(match, "26RBS01-003", 1, normal: 3);
                    Put(match, "26RBS01-019", 1, normal: 3);
                    Trash(match, "26RBS03-010");
                    match.RebasePuzzleState();
                    match.State.Phase = MatchPhase.Attack;
                    match.State.ActivePlayerIndex = blocking ? 1 : 0;
                    var events = new List<BattleEvent>(); match.EventEmitted += events.Add;
                    int actor = blocking ? 1 : 0; int source = blocking ? enemy.InstanceId : participant.InstanceId;
                    ExecutePublic(match, actor, PublicAction(match, actor, c => c.action == "attack" && c.source == source, card.id + " attack window"), "Declare attack");
                    ResolvePublic(match);
                    if (blocking)
                    {
                        PublicPass(match); PublicPass(match);
                        ExecutePublic(match, 0, PublicAction(match, 0, c => c.action == "block" && c.source == participant.InstanceId, card.id + " block"), "Declare block");
                        ResolvePublic(match); blocks++;
                    }
                    else attacks++;
                    Check(events.Any(e => e.SpiritId == participant.InstanceId && e.Kind == (blocking ? "Blocked" : "Attacked")), card.id + " reaches its " + (blocking ? "blocking" : "attacking") + " effect window");
                }
            }

            if (card.IsMagic)
            {
                // Reach both real Flash windows by attacking, passing and blocking.
                foreach (bool afterBlock in new[] { false, true })
                {
                    var match = PublicFixture(card.id);
                    Hand(match, card.id); int index = match.State.Players[0].Hand.Count - 1;
                    var attacker = match.State.Players[0].Field.First(s => s.Card.IsSpirit);
                    var blocker = match.State.Players[1].Field.First(s => s.Card.IsSpirit);
                    match.State.Phase = MatchPhase.Attack;
                    ExecutePublic(match, 0, PublicAction(match, 0, c => c.action == "attack" && c.source == attacker.InstanceId, "attacker for Magic"), "Declare Magic test attack"); ResolvePublic(match);
                    PublicPass(match);
                    if (afterBlock)
                    {
                        PublicPass(match);
                        ExecutePublic(match, 1, PublicAction(match, 1, c => c.action == "block" && c.source == blocker.InstanceId, "blocker for Magic"), "Declare Magic test block"); ResolvePublic(match);
                        PublicPass(match);
                    }
                    Check(match.State.Phase == (afterBlock ? MatchPhase.FlashAfterBlock : MatchPhase.FlashBeforeBlock), "Actual Magic Flash window");
                    ExecutePublic(match, 0, PublicAction(match, 0, c => c.action == "magic" && c.index == index && c.mode == 0 && !c.alternateCost && !c.chooseCores && c.payment == null && c.succession == null, card.id + " Flash mode"), card.id + " Flash mode"); ResolvePublic(match);
                    Check(match.State.Players[0].Discard.Contains(card.id), card.id + " Flash resolves into Trash"); magicModes++;
                }
                if (AbilityRegistry.HasMainMagic(card.id))
                {
                    foreach (var phase in new[] { MatchPhase.Main, MatchPhase.SecondMain })
                    {
                        var match = PublicFixture(card.id); Hand(match, card.id); int index = match.State.Players[0].Hand.Count - 1; match.State.Phase = phase;
                        ExecutePublic(match, 0, PublicAction(match, 0, c => c.action == "magic" && c.index == index && c.mode == 1 && !c.chooseCores && c.payment == null && c.succession == null, card.id + " Main mode"), card.id + " Main mode"); ResolvePublic(match); magicModes++;
                    }
                }
                if (AbilityRegistry.SoulMagicColor(card.id, out var color))
                {
                    var match = Fresh();
                    string symbolId = color == CardColor.Red ? "26RBS01-003" : color == CardColor.Purple ? "26RBS01-019" : color == CardColor.Green ? "26RSD03-005" : "26RCB01-040";
                    Put(match, symbolId); Put(match, "26RBS01-003", 1); Put(match, "26RBS01-003", 1);
                    Hand(match, card.id); int index = match.State.Players[0].Hand.Count - 1; match.RebasePuzzleState();
                    var events = new List<BattleEvent>(); match.EventEmitted += events.Add;
                    ExecutePublic(match, 0, PublicAction(match, 0, c => c.action == "magic" && c.index == index && c.alternateCost, card.id + " Soul Magic payment"), card.id + " Soul Magic"); ResolvePublic(match);
                    Check(match.State.Players[0].Trash.Soul && events.Any(e => e.Kind == "MagicPlayed" && e.CardId == card.id && e.Amount == 1), card.id + " pays exactly its Soul Core alternate cost"); soulMagics++;
                }
            }

            if (AbilityRegistry.HasSuccession(card.id))
            {
                var match = Fresh();
                string exId = card.color == CardColor.Red ? "26RBS01-003" : card.color == CardColor.Purple ? "26RBS01-019" : card.color == CardColor.Green ? "26RSD03-005" : "26RCB01-040";
                Trash(match, exId); Hand(match, card.id); int index = match.State.Players[0].Hand.Count - 1;
                ExecutePublic(match, 0, PublicAction(match, 0, c => c.index == index && c.action == (card.IsMagic ? "magic" : "summon") && c.succession?.SequenceEqual(new[] { exId }) == true && !c.chooseCores && c.payment == null, card.id + " Succession"), card.id + " Succession"); ResolvePublic(match);
                Check(match.State.Players[0].Removed.Contains(exId), card.id + " removes the actual EX card for Succession"); successions++;
            }
        }

        foreach (string id in new[] { "26RBS03-X04", "26RSD03-010", "26RCB01-052", "26RSD03-X02" })
        {
            foreach (bool secondWindow in new[] { false, true })
            {
                var match = Fresh(); var source = Put(match, id, normal: 5, soul: true);
                var ally = Put(match, "26RSD03-005"); ally.Exhausted = true;
                Hand(match, "26RSD03-005"); Hand(match, "26RCB01-X07"); Trash(match, "26RBS03-010"); source.Exhausted = true;
                var enemy = Put(match, "26RBS01-003", 1, normal: 3); match.RebasePuzzleState();
                match.State.Phase = secondWindow ? MatchPhase.SecondMain : MatchPhase.Main;
                if (id == "26RSD03-X02")
                {
                    var attacker = Put(match, "26RBS01-003"); match.RebasePuzzleState(); match.State.Phase = MatchPhase.Attack;
                    ExecutePublic(match, 0, PublicAction(match, 0, c => c.action == "attack" && c.source == attacker.InstanceId, "Dantecreo attack"), "Dantecreo attack"); ResolvePublic(match); PublicPass(match);
                    if (secondWindow) { PublicPass(match); ExecutePublic(match, 1, PublicAction(match, 1, c => c.action == "block" && c.source == enemy.InstanceId, "Dantecreo block"), "Dantecreo block"); ResolvePublic(match); PublicPass(match); }
                }
                ExecutePublic(match, 0, PublicAction(match, 0, c => c.action == "activate" && c.source == source.InstanceId, id + " activation"), id + " activation"); ResolvePublic(match);
                if (id == "26RCB01-052") Check(match.FindSpirit(source.InstanceId) == null && match.State.Players[0].Field.Any(s => s.Card.id == "26RCB01-X07"), "F Change physically replaces Maximum Gamer");
                else if (id == "26RSD03-X02") Check(!ally.Exhausted, "Dantecreo physically refreshes its selected ally");
                else if (id == "26RBS03-X04") Check(match.GetLegalCommands(0).All(c => c.action != "activate" || c.source != source.InstanceId), "Rindorin consumes its once-per-turn activation");
                else Check(match.State.Players[0].Hand.Count == 6, "Green Abyss returns one card and draws one");
                activations++;
            }
        }
        Check(handPlays == 124 && attacks == 43 && blocks == 43 && magicModes == 40 && soulMagics == 10 && activations == 8 && successions == 14, "Full 62-card public-command coverage counts");
        Console.WriteLine($"Public commands: {handPlays} hand plays, {attacks} attacks, {blocks} blocks, {magicModes} Magic modes/windows, {soulMagics} Soul Magic costs, {activations} activations, {successions} Successions.");
    }

    static void LevelUpDepletionThroughCommands()
    {
        foreach (string id in new[] { "26RCB01-039", "26RCB01-043", "26RCB01-047", "26RCB01-X06" })
        {
            foreach (bool blocking in new[] { false, true })
            {
                var match = Fresh(); var rider = Put(match, id);
                var opponent = Put(match, "26RBS01-003", 1, normal: 3);
                Hand(match, "26RSD02-014", 1); int magicIndex = match.State.Players[1].Hand.Count - 1;
                match.RebasePuzzleState(); match.State.Phase = MatchPhase.Attack; match.State.ActivePlayerIndex = blocking ? 1 : 0;
                var events = new List<BattleEvent>(); match.EventEmitted += events.Add;
                int actor = blocking ? 1 : 0, source = blocking ? opponent.InstanceId : rider.InstanceId;
                ExecutePublic(match, actor, PublicAction(match, actor, c => c.action == "attack" && c.source == source, "Level Up attack"), "Level Up attack"); ResolvePublic(match);
                if (blocking)
                {
                    PublicPass(match); PublicPass(match);
                    ExecutePublic(match, 0, PublicAction(match, 0, c => c.action == "block" && c.source == rider.InstanceId, "Level Up block"), "Level Up block"); ResolvePublic(match); PublicPass(match);
                }
                Check(rider.Level == 2 && rider.Cores.Total == 1, id + " genuinely Levels Up with only one physical core");
                ExecutePublic(match, 1, PublicAction(match, 1, c => c.action == "magic" && c.index == magicIndex && !c.alternateCost && !c.chooseCores && c.payment == null, "remove Level Up's last core"), "Soul Bite on Level Up Spirit"); ResolvePublic(match);
                Check(rider.Cores.Total == 0 && rider.Level == 0 && rider.Bp == 0 && match.FindSpirit(rider.InstanceId) == null, id + " depletes despite its active Lv2 battle override");
                Check(events.Any(e => e.Kind == "Depleted" && e.SpiritId == rider.InstanceId) && !events.Any(e => e.Kind == "Destroyed" && e.SpiritId == rider.InstanceId), id + " last-core removal causes Depletion");
                Check(match.State.Players[0].Discard.Contains(id) && match.State.Players[1].OpponentDepletedThisTurn, "Depletion returns the card and records the opposing effect");
            }
        }

        // Paying from the attacking Level Up Spirit is another public route to zero cores.
        var paymentMatch = Fresh(); var payer = Put(paymentMatch, "26RCB01-039");
        Hand(paymentMatch, "26RSD03-013"); int handIndex = paymentMatch.State.Players[0].Hand.Count - 1;
        paymentMatch.RebasePuzzleState(); paymentMatch.State.Phase = MatchPhase.Attack;
        ExecutePublic(paymentMatch, 0, PublicAction(paymentMatch, 0, c => c.action == "attack" && c.source == payer.InstanceId, "payment Level Up attack"), "Payment Level Up attack"); ResolvePublic(paymentMatch); PublicPass(paymentMatch);
        var paymentCommand = PublicAction(paymentMatch, 0, c => c.action == "magic" && c.index == handIndex && !c.chooseCores && !c.alternateCost && c.payment == null, "payment Magic");
        int cost = paymentMatch.GetSummonCost(0, paymentMatch.GetCard("26RSD03-013"));
        paymentCommand.payment = new[] { new CoreTransfer { source = payer.InstanceId, normal = 1 }, new CoreTransfer { source = -1, normal = cost - 1 } };
        ExecutePublic(paymentMatch, 0, paymentCommand, "Pay Magic with Level Up's last core"); ResolvePublic(paymentMatch);
        Check(paymentMatch.FindSpirit(payer.InstanceId) == null && payer.Level == 0, "Payment also depletes a Level Up Spirit when its physical core is spent");

        var minimum = new SpiritState { Card = new CardDefinition { levels = new List<CardLevel> { new CardLevel { cores = 2 }, new CardLevel { cores = 4 } } }, Cores = new CorePool { Normal = 1 }, BattleLevelOverride = 2 };
        Check(minimum.Level == 0, "The same precedence applies below a printed Lv1 minimum greater than one");
    }
}
