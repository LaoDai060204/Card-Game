using System;
using System.Collections.Generic;
using LaoDai.BattleSpirits.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LaoDai.BattleSpirits.Presentation
{
    public sealed class ArenaHud : MonoBehaviour
    {
        private delegate bool MatchAction(out string error);
        private ArenaBootstrap owner;
        private BattleMatch match;
        private VisualElement root;
        private VisualElement content;
        private VisualElement modal;
        private PanelSettings panel;
        private int selectedSpirit = -1;
        private string inspectedCard;
        private string message = "Chọn giữ tay bài hoặc đổi bài một lần để bắt đầu.";
        private bool failed;
        private bool executing;
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        public void Initialize(ArenaBootstrap bootstrap)
        {
            owner = bootstrap;
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "Battle Spirits Runtime Panel";
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/DefaultRuntimeTheme");
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.sortingOrder = 10;
            root = document.rootVisualElement;
            root.name = "arena-root";
            var styles = Resources.Load<StyleSheet>("UI/Arena");
            if (styles != null) root.styleSheets.Add(styles);
            var font = Resources.Load<Font>("UI/Fonts/NotoSansJP");
            if (font != null)
                root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));
            root.AddToClassList("root");
            content = new VisualElement();
            content.AddToClassList("content");
            root.Add(content);
        }

        public void Bind(BattleMatch value)
        {
            match = value;
            selectedSpirit = -1;
            inspectedCard = null;
            message = "Đấu trường sẵn sàng. Hai người chơi lần lượt xác nhận tay bài.";
            failed = false;
            Refresh();
        }

        public void Refresh()
        {
            if (match == null || root == null) return;
            content.Clear();
            if (selectedSpirit >= 0 && match.FindSpirit(selectedSpirit) == null)
                selectedSpirit = -1;
            BuildHeader();
            var body = Element(content, "body");
            BuildLeft(body);
            var center = Element(body, "arena-window");
            center.pickingMode = PickingMode.Ignore;
            var phase = Label(center, PhaseTitle(), "phase-title");
            phase.pickingMode = PickingMode.Ignore;
            Label(center, "ETERNAL · TRẬN ĐẤU CỤC BỘ", "eyebrow").pickingMode = PickingMode.Ignore;
            var spacer = Element(center, "spacer");
            spacer.pickingMode = PickingMode.Ignore;
            if (match.State.PendingAttackerId >= 0)
            {
                var attacker = match.FindSpirit(match.State.PendingAttackerId);
                Label(center, attacker == null ? "Đang phân xử trận đánh" : $"{Name(attacker.Card)} đang tấn công", "battle-caption");
            }
            BuildRight(body);
            BuildHand();
            var status = Label(content, message, "status");
            if (failed) status.AddToClassList("error");
        }

        private void BuildHeader()
        {
            var header = Element(content, "header");
            var brand = Element(header, "brand");
            Label(brand, "BATTLE SPIRITS", "brand-title");
            Label(brand, "ETERNAL  /  BẢN THỬ NGHIỆM", "eyebrow");
            var turn = Element(header, "turn-box");
            Label(turn, match.State.Phase == MatchPhase.Opening ? "CHUẨN BỊ" : $"LƯỢT {match.State.TurnNumber}", "turn-label");
            Label(turn, $"Người chơi {match.State.ActivePlayerIndex + 1}", "turn-name");
            var actions = Element(header, "header-actions");
            AddButton(actions, "Hướng dẫn", OpenHelp, "subtle");
            AddButton(actions, "Trận mới", ConfirmRestart, "subtle");
        }

        private void BuildLeft(VisualElement body)
        {
            var side = Element(body, "sidebar left");
            Label(side, "HAI PHÍA ĐẤU TRƯỜNG", "section-label");
            BuildPlayerSummary(side, 1);
            BuildPlayerSummary(side, 0);
            Label(side, "THẺ ĐANG XEM", "section-label");
            CardDefinition card = null;
            if (selectedSpirit >= 0) card = match.FindSpirit(selectedSpirit)?.Card;
            if (card == null && !string.IsNullOrEmpty(inspectedCard)) card = match.GetCard(inspectedCard);
            if (card == null)
            {
                Label(side, "Chọn một thẻ trên tay hoặc một Spirit trên sân để xem chi tiết.", "muted wrap");
                return;
            }
            var texture = Texture(card.id);
            if (texture != null)
            {
                var image = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("detail-art");
                side.Add(image);
            }
            Label(side, Name(card), "card-title");
            Label(side, card.id + "  ·  " + ColorName(card.color), "muted");
            Label(side, $"Cost {card.cost}  ·  Giảm {card.reductionSymbols}  ·  Symbol {card.symbols}", "wrap");
            for (var i = 0; i < card.levels.Count; i++)
                Label(side, $"Lv{i + 1}    {card.levels[i].cores} core    /    {card.levels[i].bp:N0} BP", "level-row");
            Label(side, "Spirit thường · không có hiệu ứng riêng", "muted wrap");
            if (!string.IsNullOrEmpty(card.sourceUrl))
                AddButton(side, "Xem thẻ tại nguồn chính thức ↗", () => Application.OpenURL(card.sourceUrl), "source-link");
        }

        private void BuildPlayerSummary(VisualElement parent, int playerIndex)
        {
            var player = match.State.Players[playerIndex];
            var summary = Element(parent, "player-summary");
            if (match.State.ActivePlayerIndex == playerIndex) summary.AddToClassList("active-player");
            var line = Element(summary, "row");
            Label(line, $"NGƯỜI CHƠI {playerIndex + 1}", "player-name");
            Label(line, $"♥ {player.Life}", "life");
            Label(summary, $"Reserve   {player.Reserve.Normal} core" + (player.Reserve.Soul ? " + 1 Soul" : ""), "core-count");
            Label(summary, $"Trash        {player.Trash.Normal} core" + (player.Trash.Soul ? " + 1 Soul" : ""), "muted");
            Label(summary, $"Bộ bài {player.Deck.Count}   ·   Tay {player.Hand.Count}   ·   Sân {player.Field.Count}", "muted");
        }

        private void BuildRight(VisualElement body)
        {
            var side = Element(body, "sidebar right");
            var playerIndex = match.DecisionPlayerIndex;
            var player = match.State.Players[playerIndex];
            Label(side, $"NGƯỜI CHƠI {playerIndex + 1} · HÀNH ĐỘNG", "section-label");
            var controls = Element(side, "controls");
            switch (match.State.Phase)
            {
                case MatchPhase.Opening:
                    Label(controls, "Có thể đổi toàn bộ tay bài một lần. Sau khi xác nhận, chuyển máy cho người tiếp theo.", "muted wrap");
                    AddButton(controls, "Giữ tay bài", () => Execute((out string error) => match.TryConfirmOpening(playerIndex, false, out error)), "primary");
                    AddButton(controls, "Đổi tay bài", () => Execute((out string error) => match.TryConfirmOpening(playerIndex, true, out error)));
                    break;
                case MatchPhase.Main:
                    Label(controls, "Triệu hồi trả core từ Reserve rồi từ Spirit trên sân. Spirit thiếu core duy trì sẽ tiêu biến. Chọn Spirit để chuyển core.", "muted wrap");
                    var attackStep = AddButton(controls, "Vào Attack Step", () => Execute((out string error) => match.TryBeginAttackStep(playerIndex, out error)), "primary");
                    attackStep.SetEnabled(match.State.TurnNumber > 1);
                    if (match.State.TurnNumber == 1) Label(controls, "Người đi trước bỏ Attack Step ở lượt đầu.", "tiny muted wrap");
                    AddButton(controls, "Kết thúc lượt", () => Execute((out string error) => match.TryEndTurn(playerIndex, out error)));
                    break;
                case MatchPhase.Attack:
                    Label(controls, "Chọn một Spirit đang sẵn sàng để tấn công. Spirit vừa triệu hồi cũng có thể tấn công.", "muted wrap");
                    AddButton(controls, "Kết thúc lượt", () => Execute((out string error) => match.TryEndTurn(playerIndex, out error)), "primary");
                    break;
                case MatchPhase.FlashBeforeBlock:
                case MatchPhase.FlashAfterBlock:
                    Label(controls, "Flash: phía phòng thủ được ưu tiên trước. Bộ bài mẫu không có hiệu ứng Flash; hai bên lần lượt bỏ qua.", "muted wrap");
                    AddButton(controls, "Bỏ qua Flash", () => Execute((out string error) => match.TryPassFlash(playerIndex, out error)), "primary");
                    break;
                case MatchPhase.Block:
                    Label(controls, "Chọn một Spirit đang sẵn sàng để block, hoặc nhận sát thương vào Life.", "muted wrap");
                    AddButton(controls, "Nhận sát thương vào Life", () => Execute((out string error) => match.TryTakeLife(playerIndex, out error)), "danger");
                    break;
                case MatchPhase.GameOver:
                    Label(controls, $"NGƯỜI CHƠI {match.State.WinnerIndex + 1} CHIẾN THẮNG", "victory wrap");
                    AddButton(controls, "Chơi trận mới", owner.NewMatch, "primary");
                    break;
            }

            Label(side, $"SPIRIT CỦA NGƯỜI CHƠI {playerIndex + 1}", "section-label");
            var field = new ScrollView(ScrollViewMode.Vertical);
            field.AddToClassList("field-list");
            side.Add(field);
            if (player.Field.Count == 0) Label(field, "Chưa có Spirit trên sân.", "muted wrap");
            foreach (var spirit in player.Field)
            {
                var captured = spirit;
                var unit = Element(field, "unit-row");
                if (selectedSpirit == spirit.InstanceId) unit.AddToClassList("selected-unit");
                AddButton(unit, $"{Name(spirit.Card)}\nLv{spirit.Level} · {spirit.Bp:N0} BP · {spirit.Cores.Total} core" + (spirit.Cores.Soul ? " ◈" : "") + (spirit.Exhausted ? " · Mệt" : " · Sẵn sàng"), () => Select(captured), "unit-select");
                if (match.State.Phase == MatchPhase.Attack)
                {
                    var attack = AddButton(unit, "Tấn công", () => Execute((out string error) => match.TryAttack(playerIndex, captured.InstanceId, out error)), "small");
                    attack.SetEnabled(!spirit.Exhausted);
                }
                if (match.State.Phase == MatchPhase.Block)
                {
                    var block = AddButton(unit, "Block", () => Execute((out string error) => match.TryBlock(playerIndex, captured.InstanceId, out error)), "small");
                    block.SetEnabled(!spirit.Exhausted);
                }
            }
            var selected = selectedSpirit >= 0 ? match.FindSpirit(selectedSpirit) : null;
            if (match.State.Phase == MatchPhase.Main && selected != null && player.Field.Contains(selected))
            {
                Label(side, "CHUYỂN CORE · SPIRIT ĐANG CHỌN", "section-label");
                var row = Element(side, "row");
                var add = AddButton(row, "+ Core", () => Execute((out string error) => match.TryMoveCore(playerIndex, -1, selected.InstanceId, false, out error)), "small");
                add.SetEnabled(player.Reserve.Normal > 0);
                var remove = AddButton(row, "− Core", () => Execute((out string error) => match.TryMoveCore(playerIndex, selected.InstanceId, -1, false, out error)), "small");
                remove.SetEnabled(selected.Cores.Normal > 0);
                var soulRow = Element(side, "row");
                var soulAdd = AddButton(soulRow, "Đưa Soul vào", () => Execute((out string error) => match.TryMoveCore(playerIndex, -1, selected.InstanceId, true, out error)), "small");
                soulAdd.SetEnabled(player.Reserve.Soul);
                var soulRemove = AddButton(soulRow, "Trả Soul", () => Execute((out string error) => match.TryMoveCore(playerIndex, selected.InstanceId, -1, true, out error)), "small");
                soulRemove.SetEnabled(selected.Cores.Soul);
                Label(side, "Thiếu core Lv1 khiến Spirit tiêu biến. Core trả cost nằm ở Trash đến Refresh của bạn.", "tiny muted wrap");
            }
            Label(side, "NHẬT KÝ TRẬN ĐẤU", "section-label");
            var log = new ScrollView(ScrollViewMode.Vertical);
            log.AddToClassList("match-log");
            side.Add(log);
            for (var i = Mathf.Max(0, match.Log.Count - 5); i < match.Log.Count; i++)
                Label(log, match.Log[i], "log-line wrap");
        }

        private void BuildHand()
        {
            var playerIndex = match.DecisionPlayerIndex;
            var hand = match.State.Players[playerIndex].Hand;
            var region = Element(content, "hand-region");
            var title = Element(region, "row");
            Label(title, $"TAY BÀI · NGƯỜI CHƠI {playerIndex + 1}", "section-label");
            Label(title, "Chọn thẻ để xem · Triệu hồi trong Main Step", "muted");
            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.AddToClassList("hand-scroll");
            region.Add(scroll);
            for (var i = 0; i < hand.Count; i++)
            {
                var handIndex = i;
                var card = match.GetCard(hand[i]);
                var wrapper = Element(scroll, "hand-card");
                if (inspectedCard == card.id) wrapper.AddToClassList("inspected");
                var inspect = new Button(() => { inspectedCard = card.id; selectedSpirit = -1; owner.SelectSpirit(-1); Refresh(); });
                inspect.AddToClassList("card-inspect");
                wrapper.Add(inspect);
                var image = new Image { image = Texture(card.id), scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("hand-art");
                inspect.Add(image);
                Label(inspect, Name(card), "hand-name");
                Label(inspect, $"{ColorName(card.color)} · Cost {card.cost}", "tiny muted");
                if (match.State.Phase == MatchPhase.Main && playerIndex == match.State.ActivePlayerIndex)
                {
                    var cost = match.GetSummonCost(playerIndex, card);
                    var summon = AddButton(wrapper, $"Triệu hồi · {cost} core", () => Execute((out string error) => match.TrySummon(playerIndex, handIndex, out error)), "summon");
                    var availableCores = match.State.Players[playerIndex].Reserve.Total;
                    foreach (var fieldSpirit in match.State.Players[playerIndex].Field)
                        availableCores += fieldSpirit.Cores.Total;
                    summon.SetEnabled(availableCores >= cost + card.levels[0].cores);
                }
            }
            if (hand.Count == 0) Label(scroll, "Không còn thẻ trên tay.", "muted");
        }

        private void Select(SpiritState spirit)
        {
            selectedSpirit = spirit.InstanceId;
            inspectedCard = spirit.Card.id;
            owner.SelectSpirit(spirit.InstanceId);
            Refresh();
        }

        private void Execute(MatchAction action)
        {
            if (executing) return;
            executing = true;
            try
            {
                failed = !action(out var error);
                message = failed ? error : "Hành động đã được xác nhận.";
            }
            catch (Exception exception)
            {
                failed = true;
                message = "Không thể thực hiện hành động: " + exception.Message;
                Debug.LogException(exception);
            }
            finally
            {
                executing = false;
                owner.Refresh();
            }
        }

        private void OpenHelp()
        {
            ShowModal("CÁCH CHƠI BẢN MẪU", "Hai người chơi thay phiên trên cùng máy. Mỗi bên có 5 Life, 40 thẻ và 3 core thường + 1 Soul Core ở Reserve.\n\nMain: triệu hồi Spirit, trả cost sau giảm symbol và đặt core Lv1. Chọn Spirit để chuyển core và tăng cấp. Attack: chọn Spirit sẵn sàng, hai bên bỏ qua Flash, rồi phòng thủ chọn block hoặc nhận Life. Block dẫn đến Flash lần hai; BP bằng nhau khiến cả hai Spirit bị phá hủy.\n\nNgười đi trước bỏ Core Step và Attack Step ở lượt đầu, vẫn rút bài. Core trả cost chỉ trở về Reserve ở Refresh. Life về 0 hoặc bộ bài trống tại Start Step của bạn sẽ thua.\n\nBản thử nghiệm dùng Spirit thường theo luật Eternal Nhật hiện hành. Magic, Nexus, Brave, Contract, Burst và hiệu ứng riêng của thẻ chưa được hỗ trợ. Mô hình rồng minh họa là tài sản 3D tự tạo, dùng chung cho các Spirit.\n\nNguồn thẻ: battlespirits.com. Phần giao diện và cách tổ chức được phát triển từ kinh nghiệm template MDPro3; lõi luật Battle Spirits được viết riêng.", null);
        }

        private void ConfirmRestart()
        {
            ShowModal("BẮT ĐẦU TRẬN MỚI?", "Trận đang chơi sẽ kết thúc. Hai bên nhận một bộ bài đã xáo mới.", () => { CloseModal(); owner.NewMatch(); });
        }

        private void ShowModal(string title, string text, Action confirm)
        {
            CloseModal();
            modal = Element(root, "modal-shade");
            var dialog = Element(modal, "modal-dialog");
            Label(dialog, title, "card-title");
            Label(dialog, text, "modal-text wrap");
            var row = Element(dialog, "row");
            if (confirm != null) AddButton(row, "Bắt đầu", confirm, "primary");
            AddButton(row, confirm != null ? "Tiếp tục trận hiện tại" : "Đã hiểu", CloseModal);
        }

        private void CloseModal()
        {
            if (modal != null) modal.RemoveFromHierarchy();
            modal = null;
        }

        public void ShowFatal(string title, string detail)
        {
            if (content == null) return;
            content.Clear();
            var dialog = Element(content, "fatal-panel");
            Label(dialog, title, "card-title");
            Label(dialog, detail, "wrap");
            Label(dialog, "Mở README của dự án để kiểm tra dữ liệu thẻ và phiên bản Unity.", "muted wrap");
        }

        private string PhaseTitle()
        {
            switch (match.State.Phase)
            {
                case MatchPhase.Opening: return "CHUẨN BỊ TAY BÀI";
                case MatchPhase.Main: return "MAIN STEP";
                case MatchPhase.Attack: return "ATTACK STEP";
                case MatchPhase.FlashBeforeBlock: return "FLASH · TRƯỚC BLOCK";
                case MatchPhase.Block: return "CHỌN PHÒNG THỦ";
                case MatchPhase.FlashAfterBlock: return "FLASH · SAU BLOCK";
                case MatchPhase.GameOver: return "TRẬN ĐẤU KẾT THÚC";
                default: return string.Empty;
            }
        }

        private Texture2D Texture(string id)
        {
            if (!textures.TryGetValue(id, out var texture))
            {
                texture = Resources.Load<Texture2D>("CardArt/" + id);
                textures[id] = texture;
            }
            return texture;
        }

        private static string Name(CardDefinition card) => string.IsNullOrEmpty(card.nameVi) ? card.nameJa : card.nameVi;
        private static string ColorName(CardColor color)
        {
            var index = (int)color;
            var names = new[] { "Đỏ", "Tím", "Xanh lá", "Trắng", "Vàng", "Xanh dương" };
            return index >= 0 && index < names.Length ? names[index] : "Không màu";
        }
        private static VisualElement Element(VisualElement parent, string classes)
        {
            var element = new VisualElement();
            foreach (var name in classes.Split(' ')) element.AddToClassList(name);
            parent.Add(element);
            return element;
        }
        private static Label Label(VisualElement parent, string text, string classes = null)
        {
            var label = new Label(text);
            if (classes != null) foreach (var name in classes.Split(' ')) label.AddToClassList(name);
            parent.Add(label);
            return label;
        }
        private static Button AddButton(VisualElement parent, string text, Action clicked, string classes = null)
        {
            var button = new Button(clicked) { text = text };
            button.AddToClassList("action-button");
            if (classes != null) foreach (var name in classes.Split(' ')) button.AddToClassList(name);
            parent.Add(button);
            return button;
        }
        private void OnDestroy()
        {
            if (panel != null) Destroy(panel);
        }
    }
}
