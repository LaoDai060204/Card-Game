using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LaoDai.BattleSpirits.Presentation
{
    // Receives only the filtered view. Opponent hands and deck order never enter
    // the presenter, even when the local player is hosting the match.
    internal sealed class BattleBoardPanel : IDisposable
    {
        private readonly Action<WireMessage> send;
        private readonly Action back;
        private readonly Func<string, CardDefinition> lookup;
        private readonly Action<string, RawImage> loadArt;
        private readonly RectTransform opponentField;
        private readonly RectTransform ownField;
        private readonly RectTransform hand;
        private readonly RectTransform controls;
        private readonly TextMeshProUGUI header;
        private readonly TextMeshProUGUI opponentStats;
        private readonly TextMeshProUGUI ownStats;
        private readonly TextMeshProUGUI decision;
        private readonly TextMeshProUGUI cardDetails;
        private readonly TextMeshProUGUI eventText;
        private readonly Dictionary<int, RectTransform> spiritRects = new Dictionary<int, RectTransform>();
        private FilteredMatchView current;
        private FilteredMatchView animatedView;
        private bool allowActions;
        private int selectedSpirit = -1;
        private bool disposed;
        private bool disconnected;

        internal RectTransform Root { get; }
        internal RawImage Background { get; }
        internal RawImage BackgroundRawImage => Background;
        internal event Action<PublicBattleEvent> BattleEventPlayed;

        internal BattleBoardPanel(Transform parent, Action<WireMessage> send, Action back,
            Func<string, CardDefinition> lookup, Action<string, RawImage> loadArt)
        {
            this.send = send ?? throw new ArgumentNullException(nameof(send));
            this.back = back ?? throw new ArgumentNullException(nameof(back));
            this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
            this.loadArt = loadArt ?? throw new ArgumentNullException(nameof(loadArt));
            Root = BattleUiTheme.Panel(parent, "BattleSpirits_Board", Vector2.zero, Vector2.one,
                new Color(0.005f, 0.02f, 0.04f, 0.28f));
            Background = NewRawImage(Root, "FieldBackground");
            BattleUiTheme.Stretch(Background.rectTransform);
            Background.raycastTarget = false;
            Background.color = Color.white;

            var top = BattleUiTheme.Panel(Root, "Header", new Vector2(0, 0.89f), Vector2.one,
                new Color(0.01f, 0.035f, 0.06f, 0.94f));
            var backButton = BattleUiTheme.Button(top, "‹  Quay lại", () => this.back());
            BattleUiTheme.SetRect((RectTransform)backButton.transform, new Vector2(0, 0), new Vector2(0.16f, 1), new Vector2(12, 12), new Vector2(-8, -12));
            header = BattleUiTheme.Text(top, "Turn", "BATTLE SPIRITS — ETERNAL", 29, TextAlignmentOptions.MidlineLeft);
            BattleUiTheme.SetRect(header.rectTransform, new Vector2(0.17f, 0), new Vector2(0.79f, 1), Vector2.zero, Vector2.zero);
            var surrender = BattleUiTheme.Button(top, "Đầu hàng", () => Dispatch("concede"));
            surrender.name = "Concede";
            BattleUiTheme.SetRect((RectTransform)surrender.transform, new Vector2(0.82f, 0), Vector2.one, new Vector2(5, 12), new Vector2(-12, -12));

            opponentStats = StatsBar("OpponentStats", 0.79f, 0.88f);
            ownStats = StatsBar("OwnStats", 0.21f, 0.285f);
            opponentField = HorizontalStrip("OpponentField", new Vector2(0.015f, 0.58f), new Vector2(0.79f, 0.785f));
            ownField = HorizontalStrip("OwnField", new Vector2(0.015f, 0.29f), new Vector2(0.79f, 0.495f));
            hand = HorizontalStrip("OwnHand", new Vector2(0.015f, 0.02f), new Vector2(0.79f, 0.205f));

            var sidebar = BattleUiTheme.Panel(Root, "DecisionPanel", new Vector2(0.805f, 0.015f), new Vector2(0.99f, 0.875f),
                new Color(0.01f, 0.04f, 0.07f, 0.94f));
            decision = BattleUiTheme.Text(sidebar, "Decision", string.Empty, 22, TextAlignmentOptions.TopLeft);
            BattleUiTheme.SetRect(decision.rectTransform, new Vector2(0, 0.86f), Vector2.one, new Vector2(12, 2), new Vector2(-12, -12));
            var actions = BattleUiTheme.Scroll(sidebar, "Actions", out controls);
            BattleUiTheme.SetRect((RectTransform)actions.transform, new Vector2(0, 0.48f), new Vector2(1, 0.855f), new Vector2(8, 2), new Vector2(-8, -2));
            cardDetails = BattleUiTheme.Text(sidebar, "CardDetails", "Chọn một Spirit để xem chỉ số hoặc điều chỉnh core.", 19);
            BattleUiTheme.SetRect(cardDetails.rectTransform, new Vector2(0, 0.12f), new Vector2(1, 0.475f), new Vector2(12, 4), new Vector2(-12, -6));
            eventText = BattleUiTheme.Text(sidebar, "Events", string.Empty, 17);
            eventText.color = BattleUiTheme.Muted;
            BattleUiTheme.SetRect(eventText.rectTransform, Vector2.zero, new Vector2(1, 0.115f), new Vector2(12, 8), new Vector2(-12, -2));
        }

        private TextMeshProUGUI StatsBar(string name, float bottom, float top)
        {
            var panel = BattleUiTheme.Panel(Root, name, new Vector2(0.015f, bottom), new Vector2(0.79f, top),
                new Color(0.01f, 0.04f, 0.065f, 0.9f));
            var text = BattleUiTheme.Text(panel, "Stats", string.Empty, 22, TextAlignmentOptions.MidlineLeft);
            BattleUiTheme.Stretch(text.rectTransform, 8f);
            return text;
        }

        private RectTransform HorizontalStrip(string name, Vector2 min, Vector2 max)
        {
            var root = BattleUiTheme.Panel(Root, name, min, max, new Color(0.01f, 0.035f, 0.06f, 0.32f));
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = BattleUiTheme.Panel(root, "Viewport", Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.01f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = BattleUiTheme.Rect(viewport, "Content");
            content.anchorMin = Vector2.zero;
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.scrollSensitivity = 32f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }

        internal void Render(FilteredMatchView view, bool allowActions = true)
        {
            if (disposed || view == null) return;
            current = view;
            this.allowActions = allowActions;
            bool newSnapshot = !ReferenceEquals(animatedView, view);
            if (view.players == null || view.players.Length != 2 || view.viewer < 0 || view.viewer > 1)
            {
                decision.text = "Đang chờ trạng thái trận đấu.";
                Clear(hand);
                Clear(ownField);
                Clear(opponentField);
                return;
            }
            var me = Player(view.viewer);
            var opponent = Player(1 - view.viewer);
            if (me == null || opponent == null) return;
            if (!(me.field ?? Array.Empty<SpiritView>()).Any(item => item.instanceId == selectedSpirit))
                selectedSpirit = -1;

            header.text = "BATTLE SPIRITS  •  Lượt " + view.turn + "  •  " + PhaseName(view.phase);
            ownStats.text = Stats(me, true);
            opponentStats.text = Stats(opponent, false);
            spiritRects.Clear();
            DrawField(opponentField, opponent, false);
            DrawField(ownField, me, true);
            DrawHand(view.ownHand ?? Array.Empty<string>());
            DrawActions();
            var concede = Root.Find("Header/Concede")?.GetComponent<Button>();
            if (concede != null) concede.interactable = allowActions && view.winner < 0;
            if (newSnapshot)
            {
                animatedView = view;
                AnimateEvents(view.events ?? Array.Empty<PublicBattleEvent>());
            }
        }

        private PublicPlayerView Player(int index)
        {
            return current.players.FirstOrDefault(player => player.playerIndex == index);
        }

        private static string Stats(PublicPlayerView player, bool own)
        {
            string soul = player.reserveSoul ? " + ◆ Soul" : string.Empty;
            string trashSoul = player.trashSoul ? " + ◆" : string.Empty;
            return "<b>" + (own ? "BẠN — " : "ĐỐI THỦ — ") + Escape(player.name) + "</b>"
                + "   |   Life " + player.life + "   |   Deck " + player.deckCount + "   |   Tay " + player.handCount
                + "\nReserve " + player.reserveNormal + soul + "   |   Trash " + player.trashNormal + trashSoul;
        }

        private void DrawField(RectTransform container, PublicPlayerView player, bool own)
        {
            Clear(container);
            var field = player.field ?? Array.Empty<SpiritView>();
            float width = 142f;
            container.sizeDelta = new Vector2(Mathf.Max(1, field.Length) * width + 12f, 0);
            for (int i = 0; i < field.Length; i++)
            {
                var spirit = field[i];
                var card = lookup(spirit.cardId);
                var button = CardButton(container, spirit.cardId, card, i * width + 6f, width - 10f,
                    () => SelectSpirit(spirit, own));
                var rect = (RectTransform)button.transform;
                spiritRects[spirit.instanceId] = rect;
                if (spirit.exhausted) rect.localRotation = Quaternion.Euler(0, 0, -8f);
                if (spirit.instanceId == selectedSpirit)
                {
                    var outline = button.gameObject.AddComponent<Outline>();
                    outline.effectColor = BattleUiTheme.Accent;
                    outline.effectDistance = new Vector2(3, -3);
                }
                string cores = spirit.normalCores + (spirit.soulCore ? "+◆" : string.Empty);
                var label = BattleUiTheme.Text(rect, "Stats", "Lv" + spirit.level + "  BP " + spirit.bp + "\nCore " + cores
                    + (spirit.exhausted ? " • Mệt" : " • Hồi phục"), 16, TextAlignmentOptions.BottomLeft);
                BattleUiTheme.SetRect(label.rectTransform, Vector2.zero, new Vector2(1, 0.27f), new Vector2(5, 2), new Vector2(-4, -2));
                var raw = rect.GetComponentInChildren<RawImage>();
                if (raw != null && spirit.exhausted) raw.color = new Color(0.65f, 0.7f, 0.76f, 1f);
            }
            if (field.Length == 0)
            {
                var empty = BattleUiTheme.Text(container, "Empty", own ? "Sân của bạn — triệu hồi Spirit từ bài trên tay" : "Sân đối thủ", 20, TextAlignmentOptions.MidlineLeft);
                BattleUiTheme.SetRect(empty.rectTransform, Vector2.zero, new Vector2(0, 1), new Vector2(12, 0), new Vector2(680, 0));
                empty.color = BattleUiTheme.Muted;
            }
        }

        private void DrawHand(string[] cards)
        {
            Clear(hand);
            const float width = 150f;
            hand.sizeDelta = new Vector2(Mathf.Max(1, cards.Length) * width + 12f, 0);
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                string id = cards[index];
                var card = lookup(id);
                var button = CardButton(hand, id, card, i * width + 6f, width - 10f, () =>
                {
                    Inspect(card);
                    if (CanDecide("Main") && current.activePlayer == current.viewer)
                        Dispatch("summon", index);
                });
                button.interactable = allowActions;
                var text = BattleUiTheme.Text(button.transform, "HandName", Escape(card?.DisplayName ?? id)
                    + "\nCost " + (card?.cost.ToString() ?? "?") + "  ↓" + (card?.reductionSymbols.ToString() ?? "?"), 17, TextAlignmentOptions.BottomLeft);
                BattleUiTheme.SetRect(text.rectTransform, Vector2.zero, new Vector2(1, 0.27f), new Vector2(5, 1), new Vector2(-5, -2));
            }
        }

        private Button CardButton(RectTransform container, string id, CardDefinition card, float x, float width, Action action)
        {
            var button = BattleUiTheme.Button(container, string.Empty, () => action());
            var rect = (RectTransform)button.transform;
            BattleUiTheme.SetRect(rect, Vector2.zero, new Vector2(0, 1), new Vector2(x, 5), new Vector2(x + width, -5));
            var artworkFrame = BattleUiTheme.Panel(rect, "ArtworkFrame", new Vector2(0, 0.27f), Vector2.one, Color.clear);
            BattleUiTheme.SetRect(artworkFrame, new Vector2(0, 0.27f), Vector2.one, new Vector2(3, 2), new Vector2(-3, -3));
            artworkFrame.GetComponent<Image>().raycastTarget = false;
            var artwork = NewRawImage(artworkFrame, "Artwork");
            BattleUiTheme.Stretch(artwork.rectTransform);
            var aspect = artwork.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 0.7142857f;
            artwork.raycastTarget = false;
            artwork.color = Color.white;
            loadArt(id, artwork);
            var events = button.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => Inspect(card));
            events.triggers.Add(enter);
            return button;
        }

        private static RawImage NewRawImage(Transform parent, string name)
        {
            var image = BattleUiTheme.Rect(parent, name, typeof(RawImage)).GetComponent<RawImage>();
            image.texture = Texture2D.blackTexture;
            return image;
        }

        private void SelectSpirit(SpiritView spirit, bool own)
        {
            Inspect(lookup(spirit.cardId));
            if (!own || !allowActions) return;
            selectedSpirit = spirit.instanceId;
            Render(current, allowActions);
        }

        private void Inspect(CardDefinition card)
        {
            if (card == null) return;
            string levels = card.levels == null ? string.Empty : string.Join("  ", card.levels.Select((level, index) =>
                "Lv" + (index + 1) + ": " + level.cores + " core / " + level.bp + " BP"));
            cardDetails.text = "<b>" + Escape(card.DisplayName) + "</b>\n" + Escape(card.id)
                + " • " + ColorName(card.color) + " • " + Escape(card.type)
                + "\nCost " + card.cost + "  ↓" + card.reductionSymbols + "  Symbol " + card.symbols
                + "\n" + levels + "\n" + Escape(card.effectTextJa);
        }

        private bool CanDecide(string phase)
        {
            return allowActions && current != null && current.winner < 0
                && current.viewer == current.decisionPlayer && current.phase == phase;
        }

        private void DrawActions()
        {
            Clear(controls);
            if (disconnected)
            {
                decision.text = "KẾT NỐI ĐÃ NGẮT\nQuay lại để tạo hoặc tham gia phòng mới.";
                AddAction("Quay lại", () => back(), true);
                return;
            }
            if (current.winner >= 0)
            {
                decision.text = current.winner == current.viewer ? "<color=#94F2AE>BẠN CHIẾN THẮNG</color>" : "TRẬN ĐẤU KẾT THÚC";
                AddAction("Quay lại", () => back(), true);
                return;
            }
            if (!allowActions)
            {
                decision.text = "ĐANG XEM LẠI TRẬN ĐẤU";
                return;
            }
            if (current.decisionPlayer != current.viewer)
            {
                var waiting = Player(current.decisionPlayer);
                decision.text = "Đang chờ " + Escape(waiting?.name ?? "đối thủ") + "…\n" + PhaseName(current.phase);
                return;
            }
            decision.text = "ĐẾN LƯỢT BẠN CHỌN\n" + PhaseName(current.phase);
            switch (current.phase)
            {
                case "Opening":
                    AddAction("Giữ 4 lá", () => Dispatch("opening"));
                    AddAction("Đổi cả 4 lá", () => Dispatch("opening", redraw: true));
                    break;
                case "Main":
                    decision.text += "\nBấm bài trên tay để triệu hồi.";
                    AddAction("Sang bước tấn công", () => Dispatch("attackStep"), current.turn > 1);
                    AddAction("Kết thúc lượt", () => Dispatch("endTurn"));
                    AddCoreActions();
                    break;
                case "Attack":
                    AddAction("Tấn công bằng Spirit đã chọn", () => Dispatch("attack", selectedSpirit), SelectedSpiritCanBattle());
                    AddAction("Kết thúc lượt", () => Dispatch("endTurn"));
                    break;
                case "FlashBeforeBlock":
                case "FlashAfterBlock":
                    AddAction("Bỏ qua Flash", () => Dispatch("passFlash"));
                    break;
                case "Block":
                    AddAction("Chặn bằng Spirit đã chọn", () => Dispatch("block", selectedSpirit), SelectedSpiritCanBattle());
                    AddAction("Không chặn — nhận sát thương", () => Dispatch("takeLife"));
                    break;
            }
        }

        private bool SelectedSpiritCanBattle()
        {
            var selected = Player(current.viewer)?.field?.FirstOrDefault(item => item.instanceId == selectedSpirit);
            return selected != null && !selected.exhausted;
        }

        private void AddCoreActions()
        {
            var spirit = Player(current.viewer)?.field?.FirstOrDefault(item => item.instanceId == selectedSpirit);
            if (spirit == null)
            {
                var hint = BattleUiTheme.Text(controls, "CoreHint", "Chọn Spirit trên sân để chuyển core / đổi Level.", 18);
                var layout = hint.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = 58f;
                return;
            }
            var player = Player(current.viewer);
            AddAction("+ 1 core từ Reserve", () => MoveCore(-1, selectedSpirit, false), player != null && player.reserveNormal > 0);
            AddAction("− 1 core về Reserve", () => MoveCore(selectedSpirit, -1, false), spirit.normalCores > 0);
            AddAction("◆ Soul Core → Spirit", () => MoveCore(-1, selectedSpirit, true), player != null && player.reserveSoul && !spirit.soulCore);
            AddAction("◆ Soul Core → Reserve", () => MoveCore(selectedSpirit, -1, true), spirit.soulCore);
        }

        private void MoveCore(int source, int target, bool soul)
        {
            if (!CanDecide("Main")) return;
            send(new WireMessage { type = "action", action = "moveCore", source = source, target = target, soul = soul });
        }

        private void AddAction(string text, Action action, bool enabled = true)
        {
            var button = BattleUiTheme.Button(controls, text, () => action());
            button.interactable = enabled;
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.fontSize = 18;
        }

        private void Dispatch(string action, int index = 0, bool redraw = false)
        {
            if (!allowActions || current == null || current.winner >= 0) return;
            if (action != "concede" && current.decisionPlayer != current.viewer) return;
            send(new WireMessage { type = "action", action = action, index = index, redraw = redraw });
        }

        private void AnimateEvents(PublicBattleEvent[] events)
        {
            if (events.Length == 0) return;
            var descriptions = new List<string>();
            foreach (var item in events)
            {
                if (spiritRects.TryGetValue(item.spiritId, out var spirit))
                {
                    if (item.kind == "Attacked")
                        spirit.DOPunchPosition(new Vector3(0, item.actor == current.viewer ? 18f : -18f, 0), 0.28f, 5, 0.4f);
                    else if (item.kind == "Summoned" || item.kind == "CoreMoved" || item.kind == "Blocked")
                        spirit.DOPunchScale(new Vector3(0.08f, 0.08f, 0), 0.28f, 5, 0.4f);
                }
                string text = EventName(item);
                if (!string.IsNullOrEmpty(text)) descriptions.Add(text);
                BattleEventPlayed?.Invoke(item);
            }
            eventText.text = string.Join("\n", descriptions.TakeLast(3));
        }

        private static string EventName(PublicBattleEvent item)
        {
            switch (item.kind)
            {
                case "Summoned": return "Spirit đã được triệu hồi.";
                case "Attacked": return "Spirit bắt đầu tấn công.";
                case "Blocked": return "Đòn tấn công đã bị chặn.";
                case "CoreMoved": return "Đã chuyển core.";
                case "LifeLost": return "Life giảm " + item.amount + ".";
                case "Destroyed": return "Spirit bị phá hủy.";
                case "Vanished": return "Spirit tiêu biến.";
                case "TurnStarted": return "Bắt đầu lượt mới.";
                case "GameWon": return "Trận đấu kết thúc.";
                default: return string.Empty;
            }
        }

        private static string PhaseName(string phase)
        {
            switch (phase)
            {
                case "Opening": return "CHUẨN BỊ";
                case "Main": return "MAIN STEP";
                case "Attack": return "ATTACK STEP";
                case "FlashBeforeBlock": return "FLASH TRƯỚC KHI CHẶN";
                case "Block": return "CHỌN CHẶN";
                case "FlashAfterBlock": return "FLASH SAU KHI CHẶN";
                case "GameOver": return "KẾT THÚC";
                default: return phase ?? string.Empty;
            }
        }

        private static string ColorName(CardColor color)
        {
            switch (color)
            {
                case CardColor.Red: return "Đỏ";
                case CardColor.Purple: return "Tím";
                case CardColor.Green: return "Lục";
                case CardColor.White: return "Trắng";
                case CardColor.Yellow: return "Vàng";
                case CardColor.Blue: return "Lam";
                default: return color.ToString();
            }
        }

        private static string Escape(string text)
        {
            return (text ?? string.Empty).Replace("<", "‹").Replace(">", "›");
        }

        private static void Clear(RectTransform transform)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                child.gameObject.SetActive(false);
                child.DOKill();
                child.SetParent(null, false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        internal void SetDisconnected()
        {
            if (disposed) return;
            disconnected = true;
            allowActions = false;
            foreach (var button in hand.GetComponentsInChildren<Button>(true)) button.interactable = false;
            foreach (var button in ownField.GetComponentsInChildren<Button>(true)) button.interactable = false;
            var concede = Root.Find("Header/Concede")?.GetComponent<Button>();
            if (concede != null) concede.interactable = false;
            DrawActions();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (Root == null) return;
            Root.gameObject.SetActive(false);
            foreach (var transform in Root.GetComponentsInChildren<Transform>(true)) transform.DOKill();
            UnityEngine.Object.Destroy(Root.gameObject);
        }
    }
}
