# LaoDai Battle Spirits — Eternal 0.2.0

Bản chuyển đổi trực tiếp từ **Card-Game/MDPro3** sang **Battle Spirits Nhật, thể thức Eternal**, giao diện tiếng Việt, Unity **6000.0.24f1**. Các prefab màn hình gốc được giữ bố cục và tài nguyên giao diện; controller, dữ liệu thẻ, bộ bài, Solo và trận đấu bên trong được thay bằng hệ thống Battle Spirits. Lớp giao diện chèn thêm của bản 0.1.0 đã được bỏ.

Bản này có **Solo đấu với WindBot**, tạo/tham gia phòng LAN hoặc IP trực tiếp, xây/lưu bộ bài, xem thử hoạt cảnh boss và replay Solo. Danh mục có **261 thẻ chính thức**, trong đó **21 thẻ đã hỗ trợ chơi tự động**; **240 thẻ còn lại chỉ để tra cứu**. Đây chưa phải triển khai đầy đủ mọi thẻ và hiệu ứng Eternal. Xem [phạm vi luật](Documentation/BATTLE_SPIRITS_SCOPE.md).

Nhánh phát triển là `battle-spirits-mdpro3-ui`. Bản Card-Game/Yu-Gi-Oh cũ vẫn ở `main`.

## Cập nhật và mở dự án

Đóng Unity trước khi chuyển nhánh. Lệnh stash giữ các chỉnh sửa và tệp chưa được Git theo dõi; dữ liệu cá nhân nằm trong `.gitignore` vẫn cần sao lưu riêng nếu muốn di chuyển hoặc xóa chúng.

```bash
git stash push -u -m "backup-before-native-battle-spirits-update"
git fetch origin
git switch battle-spirits-mdpro3-ui
git pull --ff-only origin battle-spirits-mdpro3-ui
```

1. Mở thư mục dự án bằng **Unity 6000.0.24f1**, nền tảng Windows.
2. Giữ `Data`, `Picture`, `Platforms` và `Sound` cùng cấp với `Assets`.
3. Mở **Window → Asset Management → Addressables → Groups**; khi chạy từ source, chọn **Play Mode Script → Use Asset Database** để dùng các prefab vừa sửa.
4. Mở `Assets/Scenes/Boot.unity`, nhấn Play; tên phiên bản trong menu là **0.2.0**.
5. Vào **Bộ bài** để chọn bộ bài, **Đấu với máy** để chơi Solo, hoặc **Hoạt cảnh** để xem thử boss.

**Các kiểm tra mã, dữ liệu và TCP đã chạy; môi trường phát triển chưa chạy Play/render/build trực tiếp trong Windows Unity.** Khi thử Editor, kiểm tra font, kích thước chữ, ảnh, thao tác chuột và việc quay lại/chơi ván tiếp theo. [Báo cáo kiểm tra](Documentation/BATTLE_SPIRITS_VALIDATION.md) ghi rõ giới hạn này.

## Màn hình và tài nguyên được thay

- **DeckEditorUI, SoloSelectorUI, OnlineServantUI, RoomServantUI, OcgCoreUI** dùng controller mới ngay trên prefab gốc. Không tạo một Canvas ứng dụng khác phủ lên menu.
- Form bộ bài giữ vùng chi tiết thẻ, bộ sưu tập và danh sách deck. Extra/Side của Yu-Gi-Oh được ẩn. Tìm theo tên, mã, hệ, nội dung hiệu ứng, loại hoặc bộ; hỗ trợ `cost:6` và `bp:6000`, lọc màu/trạng thái và sắp xếp.
- Ảnh mặt thẻ Battle Spirits nằm trực tiếp trong **`Picture/Art/{mã-thẻ}.png`**. Hai ảnh closeup tự dựng nằm trong **`Picture/Closeup`**. Bộ 26.141 ảnh Art/Closeup Yu-Gi-Oh dạng tên số đã bỏ khỏi nhánh này; bản gốc còn trên `main` và backup. [Chi tiết thay ảnh](Documentation/battle-spirits-image-replacement.json).
- Field dùng hình học mới với vùng Life, Reserve, Trash, Deck, Spirit và Nexus. Spirit có Core/Soul Core và hoạt ảnh theo sự kiện trận đấu. Pet Master Duel được giữ làm yếu tố trang trí.
- Hai boss **BS01-X01** và **BS01-097** có model 3D kiểu low-poly và ảnh closeup **tự dựng**, không phải model chính thức của BANDAI. Các Spirit khác dùng hình học thay thế theo màu; chưa có model riêng cho từng thẻ.
- Menu **Hoạt cảnh** mở màn xem thử bằng form bàn đấu gốc, cho bấm triệu hồi, tấn công, phòng thủ và chiến thắng; màn xem thử không gửi lệnh vào trận thật.
- Nhạc nền và tám hiệu ứng WAV mới được tổng hợp tại `Sound/BattleSpirits`. Xem [nguồn và cách tạo lại tài nguyên](Tools/BattleSpiritsAssets/README.md).

## Thẻ, bộ bài và Solo

Dữ liệu thẻ được đối chiếu ngày **31/10/2026**. Hai đợt mới đã nhập là [BS76 — 永皇の輝き](https://www.battlespirits.com/product/booster/booster-76.php), phát hành 30/05/2026, và [26RBS03 — 絶界の覇者](https://www.battlespirits.com/product/booster/booster-26rbs03.php), phát hành 17/10/2026. Nhóm 26RBS03 thuộc Standard và chưa được mặc định xác nhận hợp lệ trong Eternal. Danh mục này không bao gồm toàn bộ lịch sử thẻ Battle Spirits.

Lượt kiểm tra booster mới nhất ngày **04/11/2026** vẫn xác nhận BS76 cho Eternal và 26RBS03 cho Standard. Mốc này là kiểm tra danh sách đợt phát hành; ngày đối chiếu dữ liệu thẻ ở trên vẫn là 31/10/2026.

21 thẻ chơi được gồm 16 Spirit không hiệu ứng và năm thẻ có code riêng đã đối chiếu thông số/văn bản gốc:

| Mã | Thẻ | Phần xử lý chính |
| --- | --- | --- |
| BS01-X01 | 龍皇ジークフリード | Awaken; hiệu ứng bị phá hủy ở Lv3 |
| BS01-097 | 飛龍ヴァルキュリウス | Tăng BP khi chặn |
| BS01-113 | 侵食されゆく銀世界 | Nexus; hồi Core từ Trash và hiệu ứng Lv2 |
| BS01-115 | アウェイクン | Chuyển tối đa 3 Core từ Reserve và rút bài; có thể chọn 0 Core |
| BS01-117 | ダブルドロー | Main rút bài hoặc Flash tăng BP |

Bộ bài Eternal ít nhất 40 lá, giới hạn theo tên gốc tiếng Nhật và danh sách hạn chế/ngoại lệ đã xác minh. Lưu bằng JSON tại `Data/BattleSpirits/Decks`; các bộ mẫu 40 lá nằm trong `Data/BattleSpirits/StarterDecks`. Thẻ chưa có hiệu ứng được hỗ trợ sẽ bị từ chối khi bắt đầu trận tự động.

**Đấu với máy** dùng executor Battle Spirits mới trong cấu trúc WindBot. Ba bộ mẫu có mức Luyện tập, Giữ sân và Boss; bot quyết định từ tay bài của chính nó và thông tin công khai, được điều phối theo frame. Solo không dùng luật hoặc gói tin Yu-Gi-Oh để giả lập Battle Spirits. Đây là AI heuristic cho nhóm thẻ đã hỗ trợ, chưa phải AI hiểu mọi thẻ Eternal.

Trận Solo hoàn tất được lưu tại `Data/BattleSpirits/Replays`. **Lịch sử trận** mở lại theo danh mục/phiên bản phù hợp và cho xem từng thao tác. Thư mục deck/replay cá nhân được bỏ qua trong Git.

Nguồn chính thức, checksum ảnh/HTML và ngày truy cập nằm trong `Data/BattleSpirits/card-sources.json`; danh sách hạn chế ở `banlist.json`. `implemented-cards.json` và registry code khóa nhóm hiệu ứng đã kiểm tra.

```bash
python Tools/BattleSpiritsCards/catalog.py validate
python Tools/BattleSpiritsCards/catalog.py check
```

Xem [công cụ thẻ](Tools/BattleSpiritsCards/README.md) trước khi import. Cập nhật ảnh hoặc văn bản không tự làm một hiệu ứng mới trở thành chơi được.

## LAN và kết nối IP

Vào **Chơi mạng**. Một máy tạo phòng TCP **7777**; máy khác nhập IP LAN của máy chủ và đúng cổng. Trong phòng, chọn deck hợp lệ, bấm **Sẵn sàng**; người ở vị trí chủ phòng bắt đầu trận. Hai client cần cùng danh mục và giao thức **phiên bản 2**.

- Thử hai client cùng máy: dùng `127.0.0.1`.
- Thử từ máy khác cùng router: dùng IPv4 LAN của máy tạo phòng và cho phép Unity/game qua Windows Firewall trên mạng riêng.
- Kết nối Internet trực tiếp cần địa chỉ/cổng server truy cập được. Bản này chưa có tài khoản/ghép trận MyCard, xếp hạng, tự mở cổng router hoặc reconnect tiếp tục trận.

Server C# kiểm tra người gửi, deck và luật; chỉ gửi tay bài của người nhận cùng trạng thái công khai. Có thể chạy [server console](Tools/BattleSpiritsServer/README.md) ngoài Unity. **Cổng 7911/YgoServer thuộc bản Yu-Gi-Oh cũ; bản Battle Spirits này dùng 7777.**

## Build Windows

Cài Windows Build Support phù hợp Editor; với cấu hình IL2CPP hiện tại cần công cụ C++ và Windows SDK. Chọn **File → Build Profiles → Windows**, build vào một thư mục riêng, ví dụ `Builds/Windows`, và dùng Addressables mới từ các prefab hiện tại.

`BattleSpiritsWindowsPackage` có bước đóng gói sau build để chép dữ liệu Battle Spirits, ảnh được danh mục tham chiếu, closeup/nhạc mới và bundle UI/Mate còn dùng. Bundle Windows được đặt tại `<tên-game>_Data/StandaloneWindows64/MasterDuel`; `Data`, `Picture`, `Sound` nằm cạnh `.exe`. Cấu hình cá nhân, đăng nhập, deck/replay tự tạo không được lấy từ máy phát triển để đóng gói.

Bước đóng gói này chưa được xác minh bằng một Windows build thực tế. Sau build cần mở game độc lập, kiểm tra tài nguyên rồi gửi **toàn bộ thư mục bản build** cho người chơi khác; họ không cần cài Unity.

## Nguồn và bản cũ

Template: [MDPro3 của Sherry Chaos](https://code.moenext.com/sherry_chaos/MDPro3), được tùy biến bởi LaoDai060204. Thông tin bản template được giữ tại [MDPRO3_TEMPLATE.md](Documentation/MDPRO3_TEMPLATE.md); [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) giữ ghi công và thông báo của các thành phần dùng lại.

Ảnh mặt thẻ và văn bản Battle Spirits lấy từ [website chính thức](https://www.battlespirits.com/cardlist/), giữ quyền của các chủ sở hữu. Model, render closeup/menu và sound bank mới có manifest riêng, phân biệt với tài nguyên chính thức. Đây là dự án cộng đồng độc lập.

Bản trước thay thế trực tiếp được giữ tại `backup/2026-10-31-before-native-battle-spirits-replacement` (`9d1d1055`). Snapshot công việc trước kiểm tra cuối được giữ tại `backup/2026-11-04-native-replacement-wip` (`88d80baa`). Không force-push hoặc ghi đè các nhánh backup.
