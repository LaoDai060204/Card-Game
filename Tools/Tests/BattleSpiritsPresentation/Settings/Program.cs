using System;
using System.Collections.Generic;

public class Widget
{
    public Widget gameObject => this;
    public bool active;
    public string text;
    public void SetActive(bool value) => active = value;
    public void SetButtonText(string value) => text = value;
}
public class SettingsBase { public virtual void ShowEvent() { } }
public partial class SettingsUI
{
    public Widget ToggleDuel = new Widget(), ToggleWatch = new Widget(), ToggleReplay = new Widget(),
        TogglePort = new Widget(), ToggleExpansions = new Widget(), ButtonRetry = new Widget(),
        ButtonSurrender = new Widget(), ButtonCheckDecks = new Widget();
}
public class Program
{
    public static Program instance = new Program();
    public object currentServant;
    public SettingsServant setting = new SettingsServant();
    public sealed class SettingsServant { public int exits; public void OnExit() => exits++; }
}
public static class InterString { public static string Get(string value) => value; }
public static class MessageManager { public static void Cast(string message) { } }
public sealed class Deck { public List<int> Main = new List<int>(); }
public static class UIManager { public static Deck shown; public static void ShowDeckBrowserUIForDuel(Deck value) => shown = value; }
namespace MDPro3.BattleSpirits
{
    public sealed class BattleDuelServant { public int leaves; public void LeaveDuel() => leaves++; }
    public sealed class Card { public string CardId; }
    public sealed class Player { public string[] Hand = Array.Empty<string>(), Discard = Array.Empty<string>(), Removed = Array.Empty<string>(); public Card[] Field = Array.Empty<Card>(); }
    public sealed class Snapshot { public Player[] Players = Array.Empty<Player>(); }
    public sealed class Session { public bool IsReplay, IsOnline, PresentationBusy; public object Puzzle; public Snapshot View; public int Seat; }
    public static class BattleIntegration
    {
        public static Session Current;
        public static int concessions;
        public static bool retry;
        public static void Concede() => concessions++;
        public static bool Retry() => retry;
    }
    public static class BattleCatalog { public static int GetNumericId(string value) => int.TryParse(value, out int result) ? result : 0; }
}
public static class Smoke
{
    private static int checks;
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    public static void Main()
    {
        var ui = new SettingsUI();
        ui.ShowEvent();
        Check(ui.TogglePort.active && !ui.ButtonRetry.active && !ui.ButtonSurrender.active, "Menu settings have no stale duel actions.");
        var duel = new MDPro3.BattleSpirits.BattleDuelServant();
        Program.instance.currentServant = duel;
        var session = new MDPro3.BattleSpirits.Session
        {
            Seat = 1,
            View = new MDPro3.BattleSpirits.Snapshot { Players = new[] {
                new MDPro3.BattleSpirits.Player { Hand = new[] { "999" } },
                new MDPro3.BattleSpirits.Player { Hand = new[] { "1", "1" }, Discard = new[] { "2" }, Removed = new[] { "3" }, Field = new[] { new MDPro3.BattleSpirits.Card { CardId = "4" } } }
            } }
        };
        MDPro3.BattleSpirits.BattleIntegration.Current = session;
        ui.ShowEvent();
        Check(ui.ToggleDuel.active && !ui.ToggleWatch.active && !ui.ToggleReplay.active && ui.ButtonRetry.active, "Solo settings bind native Battle Spirits actions.");
        Check(!ui.TogglePort.active && !ui.ToggleExpansions.active && ui.ButtonCheckDecks.text == "Visible Cards", "Match settings do not expose file imports or old deck state.");
        session.Puzzle = new object(); ui.ShowEvent();
        Check(ui.ButtonRetry.text == "Reset Puzzle", "Puzzle reset replaces Solo retry in the original slot.");
        session.IsOnline = true; ui.ShowEvent();
        Check(!ui.ButtonRetry.active, "Online matches cannot retry the local engine.");
        session.IsReplay = true; ui.ShowEvent();
        Check(!ui.ToggleDuel.active && ui.ToggleReplay.active && ui.ButtonSurrender.text == "Exit Replay", "Replay settings use native replay context.");
        ui.OnCheckDecks();
        Check(UIManager.shown.Main.Count == 5 && !UIManager.shown.Main.Contains(999), "Visible cards include own hand/public zones without opponent hand.");
        session.Seat = -1; var shown = UIManager.shown; ui.OnCheckDecks();
        Check(UIManager.shown == shown, "An invalid viewer cannot inspect another player's cards.");
        session.Seat = 1; session.PresentationBusy = true;
        int exits = Program.instance.setting.exits; ui.OnSurrender();
        Check(duel.leaves == 0 && Program.instance.setting.exits == exits, "Presentation lock cannot silently abandon an uncommitted surrender.");
        session.PresentationBusy = false; ui.OnSurrender();
        Check(duel.leaves == 1 && MDPro3.BattleSpirits.BattleIntegration.concessions == 0, "Exiting a replay never sends a gameplay command.");
        session.IsReplay = false; ui.OnSurrender();
        Check(duel.leaves == 2 && MDPro3.BattleSpirits.BattleIntegration.concessions == 1, "Live surrender invokes the native session before leaving.");
        exits = Program.instance.setting.exits; ui.OnRetry();
        Check(Program.instance.setting.exits == exits, "Failed retry keeps settings open.");
        MDPro3.BattleSpirits.BattleIntegration.retry = true; ui.OnRetry();
        Check(Program.instance.setting.exits == exits + 1, "Successful retry closes the overlay.");
        Console.WriteLine("PASS: " + checks + " actual native settings method regressions (UI doubles; not Unity Play/build).");
    }
}
