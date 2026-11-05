using System;
using System.Collections.Generic;
using System.Linq;
using LaoDai.BattleSpirits.Core;
using LaoDai.BattleSpirits.Networking;
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
        private RectTransform hand, field0, field1;
        private TextMeshProUGUI phase;
        private int selectedSpirit = -1;
        private RawImage backdrop;
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
            var background = BattleNativeWidgets.Rect(root, "BS_Field"); BattleNativeWidgets.Place(background, 0, 0, 1, 1);
            background.SetAsFirstSibling(); backdrop = background.gameObject.AddComponent<RawImage>(); backdrop.raycastTarget = false;
            session.AttachField(backdrop);
            hand = BattleNativeWidgets.Rect(root, "BS_Hand"); BattleNativeWidgets.Place(hand, 0.17f, 0.005f, 0.83f, 0.20f);
            field0 = BattleNativeWidgets.Rect(root, "BS_OwnField"); BattleNativeWidgets.Place(field0, 0.22f, 0.24f, 0.80f, 0.43f);
            field1 = BattleNativeWidgets.Rect(root, "BS_OpponentField"); BattleNativeWidgets.Place(field1, 0.22f, 0.58f, 0.80f, 0.77f);
            phase = BattleNativeWidgets.Text(root, "BS_Phase", string.Empty, 25); BattleNativeWidgets.Place(phase.rectTransform, 0.25f, 0.45f, 0.74f, 0.56f);
            SetButton("ButtonSetting", "NHẬN THUA", () => session.Send(new WireMessage { type = "action", action = "concede" }));
            Render();
        }
        internal void ResumeVisuals()
        {
            selectedSpirit = -1;
            if (backdrop != null) session.AttachField(backdrop);
        }
        internal void Render()
        {
            var view = session.View; if (view == null || view.players == null || view.players.Length != 2) return;
            var own = view.players.First(player => player.playerIndex == view.viewer);
            var opponent = view.players.First(player => player.playerIndex != view.viewer);
            BattleNativeWidgets.Label(root, "LP0/TextPlayer0LP", own.life.ToString());
            BattleNativeWidgets.Label(root, "LP1/TextPlayer1LP", opponent.life.ToString());
            Name("LP0/NameBase/TextPlayer0Name", own); Name("LP1/NameBase/TextPlayer1Name", opponent);
            phase.text = view.winner >= 0 ? (view.winner == view.viewer ? "CHIẾN THẮNG" : "KẾT THÚC TRẬN")
                : "LƯỢT " + view.turn + " · " + Phase(view.phase) + "\n" + (view.decisionPlayer == view.viewer ? "Đến lượt bạn chọn" : "Đang chờ đối thủ…");
            DrawHand(view); DrawField(field0, own, true); DrawField(field1, opponent, false); Actions(view);
        }
        private void Name(string path, PublicPlayerView player)
        {
            BattleNativeWidgets.Label(root, path, player.name + "\nReserve " + player.reserveNormal + (player.reserveSoul ? " + ◆" : "") + " · Trash " + player.trashNormal + " · Deck " + player.deckCount);
            var label = BattleNativeWidgets.Component<TextMeshProUGUI>(root, path); if (label != null) label.fontSize = 20;
        }
        private void DrawHand(FilteredMatchView view)
        {
            BattleNativeWidgets.ClearGenerated(hand);
            int count = view.ownHand?.Length ?? 0;
            for (int i = 0; i < count; i++)
            {
                int index = i; string id = view.ownHand[i];
                var button = BattleNativeWidgets.Card(hand, id, () => PlayHand(index, id), session);
                float width = Math.Min(0.14f, 1f / Math.Max(1, count));
                BattleNativeWidgets.Place(button.transform as RectTransform, i * width, 0, (i + 1) * width - 0.004f, 1);
                button.interactable = view.winner < 0
                    && !(session is IBattleNativeReplaySession replay && replay.IsReplay);
            }
        }
        private void DrawField(RectTransform container, PublicPlayerView player, bool own)
        {
            BattleNativeWidgets.ClearGenerated(container);
            int count = player.field?.Length ?? 0;
            for (int i = 0; i < count; i++)
            {
                var spirit = player.field[i];
                var button = BattleNativeWidgets.Card(container, spirit.cardId, () => {
                    Inspect(spirit.cardId);
                    if (own) { selectedSpirit = spirit.instanceId; Actions(session.View); }
                }, session);
                float width = Math.Min(0.145f, 1f / Math.Max(1, count));
                BattleNativeWidgets.Place(button.transform as RectTransform, i * width, 0, (i + 1) * width - 0.005f, 1);
                bool nexus = cards.TryGetValue(spirit.cardId, out var definition) && definition.IsNexus;
                var info = BattleNativeWidgets.Text(button.transform, "BS_Stats", (nexus ? "Nexus Lv" + spirit.level : "Lv" + spirit.level + " · " + spirit.bp + " BP") + "\n" + spirit.normalCores + (spirit.soulCore ? " + ◆" : "") + (spirit.exhausted ? " · MỆT" : ""), 15);
                BattleNativeWidgets.Place(info.rectTransform, 0, 0, 1, 0.25f);
            }
        }
        private void Inspect(string id)
        {
            if (!cards.TryGetValue(id, out var card)) return;
            var detail = BattleNativeWidgets.Find(root, "CardDescription"); if (detail == null) return;
            detail.gameObject.SetActive(true);
            var image = detail.GetComponentsInChildren<RawImage>(true).FirstOrDefault();
            if (image != null)
            {
                if (session is IBattleNativePortraitSession portraits) portraits.LoadPortrait(id, image);
                else session.LoadArt(id, image);
            }
            var text = BattleNativeWidgets.Find(detail, "Description Scroll View/Viewport/Content")?.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null) { text.richText = false; text.text = card.DisplayName + "\n" + card.id + " · " + card.type + " · Cost " + card.cost + "\n" + card.effectTextJa; }
            BattleNativeWidgets.Bind(detail, "Window/Card/Button", () => detail.gameObject.SetActive(false));
        }
        private void PlayHand(int index, string id)
        {
            Inspect(id); var view = session.View;
            if (!CanAct(view) || !cards.TryGetValue(id, out var card)) return;
            bool main = view.phase == "Main" || view.phase == "SecondMain";
            bool flash = view.phase == "FlashBeforeBlock" || view.phase == "FlashAfterBlock";
            if (string.Equals(card.type, "Magic", StringComparison.OrdinalIgnoreCase))
            {
                if (!main && !flash) return;
                var options = new List<KeyValuePair<string, Action>>();
                if (main && card.id == AbilityRegistry.DoubleDraw)
                    options.Add(Choice("Main: rút 2 lá", () => Dispatch("playMagic", index)));
                if (card.id == AbilityRegistry.AwakenMagic)
                    options.Add(Choice("Không chuyển Core · rút 1 lá", () => Dispatch("playMagic", index,
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
                                else Dispatch("playMagic", index, target: target);
                            }));
                    }
                if (options.Count == 0) { MessageManager.Cast("Không có mục tiêu Spirit phù hợp."); return; }
                Select("Chọn mục tiêu Magic", options); return;
            }
            if (main) Dispatch("summon", index);
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
                options.Add(Choice(count + " Core thường", () => Dispatch("playMagic", handIndex, target: target,
                    transfers: new[] { new CoreTransfer { normal = count } })));
                if (hasSoul && count < 3)
                    options.Add(Choice(count + " Core thường + Soul Core", () => Dispatch("playMagic", handIndex, target: target,
                        transfers: new[] { new CoreTransfer { normal = count, soul = true } })));
            }
            Select("Awaken · chuyển tối đa 3 Core rồi rút 1 lá", options);
        }
        private void Actions(FilteredMatchView view)
        {
            if (view == null) return;
            foreach (var name in new[] { "ButtonInfo", "ButtonTiming", "ButtonAttachedEffect", "ButtonLog", "ButtonSetting" }) BattleNativeWidgets.Active(root, "Buttons/" + name, false);
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
        private bool CanAct(FilteredMatchView view) => view != null && view.winner < 0 && view.decisionPlayer == view.viewer
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
            target.gameObject.SetActive(true); BattleNativeWidgets.Bind(root, "Buttons/" + name, action);
            foreach (var old in target.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (old.gameObject.name != "BS_ActionLabel") old.gameObject.SetActive(false);
            var text = BattleNativeWidgets.Component<TextMeshProUGUI>(target, "BS_ActionLabel")
                ?? BattleNativeWidgets.Text(target, "BS_ActionLabel", label, 16);
            text.gameObject.SetActive(true); text.text = label; text.richText = false;
            var face = target.GetComponent<Image>() ?? target.gameObject.AddComponent<Image>();
            face.sprite = null; face.material = null;
            face.color = new Color(0.025f, 0.13f, 0.19f, 0.98f); face.raycastTarget = true;
            var button = target.GetComponent<Button>(); if (button != null) button.targetGraphic = face;
            BattleNativeWidgets.Interactable(root, "Buttons/" + name, enabled);
        }
        private void Dispatch(string action, int index = 0, int source = -1, int target = -1, bool soul = false, CoreTransfer[] transfers = null)
        {
            if (!session.CanSendCommands || (action != "concede" && !CanAct(session.View))) return;
            session.Send(new WireMessage { type = "action", action = action, index = index, source = source, target = target, soul = soul, transfers = transfers });
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
