using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using MDPro3;
using MDPro3.BattleSpirits;
using MDPro3.BattleSpirits.Core;
using MDPro3.BattleSpirits.Puzzles;
using UnityEngine;

static class BridgeSmoke
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    static void Main(string[] args)
    {
        string storage = Path.Combine(Path.GetTempPath(), "bs-bridge-" + Guid.NewGuid().ToString("N"));
        Application.streamingAssetsPath = Path.Combine(args[0], "Assets/StreamingAssets");
        Application.persistentDataPath = storage;
        try
        {
            Run();
            Console.WriteLine("PASS " + checks + " actual application bridge/driver startup, failure isolation, puzzle and replay checks (Unity/native doubles; no Editor).");
        }
        finally { BattleIntegration.Shutdown(); if (Directory.Exists(storage)) Directory.Delete(storage, true); }
    }
    static void Run()
    {
        var owner = MDPro3.Program.instance;
        owner.menu = new GameObject("Menu").AddComponent<MDPro3.Servant.Servant>();
        owner.currentServant = owner.menu;
        BattleIntegration.Initialize(owner);
        BattleIntegration.Initialize(owner);
        Check(owner.GetComponentsInChildren<BattleRuntimeDriver>().Length == 1, "Initialization attaches exactly one runtime driver.");
        Check(BattleCatalog.Cards.Count == 62 && BattleCatalog.Cards.All(c => c.implemented), "Startup uses actual shipped implemented catalog, without overrides.");
        Check(Directory.GetFiles(BattleDeckStorage.DeckDirectory, "*.ydk").Length == 4, "Startup seeds the four actual native deck files.");
        Check(!BattleIntegration.IsActive, "Menu initialization does not invent a match.");
        Check(BattleIntegration.StartSolo(BattleDeckStorage.DefaultDeckName), "Native Solo starts the actual BS session.");
        var solo = BattleIntegration.Current;
        Check(solo.View != null && solo.View.Players.All(p => p.Life == 5) && solo.View.Phase == MatchPhase.Opening, "Solo opens with BS life and opening state.");
        Check(owner.Registered == 1 && owner.currentServant is BattleDuelServant && owner.currentServant.returnServant == owner.menu, "Startup registers native duel once and retains return navigation.");
        Check(!BattleIntegration.PlayReplay(Path.Combine(Application.persistentDataPath, "missing.bsreplay.json")), "Invalid replay is reported as a failed start.");
        Check(ReferenceEquals(solo, BattleIntegration.Current) && solo.View != null, "Failed candidate replay preserves the previous live match.");
        Check(!BattleIntegration.Host(-1, "Player") && ReferenceEquals(solo, BattleIntegration.Current), "Invalid LAN listen port does not replace a live match.");
        Check(!BattleIntegration.StartSolo("not-a-deck") && ReferenceEquals(solo, BattleIntegration.Current), "Missing native deck does not replace a live match.");
        Check(!BattleIntegration.StartPuzzle("not-a-puzzle") && ReferenceEquals(solo, BattleIntegration.Current), "Unknown puzzle does not replace a live match.");
        Check(!BattleIntegration.PreviewCutscene("26RBS01-003") && ReferenceEquals(solo, BattleIntegration.Current), "Ordinary cards cannot activate an unassigned boss cutscene.");
        Check(!string.IsNullOrEmpty(MessageManager.LastMessage), "Failed starts reach the retained message UI.");
        BattleIntegration.Concede();
        Check(solo.View.WinnerIndex == 1, "Native concession uses the BS match.");
        var files = BattleIntegration.GetReplayFiles();
        Check(files.Length == 1 && files[0].EndsWith(".bsreplay.json", StringComparison.Ordinal), "Completed Solo saves a BS replay under isolated storage.");
        Check(BattleIntegration.Retry() && ReferenceEquals(solo, BattleIntegration.Current) && solo.View.WinnerIndex < 0, "Native retry restarts the actual Solo session.");
        Check(BattleIntegration.PlayReplay(files[0]) && BattleIntegration.Current.IsReplay, "Native replay selection starts the BS replay backend.");
        Check(owner.Registered == 1, "Multiple launches reuse the retained native duel servant.");
        bool disposed = false;
        try { solo.Tick(1); } catch (ObjectDisposedException) { disposed = true; }
        Check(disposed, "Successful session replacement disposes the previous backend.");

        var definition = BattlePuzzleCatalog.All.First();
        Check(BattleIntegration.StartPuzzle(definition.id + ".json"), "Native puzzle filename resolves to the actual puzzle catalog.");
        var puzzle = BattleIntegration.Current;
        Check(puzzle.Puzzle.Definition.id == definition.id, "Puzzle launch uses the session wrapper.");
        foreach (var step in definition.solution)
            Check(puzzle.Send(step.command, out var error), "Actual puzzle solution command succeeds: " + error);
        Check(puzzle.Puzzle.Status == PuzzleStatus.Solved && puzzle.Puzzle.CommandCount == definition.solution.Length, "Session dispatch updates puzzle status and command accounting.");
        int saved = Config.Saves;
        var driver = owner.GetComponent<BattleRuntimeDriver>();
        Time.unscaledTime = 2;
        Call(driver, "Update"); Call(driver, "Update");
        Check(Config.Get("BattleSpirits/Puzzles/" + definition.id + "_Clear") == Config.STRING_YES && Config.Saves == saved + 1, "Driver records a solved puzzle once.");
        Check(BattleIntegration.ResetPuzzle() && puzzle.Puzzle.Status == PuzzleStatus.InProgress, "Native reset returns the puzzle wrapper to its starting state.");
        BattleIntegration.ReadCommandLine(null);
        BattleIntegration.ReadCommandLine(new[] { "game", "-s", definition.id });
        Check(BattleIntegration.Current.Puzzle?.Definition.id == definition.id, "Command-line puzzle entry uses the same BS backend.");
        Check(BattleIntegration.PreviewCutscene(), "Explicit Ritenia boss preview is available.");
        Check(owner.Registered == 1 && BattleIntegration.Current.View == null, "Preview reuses the native servant without creating a fake match.");
        LeaveHost(driver);
        Call(driver, "OnDestroy");
        Check(!BattleIntegration.IsActive && BattleIntegration.Current == null, "Driver teardown releases the active backend.");
        BattleIntegration.Shutdown();
        Check(!BattleIntegration.Retry() && !BattleIntegration.ResetPuzzle(), "Repeated shutdown and empty retry/reset are safe.");
    }
    static void LeaveHost(BattleRuntimeDriver driver)
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        Check(BattleIntegration.Host(port, "Host"), "A native host starts on an available real loopback port.");
        var host = BattleIntegration.Current;
        Check(host.IsHost && host.IsOnline, "Native host uses the actual LAN backend.");
        bool bound = false;
        var duplicate = new TcpListener(IPAddress.Loopback, port);
        try { duplicate.Start(); }
        catch (SocketException) { bound = true; }
        finally { duplicate.Stop(); }
        Check(bound, "The active host owns its listening port before leaving.");
        host.PresentationBusy = true;
        int hiddenChanges = 0;
        host.Changed += () => hiddenChanges++;
        var owner = MDPro3.Program.instance;
        ((BattleDuelServant)owner.currentServant).LeaveDuel();
        Check(BattleIntegration.Current == null && !BattleIntegration.IsActive && owner.currentServant == owner.menu,
            "Leaving the native duel clears the active backend and returns to the menu.");
        Check(!host.PresentationBusy, "Leaving releases the outgoing presentation lock.");
        bool disposed = false;
        try { host.Tick(100); } catch (ObjectDisposedException) { disposed = true; }
        Check(disposed, "Leaving disposes the old session, preventing hidden gameplay ticks.");
        Time.unscaledTime = 100;
        Call(driver, "Update"); Call(driver, "Update");
        Check(hiddenChanges == 0 && BattleIntegration.Current == null, "Menu frame updates do not tick or resurrect the outgoing session.");
        Check(BattleIntegration.Host(port, "New host"), "Leaving releases the socket so the same LAN port can be hosted again immediately.");
        Check(!ReferenceEquals(host, BattleIntegration.Current) && BattleIntegration.Current.IsHost,
            "Rehosting creates a new backend rather than reusing the disposed host.");
        ((BattleDuelServant)owner.currentServant).LeaveDuel();
        Check(BattleIntegration.Current == null && owner.currentServant == owner.menu, "A repeated host/leave cycle releases the new session too.");
    }
}
