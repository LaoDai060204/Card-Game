using System;
using System.Collections.Generic;
using System.Linq;
using LaoDai.BattleSpirits.Core;
using MDPro3;
using MDPro3.UI;
using MDPro3.UI.ServantUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LaoDai.BattleSpirits.NativeUI
{
    // This controller replaces the old root controller ON the original prefab.
    // It never creates another Canvas or hides the original form beneath an overlay.
    public sealed class BattleNativeScreenUI : ServantUI
    {
        public BattleNativeScreen screen;
        private IBattleNativeSession session;
        private BattleNativeDeckView deck;
        private BattleNativeDuelView duel;
        private TextMeshProUGUI status;
        private DeckDefinition opponent;
        private string soloDeckSet;
        private TMP_InputField onlineName, onlineHost, onlinePort, hostPort;

        public override void Initialize(MDPro3.Servant.Servant servant)
        {
            base.Initialize(servant);
            session = BattleNativeNavigation.Session ?? throw new InvalidOperationException("Battle Spirits session is missing.");
            foreach (var selection in GetComponentsInChildren<SelectionButton>(true)) selection.SetClickEvent(() => { });
            foreach (var button in GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            foreach (var group in GetComponentsInChildren<CanvasGroup>(true))
                if (group.name == "Content") { group.alpha = 1; group.blocksRaycasts = true; }
            switch (screen)
            {
                case BattleNativeScreen.Deck: deck = new BattleNativeDeckView(transform, session); deck.Build(); break;
                case BattleNativeScreen.Solo: BuildSolo(); break;
                case BattleNativeScreen.Online: BuildOnline(); break;
                case BattleNativeScreen.Room: BuildRoom(); break;
                case BattleNativeScreen.Duel: duel = new BattleNativeDuelView(Root, session); duel.Build(); break;
            }
            status = BattleNativeWidgets.Text(Root, "BS_Status", session.Status, 17);
            status.alignment = TextAlignmentOptions.MidlineLeft;
            BattleNativeWidgets.Place(status.rectTransform, 0.02f, 0.005f, 0.98f, 0.045f);
            session.Changed += Refresh;
            Refresh();
        }
        public override void ShowEvent()
        {
            base.ShowEvent();
            deck?.RefreshSelectedDeck(); duel?.ResumeVisuals();
            if (screen == BattleNativeScreen.Solo) RefreshSoloChoices();
            Refresh();
        }
        protected override void OnDestroy()
        {
            if (session != null) session.Changed -= Refresh;
            duel?.Dispose();
            base.OnDestroy();
        }
        protected override void HideEvent() { duel?.SuspendPresentation(); base.HideEvent(); }
        private void Refresh()
        {
            if (session == null || !gameObject.activeInHierarchy) return;
            if (status != null) status.text = session.Status;
            if (screen == BattleNativeScreen.Duel) duel?.Render();
            else if (screen == BattleNativeScreen.Room) RefreshRoom();
            else if (screen == BattleNativeScreen.Solo) RefreshSolo();
        }
        private void BuildSolo()
        {
            BattleNativeWidgets.Label(transform, "#TextTitle", "ĐẤU VỚI MÁY · BATTLE SPIRITS");
            RefreshSoloChoices();
            BattleNativeWidgets.Bind(transform, "ButtonDeck", CyclePlayerDeck);
            BattleNativeWidgets.Bind(transform, "ButtonPlay", () => {
                if (session.SelectedDeck == null || opponent == null) { MessageManager.Cast("Chọn bộ bài trước."); return; }
                session.StartSolo(session.SelectedDeck, opponent);
            }, "ĐẤU VỚI MÁY");
            foreach (var name in new[] { "ToggleLockHand", "ToggleNoCheck", "ToggleNoShuffle" }) BattleNativeWidgets.Active(transform, name, false);
            FixedInput("Settings/InputFieldPort", "SOUL CORE", "1");
            FixedInput("Settings/InputFieldLP", "LIFE BAN ĐẦU", "5");
            FixedInput("Settings/InputFieldHand", "BÀI MỞ ĐẦU", "4");
            FixedInput("Settings/InputFieldDraw", "RÚT MỖI LƯỢT", "1");
            RefreshSolo();
        }
        private void RefreshSoloChoices()
        {
            var playable = session.Decks.Where(item => DeckValidator.Validate(session.Cards, item.cards, true, BattleSpiritsMode.RulesFormat).IsValid).ToArray();
            string signature = string.Join("\n", playable.Select(item => item.name + ":" + string.Join(",", item.cards)));
            if (signature == soloDeckSet) return;
            soloDeckSet = signature;
            var content = BattleNativeWidgets.Component<RectTransform>(transform, "ScrollRect_LeftHalf/Viewport/Content");
            if (content != null)
            {
                BattleNativeWidgets.ClearGenerated(content);
                int i = 0;
                foreach (var definition in playable)
                {
                    var captured = definition;
                    Row(content, i++, definition.name, () => { opponent = captured; RefreshSolo(); }, definition.cards.FirstOrDefault());
                }
                content.sizeDelta = new Vector2(content.sizeDelta.x, i * 138f + 30);
            }
            opponent = playable.FirstOrDefault(item => item.name == opponent?.name)
                ?? playable.Skip(1).FirstOrDefault() ?? playable.FirstOrDefault();
        }
        private void FixedInput(string path, string label, string value)
        {
            var input = BattleNativeWidgets.Input(transform, path, value);
            if (input != null) { input.readOnly = true; input.interactable = false; }
            BattleNativeWidgets.Label(transform, path + "/#Text_Title", label);
        }
        private void RefreshSolo()
        {
            BattleNativeWidgets.Label(transform, "ButtonDeck", "BẠN: " + (session.SelectedDeck?.name ?? "Chưa chọn bộ bài") + " · bấm để đổi");
            BattleNativeWidgets.Active(transform, "ButtonDeck", true);
            BattleNativeWidgets.Label(transform, "Text_Overview", (opponent?.name ?? "Chọn đối thủ") + "\n\nWindBot điều khiển đối thủ theo luật Battle Spirits Standard.\n" + (opponent?.cards.Length ?? 0) + " lá.\n\nChọn bộ bài của bạn bên dưới rồi bấm ĐẤU VỚI MÁY.");
        }
        private void CyclePlayerDeck()
        {
            var choices = session.Decks.Where(item => DeckValidator.Validate(session.Cards, item.cards, true, BattleSpiritsMode.RulesFormat).IsValid).ToArray();
            if (choices.Length == 0) return;
            int current = Array.FindIndex(choices, item => item.name == session.SelectedDeck?.name
                && item.cards.SequenceEqual(session.SelectedDeck.cards));
            session.SelectDeck(choices[(current + 1) % choices.Length]);
        }
        private void BuildOnline()
        {
            BattleNativeWidgets.Label(transform, "#TextTitle", "CHƠI MẠNG · BATTLE SPIRITS");
            BattleNativeWidgets.Active(transform, "PageMyCard", false);
            BattleNativeWidgets.Active(transform, "PageLegacy", true);
            BattleNativeWidgets.Active(transform, "PageHost", false);
            BattleNativeWidgets.Bind(transform, "ToggleLegacy", () => NetworkPage(false), "VÀO PHÒNG");
            BattleNativeWidgets.Bind(transform, "ToggleHost", () => NetworkPage(true), "TẠO PHÒNG LAN");
            BattleNativeWidgets.Bind(transform, "ToggleMyCard", () => NetworkPage(false), "IP INTERNET");
            onlineName = BattleNativeWidgets.Input(transform, "PageLegacy/Right/InputFieldName", Config.Get("BattleSpiritsPlayerName", "Người chơi"));
            onlineHost = BattleNativeWidgets.Input(transform, "PageLegacy/Right/InputFieldHost", Config.Get("BattleSpiritsHost", "127.0.0.1"));
            onlinePort = BattleNativeWidgets.Input(transform, "PageLegacy/Right/InputFieldPort", "7777");
            BattleNativeWidgets.Active(transform, "PageLegacy/Right/InputFieldPassword", false);
            BattleNativeWidgets.Bind(transform, "PageLegacy/Right/ButtonJoin", () => session.Join(onlineHost?.text ?? "127.0.0.1", Port(onlinePort), PlayerName()), "THAM GIA");
            BattleNativeWidgets.Bind(transform, "PageLegacy/Right/ButtonSave", () => {
                Config.Set("BattleSpiritsHost", onlineHost?.text ?? "127.0.0.1"); Config.Set("BattleSpiritsPlayerName", PlayerName()); Config.Save();
                MessageManager.Cast("Đã lưu địa chỉ Battle Spirits.");
            }, "LƯU ĐỊA CHỈ");
            BattleNativeWidgets.Active(transform, "ScrollRectYPK", false);
            var content = BattleNativeWidgets.Component<RectTransform>(transform, "PageLegacy/LayoutGroup/ScrollRectPreset/Viewport/Content");
            if (content != null)
            {
                Row(content, 0, "THỬ TRÊN CÙNG MÁY · 127.0.0.1:7777", () => { if (onlineHost != null) onlineHost.text = "127.0.0.1"; if (onlinePort != null) onlinePort.text = "7777"; });
                Row(content, 1, "LAN: nhập IPv4 của máy tạo phòng", () => MessageManager.Cast("Hai máy cùng mạng dùng IP LAN của chủ phòng và cổng 7777."));
                content.sizeDelta = new Vector2(content.sizeDelta.x, 300);
            }
            hostPort = BattleNativeWidgets.Input(transform, "PageHost/InputFieldTime", "7777");
            BattleNativeWidgets.Label(transform, "PageHost/InputFieldTime/#Text_Title", "CỔNG TCP");
            FixedInput("PageHost/InputFieldLP", "LIFE BAN ĐẦU", "5");
            FixedInput("PageHost/InputFieldHand", "BÀI MỞ ĐẦU", "4");
            FixedInput("PageHost/InputFieldDraw", "RÚT MỖI LƯỢT", "1");
            foreach (var name in new[] { "ButtonRegulation", "ButtonPool", "ButtonMode" }) BattleNativeWidgets.Interactable(transform, "PageHost/" + name, false);
            BattleNativeWidgets.Label(transform, "PageHost/ButtonRegulation", "BATTLE SPIRITS ETERNAL");
            BattleNativeWidgets.Label(transform, "PageHost/ButtonPool", "THẺ ĐÃ HỖ TRỢ HIỆU ỨNG");
            BattleNativeWidgets.Label(transform, "PageHost/ButtonMode", "1 ĐẤU 1");
            BattleNativeWidgets.Active(transform, "PageHost/ToggleCheck", false);
            BattleNativeWidgets.Active(transform, "PageHost/ToggleShuffle", false);
            BattleNativeWidgets.Bind(transform, "PageHost/ButtonCreate", () => session.Host(Port(hostPort), PlayerName()), "TẠO PHÒNG");
        }
        private void NetworkPage(bool host)
        {
            BattleNativeWidgets.Active(transform, "PageLegacy", !host);
            BattleNativeWidgets.Active(transform, "PageHost", host);
            BattleNativeWidgets.Active(transform, "PageMyCard", false);
        }
        private int Port(TMP_InputField input) => int.TryParse(input?.text, out var port) && port > 0 && port <= 65535 ? port : 7777;
        private string PlayerName() => string.IsNullOrWhiteSpace(onlineName?.text) ? "Người chơi" : onlineName.text.Trim();
        private void BuildRoom()
        {
            BattleNativeWidgets.Label(transform, "#Text_ServantTitle", "PHÒNG BATTLE SPIRITS");
            BattleNativeWidgets.Active(transform, "ButtonPlayer2", false); BattleNativeWidgets.Active(transform, "ButtonPlayer3", false);
            BattleNativeWidgets.Active(transform, "ButtonAddBot", false); BattleNativeWidgets.Active(transform, "ButtonToWatch", false);
            BattleNativeWidgets.Bind(transform, "DeckSelector", CyclePlayerDeck);
            BattleNativeWidgets.Bind(transform, "ButtonReady", () => session.SetReady(!session.IsReady));
            BattleNativeWidgets.Bind(transform, "ButtonStart", session.StartOnline, "BẮT ĐẦU");
            BattleNativeWidgets.Bind(transform, "ButtonToDuel", () => session.SetReady(true), "VÀO VỊ TRÍ ĐẤU");
            RefreshRoom();
        }
        private void RefreshRoom()
        {
            BattleNativeWidgets.Label(transform, "TextDeckName", session.SelectedDeck?.name ?? "Chọn bộ bài");
            for (int i = 0; i < 2; i++)
            {
                var player = session.Players.FirstOrDefault(value => value.playerIndex == i);
                BattleNativeWidgets.Label(transform, "ButtonPlayer" + i + "/Text_ButtonLTitle", player == null ? "Đang chờ người chơi…" : player.name + "\n" + player.deckCount + " lá · " + (player.ready ? "Sẵn sàng" : "Chưa sẵn sàng"));
                BattleNativeWidgets.Active(transform, "ButtonPlayer" + i + "/ReadyIcon", player?.ready == true);
            }
            BattleNativeWidgets.Label(transform, "ButtonReady", session.IsReady ? "HỦY SẴN SÀNG" : "SẴN SÀNG");
            BattleNativeWidgets.Interactable(transform, "ButtonReady", session.IsConnected);
            BattleNativeWidgets.Interactable(transform, "ButtonStart", session.Seat == 0 && session.Players.Count == 2 && session.Players.All(player => player.ready));
            BattleNativeWidgets.Active(transform, "ButtonStart", session.Seat == 0);
            var image = BattleNativeWidgets.Component<RawImage>(transform, "CardPos0/CardImage0");
            if (image != null && (session.SelectedDeck?.cards.Length ?? 0) > 0) session.LoadArt(session.SelectedDeck.cards[0], image);
        }
        private void Row(RectTransform content, int index, string label, Action click, string card = null)
        {
            var button = BattleNativeWidgets.Button(content, "BS_Row_" + index, label, click);
            var rect = button.transform as RectTransform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1); rect.anchoredPosition = new Vector2(0, -index * 138f);
            rect.sizeDelta = new Vector2(0, 126);
            if (card != null)
            {
                var art = BattleNativeWidgets.Rect(button.transform, "BS_DeckArtwork"); BattleNativeWidgets.Place(art, 0.01f, 0.03f, 0.14f, 0.97f);
                var raw = art.gameObject.AddComponent<RawImage>(); raw.raycastTarget = false; session.LoadArt(card, raw);
                var text = button.GetComponentInChildren<TextMeshProUGUI>(); if (text != null) BattleNativeWidgets.Place(text.rectTransform, 0.16f, 0, 0.99f, 1);
            }
        }
    }
}
