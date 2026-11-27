using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MDPro3.BattleSpirits;
using MDPro3.BattleSpirits.Core;
using MDPro3.BattleSpirits.Visuals;
using MDPro3.UI;
using MDPro3.UI.ServantUI;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YgomSystem.ElementSystem;

static class NativeDuelSmoke
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Main()
    {
        BattleCatalog.Cards["A"] = new CardDefinition { id = "A", nameEn = "Spirit A", effectTextEn = "Printed effect." };
        BattleCatalog.Cards["B"] = new CardDefinition { id = "B", nameEn = "Spirit B" };
        Choice(); CommandsAndSnapshot(); Lifecycle();
        Console.WriteLine("PASS " + checks + " native duel command/lifecycle checks (C#9 API doubles; no Unity Editor).");
    }
    static void Choice()
    {
        var choice = new PendingChoice { Id = 10, Min = 2, Max = 3, Ordered = true,
            Options = Enumerable.Range(0, 30).Select(i => new ChoiceOption { index = 50 + i, cardId = "A", label = "Option " + i }).ToArray() };
        var selection = new BattleChoiceSelection(); selection.Bind(choice);
        Check(!selection.Valid && selection.Command() == null, "Choice minimum must gate confirmation.");
        selection.Toggle(25); selection.Toggle(4); selection.Toggle(10); selection.Toggle(29);
        Check(selection.Valid && selection.Selected.SequenceEqual(new[] { 25, 4, 10 }), "Maximum enforced across pages; click order retained.");
        Check(selection.Command().selection.SequenceEqual(new[] { 25, 4, 10 }), "Engine receives offered-array positions, not source indices 75/54/60.");
        selection.Bind(new PendingChoice { Id = 10, Min = 2, Max = 3, Ordered = true, Options = choice.Options });
        Check(selection.Selected.Count == 3, "Snapshot refresh must retain selection for the same pending choice.");
        selection.Toggle(4); selection.Toggle(4);
        Check(selection.Command().selection.SequenceEqual(new[] { 25, 10, 4 }), "Unselect/reselect updates ordered resolution order.");
        selection.Bind(new PendingChoice { Id = 11, Min = 0, Max = 1, Options = choice.Options });
        Check(selection.Selected.Count == 0 && selection.Valid, "New choice clears old indices and optional empty confirmation is legal.");
        selection.Reset(); Check(!selection.Valid, "Rebinding a session clears old choice identity.");
    }
    static BattleSnapshot View(int seat = 0) => new BattleSnapshot { ViewerIndex = seat, DecisionPlayerIndex = seat,
        ActivePlayerIndex = seat, Phase = MatchPhase.Main, TurnNumber = 3,
        Players = new[] {
            new PlayerSnapshot { Index = 0, Name = "Player", Life = 5, ReserveNormal = 3, ReserveSoul = true, Hand = new[] { "A", "B" }, Field = new[] { new SpiritSnapshot { InstanceId = 12, Owner = 0, CardId = "A", Level = 2, Bp = 6000, NormalCores = 3, Soul = true, HeavyExhausted = true } } },
            new PlayerSnapshot { Index = 1, Name = "Opponent", Life = 4, HandCount = 5, Field = Array.Empty<SpiritSnapshot>() }
        } };
    static void CommandsAndSnapshot()
    {
        var view = View();
        Check(BattleDuelActions.CanPlay(view, 0, false, false), "Acting seat can play.");
        Check(!BattleDuelActions.CanPlay(view, 1, false, false), "Nonacting seat cannot play.");
        Check(!BattleDuelActions.CanPlay(view, 0, true, false), "Replay cannot mutate gameplay.");
        Check(!BattleDuelActions.CanPlay(view, 0, false, true), "Presentation locks gameplay.");
        Check(!BattleDuelActions.CanPlay(null, 0, false, false), "LAN waiting view can be absent.");
        var command = new BattleCommand { action = "summon", index = 1, source = 12, target = 13, mode = 2, soul = true,
            alternateCost = true, chooseCores = true, chooseSuccession = true,
            payment = new[] { new CoreTransfer { source = -1, normal = 2, soul = true } },
            transfers = new[] { new CoreTransfer { source = 12, normal = 1 } }, succession = new[] { "A" }, successionIndices = new[] { 7 },
            targets = new[] { 12, 13 }, selection = new[] { 8, 2 } };
        string label = BattleDuelActions.Describe(command, view, 0);
        foreach (string expected in new[] { "Spirit B", "Spirit A", "#12", "#13", "mode 2", "Soul", "alternate cost", "Choose cores", "Choose Succession", "pay Reserve: 2 + Soul", "cores", "succession copies 7", "selection 8, 2", "targets" })
            Check(label.Contains(expected), "Exact command label omitted " + expected);
        var presentation = BattleDuelActions.Presentation(view);
        Check(presentation.cards.Length == 1 && presentation.cards[0].exhausted && presentation.cards[0].level == 2 && presentation.cards[0].bp == 6000, "Renderer receives actual field level/BP/exhaustion.");
        Check(presentation.players[0].life == 5 && presentation.players[0].reserveSoul, "Renderer receives BS life and Soul Core pool.");
        presentation.cards[0].bp = 0;
        Check(view.Players[0].Field[0].Bp == 6000, "Presentation DTO must detach mutable snapshot records.");
        Check(BattleDuelActions.Phase(MatchPhase.SecondMain).StartsWith("7") && BattleDuelActions.Phase(MatchPhase.End).StartsWith("8"), "All eight BS steps are named.");
    }
    static GameObject Element(ElementObjectManager manager, string label, params Type[] types)
    {
        var obj = new GameObject(label, new[] { typeof(RectTransform) }.Concat(types).ToArray());
        obj.layer = manager.gameObject.layer;
        manager.Elements[label] = obj; obj.transform.SetParent(manager.transform, false); return obj;
    }
    static void Lifecycle()
    {
        var servantObject = new GameObject("servant");
        var servant = servantObject.AddComponent<BattleDuelServant>();
        var instance = new GameObject("OriginalOcgCoreInstance", typeof(RectTransform), typeof(ElementObjectManager), typeof(OcgCoreUI));
        var canvas = new GameObject("UI", typeof(RectTransform), typeof(Canvas)) { layer = 5 };
        instance.layer = 5; instance.transform.SetParent(canvas.transform, false);
        var manager = instance.GetComponent<ElementObjectManager>();
        var original = instance.GetComponent<OcgCoreUI>();
        servant.servantUI = original;
        Element(manager, "Root", typeof(CanvasGroup)); Element(manager, "TextHint", typeof(TextMeshProUGUI)); Element(manager, "Hint"); Element(manager, "Popup");
        foreach (string label in new[] { "TextPlayer0Name", "TextPlayer1Name", "TextPlayer0LP", "TextPlayer1LP" }) Element(manager, label, typeof(TextMeshProUGUI));
        int oldCalls = 0;
        foreach (string label in new[] { "ButtonStop", "ButtonPlay", "ButtonAcc", "ButtonNor", "ButtonTiming", "ButtonLog", "ButtonAttachedEffect" })
            Element(manager, label, typeof(Button)).GetComponent<Button>().onClick.AddPersistent(() => oldCalls++);
        var oldWidget = Element(manager, "oldWidget", typeof(SelectionButton)).GetComponent<SelectionButton>(); oldWidget.AddOldPersistent(() => oldCalls++);
        var oldDescription = Element(manager, "legacyDescription", typeof(MDPro3.CardDescription)).GetComponent<MDPro3.CardDescription>();
        Call(servant, "FirstLoadEvent");
        var ui = (BattleDuelUI)servant.servantUI;
        Check(ui.Root.gameObject.layer == 5 && Field<BattleFieldRenderer>(ui, "field").gameObject.layer == 5, "The native field inherits the real UI layer.");
        Check(Field<Text[]>(ui, "stepLabels").Length == 8 && Field<Text[]>(ui, "stepLabels").Last().text == "8  End", "The runtime HUD exposes all eight Standard steps.");
        Check(ui.Root.GetComponentsInChildren<Graphic>(true).All(graphic => graphic.gameObject.layer == 5), "Runtime HUD graphics preserve the real camera-visible UI layer.");
        int hides = MDPro3.UIManager.BlackBackHides; Call(servant, "ApplyShowArrangement", 0);
        Check(MDPro3.UIManager.BlackBackHides == hides + 1, "Showing the duel disables the shared backdrop instead of leaving an invisible pointer blocker.");
        Check(!original.enabled && !oldDescription.enabled, "Old root and card callbacks disabled before Start.");
        oldWidget.Click();
        Check(oldCalls == 0, "Nested native widget serialized callbacks individually disabled.");
        Addressables.Factory = path =>
        {
            var shellObject = new GameObject("NativePopup", typeof(RectTransform), typeof(PopupDuelSelection));
            var shell = shellObject.GetComponent<PopupDuelSelection>();
            shell.window = new GameObject("Window", typeof(RectTransform)).GetComponent<RectTransform>();
            shell.baseRect = shell.window;
            shell.scrollRect = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect)).GetComponent<ScrollRect>();
            shell.scrollRect.content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            shell.title = new GameObject("Title", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            shell.item = new GameObject("OriginalNativeItem", typeof(RectTransform), typeof(Button), typeof(Text));
            return shellObject;
        };
        var first = new BattleGameSession { View = View(), LegalCommands = Enumerable.Range(0, 31).Select(i => new BattleCommand { action = "transfer", source = -1, target = 12, mode = i }).ToArray() };
        ui.Bind(first); ui.ShowEvent();
        Check(Field<Text[]>(ui, "playerLife").Select(label => label.text).SequenceEqual(new[] { "5", "4" }),
            "Both player panels show the matching Life total.");
        first.View.ActivePlayerIndex = 1;
        first.View.Phase = MatchPhase.FlashBeforeBlock;
        first.View.Players[0].TrashNormal = 2;
        first.View.Players[0].TrashSoul = true;
        first.Change();
        Check(Field<Text>(ui, "phaseTitle").text.Contains("Opponent's turn")
            && Field<Text>(ui, "arenaStatus").text.Contains("Your action"),
            "Defending during Flash distinguishes turn ownership from action priority.");
        Check(Field<Text[]>(ui, "playerStats")[0].text.Contains("Trash 0 cards / 2 + Soul"),
            "Public Trash card and Core counts remain separate.");
        first.View.ActivePlayerIndex = 0;
        first.View.Phase = MatchPhase.Main;
        first.Change();
        var hand = Field<List<GameObject>>(ui, "handObjects");
        Check(hand.Count == 2 && hand.All(card => card.GetComponent<RectTransform>().sizeDelta.x == 132
            && card.GetComponentInChildren<RawImage>(true).transform.parent == card.transform), "Hand faces have independent fixed card bounds beneath their native hand area.");
        Check(hand.All(card => card.layer == 5 && card.GetComponentInChildren<RawImage>(true).gameObject.layer == 5), "Hand bounds and full-face images stay on the real UI layer.");
        Field<BattleFieldRenderer>(ui, "field").SelectCard(12);
        Check(Field<RectTransform>(ui, "inspectionWindow").gameObject.activeSelf && Field<Text>(ui, "inspectionName").text == "Spirit A", "A field-card click opens the private printed-card inspector.");
        Check(Field<Text>(ui, "inspectionEffects").text.Contains("6000 BP") && Field<Text>(ui, "inspectionEffects").text.Contains("Printed effect."), "Inspection shows the live BS field state and English printed effect.");
        var inspectionScroll = Field<ScrollRect>(ui, "inspectionScroll");
        Check(inspectionScroll.vertical && !inspectionScroll.horizontal && inspectionScroll.content != null
            && inspectionScroll.viewport.GetComponent<RectMask2D>() != null && inspectionScroll.verticalNormalizedPosition == 1,
            "The effect text uses a clipped native vertical scroll area starting at the top.");
        Call(ui, "CloseInspection");
        var allCommands = first.LegalCommands;
        var selectedCommand = new BattleCommand { action = "summon", index = 1 };
        first.LegalCommands = new[] { selectedCommand, new BattleCommand { action = "magic", index = 0 }, new BattleCommand { action = "pass" } };
        hand[1].GetComponentInChildren<Button>(true).onClick.Invoke();
        var selectedRows = Field<List<GameObject>>(ui, "rows");
        Check(Field<Text>(ui, "inspectionName").text == "Spirit B" && selectedRows.Count == 3,
            "Clicking a hand card opens its English inspection and only its two relevant legal actions plus Show all.");
        selectedRows[0].GetComponentInChildren<Button>(true).onClick.Invoke();
        Check(ReferenceEquals(first.Sent, selectedCommand), "A hand-card action sends the exact offered command object.");
        first.LegalCommands = allCommands;
        Call(ui, "CloseInspection");
        var field = Field<BattleFieldRenderer>(ui, "field");
        Check(manager.GetElement<TextMeshProUGUI>("TextPlayer0LP").text == "5", "Original LP slot shows five BS life.");
        Check(manager.GetElement<TextMeshProUGUI>("TextHint").text.Contains("Main Step"), "Original hint slot names current BS step.");
        manager.GetElement<Button>("ButtonTiming").onClick.Invoke();
        Check(oldCalls == 0, "Rebound original action button never calls old persistent duel method.");
        var shell = Field<PopupDuelSelection>(ui, "actionShell");
        Check(shell.title.text.EndsWith("1/6"), "Every legal command appears across all six pages.");
        for (int p = 0; p < 5; p++) Call(ui, "RenderPanel");
        var actionRows = Field<List<GameObject>>(ui, "rows");
        var next = actionRows.Last(); next.GetComponent<Button>().onClick.Invoke();
        Check(shell.title.text.EndsWith("2/6"), "Native page button reaches later actions.");
        actionRows = Field<List<GameObject>>(ui, "rows"); actionRows[0].GetComponent<Button>().onClick.Invoke();
        Check(ReferenceEquals(first.Sent, first.LegalCommands[6]), "Exact paginated legal-command object reaches backend Send unmodified.");
        first.Commit(new BattleEvent { Kind = "Drawn" }); Check(field.Summons == 0, "Non-summon events never trigger boss ritual.");
        first.Commit(new BattleEvent { Kind = "Summoned", SpiritId = 12 }); Check(field.Summons == 1 && first.PresentationBusy, "Committed summons engage presentation lock.");
        Check(Field<CanvasGroup>(ui, "arenaHud").alpha == 0 && !Field<CanvasGroup>(ui, "arenaHud").blocksRaycasts
            && !Field<RectTransform>(ui, "handArea").gameObject.activeSelf, "Summon presentations hide and unlock the HUD and hand controls above the rendered scene.");
        Call(ui, "Send", first.LegalCommands[0]); Check(ReferenceEquals(first.Sent, first.LegalCommands[6]), "No gameplay send while presentation is active.");
        ui.ReturnOrClose(); Check(!first.PresentationBusy, "Escape skips presentation and unlocks backend.");
        Check(Field<CanvasGroup>(ui, "arenaHud").alpha == 1 && Field<CanvasGroup>(ui, "arenaHud").blocksRaycasts,
            "Skipping a summon restores visible, interactive HUD controls.");
        var second = new BattleGameSession { Seat = 1, View = View(1), IsReplay = true };
        second.View.Players[1].Hand = Array.Empty<string>();
        ui.Bind(second); int oldSets = field.Sets; first.Change(); first.Commit(new BattleEvent { Kind = "Summoned" });
        Check(field.Sets == oldSets && field.Summons == 1, "Session rebind detaches both old events.");
        Check(manager.GetElement<TextMeshProUGUI>("TextPlayer0LP").text == "4", "Joined seat sees its own life in native near-player slot.");
        manager.GetElement<Button>("ButtonPlay").onClick.Invoke(); Check(second.ReplayDelta == 1, "Original replay button steps BS replay.");
        Field<Button>(ui, "advanceButton").onClick.Invoke(); Check(second.ReplayDelta == 2, "The new native HUD advances the BS replay backend.");
        ui.Suspend(); Check(!second.PresentationBusy, "Hiding servant releases presentation lock.");
        Call(ui, "OnDestroy"); oldSets = field.Sets; second.Change();
        Check(field.Disposals == 1 && field.Sets == oldSets, "Destroy detaches events and disposes owned field once.");
    }
}
