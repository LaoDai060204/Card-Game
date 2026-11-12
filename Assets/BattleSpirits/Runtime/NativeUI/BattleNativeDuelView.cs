using System;
using System.Collections.Generic;
using System.Linq;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Networking;
using LaoDai.BattleSpirits.Visuals;
using MDPro3;
using MDPro3.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LaoDai.BattleSpirits.NativeUI
{
    // Original OcgCoreUI LP/name/detail/button locations, driven by BS snapshots.
    internal sealed class BattleNativeDuelView
    {
        private readonly Transform root;
        private readonly IBattleNativeSession session;
        private readonly Dictionary<string, CardDefinition> cards;
        private RectTransform hand, opponentHand, field0, field1;
        private TextMeshProUGUI phase;
        private int selectedSpirit = -1;
        private int selectedHand = -1, inspectedInstance = -1;
        private string inspectedId, handSignature;
        private readonly List<BattleNativeWidgets.CardSlot> handSlots = new List<BattleNativeWidgets.CardSlot>();
        private readonly List<RawImage> opponentBacks = new List<RawImage>();
        private readonly Dictionary<int, FieldSlot> fieldSlots = new Dictionary<int, FieldSlot>();
        private readonly HashSet<int> presentFieldIds = new HashSet<int>();
        private readonly List<int> expiredFieldIds = new List<int>();
        private readonly Stack<FieldSlot> spareFieldSlots = new Stack<FieldSlot>();
        private TextMeshProUGUI opponentOverflow, inspectName, inspectMeta, inspectState, inspectText;
        private RawImage inspectArt, zoomArt;
        private RectTransform inspector, zoom;
        private ScrollRect inspectScroll;
        private Button inspectPlay, inspectZoom;
        private GameObject selectedBeforeZoom;
        private bool ZoomVisible => zoom != null && zoom.gameObject.activeSelf;
        private sealed class FieldSlot
        {
            internal int instance;
            internal string id;
            internal bool own;
            internal Button button;
            internal TextMeshProUGUI stats;
            internal GameObject selection;
        }
        private RawImage backdrop;
        private BattleSummonPresentation summonPresentation;
        private IBattleNativePresentationSession presentationSession;
        internal BattleNativeDuelView(Transform root, IBattleNativeSession session)
        { this.root = root; this.session = session; cards = session.Cards.ToDictionary(card => card.id, StringComparer.Ordinal); }
        internal void Build()
        {
            BattleNativeWidgets.Active(root, "Buttons/Buttons ControlSpeed", false);
            BattleNativeWidgets.Active(root, "DuelErrorLog", false);
            BattleNativeWidgets.Active(root, "InputField (TMP)", false);
            BattleNativeWidgets.Active(root, "CardDescription", false);
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                if (node.name == "LP" && node.GetComponent<TextMeshProUGUI>() is TextMeshProUGUI lifeLabel)
                    lifeLabel.text = "LIFE";
                if (node.name.IndexOf("Avatar", StringComparison.OrdinalIgnoreCase) >= 0)
                    node.gameObject.SetActive(false);
            }
            var background = BattleNativeWidgets.Rect(root, "BS_Field"); BattleNativeWidgets.Place(background, .24f, .18f, 1, .90f);
            background.SetAsFirstSibling(); backdrop = background.gameObject.AddComponent<RawImage>(); backdrop.raycastTarget = false;
            session.AttachField(backdrop);
            // Both hit layers use the exact rendered field rectangle. Viewport
            // coordinates must not be interpreted against the full screen.
            field0 = BattleNativeWidgets.Rect(root, "BS_OwnField"); BattleNativeWidgets.Place(field0, .24f, .18f, 1, .90f);
            field1 = BattleNativeWidgets.Rect(root, "BS_OpponentField"); BattleNativeWidgets.Place(field1, .24f, .18f, 1, .90f);
            hand = BattleNativeWidgets.Rect(root, "BS_Hand"); BattleNativeWidgets.Place(hand, .255f, .04f, .855f, .245f);
            opponentHand = BattleNativeWidgets.Rect(root, "BS_OpponentHand"); BattleNativeWidgets.Place(opponentHand, .42f, .89f, .73f, .99f);
            phase = BattleNativeWidgets.Text(root, "BS_Phase", string.Empty, 23); BattleNativeWidgets.Place(phase.rectTransform, .245f, .905f, .42f, .995f);
            phase.alignment = TextAlignmentOptions.MidlineLeft;
            ArrangeLife("LP0", .245f, .25f, .42f, .33f);
            ArrangeLife("LP1", .735f, .905f, .975f, .995f);
            BuildInspector();
            SetButton("ButtonSetting", "NHẬN THUA", () => session.Send(new WireMessage { type = "action", action = "concede" }));
            summonPresentation = root.gameObject.AddComponent<BattleSummonPresentation>();
            summonPresentation.Configure(session);
            presentationSession = session as IBattleNativePresentationSession;
            if (presentationSession != null)
            {
                presentationSession.PresentationEvent += summonPresentation.Enqueue;
                presentationSession.PresentationReset += summonPresentation.Clear;
                presentationSession.PresentationReset += ResetInspection;
            }
            Render();
        }
        internal void SuspendPresentation() { summonPresentation?.Clear(); ResetInspection(); }
        internal void Dispose()
        {
            if (presentationSession != null && summonPresentation != null)
            {
                presentationSession.PresentationEvent -= summonPresentation.Enqueue;
                presentationSession.PresentationReset -= summonPresentation.Clear;
                presentationSession.PresentationReset -= ResetInspection;
            }
            summonPresentation?.Dispose();
        }
        internal void ResumeVisuals()
        {
            ResetInspection();
            if (backdrop != null) session.AttachField(backdrop);
        }
        internal void Render()
        {
            var view = session.View;
            if (view == null || view.players == null || view.players.Length != 2)
            {
                summonPresentation?.Clear(); ResetInspection();
                foreach (var slot in handSlots) slot.Hide();
                foreach (var slot in fieldSlots.Values) slot.button.gameObject.SetActive(false);
                foreach (var back in opponentBacks) back.transform.parent.gameObject.SetActive(false);
                if (opponentOverflow != null) opponentOverflow.gameObject.SetActive(false);
                return;
            }
            var own = view.players.First(player => player.playerIndex == view.viewer);
            var opponent = view.players.First(player => player.playerIndex != view.viewer);
            BattleNativeWidgets.Label(root, "LP0/TextPlayer0LP", own.life.ToString());
            BattleNativeWidgets.Label(root, "LP1/TextPlayer1LP", opponent.life.ToString());
            Name("LP0/NameBase/TextPlayer0Name", own); Name("LP1/NameBase/TextPlayer1Name", opponent);
            phase.text = view.winner >= 0 ? (view.winner == view.viewer ? "CHIẾN THẮNG" : "KẾT THÚC TRẬN")
                : "LƯỢT " + view.turn + " · " + Phase(view.phase) + "\n" + (view.decisionPlayer == view.viewer ? "Đến lượt bạn chọn" : "Đang chờ đối thủ…");
            string signature = string.Join("\n", view.ownHand ?? Array.Empty<string>());
            if (handSignature != signature && selectedHand >= 0) ResetInspection();
            handSignature = signature;
            if (inspectedInstance >= 0 && !view.players.Any(player => (player.field ?? Array.Empty<SpiritView>())
                .Any(spirit => spirit.instanceId == inspectedInstance && spirit.cardId == inspectedId))) ResetInspection();
            presentFieldIds.Clear(); expiredFieldIds.Clear();
            foreach (var player in view.players)
                foreach (var spirit in player.field ?? Array.Empty<SpiritView>()) presentFieldIds.Add(spirit.instanceId);
            foreach (var entry in fieldSlots) if (!presentFieldIds.Contains(entry.Key)) expiredFieldIds.Add(entry.Key);
            foreach (int id in expiredFieldIds)
            {
                var slot = fieldSlots[id]; slot.button.gameObject.SetActive(false);
                spareFieldSlots.Push(slot); fieldSlots.Remove(id);
            }
            DrawHand(view); DrawOpponentHand(opponent); DrawField(field0, own, true); DrawField(field1, opponent, false);
            RefreshInspection(); Actions(view);
        }
        internal void Tick()
        {
            // A window resize can refit the stage without a network snapshot.
            // Reproject the existing targets; do not rebuild any widgets/art.
            foreach (var slot in fieldSlots.Values)
                if (slot.button.gameObject.activeSelf) PlaceFieldSlot(slot);
        }
        private void Name(string path, PublicPlayerView player)
        {
            BattleNativeWidgets.Label(root, path, player.name + "\nReserve " + player.reserveNormal + (player.reserveSoul ? " + ◆" : "") + " · Trash " + player.trashNormal + " · Deck " + player.deckCount);
            var label = BattleNativeWidgets.Component<TextMeshProUGUI>(root, path); if (label != null) label.fontSize = 20;
        }
        private void DrawHand(FilteredMatchView view)
        {
            int count = view.ownHand?.Length ?? 0;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                if (i == handSlots.Count)
                {
                    var created = BattleNativeWidgets.CreateCardSlot(hand, session);
                    created.Button.transition = Selectable.Transition.None;
                    var surface = created.Button.gameObject.AddComponent<Image>();
                    surface.raycastTarget = true; surface.color = new Color(.02f, .03f, .04f, .85f);
                    SelectionFrame(created.Button.transform, "BS_HandSelection");
                    handSlots.Add(created);
                }
                var slot = handSlots[i]; string id = view.ownHand[i];
                slot.Bind(id, () => SelectHand(index, id));
                float width = Math.Min(.16f, 1f / Math.Max(1, count));
                float start = (1 - count * width) * .5f;
                BattleNativeWidgets.Place(slot.Button.transform as RectTransform, start + i * width, 0,
                    start + (i + 1) * width - .003f, 1);
                slot.Button.interactable = presentationSession?.IsPresentationBusy != true;
                BattleNativeWidgets.Active(slot.Button.transform, "BS_HandSelection", selectedHand == i);
            }
            for (int i = count; i < handSlots.Count; i++) handSlots[i].Hide();
        }
        private void DrawOpponentHand(PublicPlayerView player)
        {
            int count = Math.Min(10, Math.Max(0, player.handCount));
            for (int i = 0; i < count; i++)
            {
                if (i == opponentBacks.Count)
                {
                    var created = BattleNativeWidgets.Rect(opponentHand, "BS_OpponentBack_" + i);
                    // FitInParent owns the artwork child, not the positioned
                    // slot: placing the fitter on the slot itself would reset
                    // its anchors and stack every back in the hand's center.
                    var artwork = BattleNativeWidgets.Rect(created, "BS_BackArtwork");
                    BattleNativeWidgets.Place(artwork, 0, 0, 1, 1);
                    var image = artwork.gameObject.AddComponent<RawImage>();
                    image.texture = BattleTheme.CardBack; image.raycastTarget = false;
                    image.color = image.texture != null ? Color.white : BattleTheme.PanelBackground;
                    var aspect = artwork.gameObject.AddComponent<AspectRatioFitter>();
                    aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    aspect.aspectRatio = image.texture != null ? (float)image.texture.width / image.texture.height : .7f;
                    opponentBacks.Add(image);
                }
                var slot = opponentBacks[i].transform.parent as RectTransform; slot.gameObject.SetActive(true);
                float width = Math.Min(0.10f, 1f / Math.Max(1, count));
                float start = (1 - count * width) * .5f;
                BattleNativeWidgets.Place(slot, start + i * width, 0, start + (i + 1) * width - .006f, 1);
            }
            for (int i = count; i < opponentBacks.Count; i++) opponentBacks[i].transform.parent.gameObject.SetActive(false);
            if (opponentOverflow == null && player.handCount > count)
            {
                opponentOverflow = BattleNativeWidgets.Text(opponentHand, "BS_HandOverflow", string.Empty, 18);
                BattleNativeWidgets.Place(opponentOverflow.rectTransform, .92f, .02f, 1, .28f);
            }
            if (opponentOverflow != null)
            { opponentOverflow.text = "+" + (player.handCount - count); opponentOverflow.gameObject.SetActive(player.handCount > count); }
        }

        private void DrawField(RectTransform container, PublicPlayerView player, bool own)
        {
            int count = player.field?.Length ?? 0;
            for (int i = 0; i < count; i++)
            {
                var spirit = player.field[i];
                if (!fieldSlots.TryGetValue(spirit.instanceId, out var slot))
                {
                    if (spareFieldSlots.Count > 0) slot = spareFieldSlots.Pop();
                    else
                    {
                        slot = new FieldSlot(); var captured = slot;
                        slot.button = BattleNativeWidgets.Button(container, "BS_FieldCard_" + spirit.instanceId, string.Empty,
                            () => SelectField(captured));
                        slot.button.transition = Selectable.Transition.None;
                        slot.button.GetComponent<Image>().color = Color.clear;
                        var strip = BattleNativeWidgets.Rect(slot.button.transform, "BS_StatsBackdrop");
                        BattleNativeWidgets.Place(strip, 0, 0, 1, .34f);
                        var face = strip.gameObject.AddComponent<Image>(); face.raycastTarget = false;
                        face.color = new Color(.015f, .025f, .035f, .94f);
                        slot.stats = BattleNativeWidgets.Text(strip, "BS_Stats", string.Empty, 18);
                        FitText(slot.stats, 11, 18);
                        slot.selection = SelectionFrame(slot.button.transform, "BS_FieldSelection");
                    }
                    slot.instance = spirit.instanceId; slot.button.gameObject.name = "BS_FieldCard_" + spirit.instanceId;
                    fieldSlots.Add(spirit.instanceId, slot);
                }
                slot.id = spirit.cardId; slot.own = own;
                if (slot.button.transform.parent != container) slot.button.transform.SetParent(container, false);
                slot.button.gameObject.SetActive(true);
                // Fallback during initial field loading, before a camera exists.
                float width = Math.Min(.105f, .58f / Math.Max(1, count));
                BattleNativeWidgets.Place(slot.button.transform as RectTransform, .22f + i * width,
                    own ? .28f : .62f, .22f + (i + 1) * width - .005f, own ? .41f : .75f);
                PlaceFieldSlot(slot);
                bool nexus = cards.TryGetValue(spirit.cardId, out var definition) && definition.IsNexus;
                slot.stats.text = (nexus ? "Nexus Lv" + spirit.level : "Lv" + spirit.level + " · " + spirit.bp + " BP")
                    + "\nCore " + spirit.normalCores + (spirit.soulCore ? " + ◆" : "") + (spirit.exhausted ? " · MỆT" : "");
                slot.selection.SetActive(inspectedInstance == spirit.instanceId);
            }
        }
        private void PlaceFieldSlot(FieldSlot slot)
        {
            slot.button.interactable = presentationSession?.IsPresentationBusy != true;
            if (presentationSession != null && presentationSession.TryGetCardViewport(slot.instance, out Rect bounds))
                BattleNativeWidgets.Place(slot.button.transform as RectTransform, bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax);
        }
        private void SelectHand(int index, string id)
        {
            var view = session.View;
            if (ZoomVisible || presentationSession?.IsPresentationBusy == true || view?.ownHand == null || index < 0
                || index >= view.ownHand.Length || view.ownHand[index] != id) return;
            selectedHand = index; inspectedInstance = selectedSpirit = -1; Inspect(id);
            RefreshSelections(); Actions(view);
        }
        private void SelectField(FieldSlot slot)
        {
            if (ZoomVisible || presentationSession?.IsPresentationBusy == true || !slot.button.gameObject.activeInHierarchy) return;
            if (session.View?.players?.Any(player => (player.field ?? Array.Empty<SpiritView>())
                .Any(spirit => spirit.instanceId == slot.instance && spirit.cardId == slot.id)) != true) return;
            selectedHand = -1; inspectedInstance = slot.instance; selectedSpirit = slot.own ? slot.instance : -1;
            Inspect(slot.id); RefreshSelections(); Actions(session.View);
        }
        private void RefreshSelections()
        {
            for (int i = 0; i < handSlots.Count; i++) BattleNativeWidgets.Active(handSlots[i].Button.transform, "BS_HandSelection", selectedHand == i);
            foreach (var slot in fieldSlots.Values) slot.selection.SetActive(inspectedInstance == slot.instance);
        }
        private static void Normalize(RectTransform rect, float left, float bottom, float right, float top)
        {
            if (rect == null) return;
            rect.localScale = Vector3.one; rect.pivot = new Vector2(.5f, .5f);
            BattleNativeWidgets.Place(rect, left, bottom, right, top);
            rect.anchoredPosition = Vector2.zero;
        }
        private static void FitText(TextMeshProUGUI text, float minimum, float maximum)
        {
            text.enableAutoSizing = true; text.fontSizeMin = minimum; text.fontSizeMax = maximum;
            text.overflowMode = TextOverflowModes.Truncate;
        }
        private void ArrangeLife(string path, float left, float bottom, float right, float top)
        {
            var area = BattleNativeWidgets.Find(root, path) as RectTransform;
            Normalize(area, left, bottom, right, top);
            if (area == null) return;
            Normalize(area.Find("NameBase") as RectTransform, .02f, .40f, .98f, 1);
            Normalize(BattleNativeWidgets.Find(area, "LP") as RectTransform, .03f, .02f, .27f, .42f);
            string value = path == "LP0" ? "TextPlayer0LP" : "TextPlayer1LP";
            Normalize(area.Find(value) as RectTransform, .29f, .02f, .98f, .42f);
            var label = BattleNativeWidgets.Component<TextMeshProUGUI>(area, value); if (label != null) label.fontSize = 26;
        }
        private static GameObject SelectionFrame(Transform parent, string name)
        {
            var frame = BattleNativeWidgets.Rect(parent, name); BattleNativeWidgets.Place(frame, 0, 0, 1, 1);
            for (int i = 0; i < 4; i++)
            {
                var edge = BattleNativeWidgets.Rect(frame, "Edge" + i);
                BattleNativeWidgets.Place(edge, i == 3 ? 1 : 0, i == 0 ? 1 : 0, i == 2 ? 0 : 1, i == 1 ? 0 : 1);
                edge.sizeDelta = i < 2 ? new Vector2(0, 3) : new Vector2(3, 0);
                var image = edge.gameObject.AddComponent<Image>(); image.color = BattleTheme.Accent; image.raycastTarget = false;
            }
            frame.gameObject.SetActive(false); return frame.gameObject;
        }
        private void BuildInspector()
        {
            var detail = BattleNativeWidgets.Find(root, "CardDescription") as RectTransform;
            if (detail == null) return;
            detail.gameObject.SetActive(true); Normalize(detail, 0, 0, 1, 1);
            inspector = detail.Find("Window") as RectTransform;
            if (inspector == null) return;
            // The old controller slid this Window from x=-420. It is now a
            // persistent native panel, so none of those hidden offsets apply.
            Normalize(inspector, .005f, .06f, .235f, .93f);
            foreach (Transform child in inspector)
                if (child.name != "Card" && child.name != "Description Scroll View") child.gameObject.SetActive(false);
            foreach (var group in detail.GetComponentsInChildren<CanvasGroup>(true))
            { group.alpha = 1; group.blocksRaycasts = true; }
            var panel = inspector.GetComponent<Image>() ?? inspector.gameObject.AddComponent<Image>();
            panel.material = null; panel.sprite = BattleTheme.PanelSprite; panel.type = Image.Type.Sliced;
            panel.color = panel.sprite == null ? BattleTheme.PanelBackground : Color.white; panel.raycastTarget = true;
            var artArea = BattleNativeWidgets.Rect(inspector, "BS_InspectArtArea");
            BattleNativeWidgets.Place(artArea, .035f, .47f, .965f, .90f);
            inspectArt = inspector.Find("Card")?.GetComponent<RawImage>();
            if (inspectArt != null)
            {
                inspectArt.rectTransform.SetParent(artArea, false); Normalize(inspectArt.rectTransform, 0, 0, 1, 1);
                inspectArt.color = Color.white; inspectArt.raycastTarget = false;
                var fitter = inspectArt.GetComponent<AspectRatioFitter>() ?? inspectArt.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = 675f / 983;
                var button = inspectArt.transform.Find("Button");
                if (button != null)
                {
                    var face = button.GetComponent<Image>(); if (face != null) face.color = Color.clear;
                    BattleNativeWidgets.Bind(inspectArt.transform, "Button", OpenZoom);
                }
            }
            inspectName = BattleNativeWidgets.Text(inspector, "BS_InspectName", "THÔNG TIN THẺ", 24);
            BattleNativeWidgets.Place(inspectName.rectTransform, .03f, .91f, .97f, .99f);
            inspectName.overflowMode = TextOverflowModes.Ellipsis;
            FitText(inspectName, 16, 24);
            inspectMeta = BattleNativeWidgets.Text(inspector, "BS_InspectMeta", string.Empty, 18);
            BattleNativeWidgets.Place(inspectMeta.rectTransform, .035f, .35f, .965f, .465f);
            inspectMeta.alignment = TextAlignmentOptions.TopLeft;
            FitText(inspectMeta, 12, 18);
            inspectState = BattleNativeWidgets.Text(inspector, "BS_InspectState", string.Empty, 18);
            BattleNativeWidgets.Place(inspectState.rectTransform, .035f, .285f, .965f, .35f);
            inspectState.alignment = TextAlignmentOptions.TopLeft; inspectState.color = BattleTheme.Accent;
            FitText(inspectState, 12, 18);
            var scrollNode = inspector.Find("Description Scroll View") as RectTransform;
            Normalize(scrollNode, .035f, .075f, .965f, .28f);
            inspectScroll = scrollNode?.GetComponent<ScrollRect>();
            if (inspectScroll != null)
            {
                inspectScroll.enabled = true; inspectScroll.horizontal = false; inspectScroll.vertical = true;
                inspectScroll.scrollSensitivity = 28; inspectScroll.movementType = ScrollRect.MovementType.Clamped;
                // The legacy auto-expanding scrollbar drives this viewport to
                // zero width while hidden. Own the viewport geometry explicitly.
                inspectScroll.horizontalScrollbar = null; inspectScroll.verticalScrollbar = null;
                var scrollbar = scrollNode.Find("Scrollbar Vertical"); if (scrollbar != null) scrollbar.gameObject.SetActive(false);
                var viewport = scrollNode.Find("Viewport") as RectTransform; Normalize(viewport, 0, 0, 1, 1);
                if (viewport != null)
                {
                    if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
                    var surface = viewport.GetComponent<Image>() ?? viewport.gameObject.AddComponent<Image>();
                    surface.enabled = true; surface.color = new Color(.015f, .025f, .035f, .45f); surface.raycastTarget = true;
                    inspectScroll.viewport = viewport;
                    var content = viewport.Find("Content") as RectTransform;
                    if (content != null)
                    {
                        Normalize(content, 0, 1, 1, 1); content.pivot = new Vector2(0, 1);
                        inspectScroll.content = content;
                        var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
                        fitter.enabled = true; fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                        inspectText = content.GetComponent<TextMeshProUGUI>();
                        if (inspectText != null)
                        { inspectText.enabled = true; inspectText.fontSize = 21; inspectText.alignment = TextAlignmentOptions.TopLeft; inspectText.richText = false; inspectText.enableWordWrapping = true; inspectText.raycastTarget = false; inspectText.color = Color.white; }
                    }
                }
            }
            inspectZoom = BattleNativeWidgets.Button(inspector, "BS_InspectZoom", "PHÓNG TO", OpenZoom);
            BattleNativeWidgets.Place(inspectZoom.transform as RectTransform, .03f, .008f, .48f, .065f);
            inspectPlay = BattleNativeWidgets.Button(inspector, "BS_InspectPlay", "CHỌN THẺ", UseSelectedHand);
            BattleNativeWidgets.Place(inspectPlay.transform as RectTransform, .52f, .008f, .97f, .065f);
            BuildZoom(); ResetInspection(); detail.SetAsLastSibling();
        }
        private void BuildZoom()
        {
            zoom = BattleNativeWidgets.Rect(root, "BS_CardZoom"); BattleNativeWidgets.Place(zoom, 0, 0, 1, 1);
            var shade = zoom.gameObject.AddComponent<Image>(); shade.color = new Color(.005f, .008f, .015f, .96f); shade.raycastTarget = true;
            var area = BattleNativeWidgets.Rect(zoom, "BS_ZoomArtArea"); BattleNativeWidgets.Place(area, .12f, .05f, .88f, .92f);
            var art = BattleNativeWidgets.Rect(area, "BS_ZoomArt"); BattleNativeWidgets.Place(art, 0, 0, 1, 1);
            zoomArt = art.gameObject.AddComponent<RawImage>(); zoomArt.color = Color.white; zoomArt.raycastTarget = false;
            var fitter = art.gameObject.AddComponent<AspectRatioFitter>(); fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = 675f / 983;
            var close = BattleNativeWidgets.Button(zoom, "BS_ZoomClose", "ĐÓNG", CloseZoom);
            var navigation = close.navigation; navigation.mode = Navigation.Mode.None; close.navigation = navigation;
            BattleNativeWidgets.Place(close.transform as RectTransform, .84f, .93f, .98f, .99f);
            var hint = BattleNativeWidgets.Text(zoom, "BS_ZoomHint", "ẢNH THẺ GỐC · Đóng để tiếp tục", 22);
            BattleNativeWidgets.Place(hint.rectTransform, .12f, .935f, .82f, .99f);
            zoom.gameObject.SetActive(false);
        }
        private void OpenZoom()
        {
            if (zoom == null || string.IsNullOrEmpty(inspectedId) || presentationSession?.IsPresentationBusy == true) return;
            if (!ZoomVisible) selectedBeforeZoom = EventSystem.current?.currentSelectedGameObject;
            zoom.gameObject.SetActive(true); zoom.SetAsLastSibling();
            session.LoadArt(inspectedId, zoomArt);
            EventSystem.current?.SetSelectedGameObject(zoom.Find("BS_ZoomClose")?.gameObject);
        }
        private void CloseZoom()
        {
            if (!ZoomVisible) return;
            zoom.gameObject.SetActive(false);
            var previous = selectedBeforeZoom; selectedBeforeZoom = null;
            EventSystem.current?.SetSelectedGameObject(previous != null && previous.activeInHierarchy
                && previous.GetComponent<Selectable>()?.interactable == true ? previous : null);
            RefreshInspection();
        }
        internal bool TryCloseZoom()
        {
            if (!ZoomVisible) return false;
            CloseZoom(); return true;
        }
        private void ResetInspection()
        {
            selectedSpirit = selectedHand = inspectedInstance = -1; inspectedId = null;
            CloseZoom();
            if (inspectName != null) inspectName.text = "THÔNG TIN THẺ";
            if (inspectMeta != null) inspectMeta.text = string.Empty;
            if (inspectState != null) inspectState.text = "Bấm thẻ trên tay hoặc trên sân để xem.";
            if (inspectText != null) inspectText.text = "Bấm PHÓNG TO để đọc ảnh thẻ lớn.\nThẻ trên tay chỉ được đánh khi bạn bấm TRIỆU HỒI / DÙNG MAGIC.";
            if (inspectArt != null) { inspectArt.texture = null; inspectArt.gameObject.SetActive(false); }
            if (zoomArt != null) zoomArt.texture = null;
            if (inspectPlay != null) inspectPlay.interactable = false;
            if (inspectZoom != null) inspectZoom.interactable = false;
            RefreshSelections();
        }
        private void Inspect(string id)
        {
            if (!cards.TryGetValue(id, out var card) || inspector == null) return;
            bool changed = inspectedId != id; inspectedId = id;
            inspectName.text = card.DisplayName;
            inspectMeta.text = card.id + " · " + card.type + " · " + card.color + "\nCost " + card.cost + " · Giảm " + card.reductionSymbols + " · Symbol " + card.symbols
                + "\n" + string.Join(" / ", (card.levels ?? new List<CardLevel>()).Where(level => level != null)
                    .Select((level, index) => "Lv" + (index + 1) + ": " + level.cores + " Core · " + level.bp + " BP"));
            if (inspectText != null && changed)
            {
                inspectText.text = (string.IsNullOrWhiteSpace(card.effectTextJa) || card.effectTextJa == "-" ? "Không có hiệu ứng in trên thẻ." : card.effectTextJa)
                    + (card.families?.Length > 0 ? "\n\nHệ: " + string.Join(" · ", card.families) : "")
                    + "\n\n" + card.setName;
                if (inspectScroll != null) inspectScroll.verticalNormalizedPosition = 1;
            }
            if (inspectArt != null)
            {
                inspectArt.gameObject.SetActive(true);
                if (changed || inspectArt.texture == null)
                { inspectArt.texture = null; inspectArt.uvRect = new Rect(0, 0, 1, 1); session.LoadArt(id, inspectArt); }
            }
            if (changed) CloseZoom();
            RefreshInspection();
        }
        private void RefreshInspection()
        {
            if (inspector == null || string.IsNullOrEmpty(inspectedId) || !cards.TryGetValue(inspectedId, out var card)) return;
            var view = session.View;
            bool inHand = selectedHand >= 0 && view?.ownHand != null && selectedHand < view.ownHand.Length && view.ownHand[selectedHand] == inspectedId;
            bool main = view?.phase == "Main" || view?.phase == "SecondMain";
            bool flash = view?.phase == "FlashBeforeBlock" || view?.phase == "FlashAfterBlock";
            inspectPlay.interactable = inHand && CanAct(view) && presentationSession?.IsPresentationBusy != true && (main || card.IsMagic && flash);
            BattleNativeWidgets.SetLabel(inspectPlay.transform, inHand ? card.IsMagic ? "DÙNG MAGIC" : "TRIỆU HỒI" : "ĐANG SOI THẺ");
            inspectZoom.interactable = presentationSession?.IsPresentationBusy != true;
            if (inHand)
                inspectState.text = inspectPlay.interactable ? "TRÊN TAY · Chọn nút bên dưới để đánh." : "TRÊN TAY · Chưa đến bước được đánh lá này.";
            else
            {
                var owner = view?.players?.FirstOrDefault(player => (player.field ?? Array.Empty<SpiritView>()).Any(item => item.instanceId == inspectedInstance));
                var spirit = owner?.field?.FirstOrDefault(item => item.instanceId == inspectedInstance);
                inspectState.text = spirit == null ? "THÔNG TIN THẺ" : (owner.playerIndex == view.viewer ? "SÂN BẠN" : "SÂN ĐỐI THỦ")
                    + " · Lv" + spirit.level + " · " + spirit.bp + " BP\nCore " + spirit.normalCores + (spirit.soulCore ? " + Soul Core" : "")
                    + (spirit.exhausted ? " · ĐÃ MỆT" : " · SẴN SÀNG");
            }
            if (inspectArt != null && inspectArt.gameObject.activeInHierarchy && inspectArt.texture == null) session.LoadArt(inspectedId, inspectArt);
        }
        private void UseSelectedHand()
        {
            int index = selectedHand; string id = inspectedId; var view = session.View;
            if (index < 0 || view?.ownHand == null || index >= view.ownHand.Length || view.ownHand[index] != id
                || !CanAct(view) || presentationSession?.IsPresentationBusy == true) return;
            PlayHand(index, id);
        }
        private void PlayHand(int index, string id)
        {
            var view = session.View;
            if (!CanAct(view) || view.ownHand == null || index < 0 || index >= view.ownHand.Length || view.ownHand[index] != id
                || !cards.TryGetValue(id, out var card)) return;
            Inspect(id);
            bool main = view.phase == "Main" || view.phase == "SecondMain";
            bool flash = view.phase == "FlashBeforeBlock" || view.phase == "FlashAfterBlock";
            if (string.Equals(card.type, "Magic", StringComparison.OrdinalIgnoreCase))
            {
                if (!main && !flash) return;
                var options = new List<KeyValuePair<string, Action>>();
                if (main && card.id == AbilityRegistry.DoubleDraw)
                    options.Add(Choice("Main: rút 2 lá", () => DispatchHand("playMagic", index, id)));
                if (card.id == AbilityRegistry.AwakenMagic)
                    options.Add(Choice("Không chuyển Core · rút 1 lá", () => DispatchHand("playMagic", index, id,
                        transfers: Array.Empty<CoreTransfer>())));
                foreach (var player in view.players)
                    foreach (var spirit in player.field ?? Array.Empty<SpiritView>())
                    {
                        if (!cards.TryGetValue(spirit.cardId, out var targetCard) || !targetCard.IsSpirit) continue;
                        if (card.id == AbilityRegistry.AwakenMagic && player.playerIndex != view.viewer) continue;
                        int target = spirit.instanceId;
                        options.Add(Choice((player.playerIndex == view.viewer ? "Mình: " : "Đối thủ: ") + spirit.cardId + " #" + target,
                            () => {
                                if (card.id == AbilityRegistry.AwakenMagic) ChooseAwakenMagic(index, target, card);
                                else DispatchHand("playMagic", index, id, target: target);
                            }));
                    }
                if (options.Count == 0) { MessageManager.Cast("Không có mục tiêu Spirit phù hợp."); return; }
                Select("Chọn mục tiêu Magic", options); return;
            }
            if (main) DispatchHand("summon", index, id);
        }
        private void ChooseAwakenMagic(int handIndex, int target, CardDefinition card)
        {
            var view = session.View;
            if (!CanAct(view)) return;
            var owner = view.players.First(player => player.playerIndex == view.viewer);
            int reduction = owner.field.Sum(spirit => cards.TryGetValue(spirit.cardId, out var item)
                && item.color == card.color ? item.symbols : 0);
            int cost = Math.Max(0, card.cost - Math.Min(reduction, card.reductionSymbols));
            int available = Math.Max(0, owner.reserveNormal - cost);
            bool hasSoul = owner.reserveSoul && cost <= owner.reserveNormal;
            var options = new List<KeyValuePair<string, Action>>();
            for (int n = 0; n <= Math.Min(3, available); n++)
            {
                int count = n;
                options.Add(Choice(count + " Core thường", () => DispatchHand("playMagic", handIndex, card.id, target: target,
                    transfers: new[] { new CoreTransfer { normal = count } })));
                if (hasSoul && count < 3)
                    options.Add(Choice(count + " Core thường + Soul Core", () => DispatchHand("playMagic", handIndex, card.id, target: target,
                        transfers: new[] { new CoreTransfer { normal = count, soul = true } })));
            }
            Select("Awaken · chuyển tối đa 3 Core rồi rút 1 lá", options);
        }
        private void Actions(FilteredMatchView view)
        {
            if (view == null) return;
            foreach (var name in new[] { "ButtonInfo", "ButtonTiming", "ButtonAttachedEffect", "ButtonLog", "ButtonSetting" }) BattleNativeWidgets.Active(root, "Buttons/" + name, false);
            if (presentationSession?.IsPresentationBusy == true) return;
            if (session is IBattleNativePreviewSession preview && preview.IsPreview)
            {
                SetButton("ButtonInfo", "TRIỆU HỒI", () => preview.PlayPreview("Summoned"));
                SetButton("ButtonTiming", "TẤN CÔNG", () => preview.PlayPreview("Attacked"));
                SetButton("ButtonAttachedEffect", "PHÒNG THỦ", () => preview.PlayPreview("Blocked"));
                SetButton("ButtonSetting", "CHIẾN THẮNG", () => preview.PlayPreview("GameWon"));
                SetButton("ButtonLog", "QUAY LẠI", () => { session.Disconnect(); BattleNativeNavigation.ReturnToMenu(); });
                return;
            }
            if (session is IBattleNativeReplaySession replay && replay.IsReplay)
            {
                SetButton("ButtonInfo", "BƯỚC TIẾP", () => replay.StepReplay(1));
                SetButton("ButtonTiming", "BƯỚC TRƯỚC", () => replay.StepReplay(-1));
                SetButton("ButtonLog", "QUAY LẠI", BattleNativeNavigation.ReturnToMenu);
                BattleNativeWidgets.Active(root, "Buttons/ButtonSetting", false);
                return;
            }
            if (view.winner >= 0)
            { SetButton("ButtonInfo", "QUAY LẠI", () => { session.Disconnect(); BattleNativeNavigation.ReturnToMenu(); }); return; }
            if (!session.CanSendCommands)
            { SetButton("ButtonInfo", "QUAY LẠI", BattleNativeNavigation.ReturnToMenu); return; }
            SetButton("ButtonSetting", "NHẬN THUA", () => Dispatch("concede"));
            if (view.decisionPlayer != view.viewer) return;
            switch (view.phase)
            {
                case "Opening":
                    SetButton("ButtonInfo", "GIỮ 4 LÁ", () => Dispatch("opening"));
                    SetButton("ButtonTiming", "ĐỔI 4 LÁ", () => session.Send(new WireMessage { type = "action", action = "opening", redraw = true }));
                    break;
                case "Main":
                    SetButton("ButtonInfo", "TẤN CÔNG", () => Dispatch("attackStep"), view.turn > 1);
                    SetButton("ButtonLog", view.rulesFormat == "Standard" && view.turn > 1 ? "MAIN 2" : "KẾT THÚC LƯỢT", () => Dispatch("endTurn"));
                    SetButton("ButtonAttachedEffect", "CHUYỂN CORE", CoreMenu);
                    break;
                case "SecondMain":
                    SetButton("ButtonLog", "KẾT THÚC LƯỢT", () => Dispatch("endTurn"));
                    SetButton("ButtonAttachedEffect", "CHUYỂN CORE", CoreMenu);
                    break;
                case "Attack":
                    SetButton("ButtonInfo", "TẤN CÔNG", () => Dispatch("attack", selectedSpirit), HasSelected());
                    SetButton("ButtonLog", view.rulesFormat == "Standard" ? "MAIN 2" : "KẾT THÚC LƯỢT", () => Dispatch("endTurn"));
                    break;
                case "Block":
                    SetButton("ButtonInfo", "CHẶN", () => Dispatch("block", selectedSpirit), HasSelected());
                    SetButton("ButtonTiming", "NHẬN SÁT THƯƠNG", () => Dispatch("takeLife"));
                    break;
                case "FlashBeforeBlock": case "FlashAfterBlock":
                    SetButton("ButtonTiming", "BỎ QUA FLASH", () => Dispatch("passFlash"));
                    SetButton("ButtonAttachedEffect", "THỨC TỈNH", AwakenMenu);
                    break;
            }
        }
        private bool HasSelected() => session.View?.players?.FirstOrDefault(player => player.playerIndex == session.View.viewer)?.field?.Any(spirit => spirit.instanceId == selectedSpirit && !spirit.exhausted
            && cards.TryGetValue(spirit.cardId, out var card) && card.IsSpirit) == true;
        private bool CanAct(FilteredMatchView view) => !ZoomVisible && view != null && view.winner < 0 && view.decisionPlayer == view.viewer
            && session.CanSendCommands && !(session is IBattleNativeReplaySession replay && replay.IsReplay);
        private void CoreMenu()
        {
            if (selectedSpirit < 0) { MessageManager.Cast("Chọn Spirit của bạn trước khi chuyển core."); return; }
            var owner = session.View.players.First(player => player.playerIndex == session.View.viewer);
            var selected = owner.field.FirstOrDefault(item => item.instanceId == selectedSpirit);
            if (selected == null) return;
            var options = new List<KeyValuePair<string, Action>> {
                Choice("Reserve → Spirit: 1 Core", () => Dispatch("moveCore", source: -1, target: selectedSpirit)),
                Choice("Spirit → Reserve: 1 Core", () => Dispatch("moveCore", source: selectedSpirit, target: -1)),
                Choice("Reserve → Spirit: Soul Core", () => Dispatch("moveCore", source: -1, target: selectedSpirit, soul: true)),
                Choice("Spirit → Reserve: Soul Core", () => Dispatch("moveCore", source: selectedSpirit, target: -1, soul: true)),
                Choice("Thức tỉnh", AwakenMenu)
            };
            void SwapOption(int target, int normal, bool soul, string label)
            {
                if (selected.soulCore && normal > 0)
                    options.Add(Choice("Đổi Soul Core với Core thường · " + label, () => Dispatch("swapCore", source: selectedSpirit, target: target, soul: true)));
                if (selected.normalCores > 0 && soul)
                    options.Add(Choice("Đổi Core thường với Soul Core · " + label, () => Dispatch("swapCore", source: selectedSpirit, target: target)));
            }
            SwapOption(-1, owner.reserveNormal, owner.reserveSoul, "Reserve");
            foreach (var target in owner.field.Where(item => item.instanceId != selectedSpirit))
                SwapOption(target.instanceId, target.normalCores, target.soulCore, target.cardId + " #" + target.instanceId);
            Select("Chuyển Core · Spirit #" + selectedSpirit, options);
        }
        private void AwakenMenu()
        {
            if (!CanAct(session.View)) return;
            var owner = session.View.players.First(player => player.playerIndex == session.View.viewer);
            var targetSpirit = owner.field.FirstOrDefault(item => item.instanceId == selectedSpirit);
            if (targetSpirit == null || !cards.TryGetValue(targetSpirit.cardId, out var targetCard) || !AbilityRegistry.HasAwaken(targetCard))
            { MessageManager.Cast("Chọn Spirit có Giác tỉnh trước."); return; }
            var options = new List<KeyValuePair<string, Action>>();
            var sources = owner.field.Where(item => item.instanceId != selectedSpirit
                && cards.TryGetValue(item.cardId, out var card) && card.IsSpirit).ToArray();
            foreach (var spirit in sources)
            {
                int source = spirit.instanceId;
                if (spirit.normalCores > 0) options.Add(Choice(spirit.cardId + " → #" + selectedSpirit + ": 1 Core", () => Dispatch("awaken", 1, source, selectedSpirit)));
                if (spirit.soulCore) options.Add(Choice(spirit.cardId + " → #" + selectedSpirit + ": Soul Core", () => Dispatch("awaken", 0, source, selectedSpirit, true)));
            }
            if (sources.Length > 0)
                options.Add(Choice("Chọn nhiều nguồn Core trong một lần", () => PickAwakenSources(selectedSpirit, sources, 0, new List<CoreTransfer>())));
            if (options.Count == 0) { MessageManager.Cast("Không có Core trên Spirit khác để Thức tỉnh."); return; }
            Select("Thức tỉnh", options);
        }
        private void PickAwakenSources(int target, SpiritView[] sources, int position, List<CoreTransfer> chosen)
        {
            if (!CanAct(session.View)) return;
            if (position >= sources.Length)
            {
                if (chosen.Count > 0) Dispatch("awaken", target: target, transfers: chosen.ToArray());
                return;
            }
            var source = sources[position];
            var choices = new List<KeyValuePair<string, Action>>();
            for (int n = 0; n <= source.normalCores; n++)
            {
                int count = n;
                void Next(bool soul)
                {
                    var next = new List<CoreTransfer>(chosen);
                    if (count > 0 || soul) next.Add(new CoreTransfer { source = source.instanceId, normal = count, soul = soul });
                    PickAwakenSources(target, sources, position + 1, next);
                }
                choices.Add(Choice(count + " Core thường", () => Next(false)));
                if (source.soulCore) choices.Add(Choice(count + " Core thường + Soul Core", () => Next(true)));
            }
            Select("Giác tỉnh · nguồn " + source.cardId + " #" + source.instanceId, choices);
        }
        private static KeyValuePair<string, Action> Choice(string text, Action action) => new KeyValuePair<string, Action>(text, action);
        private static void Select(string title, List<KeyValuePair<string, Action>> options)
        {
            var labels = new List<string> { title, string.Empty }; labels.AddRange(options.Select(option => option.Key));
            UIManager.ShowPopupSelection(labels, () => {
                var selected = EventSystem.current?.currentSelectedGameObject?.GetComponent<SelectionButton>()?.GetButtonText();
                foreach (var option in options) if (option.Key == selected) { option.Value(); break; }
            }, null);
        }
        private void SetButton(string name, string label, Action action, bool enabled = true)
        {
            var target = BattleNativeWidgets.Find(root, "Buttons/" + name); if (target == null) return;
            target.gameObject.SetActive(true); BattleNativeWidgets.Bind(root, "Buttons/" + name, () => { if (!ZoomVisible) action(); });
            foreach (var old in target.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (old.gameObject.name != "BS_ActionLabel") old.gameObject.SetActive(false);
            var text = BattleNativeWidgets.Component<TextMeshProUGUI>(target, "BS_ActionLabel")
                ?? BattleNativeWidgets.Text(target, "BS_ActionLabel", label, 16);
            text.gameObject.SetActive(true); text.text = label; text.richText = false;
            text.color = Color.white;
            // Reuse the native controls with enough room for Vietnamese action
            // names, rather than placing long labels inside the old tiny discs.
            float bottom = name == "ButtonAttachedEffect" ? .195f : name == "ButtonInfo" ? .148f
                : name == "ButtonTiming" ? .101f : name == "ButtonLog" ? .054f : .007f;
            Normalize(target as RectTransform, .862f, bottom, .993f, bottom + .043f);
            var face = target.GetComponent<Image>() ?? target.gameObject.AddComponent<Image>();
            face.raycastTarget = true;
            var button = target.GetComponent<Button>(); if (button != null) BattleTheme.StyleButton(button, face);
            BattleNativeWidgets.Interactable(root, "Buttons/" + name, enabled);
        }
        private void Dispatch(string action, int index = 0, int source = -1, int target = -1, bool soul = false, CoreTransfer[] transfers = null)
        {
            if (ZoomVisible || !session.CanSendCommands || (action != "concede" && !CanAct(session.View))) return;
            session.Send(new WireMessage { type = "action", action = action, index = index, source = source, target = target, soul = soul, transfers = transfers });
        }
        private void DispatchHand(string action, int index, string id, int target = -1, CoreTransfer[] transfers = null)
        {
            // A Magic chooser can stay open while another snapshot arrives.
            // Revalidate identity at dispatch, never play a replacement card at
            // the captured index. The authoritative engine still checks rules.
            var view = session.View;
            if (view?.ownHand == null || index < 0 || index >= view.ownHand.Length || view.ownHand[index] != id
                || presentationSession?.IsPresentationBusy == true) return;
            Dispatch(action, index, target: target, transfers: transfers);
        }
        private static string Phase(string value)
        {
            switch (value)
            {
                case "Opening": return "CHỌN BÀI MỞ ĐẦU";
                case "Main": return "MAIN STEP";
                case "SecondMain": return "SECOND MAIN STEP";
                case "Attack": return "ATTACK STEP";
                case "Block": return "CHỌN CHẶN";
                case "FlashBeforeBlock": case "FlashAfterBlock": return "FLASH TIMING";
                default: return value;
            }
        }
    }
}
