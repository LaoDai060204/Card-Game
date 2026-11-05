using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LaoDai.BattleSpirits.Integration;
using LaoDai.BattleSpirits.NativeUI;
using MDPro3;
using MDPro3.UI;
using MDPro3.UI.ServantUI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LaoDai.BattleSpirits.Presentation
{
    // Compatibility entry points only. Views are the original template prefabs,
    // registered with Program/Servant; there is no BattleSpirits overlay Canvas.
    public static class BattleSpiritsApp
    {
        public static bool Active => BattleSpiritsMode.Enabled;
        public static bool IsOpen => BattleNativeNavigation.IsOpen;

        public static void ConfigureMenu(MainMenuUI source)
        {
            if (!Active || source == null) return;
            BattleNativeSession.Ensure();
            var labels = new Dictionary<string, string>
            {
                { "ButtonSolo", "ĐẤU VỚI MÁY" }, { "ButtonPuzzle", "LUẬT CHƠI" },
                { "ButtonReplay", "LỊCH SỬ TRẬN" }, { "ButtonCutin", "HOẠT CẢNH" }
            };
            foreach (var item in source.GetComponentsInChildren<Transform>(true))
                if (labels.TryGetValue(item.name, out string value))
                    foreach (var text in item.GetComponentsInChildren<TextMeshProUGUI>(true))
                        if (text.name.EndsWith("_Menu", StringComparison.Ordinal)) text.text = value;
        }

        private static bool Ready()
        {
            var session = BattleNativeSession.Ensure();
            if (session.Initialized) return true;
            UIManager.ShowPopupText(new List<string> { "Battle Spirits", session.Status, "ĐÓNG" });
            return false;
        }

        public static void ShowDeckBuilder() { if (Ready()) BattleNativeNavigation.Open(BattleNativeScreen.Deck); }
        public static void ShowLocal() { if (Ready()) BattleNativeNavigation.Open(BattleNativeScreen.Solo); }
        public static void ShowOnline() { if (Ready()) BattleNativeNavigation.Open(BattleNativeScreen.Online); }

        public static void ShowBossGallery()
        {
            if (!Ready()) return;
            var session = BattleNativeSession.Ensure();
            var bosses = session.Cards.Where(card => card.id == "BS01-X01" || card.id == "BS01-097")
                .ToDictionary(card => card.id + " · " + card.DisplayName, card => card.id, StringComparer.Ordinal);
            var selections = new List<string> { "HOẠT ẢNH TRIỆU HỒI", "Xem hiệu ứng và artwork lá bài. Hai thẻ BS01 này thuộc Eternal, không dùng trong deck Standard." };
            selections.AddRange(bosses.Keys);
            UIManager.ShowPopupSelection(selections, () =>
            {
                var selected = EventSystem.current?.currentSelectedGameObject?.GetComponent<SelectionButton>();
                if (selected != null && bosses.TryGetValue(selected.GetButtonText(), out string id)) session.StartBossPreview(id);
            }, null);
        }

        public static void ShowRules()
        {
            UIManager.ShowPopupText(new List<string> { "BATTLE SPIRITS — STANDARD",
                "Bộ bài ít nhất 40 lá; mặc định tối đa 3 lá cùng tên, theo danh sách cấm/hạn chế.\n\n"
                + "Bắt đầu: 5 Life, 3 Core thường và 1 Soul Core ở Reserve, 4 lá trên tay. Mỗi người được đổi toàn bộ bài đầu một lần.\n\n"
                + "Thẻ Standard hiện hành cần block A. Thẻ BS01 và thẻ ngoài pool Standard không dùng trong deck này.\n\n"
                + "Lượt: Start → Core → Draw → Refresh → Main → Attack → Second Main → End. Người đi trước bỏ Core, Attack và Second Main ở lượt đầu, vẫn Draw.\n\n"
                + "Main: triệu hồi Spirit/đặt Nexus, giảm Cost theo symbol cùng màu; chuyển Core để đổi Level/BP. Spirit dưới Lv1 tiêu biến, Nexus có thể ở Lv1 không Core theo chỉ số in.\n\n"
                + "Attack: Spirit hồi phục có thể tấn công ngay lượt triệu hồi. Bên phòng thủ có Flash trước; hai lần bỏ qua chuyển sang chặn/nhận sát thương. Nếu chặn, có Flash lần hai trước khi so BP. BP bằng nhau: cả hai bị phá hủy.\n\n"
                + "Magic và kỹ năng chỉ dùng đúng Main/Flash, Level và mục tiêu đã ghi. Life về 0 hoặc hết Deck ở Start Step thì thua.\n\n"
                + "Chỉ thẻ được ghi Đã hỗ trợ được dùng trong trận tự động. Các thẻ khác giữ nguyên ảnh/văn bản chính thức để tra cứu.\n\n"
                + "Nguồn: Standard Official Rules/Manual v1.0 được cung cấp cùng dự án. Floor Rules dùng cho giải đấu, không tự áp dụng timer hoặc hình phạt vào Solo.", "ĐÓNG" }, HorizontalAlignmentOptions.Left);
        }

        public static void ShowHistory()
        {
            if (!Ready()) return;
            var session = BattleNativeSession.Ensure();
            string[] files = session.ReplayFiles();
            if (files.Length == 0)
            {
                UIManager.ShowPopupText(new List<string> { "LỊCH SỬ TRẬN", "Trận Solo hoàn tất được tự lưu. Chưa có lịch sử trận.", "ĐÓNG" }); return;
            }
            var labels = files.Take(100).ToDictionary(Path.GetFileNameWithoutExtension, path => path, StringComparer.Ordinal);
            var selections = new List<string> { "LỊCH SỬ TRẬN", "Chọn trận để xem lại." }; selections.AddRange(labels.Keys);
            UIManager.ShowPopupSelection(selections, () =>
            {
                var selected = EventSystem.current?.currentSelectedGameObject?.GetComponent<SelectionButton>();
                if (selected != null && labels.TryGetValue(selected.GetButtonText(), out string path)) session.LoadReplay(path, 0);
            }, null);
        }
    }
}
