using System;
using System.Linq;
using MDPro3.BattleSpirits.Core;
using MDPro3.BattleSpirits.Visuals;
using UnityEngine;
using UnityEngine.UI;

namespace MDPro3.BattleSpirits
{
    public sealed partial class StandardFrontend
    {
        private const int HandPageSize = 7, ActionPageSize = 12;
        private enum ArenaPane { Actions, Card, Log, Info, Exit }
        private BattleGameSession match;
        private BattleFieldRenderer renderer;
        private RectTransform hand, drawer, consoleRows;
        private Text opponentStatus, playerStatus, phaseStatus, actionStatus, handPageLabel;
        private Button previousHand, nextHand, progressButton;
        private ArenaPane pane;
        private string focusCard, handSignature;
        private int handPage, actionPage, focusedInstance, eventIndex;
        private readonly BattleChoiceSelection choice = new BattleChoiceSelection();
        private bool bossPreview;

        private void OpenArena(BattleGameSession session)
        {
            ReleaseArena();
            ResetScreen();
            match = session;
            back = ArenaBack;
            pane = ArenaPane.Actions;
            focusCard = null;
            focusedInstance = -1;
            handPage = actionPage = eventIndex = 0;
            handSignature = null;
            consoleRows = null;
            progressButton = null;
            choice.Reset();

            var header = Panel(screen, "Match status", .035f, .869f, .966f, .971f, PanelColor);
            playerStatus = Label(header, "", .022f, .10f, .335f, .90f, 24, Glacier);
            phaseStatus = Label(header, "", .336f, .10f, .669f, .90f, 25, Ink, TextAnchor.MiddleCenter);
            opponentStatus = Label(header, "", .672f, .10f, .982f, .90f, 24, Copper, TextAnchor.MiddleRight);

            var fieldBounds = Panel(screen, "Arena", .035f, .233f, .695f, .851f, PanelColor);
            var target = Node(fieldBounds, "Arena renderer", 0, 0, 1, 1).gameObject.AddComponent<RawImage>();
            renderer = target.gameObject.AddComponent<BattleFieldRenderer>();
            renderer.Setup(target);
            renderer.SetPreferences(Config.GetBool("BattleReducedMotion", false), Config.GetBool("BattleMuted", false));
            renderer.CardSelected += InspectSpirit;
            renderer.PresentationBusyChanged += PresentationBusyChanged;

            var console = Panel(screen, "Action console", .711f, .126f, .966f, .851f, PanelColor);
            Button(console, "Actions", .024f, .895f, .325f, .975f, () => SelectPane(ArenaPane.Actions));
            Button(console, "Match info", .345f, .895f, .653f, .975f, () => SelectPane(ArenaPane.Info));
            Button(console, "Log", .675f, .895f, .976f, .975f, () => SelectPane(ArenaPane.Log));
            actionStatus = Label(console, "", .043f, .775f, .957f, .886f, 23, Quiet);
            drawer = Node(console, "Console content", .025f, .035f, .975f, .77f);

            hand = Panel(screen, "Player hand", .035f, .125f, .695f, .217f, PanelColor);
            var controls = Panel(screen, "Match controls", .035f, .041f, .966f, .109f, PanelColor);
            Button(controls, "Menu", .013f, .12f, .118f, .88f, () => SelectPane(ArenaPane.Exit));
            previousHand = Button(controls, "Hand ‹", .141f, .12f, .25f, .88f, () => MoveHand(-1));
            handPageLabel = Label(controls, "", .254f, .12f, .349f, .88f, 22, Quiet, TextAnchor.MiddleCenter);
            nextHand = Button(controls, "Hand ›", .352f, .12f, .461f, .88f, () => MoveHand(1));
            if (session.IsReplay)
            {
                Button(controls, "Previous action", .504f, .12f, .722f, .88f, () => session.StepReplay(-1));
                Button(controls, "Next action", .741f, .12f, .984f, .88f, () => session.StepReplay(1), true);
            }
            else
            {
                Button(controls, "Show legal actions", .504f, .12f, .722f, .88f, () => SelectPane(ArenaPane.Actions));
                progressButton = Button(controls, "Continue", .741f, .12f, .984f, .88f, Progress, true);
            }

            message = Label(screen, "", .039f, .005f, .966f, .035f, 20, Copper);
            match.Changed += RefreshArena;
            match.CommittedEvent += PresentCommitted;
            RefreshArena();
        }

        private void ReleaseArena()
        {
            if (match != null)
            {
                match.Changed -= RefreshArena;
                match.CommittedEvent -= PresentCommitted;
                match.PresentationBusy = false;
            }
            if (renderer != null)
            {
                renderer.CardSelected -= InspectSpirit;
                renderer.PresentationBusyChanged -= PresentationBusyChanged;
                renderer.Dispose();
            }
            renderer = null;
            match = null;
            bossPreview = false;
        }

        private void LeaveArena()
        {
            bool preview = bossPreview;
            ReleaseArena();
            BattleIntegration.Shutdown();
            if (preview) SummonPreviews();
            else Home();
        }

        private void ArenaBack()
        {
            if (renderer != null && renderer.IsPresentationBusy)
                renderer.SkipPresentation();
            else if (pane == ArenaPane.Exit || pane == ArenaPane.Card)
                SelectPane(ArenaPane.Actions);
            else
                SelectPane(ArenaPane.Exit);
        }

        private void PreviewBoss(string cardId)
        {
            bossPreview = true;
            focusCard = cardId;
            phaseStatus.text = "Summon preview";
            renderer.SetState(BattleDuelUI.PreviewState(cardId));
            SelectPane(ArenaPane.Card);
            renderer.NotifyCommittedSummon(int.MaxValue, "gallery");
        }

        private void PresentationBusyChanged(bool busy)
        {
            if (match == null) return;
            match.PresentationBusy = busy;
            ShowConsole();
        }

        private void PresentCommitted(BattleEvent evt)
        {
            if (evt?.Kind == "Summoned")
                renderer.NotifyCommittedSummon(evt.SpiritId, "arena:" + ++eventIndex);
        }

        private void RefreshArena()
        {
            if (match == null) return;
            var view = match.View;
            if (!bossPreview) renderer.SetState(BattleDuelActions.Presentation(view));
            playerStatus.text = StatusFor(view, match.Seat, "You");
            opponentStatus.text = StatusFor(view, 1 - match.Seat, "Opponent");
            phaseStatus.text = view == null ? (bossPreview ? "Summon preview" : "Match lobby")
                : "TURN " + view.TurnNumber + "  /  " + (view.ActivePlayerIndex == match.Seat ? "YOU" : "OPPONENT")
                    + "\n" + BattleDuelActions.Phase(view.Phase);
            choice.Bind(view?.PendingChoice);
            actionPage = 0;
            ShowHand();
            ShowConsole();
        }

        private static string StatusFor(BattleSnapshot view, int seat, string fallback)
        {
            var player = view?.Players.FirstOrDefault(p => p.Index == seat);
            if (player == null) return fallback;
            return player.Name + "  /  Life " + player.Life
                + "\nReserve " + player.ReserveNormal + (player.ReserveSoul ? " + Soul" : "")
                + " · Deck " + player.DeckCount + " · Hand " + player.HandCount
                + " · Trash " + player.Discard.Length;
        }

        private void MoveHand(int delta)
        {
            handPage += delta;
            ShowHand();
        }

        private void ShowHand()
        {
            var cards = match.View?.Players.FirstOrDefault(p => p.Index == match.Seat)?.Hand ?? Array.Empty<string>();
            int pages = Math.Max(1, (cards.Length + HandPageSize - 1) / HandPageSize);
            handPage = Math.Max(0, Math.Min(handPage, pages - 1));
            previousHand.interactable = handPage > 0;
            nextHand.interactable = handPage + 1 < pages;
            handPageLabel.text = (handPage + 1) + " / " + pages;
            string signature = handPage + "|" + string.Join("|", cards);
            if (handSignature == signature) return;
            handSignature = signature;

            Clear(hand);
            Picture(hand, Cosmetic("DuelProtector0", Items.ItemType.Protector, BattleCosmetics.ProtectorId), .012f, .07f, .070f, .93f);
            for (int i = 0; i < HandPageSize && handPage * HandPageSize + i < cards.Length; i++)
            {
                string id = cards[handPage * HandPageSize + i];
                float start = .084f + i * .129f;
                var tile = Button(hand, "", start, .07f, start + .120f, .93f, () =>
                {
                    focusedInstance = -1;
                    focusCard = id;
                    SelectPane(ArenaPane.Card);
                });
                Picture(tile.transform, BattleCosmetics.LoadTexture(BattleCatalog.CardImagePath(id)), .04f, .08f, .39f, .92f);
                Label(tile.transform, BattleDuelActions.CardName(id), .42f, .08f, .97f, .92f, 17, Ink);
            }
        }

        private void InspectSpirit(int instanceId)
        {
            var card = match?.View?.Players.SelectMany(p => p.Field).FirstOrDefault(item => item.InstanceId == instanceId);
            if (card == null) return;
            focusedInstance = instanceId;
            focusCard = card.CardId;
            SelectPane(ArenaPane.Card);
        }

        private void SelectPane(ArenaPane next)
        {
            pane = next;
            actionPage = 0;
            ShowConsole();
        }

        private void ShowConsole(bool preserveScroll = false)
        {
            if (drawer == null || match == null) return;
            float offset = preserveScroll && consoleRows != null
                ? consoleRows.GetComponentInParent<ScrollRect>().verticalNormalizedPosition : 1;
            Clear(drawer);
            actionStatus.text = match.PresentationBusy ? "Summon in progress"
                : bossPreview ? "Summon preview" : match.Status;
            var list = consoleRows = Scroll(drawer, "Console rows", 0, 0, 1, 1);
            if (progressButton != null)
            {
                var command = ProgressCommand();
                progressButton.interactable = !bossPreview && !match.PresentationBusy && match.LegalCommands.Count > 0;
                progressButton.GetComponentInChildren<Text>().text = command?.action == "pass" ? "Pass"
                    : command?.action == "advance" ? "Next step" : "Choose action";
            }
            switch (pane)
            {
                case ArenaPane.Card:
                    ShowInspectedCard(list);
                    break;
                case ArenaPane.Log:
                    if (match.Log.Count == 0) Row(list, "There are no events yet.", null);
                    foreach (string entry in match.Log.Reverse().Take(80)) Description(list, entry);
                    break;
                case ArenaPane.Info:
                    if (match.Puzzle != null)
                    {
                        Description(list, match.Puzzle.Definition.title + "\n" + match.Puzzle.ResultMessage);
                        Description(list, "Hint · " + match.Puzzle.Definition.hint);
                        Row(list, "Reset puzzle", () => { SelectPane(ArenaPane.Actions); match.ResetPuzzle(); });
                    }
                    else
                    {
                        Description(list, "Victory · Reduce the opponent's Life to zero or leave them with no cards in their deck at Start Step.");
                        if (match.View != null)
                        {
                            Description(list, StatusFor(match.View, match.Seat, "You"));
                            Description(list, StatusFor(match.View, 1 - match.Seat, "Opponent"));
                        }
                    }
                    break;
                case ArenaPane.Exit:
                    Row(list, bossPreview ? "Back to previews" : "Leave arena", LeaveArena, 68);
                    if (match.View != null && !match.IsReplay && match.View.Phase != MatchPhase.GameOver)
                    {
                        Row(list, "Concede", () =>
                        {
                            Clear(list);
                            Row(list, "Confirm concession", () => { match.Concede(); SelectPane(ArenaPane.Actions); });
                            Row(list, "Cancel", () => SelectPane(ArenaPane.Exit));
                        });
                        if (!match.IsOnline && match.Puzzle == null)
                            Row(list, "Retry this match", () => { SelectPane(ArenaPane.Actions); match.Retry(); });
                    }
                    Row(list, "Resume", () => SelectPane(ArenaPane.Actions));
                    break;
                default:
                    ShowLegalActions(list);
                    break;
            }
            if (preserveScroll)
            {
                Canvas.ForceUpdateCanvases();
                list.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = offset;
            }
        }

        private void ShowInspectedCard(RectTransform list)
        {
            if (!BattleCatalog.TryGet(focusCard, out var card))
            {
                Description(list, "Select a card to see its details.");
                return;
            }
            Description(list, card.DisplayName + "\n" + card.type + "  /  Cost " + card.cost);
            var slot = Panel(list, "Printed card", 0, 0, 1, 1, CanvasColor);
            slot.gameObject.AddComponent<LayoutElement>().preferredHeight = 262;
            Picture(slot, BattleCosmetics.LoadTexture(BattleCatalog.CardImagePath(card.id)), .04f, .025f, .96f, .975f);
            var spirit = match.View?.Players.SelectMany(p => p.Field).FirstOrDefault(item => item.InstanceId == focusedInstance);
            if (spirit != null)
                Description(list, "Level " + spirit.Level + " · " + spirit.Bp + " BP · " + spirit.NormalCores
                    + " Core" + (spirit.Soul ? " + Soul" : ""));
            Description(list, card.effectTextEn);
            Row(list, "Back to actions", () => SelectPane(ArenaPane.Actions));
        }

        private void ShowLegalActions(RectTransform list)
        {
            var view = match.View;
            if (match.IsReplay || match.PresentationBusy || view == null || view.DecisionPlayerIndex != match.Seat)
            {
                Description(list, actionStatus.text);
                return;
            }
            var pending = view.PendingChoice;
            if (pending != null)
            {
                Description(list, pending.Prompt);
                Row(list, "Confirm choice", choice.Valid ? (Action)(() => Send(choice.Command())) : null);
                int offset = ActionPageOffset(list, pending.Options.Length);
                for (int index = offset; index < Math.Min(pending.Options.Length, offset + ActionPageSize); index++)
                {
                    int selected = index;
                    int position = choice.Selected.IndexOf(index);
                    string marker = position >= 0 ? (position + 1) + "  ·  " : "○  ";
                    Row(list, marker + pending.Options[index].label, () =>
                    {
                        choice.Toggle(selected);
                        ShowConsole(true);
                    }, 82);
                }
                return;
            }

            var commands = match.LegalCommands;
            if (commands.Count == 0) Description(list, "No action is currently available.");
            foreach (var command in commands.Skip(ActionPageOffset(list, commands.Count)).Take(ActionPageSize))
                Row(list, BattleDuelActions.Describe(command, view, match.Seat), () => Send(command), 94);
        }

        private int ActionPageOffset(RectTransform list, int count)
        {
            int pages = Math.Max(1, (count + ActionPageSize - 1) / ActionPageSize);
            actionPage = Math.Clamp(actionPage, 0, pages - 1);
            if (pages > 1)
            {
                if (actionPage > 0)
                    Row(list, "Previous options", () => { actionPage--; ShowConsole(); });
                if (actionPage + 1 < pages)
                    Row(list, "Next options · " + (actionPage + 1) + " / " + pages, () => { actionPage++; ShowConsole(); });
            }
            return actionPage * ActionPageSize;
        }

        private void Progress()
        {
            if (match == null || match.PresentationBusy) return;
            var command = ProgressCommand();
            if (command != null) Send(command);
            else SelectPane(ArenaPane.Actions);
        }

        private BattleCommand ProgressCommand() => match.View?.PendingChoice == null
            ? match.LegalCommands.FirstOrDefault(item => item.action == "advance" || item.action == "pass")
            : null;

        private void Send(BattleCommand command)
        {
            if (command == null || match == null) return;
            if (!match.Send(command, out var error)) ReportError(error);
            else ReportError("");
        }

        private void Description(RectTransform list, string value)
        {
            var holder = Node(list, "Paragraph", 0, 0, 1, 1);
            var text = Label(holder, value, .035f, 0, .965f, 1, 23, Ink);
            var layout = holder.gameObject.AddComponent<LayoutElement>();
            float width = Math.Max(100, (list.rect.width > 0 ? list.rect.width : 340) * .91f);
            var settings = text.GetGenerationSettings(new Vector2(width, 0));
            layout.preferredHeight = Math.Max(46, text.cachedTextGeneratorForLayout.GetPreferredHeight(value, settings)
                / Math.Max(1, text.pixelsPerUnit) + 18);
        }
    }
}
