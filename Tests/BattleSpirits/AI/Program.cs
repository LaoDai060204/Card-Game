using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Networking;
using Newtonsoft.Json;
using WindBot.BattleSpirits;

internal static class Program
{
    private static CardsDatabase database;
    private static int cases, totalActions, completeMatches;
    private static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    private static void Case(string name, Action action) { action(); cases++; Console.WriteLine("PASS " + name); }
    private static string Encode(BattleCommand command) => JsonConvert.SerializeObject(command);
    private static BattleSpiritsExecutor Bot(BotDifficulty difficulty = BotDifficulty.Normal, int seed = 0)
        => new BattleSpiritsExecutor(database.cards, difficulty, seed);
    private static PublicPlayerView Player(int index, int life = 5) => new PublicPlayerView {
        playerIndex = index, life = life, reserveNormal = 3, reserveSoul = true, deckCount = 30
    };
    private static FilteredMatchView View(string phase, int viewer = 0) => new FilteredMatchView {
        viewer = viewer, decisionPlayer = viewer, activePlayer = 0, phase = phase, turn = 2,
        ownHand = Array.Empty<string>(), players = new[] { Player(0), Player(1) }
    };
    private static SpiritView Spirit(string card, int id, int bp, int normal = 1, bool exhausted = false)
        => new SpiritView { cardId = card, instanceId = id, bp = bp, normalCores = normal, exhausted = exhausted, level = 1 };
    private static void Execute(BattleMatch match, BattleCommand action)
    {
        int actor = match.DecisionPlayerIndex;
        Check(action != null, "AI produced no command in " + match.State.Phase);
        Check(BattleCommandDispatcher.TryExecute(match, actor, action, out string error),
            "AI command rejected in " + match.State.Phase + ": " + Encode(action) + " — " + error);
        match.AssertInvariants(); totalActions++;
    }

    private static BattleMatch FlashFixture(string ownCard, int ownCores, string otherCard, int otherCores,
        string magic, int viewer, bool beforeBlock)
    {
        var ownDeck = Enumerable.Repeat("BS01-001", 38).Concat(new[] { ownCard, magic }).ToArray();
        var otherDeck = Enumerable.Repeat("BS01-001", 39).Concat(new[] { otherCard }).ToArray();
        var match = BattleMatch.Create(database.cards, viewer == 0 ? ownDeck : otherDeck,
            viewer == 0 ? otherDeck : ownDeck, 142, 0, false);
        CardDefinition Card(string id) => database.cards.Single(card => card.id == id);
        void Remove(PlayerState player, string id)
        {
            if (!player.Hand.Remove(id)) Check(player.Deck.Remove(id), "Fixture card was not in the original deck: " + id);
        }
        var me = match.State.Players[viewer]; var other = match.State.Players[1 - viewer];
        Remove(me, ownCard); Remove(other, otherCard);
        if (!me.Hand.Contains(magic)) { Remove(me, magic); me.Hand.Add(magic); }
        var ownSpirit = new SpiritState { InstanceId = 101, Card = Card(ownCard),
            Cores = new CorePool { Normal = ownCores }, Exhausted = !beforeBlock || viewer == 0 };
        var otherSpirit = new SpiritState { InstanceId = 102, Card = Card(otherCard),
            Cores = new CorePool { Normal = otherCores }, Exhausted = !beforeBlock || viewer == 1 };
        me.Field.Add(ownSpirit); other.Field.Add(otherSpirit);
        me.CoreGains += ownCores; other.CoreGains += otherCores;
        match.State.ActivePlayerIndex = 0; match.State.TurnNumber = 2;
        match.State.Phase = beforeBlock ? MatchPhase.FlashBeforeBlock : MatchPhase.FlashAfterBlock;
        match.State.PendingAttackerId = viewer == 0 ? 101 : 102;
        match.State.PendingBlockerId = beforeBlock ? -1 : viewer == 0 ? 102 : 101;
        match.State.FlashPlayerIndex = viewer;
        match.AssertInvariants(); return match;
    }

    private static bool RandomStep(BattleMatch match, Random random)
    {
        int actor = match.DecisionPlayerIndex;
        var view = ViewFactory.Create(match, actor);
        var me = view.players.Single(player => player.playerIndex == actor);
        var commands = new List<BattleCommand>();
        switch (view.phase)
        {
            case "Opening": commands.Add(new BattleCommand { action = "opening", redraw = random.Next(3) == 0 }); break;
            case "Main":
                for (int i = 0; i < view.ownHand.Length; i++) commands.Add(new BattleCommand { action = "summon", index = i });
                foreach (var spirit in me.field)
                    if (me.reserveNormal > 0 || me.reserveSoul)
                        commands.Add(new BattleCommand { action = "moveCore", target = spirit.instanceId, soul = me.reserveNormal == 0 });
                commands.Add(new BattleCommand { action = "attackStep" }); commands.Add(new BattleCommand { action = "endTurn" }); break;
            case "Attack":
                foreach (var spirit in me.field.Where(spirit => !spirit.exhausted)) commands.Add(new BattleCommand { action = "attack", index = spirit.instanceId });
                commands.Add(new BattleCommand { action = "endTurn" }); break;
            case "FlashBeforeBlock": case "FlashAfterBlock": commands.Add(new BattleCommand { action = "passFlash" }); break;
            case "Block":
                foreach (var spirit in me.field.Where(spirit => !spirit.exhausted)) commands.Add(new BattleCommand { action = "block", index = spirit.instanceId });
                commands.Add(new BattleCommand { action = "takeLife" }); break;
        }
        for (int i = commands.Count - 1; i > 0; i--)
        { int other = random.Next(i + 1); var swap = commands[i]; commands[i] = commands[other]; commands[other] = swap; }
        foreach (var command in commands)
            if (BattleCommandDispatcher.TryExecute(match, actor, command, out _))
            { match.AssertInvariants(); totalActions++; return true; }
        return false;
    }

    private static int Play(int seed, BotDifficulty first, BotDifficulty second, bool randomOpponent = false, int botSeat = 0)
    {
        var profiles = BattleSpiritsBotProfiles.Create(database.cards);
        var match = BattleMatch.Create(database.cards, profiles[(int)first].Deck.cards,
            profiles[(int)second].Deck.cards, seed, seed % 2);
        var bots = new[] { Bot(first, seed), Bot(second, seed + 1) };
        var random = new Random(seed * 31 + 19);
        for (int step = 0; step < 3000 && match.State.Phase != MatchPhase.GameOver; step++)
        {
            int actor = match.DecisionPlayerIndex;
            if (randomOpponent && actor != botSeat) Check(RandomStep(match, random), "Random baseline stalled.");
            else Execute(match, bots[actor].ChooseAction(ViewFactory.Create(match, actor)));
        }
        Check(match.State.Phase == MatchPhase.GameOver, "Bot match did not terminate for seed " + seed);
        completeMatches++; return match.State.WinnerIndex;
    }

    public static int Main(string[] args)
    {
        string catalog = args.Length > 0 ? args[0] : "Data/BattleSpirits/cards.json";
        database = JsonConvert.DeserializeObject<CardsDatabase>(File.ReadAllText(catalog));
        var watch = Stopwatch.StartNew();
        Case("Production WindBot backend compiles against Unity-compatible .NET Standard 2.1", () => {
            string framework = typeof(BattleSpiritsExecutor).Assembly
                .GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false)
                .Cast<System.Runtime.Versioning.TargetFrameworkAttribute>().Single().FrameworkName;
            Check(framework == ".NETStandard,Version=v2.1", "AI target framework is incompatible: " + framework);
        });
        Case("Registered WindBot profiles have valid, distinct forty-card Eternal decks", () => {
            var profiles = BattleSpiritsBotProfiles.Create(database.cards);
            Check(profiles.Length == 3, "Missing registered profiles.");
            Check(profiles.Select(profile => profile.Id).Distinct().Count() == 3, "Duplicate profile IDs.");
            foreach (var profile in profiles)
            { Check(profile.Deck.cards.Length == 40, "Deck size."); Check(DeckValidator.Validate(database.cards, profile.Deck.cards).IsValid, profile.Name); }
            Check(!profiles[0].Deck.cards.SequenceEqual(profiles[1].Deck.cards), "Profiles use identical recipes.");
        });
        Case("The shipped catalog enables the five verified ability cards used by AI profiles", () => {
            foreach (string id in new[] { AbilityRegistry.Siegfried, AbilityRegistry.Valkyrius,
                AbilityRegistry.SilverWorld, AbilityRegistry.AwakenMagic, AbilityRegistry.DoubleDraw })
            {
                var card = database.cards.Single(item => item.id == id);
                Check(AbilityRegistry.IsRegistered(card), "Unverified rule fixture: " + id);
                Check(card.implemented, "The shipped catalog has not enabled its verified rule: " + id);
                Check(card.IsPlayable, "Registered fixture is not accepted: " + id);
            }
            foreach (var profile in BattleSpiritsBotProfiles.Create(database.cards))
                Check(DeckValidator.Validate(database.cards, profile.Deck.cards).IsValid, "Ability deck invalid: " + profile.Id);
            var bossDeck = BattleSpiritsBotProfiles.Create(database.cards).Single(profile => profile.Id == "boss").Deck.cards;
            Check(bossDeck.Contains(AbilityRegistry.Siegfried) && bossDeck.Contains(AbilityRegistry.AwakenMagic)
                && bossDeck.Contains(AbilityRegistry.DoubleDraw), "Boss profile does not contain its implemented boss and Magic.");
        });
        Case("Executor sees only filtered own-hand and public state", () => {
            var match = BattleMatch.CreateDefault(database.cards, 41);
            var before = ViewFactory.Create(match, 0);
            match.State.Players[1].Hand.Reverse(); match.State.Players[1].Deck.Reverse();
            var after = ViewFactory.Create(match, 0);
            Check(Encode(Bot().ChooseAction(before)) == Encode(Bot().ChooseAction(after)), "Private opponent order changed the decision.");
            Check(typeof(BattleSpiritsExecutor).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(method => method.GetParameters()).All(parameter => parameter.ParameterType != typeof(BattleMatch)
                    && parameter.ParameterType != typeof(MatchState) && parameter.ParameterType != typeof(PlayerState)), "AI accepts unfiltered game state.");
        });
        Case("Opening keeps an affordable hand and redraws an unaffordable one", () => {
            var view = View("Opening"); view.ownHand = new[] { "BS01-001", "BS01-005" };
            Check(!Bot().ChooseAction(view).redraw, "Affordable hand discarded.");
            view.players[0].reserveNormal = 0; view.players[0].reserveSoul = false;
            Check(Bot().ChooseAction(view).redraw, "Unaffordable opening kept.");
        });
        Case("Main chooses an affordable summon and never loops Reserve allocation", () => {
            var view = View("Main"); view.ownHand = new[] { "BS01-001" };
            Check(Bot().ChooseAction(view).action == "summon", "Free Spirit not summoned.");
            view.ownHand = Array.Empty<string>(); view.players[0].field = new[] { Spirit("BS01-002", 1, 1000) };
            var action = Bot().ChooseAction(view);
            Check(action.action == "moveCore" && action.source == -1 && action.target == 1, "Level allocation not monotonic Reserve -> Spirit.");
            view.players[0].reserveNormal = 0; view.players[0].reserveSoul = false;
            Check(Bot().ChooseAction(view).action == "attackStep", "No-core Main did not advance.");
        });
        Case("The first player does not enter the first-turn Attack Step", () => {
            var view = View("Main"); view.turn = 1;
            Check(Bot().ChooseAction(view).action == "endTurn", "First-turn attack proposed.");
        });
        Case("Defender chooses a winning blocker or sacrifices to prevent lethal", () => {
            var view = View("Block", 1); view.attacker = 11;
            view.players[0].field = new[] { Spirit("BS01-001", 11, 2000) };
            view.players[1].field = new[] { Spirit("BS01-005", 12, 4000), Spirit("BS01-001", 13, 1000) };
            Check(Bot().ChooseAction(view).index == 12, "Winning blocker not selected.");
            view.players[1].life = 1; view.players[1].field = new[] { Spirit("BS01-001", 13, 1000) };
            Check(Bot().ChooseAction(view).action == "block", "Bot accepted lethal with a ready blocker.");
        });
        Case("Nexus are excluded from attack and block candidates", () => {
            var view = View("Attack"); view.players[0].field = new[] { Spirit(AbilityRegistry.SilverWorld, 99, 0, 0) };
            Check(Bot().ChooseAction(view).action == "endTurn", "Nexus tried to attack.");
            view = View("Block", 1); view.attacker = 1; view.players[1].life = 1;
            view.players[0].field = new[] { Spirit("BS01-001", 1, 1000) };
            view.players[1].field = new[] { Spirit(AbilityRegistry.SilverWorld, 2, 0, 0) };
            Check(Bot().ChooseAction(view).action == "takeLife", "Nexus tried to block.");
        });
        Case("Valkyrius blocking bonus is considered before declaring a block", () => {
            var boss = database.cards.Single(card => card.id == AbilityRegistry.Valkyrius);
            Check(boss.IsPlayable, "Valkyrius fixture must be enabled.");
            var view = View("Block", 1); view.attacker = 1;
            view.players[0].field = new[] { Spirit("BS01-001", 1, 7000) };
            view.players[1].field = new[] { Spirit(boss.id, 2, 4000) };
            Check(Bot().ChooseAction(view).action == "block", "Printed block BP bonus ignored.");
        });
        Case("Double Draw is used for Main draw and a battle-changing Flash BP boost", () => {
            var view = View("Main"); view.ownHand = new[] { AbilityRegistry.DoubleDraw };
            view.players[0].field = new[] { Spirit("BS01-001", 1, 1000), Spirit("BS01-002", 2, 1000) };
            var action = Bot().ChooseAction(view);
            Check(action.action == "playMagic" && action.target == -1, "Main Double Draw mode ignored.");
            view = View("FlashAfterBlock"); view.attacker = 1; view.blocker = 2;
            view.ownHand = new[] { AbilityRegistry.DoubleDraw };
            view.players[0].field = new[] { Spirit(AbilityRegistry.Siegfried, 1, 4000) };
            view.players[1].field = new[] { Spirit("BS01-005", 2, 5000) };
            action = Bot().ChooseAction(view);
            Check(action.action == "playMagic" && action.target == 1, "Battle-changing Flash not chosen.");
        });
        Case("Siegfried awakening transfers the required spare cores atomically", () => {
            var view = View("FlashAfterBlock"); view.attacker = 1; view.blocker = 2;
            view.players[0].field = new[] { Spirit(AbilityRegistry.Siegfried, 1, 6000, 2),
                Spirit("BS01-001", 3, 1000, 2), Spirit("BS01-002", 4, 1000, 3) };
            view.players[1].field = new[] { Spirit("BS01-005", 2, 9000) };
            var action = Bot().ChooseAction(view);
            Check(action.action == "awaken" && action.target == 1 && action.transfers.Length == 2,
                "Missing atomic multi-source awakening.");
            Check(action.transfers.Sum(transfer => transfer.normal + (transfer.soul ? 1 : 0)) == 3,
                "Awakening did not reach the required Lv3.");
        });
        Case("Awaken Magic changes a Flash battle through an authoritative legal command", () => {
            var match = FlashFixture(AbilityRegistry.Siegfried, 1, "BS01-008", 3, AbilityRegistry.AwakenMagic, 0, false);
            var command = Bot().ChooseAction(ViewFactory.Create(match, 0));
            Check(command.action == "playMagic" && command.target == 101, "Flash Awaken Magic was ignored.");
            Execute(match, command);
            Check(match.FindSpirit(101).Bp > match.FindSpirit(102).Bp, "Awaken did not change the battle outcome.");
            Check(match.State.Players[0].Discard.Contains(AbilityRegistry.AwakenMagic), "Magic was not paid and discarded.");
        });
        Case("Pre-block Flash accounts for Valkyrius printed block bonus", () => {
            var match = FlashFixture(AbilityRegistry.Valkyrius, 1, AbilityRegistry.Siegfried, 5,
                AbilityRegistry.DoubleDraw, 1, true);
            var enemy = match.FindSpirit(102);
            var command = Bot().ChooseAction(ViewFactory.Create(match, 1));
            Check(command.action == "playMagic" && command.target == 101, "Valkyrius could win after Flash but AI passed.");
            Execute(match, command);
            Check(match.FindSpirit(101).Bp + 6000 > enemy.Bp, "Flash plus printed block bonus does not win.");
        });
        Case("Attacker preserves Flash cards against a stronger potential Valkyrius blocker", () => {
            var view = View("FlashBeforeBlock"); view.attacker = 1;
            view.ownHand = new[] { AbilityRegistry.DoubleDraw };
            view.players[0].field = new[] { Spirit(AbilityRegistry.Siegfried, 1, 4000) };
            view.players[1].field = new[] { Spirit(AbilityRegistry.Valkyrius, 2, 4000) };
            Check(Bot().ChooseAction(view).action == "passFlash", "Double Draw was wasted while ignoring the block bonus.");
        });
        Case("Silver World Lv2 is allocated only when there are Spirits worth defending", () => {
            var view = View("Main"); view.players[0].reserveNormal = 4;
            view.players[0].field = new[] { Spirit(AbilityRegistry.SilverWorld, 1, 0, 0),
                Spirit("BS01-001", 2, 3000, 3), Spirit("BS01-026", 3, 2000, 2) };
            var command = Bot().ChooseAction(view);
            Check(command.action == "moveCore" && command.target == 1, "Useful Lv2 Nexus ignored.");
            view.players[0].field = new[] { Spirit(AbilityRegistry.SilverWorld, 1, 0, 0) };
            Check(Bot().ChooseAction(view).action == "attackStep", "Empty board over-invested in a Nexus.");
        });
        Case("Inactive, finished, malformed and oversized views do not execute", () => {
            var bot = Bot(); Check(bot.ChooseAction(null) == null, "Null view.");
            var view = View("Main"); view.decisionPlayer = 1; Check(bot.ChooseAction(view) == null, "Opponent decision.");
            view.decisionPlayer = 0; view.winner = 1; Check(bot.ChooseAction(view) == null, "Finished match.");
            view.winner = -1; view.players[0].field = new SpiritView[] { null }; Check(bot.ChooseAction(view) == null, "Malformed field.");
            view.players[0].field = Array.Empty<SpiritView>(); view.ownHand = new string[BattleSpiritsExecutor.MaximumZoneCards + 1];
            Check(bot.ChooseAction(view) == null, "Unbounded hand input.");
        });
        Case("Frame scheduler delays once, deduplicates a state, cancels and resets", () => {
            var session = new BattleSpiritsBotSession(0, Bot(), 0.5); var view = View("Opening");
            Check(!session.TryPoll(view, 0, out _), "No thinking delay.");
            Check(!session.TryPoll(view, 0.49, out _), "Early command.");
            Check(session.TryPoll(view, 0.5, out var command) && command.action == "opening", "Delayed command missing.");
            Check(!session.TryPoll(view, 100, out _), "Same state sent twice.");
            session.Cancel(); Check(session.IsCancelled && !session.TryPoll(view, 101, out _), "Cancelled session still acts.");
            session.Reset(); Check(!session.IsCancelled && !session.TryPoll(view, 102, out _), "Reset lost delay.");
            Check(session.TryPoll(view, 103, out _), "Reset did not resume.");
            Check(!session.TryPoll(view, double.NaN, out _), "NaN clock accepted.");
        });
        Case("Authority rejection advances through a safe phase fallback", () => {
            var session = new BattleSpiritsBotSession(0, Bot(), 0); var view = View("Main"); view.ownHand = new[] { "BS01-001" };
            Check(session.TryPoll(view, 0, out var command) && command.action == "summon", "Fixture summon.");
            session.ReportRejectedAction();
            Check(session.TryPoll(view, 1, out command) && command.action == "endTurn", "Rejected command retried or stalled.");
            Check(!session.TryPoll(view, 2, out _), "Fallback busy loop.");
        });
        Case("Maximum supported public state has bounded inspection work", () => {
            var view = View("Main");
            view.ownHand = Enumerable.Repeat("BS01-001", BattleSpiritsExecutor.MaximumZoneCards).ToArray();
            view.players[0].field = Enumerable.Range(0, BattleSpiritsExecutor.MaximumZoneCards)
                .Select(index => Spirit("BS01-001", index, 1000)).ToArray();
            view.players[1].field = Enumerable.Range(0, BattleSpiritsExecutor.MaximumZoneCards)
                .Select(index => Spirit("BS01-001", index + 1000, 1000)).ToArray();
            var bot = Bot(); Check(bot.ChooseAction(view) != null, "Maximum view stalled.");
            Check(bot.LastInspectionCount <= 6 * BattleSpiritsExecutor.MaximumZoneCards, "Candidate work exceeded bound.");
        });
        Case("Forty deterministic bot duels finish with only authoritative legal actions", () => {
            for (int seed = 0; seed < 40; seed++) Play(seed, BotDifficulty.Normal, seed % 2 == 0 ? BotDifficulty.Hard : BotDifficulty.Easy);
        });
        Case("Normal WindBot plays forty complete games against a random legal baseline", () => {
            int wins = 0;
            for (int seed = 40; seed < 80; seed++)
            { int seat = seed % 2; if (Play(seed, BotDifficulty.Normal, BotDifficulty.Normal, true, seat) == seat) wins++; }
            Console.WriteLine("METRIC Normal versus random legal baseline: " + wins + "/40 wins, alternating seats and starting player.");
            Check(wins >= 20, "Normal strategy underperformed the random legal baseline.");
        });
        Case("Frame-polled sessions complete a real duel and stop after GameOver", () => {
            var match = BattleMatch.CreateDefault(database.cards, 501);
            var sessions = new[] { new BattleSpiritsBotSession(0, Bot(), 0.05), new BattleSpiritsBotSession(1, Bot(), 0.05) };
            double time = 0;
            for (int frame = 0; frame < 10000 && match.State.Phase != MatchPhase.GameOver; frame++, time += 0.1)
            {
                int actor = match.DecisionPlayerIndex;
                if (sessions[actor].TryPoll(ViewFactory.Create(match, actor), time, out var command)) Execute(match, command);
            }
            Check(match.State.Phase == MatchPhase.GameOver, "Polled session stalled."); completeMatches++;
            for (int actor = 0; actor < 2; actor++)
                Check(!sessions[actor].TryPoll(ViewFactory.Create(match, actor), time + 1, out _), "AI acted after GameOver.");
        });
        Console.WriteLine(JsonConvert.SerializeObject(new { status = "PASS", scenarios = cases, completeMatches,
            authoritativeActions = totalActions, elapsedMilliseconds = watch.ElapsedMilliseconds,
            windowsUnityPlayTested = false, view = "FilteredMatchView only", threads = 0 }));
        return 0;
    }
}
