using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using MDPro3.BattleSpirits;
using MDPro3.BattleSpirits.AI;
using MDPro3.BattleSpirits.Core;
using MDPro3.BattleSpirits.Puzzles;
using Newtonsoft.Json;

internal static class SessionTests
{
    private sealed class DeckFile { public DeckDefinition[] decks; }
    private static int checks;
    private static float time;
    private static IReadOnlyList<CardDefinition> cards;
    private static DeckDefinition[] decks;
    private static string storage;
    private static readonly BattleSpiritsBot[] bots = new BattleSpiritsBot[2];
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Throws(Action action, string message) { bool threw = false; try { action(); } catch { threw = true; } Check(threw, message); }
    private static string Json(object value) => BattleReplay.Encode(value);
    private static BattleGameSession Session(string sub = "") => new BattleGameSession(cards, Path.Combine(storage, sub));
    private static void Pump(BattleGameSession host, BattleGameSession guest = null)
    { time += .5f; host.Tick(time); guest?.Tick(time); Thread.Sleep(2); }
    private static void Until(Func<bool> condition, BattleGameSession host, BattleGameSession guest, string message)
    { for (int i = 0; i < 2000 && !condition(); i++) Pump(host, guest); Check(condition(), message + ": " + host.Status + " / " + guest?.Status); }
    private static int FreePort()
    { var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port; }
    private static void RawSend(TcpClient peer, string json)
    { byte[] body = Encoding.UTF8.GetBytes(json); var header = new[] { (byte)(body.Length >> 24), (byte)(body.Length >> 16), (byte)(body.Length >> 8), (byte)body.Length }; peer.GetStream().Write(header, 0, 4); peer.GetStream().Write(body, 0, body.Length); }
    public static int Main(string[] args)
    {
        string root = args[0]; storage = Path.Combine(Path.GetTempPath(), "battle-session-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            cards = JsonConvert.DeserializeObject<CardsDatabase>(File.ReadAllText(Path.Combine(root, "Assets/StreamingAssets/BattleSpirits/cards.json"))).cards;
            decks = JsonConvert.DeserializeObject<DeckFile>(File.ReadAllText(Path.Combine(root, "Assets/StreamingAssets/BattleSpirits/starter-decks.json"))).decks;
            Check(decks.Length == 4 && cards.Count == 62, "Production 62-card catalog and four decks loaded");
            bots[0] = new BattleSpiritsBot(cards); bots[1] = new BattleSpiritsBot(cards);
            ClockAndPresentation(); SoloAndReplay(); PuzzleFlow(); LanFlow(); InvalidProtocol();
            Console.WriteLine("PASS " + checks + " assertions: real Solo/AI, all puzzle wrappers, completed deterministic replay seek, authoritative loopback LAN, privacy and malformed protocol.");
            return 0;
        }
        finally { if (Directory.Exists(storage)) Directory.Delete(storage, true); }
    }
    private static void ClockAndPresentation()
    {
        using (var session = Session("clock"))
        {
            session.StartSolo(decks[0], decks[1]);
            Check(session.Send(new BattleCommand { action = "opening" }, out _), "Player opening advances to AI decision");
            string aiTurn = Json(session.View); session.Tick(.1f);
            Check(Json(session.View) == aiTurn, "AI waits for the decision clock");
            session.PresentationBusy = true; session.Tick(10);
            Check(Json(session.View) == aiTurn, "Pending AI decision honors presentation busy");
            session.PresentationBusy = false; session.Tick(10.1f);
            Check(session.View.Phase == MatchPhase.Start, "AI resumes after presentation and makes its opening decision");
            session.Tick(10.2f); Check(session.View.Phase == MatchPhase.Start, "Automatic steps also honor the clock");
            session.Tick(11); Check(session.View.Phase == MatchPhase.Draw, "First-player system step follows actual rules");
            session.Tick(12); session.Tick(13); Check(session.View.Phase == MatchPhase.Main, "Automatic no-choice steps reach Main");
            string main = Json(session.View); session.Tick(100); Check(Json(session.View) == main, "Local Main choices never auto-advance");
            Exception wrongThread = null;
            var thread = new Thread(() => { try { session.Send(new BattleCommand { action = "advance" }, out _); } catch (Exception e) { wrongThread = e; } });
            thread.Start(); thread.Join(); Check(wrongThread is InvalidOperationException && Json(session.View) == main, "Worker threads cannot mutate a session");
            Check(session.Retry() && session.View.Phase == MatchPhase.Opening, "Solo retry starts a clean valid match");
        }
        Console.WriteLine("PASS AI clock, presentation and main-thread boundary");
    }
    private static void SoloAndReplay()
    {
        string firstReplay = null;
        for (int d = 0; d < decks.Length; d++)
        {
            using (var session = Session("solo" + d))
            {
                session.StartSolo(decks[d], decks[(d + 1) % decks.Length], 2026 + d, "Tester");
                Check(session.View.Players[0].Life == 5 && session.View.Players[1].Hand.Length == 0, "Solo uses five life and hides AI hand");
                Check(BattleGameSession.ReplayFiles(Path.Combine(storage, "solo" + d)).Length == 0, "Incomplete match is not saved");
                string before = Json(session.View); session.PresentationBusy = true;
                Pump(session); Check(Json(session.View) == before && session.LegalCommands.Count == 0, "Presentation blocks automatic and AI input");
                Check(!session.Send(new BattleCommand { action = "opening" }, out _), "Presentation blocks player input"); session.PresentationBusy = false;
                Check(!session.Send(new BattleCommand { action = "bad" }, out _), "Unknown player command is rejected");
                int steps = 0;
                while (session.View.WinnerIndex < 0 && steps++ < 4000)
                {
                    if (session.View.DecisionPlayerIndex == 0 && session.LegalCommands.Count > 0)
                    {
                        var command = bots[0].Choose(session.View, session.LegalCommands, BotDifficulty.Expert, steps + 31);
                        Check(session.Send(command, out var error), "Local policy command executes: " + error);
                    }
                    Pump(session);
                }
                Check(session.View.WinnerIndex >= 0, "Session AI completes production deck " + decks[d].name);
                string expected = Json(session.View); var files = BattleGameSession.ReplayFiles(Path.Combine(storage, "solo" + d));
                Check(files.Length == 1, "Exactly one completed replay stored"); firstReplay = files[0];
                Pump(session); Check(BattleGameSession.ReplayFiles(Path.Combine(storage, "solo" + d)).Length == 1, "No duplicate replay save");
                session.StartReplay(firstReplay); Check(session.IsReplay && session.ReplayPosition == 0, "Replay starts at opening");
                Check(!session.Send(new BattleCommand { action = "concede" }, out _) && session.LegalCommands.Count == 0, "Replay gameplay input is closed");
                session.StepReplay(int.MaxValue); Check(Json(session.View) == expected && session.ReplayPosition == session.ReplayCount, "Re-simulation reproduces full completed match");
                session.StepReplay(-1); string previous = Json(session.View); session.StepReplay(1); Check(Json(session.View) == expected, "Replay previous/next deterministic");
                session.StepReplay(-1); Check(Json(session.View) == previous, "Replay seeks reproduce intermediate snapshots");
                session.StepReplay(int.MinValue); Check(session.ReplayPosition == 0, "Replay seek clamps safely");
                Throws(() => session.StartReplay(Path.Combine(storage, "outside.bsreplay.json")), "Replay cannot escape its directory");
                var document = BattleReplay.Decode<BattleReplayDocument>(File.ReadAllText(firstReplay)); document.catalogHash = "wrong";
                string bad = Path.Combine(Path.GetDirectoryName(firstReplay), "bad.bsreplay.json"); File.WriteAllText(bad, Json(document));
                Throws(() => session.StartReplay(bad), "Replay rejects mismatched card catalog"); File.Delete(bad);
                Console.WriteLine("PASS Solo + replay " + decks[d].name + " (" + steps + " ticks)");
            }
        }
        using (var session = Session("invalid-deck"))
        { Throws(() => session.StartSolo(new DeckDefinition { cards = new[] { "26RBS02-002" } }, decks[0]), "Short decks rejected before match starts"); }
    }
    private static void PuzzleFlow()
    {
        using (var session = Session("puzzle")) foreach (var definition in BattlePuzzleCatalog.All)
        {
            session.StartPuzzle(definition); int committed = 0; Action<BattleEvent> handler = _ => committed++; session.CommittedEvent += handler;
            foreach (var step in definition.solution) Check(session.Send(step.command, out var error), "Session puzzle solution " + definition.id + ": " + error);
            Check(session.Puzzle.Status == PuzzleStatus.Solved && session.Status == "Puzzle complete." && session.LegalCommands.Count == 0, "Session puzzle solved status and closed commands");
            Check(committed > 0, "Committed puzzle events forwarded");
            Check(session.ResetPuzzle() && session.Puzzle.CommandCount == 0 && session.Puzzle.Status == PuzzleStatus.InProgress, "Puzzle reset uses wrapper");
            session.Concede(); Check(session.Puzzle.Status == PuzzleStatus.Failed, "Concede runs through puzzle failure wrapper");
            Check(session.Retry() && session.Puzzle.Status == PuzzleStatus.InProgress, "Retry resets failed puzzle"); session.CommittedEvent -= handler;
        }
        Check(BattleGameSession.ReplayFiles(Path.Combine(storage, "puzzle")).Length == 0, "Puzzle fixture is not misrecorded as a normal replay");
        Console.WriteLine("PASS all ten puzzle session flows");
    }
    private static void LanFlow()
    {
        using (var host = Session("host")) using (var guest = Session("guest"))
        {
            int port = FreePort(); host.StartHost(port, decks[0], "Host"); guest.Join("127.0.0.1", port, decks[1], "Guest");
            Until(() => host.IsConnected && guest.IsConnected && guest.View != null, host, guest, "LAN handshake completed");
            Check(host.IsHost && host.Seat == 0 && !guest.IsHost && guest.Seat == 1, "LAN socket seats fixed");
            Check(host.View.Players[1].Hand.Length == 0 && guest.View.Players[0].Hand.Length == 0 && guest.View.Players[1].Hand.Length == 4, "Each LAN owner only sees their own hand");
            Check(guest.LegalCommands.Count == 0 && !guest.Send(new BattleCommand { action = "opening" }, out _), "Guest cannot act for the host");
            var filter = typeof(BattleGameSession).GetMethod("FilterEvent", BindingFlags.NonPublic | BindingFlags.Static);
            var hidden = (BattleEvent)filter.Invoke(null, new object[] { new BattleEvent { Kind = "Drawn", Actor = 0, CardId = "secret", SpiritId = 99, TargetId = 22 }, 1 });
            Check(hidden.CardId == "" && hidden.SpiritId == -1 && hidden.TargetId == -1, "Opposing draw identity explicitly filtered from events");
            int moves = 0;
            while (host.View.WinnerIndex < 0 && moves++ < 6000)
            {
                foreach (var session in new[] { host, guest })
                    if (session.View != null && session.View.DecisionPlayerIndex == session.Seat && session.LegalCommands.Count > 0)
                    {
                        var command = bots[session.Seat].Choose(session.View, session.LegalCommands, BotDifficulty.Expert, moves + 20);
                        Check(session.Send(command, out var error), "LAN legal command accepted: " + error);
                    }
                Pump(host, guest);
            }
            Check(host.View.WinnerIndex >= 0, "Authoritative LAN full match completes");
            Until(() => guest.View.WinnerIndex >= 0, host, guest, "Guest receives completed result");
            Check(host.View.WinnerIndex == guest.View.WinnerIndex, "LAN final winner consistent");
            Check(BattleGameSession.ReplayFiles(Path.Combine(storage, "host")).Length == 1 && BattleGameSession.ReplayFiles(Path.Combine(storage, "guest")).Length == 0, "Only authority saves full-information replay");
            guest.Dispose(); Until(() => !host.IsConnected, host, null, "Host notices socket cleanup");
        }
        Console.WriteLine("PASS full authoritative loopback LAN match and privacy");
    }
    private static void InvalidProtocol()
    {
        foreach (string kind in new[] { "version", "catalog", "deck", "actor", "length", "missing" })
        using (var host = Session("bad-" + kind)) using (var peer = new TcpClient())
        {
            int port = FreePort(); host.StartHost(port, decks[0], "Host"); peer.Connect(IPAddress.Loopback, port);
            var hello = new BattleLanPacket { type = "hello", catalogHash = BattleReplay.CatalogHash(cards), deck = decks[1], name = "Raw guest" };
            if (kind == "version") hello.version++;
            if (kind == "catalog") hello.catalogHash = "different";
            if (kind == "deck") hello.deck = new DeckDefinition { cards = new[] { "unknown" } };
            if (kind == "length") peer.GetStream().Write(new byte[] { 127, 255, 255, 255 }, 0, 4);
            else if (kind == "missing") RawSend(peer, "{\"type\":\"hello\"}");
            else RawSend(peer, Json(hello));
            if (kind == "actor")
            {
                Until(() => host.IsConnected, host, null, "Raw peer handshake");
                // At opening only actor 0 can act. A remote opening must still bind to seat 1.
                RawSend(peer, Json(new BattleLanPacket { type = "command", catalogHash = hello.catalogHash, revision = 0, command = new BattleCommand { action = "opening" } }));
                for (int i = 0; i < 10; i++) Pump(host);
                Check(host.View.DecisionPlayerIndex == 0 && host.View.Phase == MatchPhase.Opening, "Remote packet cannot act as host");
                string forged = Json(new BattleLanPacket { type = "command", catalogHash = hello.catalogHash, command = new BattleCommand { action = "concede" } });
                RawSend(peer, forged.Substring(0, forged.Length - 1) + ",\"actor\":0}");
            }
            Until(() => host.Status.IndexOf("rejected", StringComparison.OrdinalIgnoreCase) >= 0 || host.Status.IndexOf("Invalid LAN frame", StringComparison.OrdinalIgnoreCase) >= 0, host, null, "Invalid " + kind + " protocol rejected");
            Check(!host.IsConnected && host.LegalCommands.Count == 0, "Protocol rejection closes input");
            Check(BattleGameSession.ReplayFiles(Path.Combine(storage, "bad-" + kind)).Length == 0, "Invalid connection cannot save replay");
        }
        Console.WriteLine("PASS malformed protocol/version/catalog/deck/frame and actor binding");
    }
}
