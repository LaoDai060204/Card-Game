using System;
using System.Linq;
using MDPro3.BattleSpirits.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MDPro3.BattleSpirits
{
    // Native controls over the render texture. All decisions use the session's legal commands.
    public sealed partial class BattleDuelUI
    {
        private CanvasGroup arenaHud;
        private Text arenaStatus, phaseTitle, advanceCaption;
        private readonly Text[] playerNames = new Text[2], playerLife = new Text[2], playerStats = new Text[2], stepLabels = new Text[8];
        private readonly Image[] turnIndicators = new Image[2];
        private Button advanceButton, legalButton;
        private RectTransform handArea, opponentHandArea, inspectionWindow;
        private RawImage inspectionImage;
        private Text inspectionName, inspectionStats, inspectionEffects;
        private ScrollRect inspectionScroll;
        private int inspectedHandIndex = -1, inspectedInstance = -1, opponentHandCount = -1;
        private readonly System.Collections.Generic.List<GameObject> concealedHand = new System.Collections.Generic.List<GameObject>();
        private static readonly Color PanelColor = new Color(.064f, .079f, .102f, .97f);
        private static readonly Color BorderColor = new Color(.29f, .36f, .45f, .84f);
        private static readonly Color LocalColor = new Color(.45f, .90f, .75f);
        private static readonly Color RivalColor = new Color(.98f, .73f, .45f);
        private static readonly Color MutedColor = new Color(.70f, .75f, .81f);

        private GameObject CanvasObject(string name, Transform parent, params Type[] components)
        {
            var types = new[] { typeof(RectTransform) }.Concat(components).ToArray();
            var go = new GameObject(name, types) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one;
            return go;
        }

        private static void HudRect(RectTransform rect, float left, float bottom, float right, float top, float inset = 0)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private GameObject HudPanel(string name, Transform parent, float left, float bottom, float right, float top)
        {
            var go = CanvasObject(name, parent, typeof(Image), typeof(Outline));
            HudRect(go.GetComponent<RectTransform>(), left, bottom, right, top);
            go.GetComponent<Image>().color = PanelColor;
            go.GetComponent<Image>().raycastTarget = true;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = BorderColor;
            outline.effectDistance = new Vector2(1, -1);
            return go;
        }

        private Text HudText(string name, Transform parent, int size, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var go = CanvasObject(name, parent, typeof(Text));
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color(.9f, .95f, 1, 1);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private Button HudButton(string name, Transform parent, string caption, float left, float bottom, float right, float top, Action click)
        {
            var go = CanvasObject(name, parent, typeof(Image), typeof(Button), typeof(Outline));
            HudRect(go.GetComponent<RectTransform>(), left, bottom, right, top, 2);
            var image = go.GetComponent<Image>();
            image.color = new Color(.12f, .16f, .21f, 1);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => click());
            var outline = go.GetComponent<Outline>();
            outline.effectColor = BorderColor;
            outline.effectDistance = new Vector2(1, -1);
            var text = HudText("Caption", go.transform, 22, TextAnchor.MiddleCenter);
            text.text = caption;
            HudRect(text.rectTransform, 0, 0, 1, 1, 4);
            return button;
        }

        private void BuildArenaHud()
        {
            // These objects belong to this private prefab instance. The original
            // menu widgets, prefab hierarchy and shared assets stay untouched.
            foreach (string name in new[] { "LP0", "LP1" })
            {
                var child = Root.Find(name);
                if (child != null) child.gameObject.SetActive(false);
            }
            SetActive("Buttons", false);
            SetActive("Hint", false);
            var oldDescription = Manager.GetElement("CardDescription");
            if (oldDescription != null) oldDescription.SetActive(false);
            var hud = CanvasObject("Battle Spirits arena HUD", Root, typeof(CanvasGroup));
            HudRect(hud.GetComponent<RectTransform>(), 0, 0, 1, 1);
            arenaHud = hud.GetComponent<CanvasGroup>();
            for (int side = 0; side < 2; side++)
            {
                bool local = side == 0;
                var color = local ? LocalColor : RivalColor;
                var plate = HudPanel(local ? "Your status" : "Opponent status", hud.transform,
                    .014f, local ? .027f : .813f, .205f, local ? .194f : .980f);
                var indicator = CanvasObject("Turn indicator", plate.transform, typeof(Image)).GetComponent<Image>();
                indicator.raycastTarget = false;
                HudRect(indicator.rectTransform, 0, .966f, 1, 1);
                turnIndicators[side] = indicator;
                playerNames[side] = HudText("Player name", plate.transform, 23);
                playerNames[side].color = color;
                HudRect(playerNames[side].rectTransform, .06f, .72f, .95f, .95f);
                var lifeLabel = HudText("Life label", plate.transform, 17, TextAnchor.MiddleCenter);
                lifeLabel.text = "LIFE";
                lifeLabel.color = MutedColor;
                HudRect(lifeLabel.rectTransform, .04f, .12f, .30f, .31f);
                playerLife[side] = HudText("Life value", plate.transform, 38, TextAnchor.MiddleCenter);
                playerLife[side].color = color;
                HudRect(playerLife[side].rectTransform, .04f, .30f, .30f, .72f);
                playerStats[side] = HudText("Public counters", plate.transform, 19);
                HudRect(playerStats[side].rectTransform, .34f, .08f, .96f, .69f);
            }
            var phase = HudPanel("Standard turn steps", hud.transform, .807f, .245f, .985f, .79f);
            phaseTitle = HudText("Turn and decision", phase.transform, 23);
            HudRect(phaseTitle.rectTransform, .06f, .85f, .94f, .99f);
            string[] names = { "Start", "Core", "Draw", "Refresh", "Main", "Attack", "Second Main", "End" };
            for (int i = 0; i < 8; i++)
            {
                var label = HudText("Step " + (i + 1), phase.transform, 22);
                label.text = (i + 1) + "  " + names[i];
                HudRect(label.rectTransform, .08f, .78f - i * .071f, .96f, .849f - i * .071f);
                stepLabels[i] = label;
            }
            legalButton = HudButton("Legal actions", phase.transform, "Actions", .05f, .13f, .95f, .218f,
                () => { ClearCardSelection(); OpenPanel("actions"); });
            advanceButton = HudButton("Next decision", phase.transform, "Choose action", .05f, .025f, .95f, .115f, AdvanceFromHud);
            advanceCaption = advanceButton.GetComponentInChildren<Text>(true);
            advanceCaption.color = LocalColor;
            var tools = HudPanel("Arena controls", hud.transform, .807f, .027f, .985f, .224f);
            HudButton("Inspect hand", tools.transform, "Hand", .04f, .67f, .49f, .97f, () => OpenPanel("hand"));
            HudButton("Battle log", tools.transform, "Log", .51f, .67f, .96f, .97f, () => OpenPanel("log"));
            HudButton("Battle info", tools.transform, "Info", .04f, .35f, .49f, .65f, () => OpenPanel("info"));
            HudButton("Battle settings", tools.transform, "Settings", .51f, .35f, .96f, .65f,
                () => Program.instance.ShowSubServant(Program.instance.setting));
            HudButton("Leave battle", tools.transform, "Surrender / Exit", .04f, .03f, .96f, .33f, OpenExit);
            var status = HudPanel("Battle prompt", hud.transform, .225f, .835f, .78f, .905f);
            arenaStatus = HudText("Current prompt", status.transform, 22, TextAnchor.MiddleCenter);
            HudRect(arenaStatus.rectTransform, .02f, .04f, .98f, .96f);
            handArea = CanvasObject("Your hand", Root).GetComponent<RectTransform>();
            HudRect(handArea, .216f, .015f, .795f, .205f);
            opponentHandArea = CanvasObject("Concealed opponent hand", Root).GetComponent<RectTransform>();
            HudRect(opponentHandArea, .28f, .91f, .72f, .995f);
            BuildInspection(hud.transform);
        }

        private void BuildInspection(Transform parent)
        {
            var panel = HudPanel("Printed card inspection", parent, .012f, .025f, .255f, .975f);
            inspectionWindow = panel.GetComponent<RectTransform>();
            inspectionName = HudText("English card name", panel.transform, 27);
            HudRect(inspectionName.rectTransform, .05f, .927f, .85f, .985f);
            HudButton("Close card", panel.transform, "X", .86f, .933f, .97f, .983f, CloseInspection);
            // FitInParent belongs inside a dedicated image slot. Putting that
            // fitter directly below the panel would cover its text and buttons.
            var faceSlot = CanvasObject("Printed face bounds", panel.transform);
            HudRect(faceSlot.GetComponent<RectTransform>(), .12f, .425f, .88f, .92f);
            inspectionImage = CanvasObject("Full resolution printed face", faceSlot.transform, typeof(RawImage), typeof(Button)).GetComponent<RawImage>();
            HudRect(inspectionImage.rectTransform, 0, 0, 1, 1);
            var imageButton = inspectionImage.GetComponent<Button>();
            imageButton.targetGraphic = inspectionImage;
            imageButton.onClick.AddListener(ZoomCard);
            inspectionStats = HudText("Card identity and current state", panel.transform, 21);
            HudRect(inspectionStats.rectTransform, .05f, .335f, .95f, .42f);
            var scrollObject = CanvasObject("English effect text", panel.transform, typeof(Image), typeof(ScrollRect));
            HudRect(scrollObject.GetComponent<RectTransform>(), .045f, .085f, .955f, .33f);
            scrollObject.GetComponent<Image>().color = new Color(.037f, .047f, .063f, 1);
            inspectionScroll = scrollObject.GetComponent<ScrollRect>();
            var viewport = CanvasObject("Viewport", scrollObject.transform, typeof(RectMask2D)).GetComponent<RectTransform>();
            HudRect(viewport, 0, 0, 1, 1, 7);
            inspectionEffects = HudText("English rules and effects", viewport, 22, TextAnchor.UpperLeft);
            inspectionEffects.verticalOverflow = VerticalWrapMode.Overflow;
            var content = inspectionEffects.rectTransform;
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
            inspectionScroll.viewport = viewport;
            inspectionScroll.content = content;
            inspectionScroll.horizontal = false;
            inspectionScroll.vertical = true;
            inspectionScroll.scrollSensitivity = 32;
            HudButton("Zoom printed card", panel.transform, "Enlarge", .04f, .018f, .46f, .073f, ZoomCard);
            HudButton("Selected card actions", panel.transform, "Card actions", .48f, .018f, .96f, .073f, () => OpenPanel("actions"));
            panel.SetActive(false);
        }

        private void AdvanceFromHud()
        {
            if (session?.IsReplay == true) { session.StepReplay(1); return; }
            if (!CanPlay) return;
            var next = session.LegalCommands.FirstOrDefault(c => c.action == "advance" || c.action == "pass");
            if (next != null && session.View.PendingChoice == null) Send(next);
            else { ClearCardSelection(); OpenPanel("actions"); }
        }

        private void RefreshArenaHud()
        {
            if (arenaHud == null) return;
            bool busy = session?.PresentationBusy == true;
            arenaHud.alpha = busy ? 0 : 1;
            arenaHud.blocksRaycasts = !busy;
            if (busy) ClosePanel();
            var view = session?.View;
            for (int side = 0; side < 2; side++)
            {
                int seat = side == 0 ? session?.Seat ?? 0 : 1 - (session?.Seat ?? 0);
                var p = view?.Players?.FirstOrDefault(v => v.Index == seat);
                playerNames[side].text = p?.Name ?? (side == 0 ? "Player" : "Opponent");
                playerLife[side].text = p == null ? "–" : p.Life.ToString();
                playerStats[side].text = p == null ? "" :
                    "Reserve " + Pool(p.ReserveNormal, p.ReserveSoul)
                    + "\nDeck " + p.DeckCount + "  ·  Hand " + p.HandCount
                    + "\nTrash " + p.Discard.Length + " cards / " + Pool(p.TrashNormal, p.TrashSoul);
                bool active = p != null && view.Phase != MatchPhase.GameOver && view.ActivePlayerIndex == seat;
                turnIndicators[side].color = active ? (side == 0 ? LocalColor : RivalColor) : BorderColor;
            }
            phaseTitle.text = view == null ? (string.IsNullOrEmpty(previewCard) ? "Battle Spirits" : "Summon gallery") : "Turn " + view.TurnNumber
                + "  ·  " + (view.ActivePlayerIndex == session.Seat ? "Your turn" : "Opponent's turn");
            int step = view == null ? -1 : PhaseStep(view.Phase);
            for (int i = 0; i < stepLabels.Length; i++)
                stepLabels[i].color = i == step ? (view.ActivePlayerIndex == session.Seat ? LocalColor : RivalColor) : MutedColor;
            string prompt = error;
            if (string.IsNullOrEmpty(prompt)) prompt = view?.PendingChoice?.Prompt;
            if (string.IsNullOrEmpty(prompt)) prompt = view == null ? (string.IsNullOrEmpty(previewCard)
                    ? session?.Status ?? "Preparing the arena" : "Summon preview · Escape skips the presentation.")
                : view.Phase == MatchPhase.GameOver ? (view.WinnerIndex == session.Seat ? "Victory" : "Defeat")
                : BattleDuelActions.Phase(view.Phase)
                    + (view.DecisionPlayerIndex == session.Seat ? "  ·  Your action" : "  ·  Opponent's action");
            arenaStatus.text = prompt;
            legalButton.interactable = CanPlay;
            advanceButton.interactable = session?.IsReplay == true || CanPlay;
            advanceCaption.text = session?.IsReplay == true ? "Next replay action" :
                view?.PendingChoice != null ? "Resolve effect" : view?.Phase == MatchPhase.Opening ? "Choose opening hand" : "Next step / Pass";
            SetActive("Hint", false);
            RefreshConcealedHand(view?.Players?.FirstOrDefault(v => v.Index != session.Seat)?.HandCount ?? 0);
            if (handArea != null) handArea.gameObject.SetActive(!busy);
            if (opponentHandArea != null) opponentHandArea.gameObject.SetActive(!busy);
        }

        private static int PhaseStep(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Start: return 0; case MatchPhase.Core: return 1;
                case MatchPhase.Draw: return 2; case MatchPhase.Refresh: return 3;
                case MatchPhase.Main: return 4; case MatchPhase.Attack:
                case MatchPhase.FlashBeforeBlock: case MatchPhase.Block: case MatchPhase.FlashAfterBlock: return 5;
                case MatchPhase.SecondMain: return 6; case MatchPhase.End: return 7;
                default: return -1;
            }
        }

        private void RefreshConcealedHand(int count)
        {
            if (opponentHandArea == null || opponentHandCount == count) return;
            opponentHandCount = count;
            foreach (var go in concealedHand) { go.SetActive(false); Destroy(go); }
            concealedHand.Clear();
            int visible = Math.Min(12, Math.Max(0, count));
            for (int i = 0; i < visible; i++)
            {
                var go = CanvasObject("Opponent concealed card bounds", opponentHandArea);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(48, 70);
                rect.anchoredPosition = new Vector2((i - (visible - 1) * .5f) * 32, 0);
                var face = CanvasObject("Opponent concealed card", go.transform, typeof(RawImage));
                var image = face.GetComponent<RawImage>();
                HudRect(image.rectTransform, 0, 0, 1, 1);
                image.raycastTarget = false;
                concealedHand.Add(go);
                _ = LoadConcealedFace(image);
            }
        }

        private async System.Threading.Tasks.Task LoadConcealedFace(RawImage image)
        {
            try
            {
                var texture = await BattleCardAdapter.BackAsync(lifetime.Token);
                if (!disposed && image != null && texture != null) BattleCardAdapter.FitFace(image, texture);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!disposed) error = "Card back: " + ex.Message; }
        }

        private void ClearCardSelection() { inspectedHandIndex = inspectedInstance = -1; }
        private bool SelectedCommand(BattleCommand command)
        {
            if (inspectedHandIndex >= 0)
                return (command.action == "summon" || command.action == "magic") && command.index == inspectedHandIndex
                    || command.action == "opening" || command.action == "pass" || command.action == "advance";
            if (inspectedInstance >= 0)
                return command.source == inspectedInstance || command.target == inspectedInstance
                    || command.action == "pass" || command.action == "advance";
            return true;
        }
    }
}
