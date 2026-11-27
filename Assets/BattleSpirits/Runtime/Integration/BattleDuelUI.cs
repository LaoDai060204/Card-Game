using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MDPro3.BattleSpirits.Core;
using MDPro3.BattleSpirits.Visuals;
using MDPro3.UI;
using MDPro3.UI.ServantUI;
using MDPro3.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YgomSystem.ElementSystem;

namespace MDPro3.BattleSpirits
{
    /// <summary>Controller for an unmodified, private OcgCoreUI prefab instance.</summary>
    public sealed partial class BattleDuelUI : ServantUI
    {
        private const int PageSize = 6, HandPageSize = 9;
        private BattleGameSession session;
        private BattleFieldRenderer field;
        private BattleCardInspection cardInspection;
        private readonly List<GameObject> handObjects = new List<GameObject>();
        private readonly List<GameObject> rows = new List<GameObject>();
        private readonly BattleChoiceSelection choiceSelection = new BattleChoiceSelection();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private PopupDuelSelection actionShell;
        private GameObject actionObject;
        private int page, handPage, faceRevision, eventSequence, bindingRevision;
        private float rowHeight;
        private bool loadingActions, suspended, disposed;
        private string panel = "actions", error = "", previewCard;

        public static void DisableLegacyInstance(GameObject instance)
        {
            // RemoveAllListeners does not clear serialized persistent calls. Disable
            // each persistent call, including nested SelectionButton UnityEvents.
            foreach (var button in instance.GetComponentsInChildren<Button>(true)) Clear(button.onClick);
            foreach (var trigger in instance.GetComponentsInChildren<EventTrigger>(true))
                foreach (var entry in trigger.triggers) DisablePersistent(entry.callback);
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                string ns = behaviour.GetType().Namespace ?? "";
                if (behaviour is OcgCoreUI || behaviour is CardDescription
                    || ns == "MDPro3.Duel" || ns.StartsWith("MDPro3.Duel.", StringComparison.Ordinal)
                    || behaviour is MDPro3.UI.PhaseButtonHandler)
                    behaviour.enabled = false;
                DisableEvents(behaviour);
            }
        }

        private static void DisableEvents(object target)
        {
            // Original widgets keep their animation/style controller. Only their
            // serialized callback fields are disconnected in this instance.
            for (Type type = target.GetType(); type != null; type = type.BaseType)
                foreach (var member in type.GetFields(System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    var value = member.GetValue(target);
                    if (value is UnityEventBase evt) DisablePersistent(evt);
                    else if (member.Name == "clickEvent" || member.Name == "navigationEvent"
                        || member.Name == "hoverEvent" || member.Name == "selectEvent")
                    {
                        if (value == null) continue;
                        foreach (var item in value.GetType().GetFields())
                            if (item.GetValue(value) is UnityEventBase nested) DisablePersistent(nested);
                    }
                }
        }

        private static void DisablePersistent(UnityEventBase evt)
        {
            for (int i = 0; i < evt.GetPersistentEventCount(); i++)
                evt.SetPersistentListenerState(i, UnityEventCallState.Off);
            evt.RemoveAllListeners();
        }

        private static void Clear(UnityEvent evt) => DisablePersistent(evt);

        public override void Initialize(MDPro3.Servant.Servant servant)
        {
            base.Initialize(servant);
            SetActive("InputDebug", false);
            SetActive("PlaceCount", false);
            SetActive("DamageFrame", false);
            SetActive("DuelErrorLog", false);
            SetActive("AttachedEffectNum", false);
            // Original avatar frames remain; their Master Duel character art does not.
            foreach (string label in new[] { "AvatarPlayer0", "AvatarPlayer1" })
            {
                var avatar = Manager.GetElement<Image>(label);
                if (avatar != null) avatar.enabled = false;
            }
            foreach (var text in GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.text == "LP") text.text = "Life";
            var imageObject = new GameObject("BattleSpiritsField", typeof(RectTransform), typeof(RawImage));
            imageObject.layer = Root.gameObject.layer;
            imageObject.transform.SetParent(Root, false);
            imageObject.transform.SetAsFirstSibling();
            var image = imageObject.GetComponent<RawImage>();
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = Vector2.zero;
            image.rectTransform.offsetMax = Vector2.zero;
            field = imageObject.AddComponent<BattleFieldRenderer>();
            field.CardSelected += InspectInstance;
            field.PresentationBusyChanged += OnPresentationBusy;
            field.Setup(image);
            field.SetPreferences(Config.GetBool("BattleReducedMotion", false), Config.GetBool("BattleMuted", false));
            BindButton("ButtonStop", "Surrender / Exit", OpenExit);
            BindButton("ButtonPlay", "Next", () => session?.StepReplay(1));
            BindButton("ButtonAcc", "Previous", () => session?.StepReplay(-1));
            BindButton("ButtonNor", "Reset", ResetPuzzle);
            BindButton("ButtonTiming", "Actions", () => { if (session?.IsReplay == true) OpenExit(); else OpenPanel("actions"); });
            BindButton("ButtonLog", "Log", () => OpenPanel("log"));
            BindButton("ButtonAttachedEffect", "Hand", () => OpenPanel("hand"));
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject.name == "ButtonClose") button.onClick.AddListener(CloseInspection);
                if (button.gameObject.name == "ButtonInfo") button.onClick.AddListener(() => OpenPanel("info"));
                if (button.gameObject.name == "ButtonSetting") button.onClick.AddListener(() => Program.instance.ShowSubServant(Program.instance.setting));
            }
            BuildArenaHud();
        }

        private void BindButton(string label, string caption, UnityAction callback)
        {
            var button = Manager.GetElement<Button>(label);
            if (button == null) return;
            Clear(button.onClick);
            button.onClick.AddListener(callback);
            var prototype = Manager.GetElement<TextMeshProUGUI>("TextHint");
            if (prototype == null) return;
            var text = Instantiate(prototype, button.transform, false);
            text.name = "BattleCaption";
            text.text = caption;
            text.fontSize = 15;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = new Vector2(.5f, 0);
            text.rectTransform.anchorMax = new Vector2(.5f, 0);
            text.rectTransform.anchoredPosition = new Vector2(0, -18);
            text.rectTransform.sizeDelta = new Vector2(130, 36);
            text.gameObject.SetActive(true);
        }

        public void Bind(BattleGameSession value)
        {
            if (session != null)
            {
                session.Changed -= Refresh;
                session.CommittedEvent -= OnCommitted;
                session.PresentationBusy = false;
            }
            session = value;
            previewCard = null;
            bindingRevision++;
            error = ""; page = 0; handPage = 0;
            choiceSelection.Reset();
            field?.SkipPresentation();
            if (session != null)
            {
                session.Changed += Refresh;
                session.CommittedEvent += OnCommitted;
            }
            Refresh();
        }

        public override void Show(bool cover) { ResetUI(); ShowEvent(); }
        public override void ShowEvent() { suspended = false; Refresh(); }
        public void Suspend()
        {
            suspended = true;
            field?.SkipPresentation();
            if (session != null) session.PresentationBusy = false;
            ClosePanel();
        }

        private void OnPresentationBusy(bool busy)
        {
            if (session != null) session.PresentationBusy = busy && !suspended;
            if (!disposed) Refresh();
        }

        private void OnCommitted(BattleEvent evt)
        {
            if (disposed || suspended || evt == null || evt.Kind != "Summoned") return;
            // Changed has already published the committed snapshot. Never animate a
            // hovered/legal/speculative summon or a replay's initial field.
            field.SetState(BattleDuelActions.Presentation(session.View));
            field.NotifyCommittedSummon(evt.SpiritId, bindingRevision + ":" + (++eventSequence));
        }

        private bool CanPlay => !disposed && !suspended && (session?.IsOnline != true || session.IsConnected) && BattleDuelActions.CanPlay(session?.View,
            session?.Seat ?? -1, session?.IsReplay ?? false, session?.PresentationBusy ?? false);

        private void Refresh()
        {
            if (disposed || field == null) return;
            var view = session?.View;
            field.SetState(string.IsNullOrEmpty(previewCard) ? BattleDuelActions.Presentation(view) : PreviewState(previewCard));
            int seat = session?.Seat ?? 0;
            for (int side = 0; side < 2; side++)
            {
                var player = view?.Players?.FirstOrDefault(p => p.Index == (side == 0 ? seat : 1 - seat));
                SetText("TextPlayer" + side + "Name", player == null ? (side == 0 ? "Player" : "Opponent") : player.Name);
                SetText("TextPlayer" + side + "LP", (player?.Life ?? 5).ToString());
            }
            SetActive("Hint", true);
            string status = session?.Status ?? "Battle Spirits";
            if (view != null)
                status = "Turn " + view.TurnNumber + " · " + BattleDuelActions.Phase(view.Phase)
                    + " · " + (view.DecisionPlayerIndex == seat ? "Your decision" : "Opponent's decision")
                    + "\n" + string.Join("   |   ", view.Players.Select(p => (p.Index == seat ? "You" : "Opponent")
                        + ": Reserve " + Pool(p.ReserveNormal, p.ReserveSoul) + " · Trash " + Pool(p.TrashNormal, p.TrashSoul)
                        + " · Deck " + p.DeckCount + " · Hand " + p.HandCount));
            if (view?.Phase == MatchPhase.GameOver) status += "\n" + (view.WinnerIndex == seat ? "Victory" : "Defeat");
            if (session?.Puzzle != null)
            {
                var puzzle = session.Puzzle;
                status += "\n" + puzzle.Definition.title + " · " + puzzle.Status + " · Info: goal / hint";
            }
            if (view?.PendingChoice != null) status += "\n" + view.PendingChoice.Prompt;
            if (session?.PresentationBusy == true) status += "\nSummon presentation · Escape skips";
            if (!string.IsNullOrEmpty(error)) status += "\n" + error;
            SetText("TextHint", status);
            var hint = Manager.GetElement<TextMeshProUGUI>("TextHint");
            if (hint != null) hint.fontSize = 18;
            SetActive("ButtonPlay", session?.IsReplay == true);
            SetActive("ButtonAcc", session?.IsReplay == true);
            SetActive("ButtonNor", session?.Puzzle != null);
            // Stop/Play and Acc/Nor occupy the same original positions.
            SetActive("ButtonStop", session?.IsReplay != true);
            SetActive("ButtonTiming", session != null);
            SetActive("ButtonLog", true);
            SetActive("ButtonAttachedEffect", true);
            var actions = Manager.GetElement<Button>("ButtonTiming");
            if (actions != null)
            {
                actions.interactable = session?.IsReplay == true || CanPlay;
                var caption = actions.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.gameObject.name == "BattleCaption");
                if (caption != null) caption.text = session?.IsReplay == true ? "Exit replay" : "Actions";
            }
            foreach (string label in new[] { "ButtonPlay", "ButtonAcc", "ButtonNor" })
            {
                var button = Manager.GetElement<Button>(label);
                if (button != null) button.interactable = session != null && !session.PresentationBusy;
            }
            RefreshHand();
            RefreshArenaHud();
            if (actionObject != null && actionObject.activeSelf) RenderPanel();
        }

        private static string Pool(int normal, bool soul) => normal + (soul ? " + Soul" : "");
        private void SetText(string label, string value)
        {
            var text = Manager.GetElement<TextMeshProUGUI>(label);
            if (text != null) text.text = value;
        }
        private void SetActive(string label, bool active)
        {
            var element = Manager.GetElement(label);
            if (element != null) element.SetActive(active);
        }

        private string handSignature;
        private void RefreshHand()
        {
            var player = session?.View?.Players?.FirstOrDefault(p => p.Index == session.Seat);
            var hand = player?.Hand ?? Array.Empty<string>();
            handPage = Math.Max(0, Math.Min(handPage, Math.Max(0, (hand.Length - 1) / HandPageSize)));
            string signature = bindingRevision + ":" + handPage + ":" + string.Join("|", hand);
            if (signature == handSignature) return;
            handSignature = signature;
            foreach (var old in handObjects) { old.SetActive(false); Destroy(old); }
            handObjects.Clear();
            var visible = hand.Skip(handPage * HandPageSize).Take(HandPageSize).ToArray();
            for (int i = 0; i < visible.Length; i++)
            {
                string id = visible[i];
                if (!BattleCatalog.TryGet(id, out var definition)) continue;
                int handIndex = handPage * HandPageSize + i;
                var obj = CanvasObject("BattleHand_" + handIndex, handArea != null ? handArea : Root);
                var bounds = obj.GetComponent<RectTransform>();
                bounds.anchorMin = bounds.anchorMax = new Vector2(.5f, 0);
                bounds.pivot = new Vector2(.5f, 0);
                float available = handArea != null && handArea.rect.width > 0 ? handArea.rect.width : 1050;
                float stride = Math.Min(136, Math.Max(48, (available - 132) / Math.Max(1, visible.Length - 1)));
                bounds.anchoredPosition = new Vector2((i - (visible.Length - 1) * .5f) * stride, 10);
                bounds.sizeDelta = new Vector2(132, 192);
                var face = CanvasObject("Printed card", obj.transform, typeof(RawImage), typeof(Button));
                var image = face.GetComponent<RawImage>();
                HudRect(image.rectTransform, 0, 0, 1, 1);
                var button = face.GetComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => { InspectCard(id, null, handIndex); if (CanPlay) OpenPanel("actions"); });
                handObjects.Add(obj);
                _ = LoadFace(image, id, -1);
            }
            // Native Hand button exposes every remaining card/page; nothing is discarded.
        }

        private async Task LoadFace(RawImage image, string id, int revision)
        {
            try
            {
                var texture = await CardImageLoader.LoadBattleFaceAsync(id, lifetime.Token);
                if (disposed || image == null || texture == null || revision >= 0 && revision != faceRevision) return;
                BattleCardAdapter.FitFace(image, texture);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!disposed) { error = "Card image: " + ex.Message; } }
        }

        private void InspectInstance(int instanceId)
        {
            var spirit = session?.View?.Players?.SelectMany(p => p.Field).FirstOrDefault(s => s.InstanceId == instanceId);
            if (spirit != null) InspectCard(spirit.CardId, spirit);
        }

        private void InspectCard(string id, SpiritSnapshot state = null, int handIndex = -1)
        {
            if (inspectionWindow == null || !BattleCatalog.TryGet(id, out var definition)) return;
            cardInspection = new BattleCardInspection { Definition = definition };
            inspectedHandIndex = handIndex;
            inspectedInstance = state?.InstanceId ?? -1;
            inspectionWindow.gameObject.SetActive(true);
            inspectionName.text = definition.DisplayName;
            inspectionStats.text = definition.id + " · " + definition.type + "\n" + BattleCardAdapter.Colors(definition);
            string details = BattleCardAdapter.Description(definition);
            if (state != null) details = "Lv" + state.Level + " · " + state.Bp + " BP · " + Pool(state.NormalCores, state.Soul)
                + " · " + (state.HeavyExhausted ? "Heavy exhausted" : state.Exhausted ? "Exhausted" : "Refreshed") + "\n" + details;
            inspectionEffects.text = details;
            Canvas.ForceUpdateCanvases();
            inspectionEffects.rectTransform.sizeDelta = new Vector2(0, Math.Max(inspectionScroll.viewport.rect.height, inspectionEffects.preferredHeight + 24));
            inspectionScroll.verticalNormalizedPosition = 1;
            _ = LoadFace(inspectionImage, id, ++faceRevision);
            inspectionWindow.SetAsLastSibling();
        }

        private void ZoomCard()
        {
            if (cardInspection != null) UIManager.ShowCardInfoDetail(BattleCardAdapter.ToNative(cardInspection.Definition));
        }

        private void ChangeInspectionFont(int delta)
        {
            var text = inspectionEffects;
            if (text != null) text.fontSize = Math.Max(16, Math.Min(40, text.fontSize + delta));
        }

        private void CloseInspection()
        {
            if (inspectionWindow != null) inspectionWindow.gameObject.SetActive(false);
            cardInspection = null;
            ClearCardSelection();
        }

        public void PreviewBoss(string id)
        {
            if (!BattleCatalog.TryGet(id, out _)) return;
            previewCard = id;
            // Explicit Cutscene preview has no game session. Its synthetic committed
            // summon is isolated from gameplay and cannot cause a rules mutation.
            field.SetState(PreviewState(id));
            field.NotifyCommittedSummon(int.MaxValue, "preview:" + (++eventSequence));
            InspectCard(id);
        }

        internal static BattlePresentationState PreviewState(string id)
        {
            BattleCatalog.TryGet(id, out var definition);
            return new BattlePresentationState
            {
                viewer = 0, currentPlayer = 0,
                cards = new[] { new BattleCardPresentation { instanceId = int.MaxValue, cardId = id, owner = 0,
                    level = 1, bp = definition?.levels?.FirstOrDefault()?.bp ?? 0, normalCores = definition?.levels?.FirstOrDefault()?.cores ?? 1 } }
            };
        }

        private void ResetPuzzle() { if (session?.PresentationBusy != true) session?.ResetPuzzle(); }

        private async void OpenPanel(string kind)
        {
            if (disposed) return;
            panel = kind; page = 0;
            if (actionObject != null)
            {
                actionObject.SetActive(true); RenderPanel(); return;
            }
            if (loadingActions) return;
            loadingActions = true;
            try
            {
                var handle = Addressables.InstantiateAsync("UI/PopupDuelSelection.prefab");
                await handle.Task;
                var obj = handle.Result;
                if (disposed) { Destroy(obj); return; }
                actionObject = obj;
                actionObject.transform.SetParent(Manager.GetElement<RectTransform>("Popup"), false);
                actionShell = obj.GetComponent<PopupDuelSelection>();
                // Keep serialized references, visuals and geometry. Never call its
                // Initialize/Show/Hide methods, which belong to the legacy engine.
                actionShell.enabled = false;
                foreach (var b in obj.GetComponentsInChildren<Button>(true)) Clear(b.onClick);
                actionShell.window.anchoredPosition = new Vector2(0, actionShell.showY);
                if (actionShell.btnHide != null) actionShell.btnHide.onClick.AddListener(ClosePanel);
                if (suspended) actionObject.SetActive(false);
                else RenderPanel();
            }
            catch (Exception ex) { error = "Actions could not open: " + ex.Message; Refresh(); }
            finally { loadingActions = false; }
        }

        private void ClosePanel() { if (actionObject != null) actionObject.SetActive(false); }
        private void OpenExit() => OpenPanel("exit");

        public void ReturnOrClose()
        {
            if (field?.IsPresentationBusy == true) { field.SkipPresentation(); return; }
            if (actionObject != null && actionObject.activeSelf) { ClosePanel(); return; }
            if (cardInspection != null)
            {
                CloseInspection(); return;
            }
            OpenExit();
        }

        private void RenderPanel()
        {
            if (actionShell == null || disposed) return;
            foreach (var row in rows) { row.SetActive(false); Destroy(row); }
            rows.Clear();
            rowHeight = 0;
            var entries = new List<BattleNativeEntry>();
            var view = session?.View;
            if (panel == "exit")
            {
                if (session != null && !session.IsReplay && view != null && view.Phase != MatchPhase.GameOver)
                    entries.Add(new BattleNativeEntry("Surrender and return to menu", () => { session.Concede(); Leave(); }, !session.PresentationBusy));
                else entries.Add(new BattleNativeEntry("Return to menu", Leave));
                if (session?.Puzzle != null) entries.Add(new BattleNativeEntry("Reset puzzle", () => { ClosePanel(); ResetPuzzle(); }));
                else if (session != null && !session.IsReplay && !session.IsOnline)
                    entries.Add(new BattleNativeEntry("Retry match", () => { ClosePanel(); session.Retry(); }));
                entries.Add(new BattleNativeEntry("Keep playing", ClosePanel));
            }
            else if (panel == "info")
            {
                entries.Add(new BattleNativeEntry(session?.Status ?? "Battle Spirits presentation preview", null, false));
                if (view != null)
                {
                    entries.Add(new BattleNativeEntry("Turn " + view.TurnNumber + " · " + BattleDuelActions.Phase(view.Phase), null, false));
                    foreach (var player in view.Players)
                        entries.Add(new BattleNativeEntry(player.Name + " · Life " + player.Life + " · Reserve " + Pool(player.ReserveNormal, player.ReserveSoul)
                            + " · Trash " + Pool(player.TrashNormal, player.TrashSoul) + " · Deck " + player.DeckCount + " · Hand " + player.HandCount, null, false));
                }
                if (session?.Puzzle != null)
                {
                    entries.Add(new BattleNativeEntry("Goal: " + session.Puzzle.Definition.description, null, false));
                    entries.Add(new BattleNativeEntry("Hint: " + session.Puzzle.Definition.hint, null, false));
                    entries.Add(new BattleNativeEntry("Puzzle: " + session.Puzzle.Status + " · " + session.Puzzle.ResultMessage, null, false));
                    entries.Add(new BattleNativeEntry("Reset puzzle", () => { ClosePanel(); ResetPuzzle(); }, !session.PresentationBusy));
                }
                entries.Add(new BattleNativeEntry("Reduced motion: " + (Config.GetBool("BattleReducedMotion", false) ? "On" : "Off"), () => TogglePreference("BattleReducedMotion")));
                entries.Add(new BattleNativeEntry("Mute presentations: " + (Config.GetBool("BattleMuted", false) ? "On" : "Off"), () => TogglePreference("BattleMuted")));
            }
            else if (panel == "log")
            {
                foreach (string message in session?.Log ?? Array.Empty<string>()) entries.Add(new BattleNativeEntry(message, null, false));
            }
            else if (panel == "hand")
            {
                var hand = view?.Players?.FirstOrDefault(p => p.Index == session.Seat)?.Hand ?? Array.Empty<string>();
                for (int i = 0; i < hand.Length; i++)
                {
                    string id = hand[i]; int handIndex = i;
                    entries.Add(new BattleNativeEntry((i + 1) + ". " + BattleDuelActions.CardName(id), () => { handPage = handIndex / HandPageSize; RefreshHand(); InspectCard(id, null, handIndex); }));
                }
            }
            else if (view?.PendingChoice != null && view.PendingChoice.Owner == session.Seat)
            {
                var choice = view.PendingChoice;
                choiceSelection.Bind(choice);
                for (int i = 0; i < choice.Options.Length; i++)
                {
                    int position = i;
                    var option = choice.Options[i];
                    int selected = choiceSelection.Selected.IndexOf(i);
                    string prefix = selected < 0 ? "[ ] " : choice.Ordered ? "[" + (selected + 1) + "] " : "[✓] ";
                    entries.Add(new BattleNativeEntry(prefix + option.label
                        + (string.IsNullOrEmpty(option.cardId) ? "" : " · " + BattleDuelActions.CardName(option.cardId))
                        + (option.index < 0 ? "" : " · source " + option.index), () =>
                        {
                            if (!CanPlay) return;
                            choiceSelection.Toggle(position);
                            if (!string.IsNullOrEmpty(option.cardId)) InspectCard(option.cardId);
                            RenderPanel();
                        }, CanPlay));
                }
                // Confirmation is present on every page and preserves click order.
            }
            else foreach (var command in (session?.LegalCommands ?? Array.Empty<BattleCommand>()).Where(SelectedCommand))
            {
                var exactCommand = command;
                entries.Add(new BattleNativeEntry(BattleDuelActions.Describe(command, view, session.Seat), () => Send(exactCommand), CanPlay));
            }
            int pageCount = Math.Max(1, (entries.Count + PageSize - 1) / PageSize);
            page = Math.Max(0, Math.Min(page, pageCount - 1));
            string title = panel == "actions" ? (view?.PendingChoice?.Prompt ?? "Legal actions") : panel == "hand" ? "Hand · inspect a printed card" : panel == "log" ? "Battle log" : panel == "info" ? "Battle information" : "Battle Spirits";
            if (actionShell.title != null) actionShell.title.text = title + "  " + (page + 1) + "/" + pageCount;
            foreach (var entry in entries.Skip(page * PageSize).Take(PageSize)) AddRow(entry);
            if (entries.Count == 0) AddRow(new BattleNativeEntry(panel == "actions" ? "Waiting for a decision / connection" : "No entries", null, false));
            if (panel == "actions" && (inspectedHandIndex >= 0 || inspectedInstance >= 0))
                AddRow(new BattleNativeEntry("Show all legal actions", () => { ClearCardSelection(); page = 0; RenderPanel(); }));
            if (panel == "actions" && view?.PendingChoice != null && view.PendingChoice.Owner == session.Seat)
            {
                var choice = view.PendingChoice;
                AddRow(new BattleNativeEntry("Confirm " + choiceSelection.Selected.Count + " selected (" + choice.Min + "–" + choice.Max + ")"
                    + (choice.Ordered ? " · click order is resolution order" : ""), () => Send(choiceSelection.Command()), CanPlay && choiceSelection.Valid));
                AddRow(new BattleNativeEntry("Clear selection", () => { choiceSelection.Clear(); RenderPanel(); }, CanPlay));
            }
            if (pageCount > 1)
            {
                AddRow(new BattleNativeEntry("Previous page", () => { page--; RenderPanel(); }, page > 0));
                AddRow(new BattleNativeEntry("Next page", () => { page++; RenderPanel(); }, page + 1 < pageCount));
            }
            float contentHeight = 25 + rowHeight;
            actionShell.scrollRect.content.sizeDelta = new Vector2(actionShell.scrollRect.content.sizeDelta.x, contentHeight);
            actionShell.baseRect.sizeDelta = new Vector2(actionShell.baseRect.sizeDelta.x, Math.Min(800, contentHeight + 50));
            actionShell.scrollRect.verticalNormalizedPosition = 1;
            var buttons = rows.Select(r => r.GetComponentInChildren<Button>(true)).Where(b => b != null).ToArray();
            for (int i = 0; i < buttons.Length; i++)
            {
                var navigation = buttons[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = buttons[Math.Max(0, i - 1)];
                navigation.selectOnDown = buttons[Math.Min(buttons.Length - 1, i + 1)];
                buttons[i].navigation = navigation;
            }
        }

        private void AddRow(BattleNativeEntry entry)
        {
            var row = Instantiate(actionShell.item, actionShell.scrollRect.content, false);
            row.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -20 - rowHeight);
            var button = row.GetComponentInChildren<Button>(true);
            Clear(button.onClick);
            button.interactable = entry.Enabled;
            if (entry.Click != null) button.onClick.AddListener(() => entry.Click());
            var text = row.GetComponentInChildren<Text>(true);
            text.text = entry.Label;
            text.fontSize = 19;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            float height = Math.Max(74, text.preferredHeight + 16);
            var rect = row.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, height);
            rowHeight += height + 16;
            rows.Add(row);
        }

        private void TogglePreference(string key)
        {
            Config.Set(key, Config.GetBool(key, false) ? "0" : "1");
            Config.Save();
            field.SetPreferences(Config.GetBool("BattleReducedMotion", false), Config.GetBool("BattleMuted", false));
            RenderPanel();
        }

        private void Send(BattleCommand command)
        {
            if (!CanPlay || command == null) return;
            if (!session.Send(command, out error)) { Refresh(); return; }
            error = "";
            ClearCardSelection();
            Refresh();
            if (session.View?.PendingChoice == null) ClosePanel();
            else { panel = "actions"; page = 0; RenderPanel(); }
        }

        private void Leave() { ClosePanel(); ((BattleDuelServant)parentServant).LeaveDuel(); }

        protected override void OnDestroy()
        {
            disposed = true;
            lifetime.Cancel(); lifetime.Dispose();
            if (session != null)
            {
                session.Changed -= Refresh;
                session.CommittedEvent -= OnCommitted;
                session.PresentationBusy = false;
            }
            if (field != null) { field.CardSelected -= InspectInstance; field.PresentationBusyChanged -= OnPresentationBusy; field.Dispose(); }
            base.OnDestroy();
        }

        private sealed class BattleCardInspection { public CardDefinition Definition; }
        private sealed class BattleNativeEntry
        {
            public readonly string Label; public readonly Action Click; public readonly bool Enabled;
            public BattleNativeEntry(string label, Action click, bool enabled = true) { Label = label; Click = click; Enabled = enabled; }
        }
    }

    /// <summary>Choice positions refer to the offered array, never a card's hand/source index.</summary>
    public sealed class BattleChoiceSelection
    {
        private PendingChoice choice;
        public List<int> Selected { get; } = new List<int>();
        public bool Valid => choice != null && Selected.Count >= choice.Min && Selected.Count <= choice.Max;
        public void Bind(PendingChoice value)
        {
            if (choice == null || value == null || value.Id != choice.Id) Selected.Clear();
            choice = value;
        }
        public void Toggle(int position)
        {
            if (choice == null || position < 0 || position >= choice.Options.Length) return;
            if (Selected.Contains(position)) Selected.Remove(position);
            else if (Selected.Count < choice.Max) Selected.Add(position);
        }
        public void Clear() => Selected.Clear();
        public void Reset() { choice = null; Clear(); }
        public BattleCommand Command() => Valid ? new BattleCommand { action = "choose", selection = Selected.ToArray() } : null;
    }

    public static class BattleDuelActions
    {
        public static bool CanPlay(BattleSnapshot view, int seat, bool replay, bool busy) => view != null
            && seat >= 0 && seat <= 1 && !replay && !busy && view.Phase != MatchPhase.GameOver && view.DecisionPlayerIndex == seat;
        public static string CardName(string id) => BattleCatalog.TryGet(id, out var card) ? card.DisplayName + " [" + id + "]" : id;
        public static string Phase(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Opening: return "Opening hand";
                case MatchPhase.Start: return "1 · Start Step";
                case MatchPhase.Core: return "2 · Core Step";
                case MatchPhase.Draw: return "3 · Draw Step";
                case MatchPhase.Refresh: return "4 · Refresh Step";
                case MatchPhase.Main: return "5 · Main Step";
                case MatchPhase.Attack: return "6 · Attack Step";
                case MatchPhase.FlashBeforeBlock: return "6 · Attack Step / Flash before block";
                case MatchPhase.Block: return "6 · Attack Step / Block";
                case MatchPhase.FlashAfterBlock: return "6 · Attack Step / Flash after block";
                case MatchPhase.SecondMain: return "7 · Second Main Step";
                case MatchPhase.End: return "8 · End Step";
                default: return "Game Over";
            }
        }
        public static string Describe(BattleCommand command, BattleSnapshot view, int seat)
        {
            var fields = new List<string> { command.action };
            var hand = view?.Players?.FirstOrDefault(p => p.Index == seat)?.Hand;
            if (command.action == "summon" || command.action == "magic")
                fields.Add("hand " + (command.index + 1) + ": " + (hand != null && command.index >= 0 && command.index < hand.Length ? CardName(hand[command.index]) : command.index.ToString()));
            if (command.source != -1 || command.action == "transfer" || command.action == "swap") fields.Add("from " + Source(command.source, view));
            if (command.target != -1 || command.action == "transfer" || command.action == "swap") fields.Add("to " + Source(command.target, view));
            fields.Add("mode " + command.mode);
            if (command.action == "opening") fields.Add(command.redraw ? "redraw hand" : "keep hand");
            if (command.soul) fields.Add("Soul Core");
            if (command.action == "transfer") fields.Add(command.soul ? "move Soul Core" : "move " + Math.Max(1, command.mode) + " normal core(s)");
            if (command.action == "swap") fields.Add("swap Soul Core with one normal core");
            if (command.alternateCost) fields.Add("alternate cost");
            if (command.chooseCores) fields.Add("Choose cores…");
            if (command.chooseSuccession) fields.Add("Choose Succession…");
            if (command.payment != null) fields.Add("pay " + Transfers(command.payment, view));
            else if (command.action == "summon" || command.action == "magic") fields.Add("automatic normal-core payment");
            if (command.transfers != null) fields.Add("cores " + Transfers(command.transfers, view));
            if (command.succession != null) fields.Add("succession " + string.Join(", ", command.succession.Select(CardName)));
            if (command.successionIndices != null) fields.Add("succession copies " + string.Join(", ", command.successionIndices));
            if (command.targets != null) fields.Add("targets " + string.Join(", ", command.targets.Select(t => Source(t, view))));
            if (command.selection != null) fields.Add("selection " + string.Join(", ", command.selection));
            return string.Join(" · ", fields);
        }
        private static string Transfers(CoreTransfer[] values, BattleSnapshot view) => string.Join("; ", values.Select(v => Source(v.source, view) + ": " + v.normal + (v.soul ? " + Soul" : "")));
        private static string Source(int id, BattleSnapshot view)
        {
            if (id == -1) return "Reserve";
            var spirit = view?.Players?.SelectMany(p => p.Field).FirstOrDefault(s => s.InstanceId == id);
            return spirit == null ? "#" + id : CardName(spirit.CardId) + " #" + id;
        }
        public static BattlePresentationState Presentation(BattleSnapshot view)
        {
            var people = view?.Players ?? Array.Empty<PlayerSnapshot>();
            return new BattlePresentationState
            {
                viewer = view?.ViewerIndex ?? 0, currentPlayer = view?.ActivePlayerIndex ?? 0,
                cards = people.SelectMany(p => p.Field ?? Array.Empty<SpiritSnapshot>()).Select(s => new BattleCardPresentation
                { instanceId = s.InstanceId, cardId = s.CardId, owner = s.Owner, exhausted = s.Exhausted || s.HeavyExhausted,
                    level = s.Level, bp = s.Bp, normalCores = s.NormalCores, soul = s.Soul }).ToArray(),
                players = people.Select(p => new BattlePlayerPresentation { playerIndex = p.Index, life = p.Life,
                    reserveNormal = p.ReserveNormal, reserveSoul = p.ReserveSoul, trashNormal = p.TrashNormal,
                    trashSoul = p.TrashSoul, deckCount = p.DeckCount, trashCardCount = p.Discard?.Length ?? 0, burstCount = 0 }).ToArray()
            };
        }
    }
}
