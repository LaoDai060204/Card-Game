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
using UnityEngine.UI;

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
            RunStandardFrontend();
            Console.WriteLine("PASS " + checks + " actual application bridge/driver startup, failure isolation, puzzle and replay checks (Unity/native doubles; no Editor).");
        }
        finally { BattleIntegration.Shutdown(); if (Directory.Exists(storage)) Directory.Delete(storage, true); }
    }

    private static void RunStandardFrontend()
    {
        var boot = new GameObject("Boot canvas", typeof(Canvas)).GetComponent<Canvas>();
        var owner = MDPro3.Program.instance;
        int registered = owner.Registered;
        StandardFrontend.Launch(boot);
        var root = GameObject.All.Last(item => item.name == "Standard interface");
        var frontend = root.GetComponent<StandardFrontend>();
        Check(!boot.enabled && root.GetComponent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay,
            "The desktop entry hides the previous boot Canvas and creates an independent UI.");

        Click(root, "Deck library");
        Click(root, "New deck");
        var available = BattleCatalog.Cards.First(card => card.IsPlayable);
        Click(root, available.DisplayName + "   /   " + available.type + "   /   Cost " + available.cost);
        Click(root, "Add");
        Check(Live(root).Any(item => item.GetComponent<Text>()?.text == "1 card · 39 short"),
            "The replacement editor updates the deck count from the catalog addition.");
        Click(root, "Back");
        Click(root, "Cancel");
        Check(Live(root).Any(item => item.GetComponent<Text>()?.text == "1 card · 39 short"),
            "Cancelling discard preserves the unsaved deck.");
        Click(root, "Back");
        Click(root, "Discard");
        Click(root, "Back");

        Click(root, "Choose opponent");
        Click(root, BattleCatalog.StarterDecks[0].name);
        Check(BattleIntegration.Current == null, "The new briefing does not start a session until confirmed.");
        Click(root, "Start duel");
        var match = BattleIntegration.Current;
        Check(match?.View?.Phase == MatchPhase.Opening && owner.Registered == registered,
            "The new arena binds a real match without registering a legacy duel servant.");
        var opening = match.LegalCommands.First(command => command.action == "opening" && !command.redraw);
        Click(root, BattleDuelActions.Describe(opening, match.View, match.Seat));
        Check(match.LegalCommands.All(command => command.action != "opening"),
            "The new action console sends the actual opening decision.");
        Click(root, "Menu");
        Click(root, "Leave arena");
        Check(BattleIntegration.Current == null, "Leaving the new arena releases the real match.");

        Click(root, "Replays");
        var file = BattleIntegration.GetReplayFiles().First();
        Click(root, Path.GetFileName(file));
        Check(BattleIntegration.Current?.IsReplay == true, "The new replay library opens the existing replay format.");
        Click(root, "Next action");
        Check(BattleIntegration.Current.ReplayPosition == 1, "The replacement replay controls step through the session.");
        Click(root, "Menu");
        Click(root, "Leave arena");

        Click(root, "Browse puzzles");
        var definition = BattlePuzzleCatalog.All.First();
        Click(root, definition.title + "  /  " + definition.category);
        Check(BattleIntegration.Current?.Puzzle?.Definition.id == definition.id,
            "The new puzzle menu launches the existing puzzle definition.");
        Click(root, "Match info");
        Click(root, "Reset puzzle");
        Click(root, "Menu");
        Click(root, "Leave arena");
        Check(!BattleIntegration.StartSolo("missing-native-deck"), "A missing deck cannot start a new session.");
        Check(Live(root).Any(item => item.GetComponent<Text>()?.text.Contains("missing-native-deck") == true),
            "A failed start reports the error in the replacement UI.");
        Call(frontend, "OnDestroy");
        Check(BattleIntegration.Current == null, "Destroying the replacement UI releases the session.");
    }

    private static System.Collections.Generic.IEnumerable<GameObject> Live(GameObject root)
    {
        if (!root.activeSelf) yield break;
        yield return root;
        foreach (var child in root.transform.Children)
            foreach (var item in Live(child.gameObject))
                yield return item;
    }

    private static void Click(GameObject root, string title)
    {
        var button = Live(root).FirstOrDefault(item => item.name == title)?.GetComponent<Button>();
        if (button == null || !button.interactable) throw new InvalidOperationException("Missing button: " + title);
        button.onClick.Invoke();
    }
    static void Run()
    {
        var owner = MDPro3.Program.instance;
        // Main.unity keeps Program outside the camera-rendered UI Canvas.
        var canvas = new GameObject("UI", typeof(RectTransform), typeof(Canvas)) { layer = 5 };
        owner.menu = new GameObject("Menu", typeof(RectTransform)) { layer = 5 }.AddComponent<MDPro3.Servant.Servant>();
        owner.menu.transform.SetParent(canvas.transform, false);
        owner.ocgcore = new GameObject("OcgCore", typeof(RectTransform)) { layer = 5 }.AddComponent<MDPro3.Servant.Servant>();
        owner.ocgcore.transform.SetParent(canvas.transform, false);
        var blackBack = new GameObject("BlackBack", typeof(RectTransform)) { layer = 5 };
        blackBack.transform.SetParent(canvas.transform, false);
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
        var duelRect = owner.currentServant.transform as RectTransform;
        Check(duelRect != null && duelRect.parent == canvas.transform && duelRect.GetComponentInParent<Canvas>() != null,
            "The duel is a RectTransform below the actual UI Canvas rather than below Program.");
        Check(owner.currentServant.gameObject.layer == 5 && duelRect.GetSiblingIndex() == 1 && blackBack.transform.GetSiblingIndex() > duelRect.GetSiblingIndex(),
            "The duel inherits the camera-visible UI layer and stays below modal backdrop layers.");
        Check(duelRect.anchorMin.x == 0 && duelRect.anchorMin.y == 0 && duelRect.anchorMax.x == 1 && duelRect.anchorMax.y == 1
            && duelRect.offsetMin.x == 0 && duelRect.offsetMax.y == 0 && duelRect.localScale.x == 1,
            "The runtime duel stretches across its Canvas parent at normal scale.");
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
