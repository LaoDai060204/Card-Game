# LaoDai Battle Spirits — Standard 0.4.0

Bản chuyển đổi từ **Card-Game/MDPro3** sang **Battle Spirits Nhật — Standard**, giao diện tiếng Việt, Unity **6000.0.24f1**. Theo lựa chọn của chủ dự án, bản này giữ bố cục các màn hình, thay đồ họa Master Duel bằng UI mới và chỉ giữ pet, wallpaper cùng những phụ thuộc cần để các tài nguyên đó hoạt động. Lớp ứng dụng overlay của bản 0.1.0 đã bỏ.

Có **Solo đấu WindBot**, bộ bài, phòng LAN/IP trực tiếp, replay Solo và xem thử hoạt cảnh. Danh mục gồm **270 thẻ chính thức**; **19 thẻ Standard chơi được**: 16 Spirit không hiệu ứng và ba Spirit có Super Unleash đã triển khai. **251 thẻ khác không dùng trong trận Standard hiện tại**, gồm 21 thẻ Eternal đã có code tương thích. Đây chưa phải triển khai đầy đủ mọi thẻ và hiệu ứng Standard. Xem [phạm vi luật](Documentation/BATTLE_SPIRITS_SCOPE.md).

Nhánh phát triển: `battle-spirits-mdpro3-ui`. Bản Card-Game/Yu-Gi-Oh cũ được giữ trên `main`.

**Bản 0.4.0 đổi phần triệu hồi và hiển thị trên sân:** phóng to mặt thẻ, quét sáng, nâng ảnh minh họa rồi đưa lá bài xuống sân. Sau đó mặt thẻ nằm trên sân và ảnh minh họa của chính lá đó nổi phía trên. Hình nổi được lấy từ artwork có sẵn, giữ nền minh họa; không phải ảnh quái đã tách nền hay model 3D. Có nút **BỎ QUA** hoạt ảnh. [Chi tiết và cách thử](Documentation/BATTLE_SPIRITS_SUMMON_PRESENTATION.md).

## Cập nhật và mở dự án

Đóng Unity trước khi cập nhật. Stash giữ chỉnh sửa và tệp chưa được Git theo dõi; dữ liệu cá nhân bị `.gitignore` loại trừ cần sao lưu riêng trước khi di chuyển hoặc xóa.

```bash
git stash push -u -m "backup-before-standard-0.4-update"
git fetch origin
git switch battle-spirits-mdpro3-ui
git pull --ff-only origin battle-spirits-mdpro3-ui
```

1. Mở thư mục dự án bằng **Unity 6000.0.24f1**, nền tảng Windows x64.
2. Giữ `Data`, `Picture`, `Platforms`, `Sound` cùng cấp với `Assets`.
3. Vào **Window → Asset Management → Addressables → Groups**, chọn **Play Mode Script → Use Asset Database** khi chạy từ source để dùng prefab mới.
4. Mở `Assets/Scenes/Boot.unity`, nhấn Play. Menu phải hiện **0.4.0**.
5. Vào **Bộ bài** chọn bộ Standard mẫu, rồi **Đấu với máy**. Sau khi chọn **GIỮ 4 LÁ**, đến Main thì bấm lá trên tay để triệu hồi; chưa có thao tác kéo thả.

**Mã, dữ liệu, UI stand-in và TCP đã được kiểm tra; chưa chạy Play/render/build trong Unity Windows thật.** Xem [báo cáo kiểm tra](Documentation/BATTLE_SPIRITS_VALIDATION.md).

## Giao diện và tài nguyên

- Các form **DeckEditorUI, SoloSelectorUI, OnlineServantUI, RoomServantUI, OcgCoreUI** và ba Parts giữ hierarchy, vị trí và kích thước control. Đồ họa UI cũ được thay bằng ba sprite panel/button/clear tự tạo; không thêm Canvas ứng dụng phủ lên menu.
- Deck giữ vùng bộ sưu tập, chi tiết và danh sách bài. Extra/Side được ẩn. Tìm theo mã/tên/hệ/hiệu ứng/loại/bộ, hoặc `cost:6`, `bp:6000`; có lọc màu, trạng thái và sắp xếp. Thẻ ngoài pool Standard bị từ chối khi thêm vào deck; thẻ chưa hỗ trợ bị từ chối lúc bắt đầu trận tự động.
- Mặt thẻ nằm ở `Picture/Art/{mã-thẻ}.png`. Sân có vùng Life, Reserve, Trash, Deck, Spirit/Nexus và Core/Soul Core. Mỗi Spirit hiện đúng mặt thẻ trên sân và vùng minh họa nổi hướng về camera, thay các hình quái chung theo màu. Chọn thẻ ngay tại vị trí lá bài trên sân.
- Pet và wallpaper được giữ: danh mục độc lập có **108 pet, 149 wallpaper**. Chọn pet ở **Đồng hành**, chọn wallpaper ở **Cài đặt → Hình nền**. Giữ bundle Mate, Wallpaper, shader chung bắt buộc và âm thanh `SE_MATE`; bootstrap không còn đọc CDB/CID hay danh mục nhân vật Yu-Gi-Oh để lấy tên chúng.
- **Hoạt cảnh** cho xem thử cách trình diễn artwork của **BS01-X01** và **BS01-097**. Hai thẻ đó thuộc Eternal, **không vào deck Standard**. Nguồn model low-poly/closeup tự dựng trước đây vẫn được lưu trong repository, nhưng không còn làm hình quái trên sân.
- Nhạc nền và tám cue WAV mới ở `Sound/BattleSpirits`. Xem [nguồn tài nguyên tự tạo](Tools/BattleSpiritsAssets/README.md).

Các thư mục cũ được giữ bằng `.gitkeep` khi rỗng. Manifest ghi chính xác từng đợt dọn dữ liệu:

- [Cut-in và giọng nhân vật cũ](Documentation/master-duel-initial-cleanup.json).
- [Bundle/âm thanh ngoài pet, wallpaper và phụ thuộc cần giữ](Documentation/master-duel-external-cleanup.json).
- [Đồ họa, font game cũ và tham chiếu UI được thay](Documentation/master-duel-art-replacement.json).
- [CDB/CID, voice và dữ liệu Yu-Gi-Oh không còn dùng](Documentation/master-duel-data-cleanup.json).

Mã template tổng quát, thư viện và các tên lớp/namespace cần tương thích với bundle vẫn được giữ. Việc giữ những định danh đó không đưa gameplay hay đồ họa duel Master Duel trở lại game.

## Luật, thẻ và Solo

Bản này dùng **Standard Official Rules/Manual v1.0** được cung cấp trong ba PDF. Pool được xác minh cho mùa **01/04/2026–31/03/2027: block A**. Thứ tự lượt gồm **Main → Attack → Second Main → End**; người đi trước bỏ Core, Attack và Second Main ở lượt đầu. Second Main cho phát triển sân/chuyển Core, không cho tấn công lần nữa. [Đối chiếu PDF và Floor Rules](Documentation/BATTLE_SPIRITS_RULE_SOURCES.md).

19 thẻ Standard đã kiểm tra thuộc 26RBS01/02/03. Ba thẻ Super Unleash là `26RBS03-052`, `26RBS03-061`, `26RBS03-071`: Soul Core cho Lv2 chỉ trên các bản in đã xác minh có biểu tượng đó. Không áp dụng Soul Core → Lv2 cho mọi Spirit. Soul Magic, Succession và các hiệu ứng chưa lập trình vẫn bị khóa khỏi trận tự động.

Deck ít nhất 40 lá, tối đa ba lá cùng tên theo pool/hạn chế đã xác minh. Ba bộ mẫu tại `Data/BattleSpirits/StarterDecks` được xuất từ API profile WindBot Standard thực tế. Deck cá nhân lưu ở `Data/BattleSpirits/Decks`; replay Solo hoàn tất ở `Data/BattleSpirits/Replays`. Deck/replay cá nhân không được Git theo dõi. Bản replay 0.2.0/Eternal không được coi là replay Standard; bản 0.4.0 giữ luật, danh mục và giao thức 3 của 0.3.0.

AI chỉ nhận tay bài của nó và thông tin công khai. Có ba mức ưu tiên, xử lý cả Second Main và Super Unleash được hỗ trợ. Đây là AI heuristic cho pool hiện có, chưa hiểu toàn bộ thẻ Standard.

Danh mục được mở rộng và đối chiếu Standard ngày **04/11/2026**; ngày truy cập gốc của từng thẻ được giữ trong `card-sources.json`. Có BS76 Eternal, 26RBS03 Standard và các baseline đã kiểm tra; không bao gồm toàn bộ lịch sử thẻ. [Công cụ thẻ](Tools/BattleSpiritsCards/README.md) ghi nguồn, checksum, banlist và cách nhập lại.

```bash
python Tools/BattleSpiritsCards/catalog.py validate
python Tools/BattleSpiritsCards/catalog.py check
```

## LAN và IP trực tiếp

Vào **Chơi mạng**. Một máy tạo phòng TCP **7777**; máy khác nhập IP và cổng, chọn deck rồi bấm **Sẵn sàng**. Chủ phòng bắt đầu khi cả hai sẵn sàng. Hai client cần cùng **Standard, danh mục và giao thức 3**; server từ chối khác thể thức hoặc bản giao thức cũ.

- Hai client cùng máy: `127.0.0.1`.
- Hai máy cùng router: dùng IPv4 LAN của máy tạo phòng, cho phép game/Unity qua Windows Firewall trên mạng riêng.
- IP Internet trực tiếp cần server/cổng truy cập được; chưa có MyCard, ghép trận, xếp hạng, tự mở cổng hoặc reconnect tiếp tục trận.

Server kiểm tra deck, actor và luật; snapshot chỉ có tay bài của người nhận cùng thông tin công khai. Có thể dùng [server console](Tools/BattleSpiritsServer/README.md); mặc định là Standard. Floor Rules của giải đấu không tự bật đồng hồ hay hình phạt trong Solo/LAN.

## Build Windows

Cài Windows Build Support đúng Editor. Với cấu hình IL2CPP cần công cụ C++ và Windows SDK. Build vào **thư mục đầu ra mới/rỗng**, ví dụ `Builds/Windows`, với Addressables được build mới từ source hiện tại. Allowlist chép tài nguyên cần dùng nhưng không tự xóa tệp cũ còn trong một bản build trước.

Bước hậu xử lý `BattleSpiritsWindowsPackage` chép dữ liệu Battle Spirits, UI translations, `Settings.json`, ảnh/âm thanh mới và allowlist **Mate + Wallpaper + shader dùng chung**. Không đóng gói toàn bộ Master Duel hoặc dữ liệu CDB/CID/nhân vật cũ. Bundle giữ đường dẫn `<tên-game>_Data/StandaloneWindows64/MasterDuel` để tương thích với pet/wallpaper; `Data`, `Picture`, `Sound` nằm cạnh `.exe`. Cấu hình cá nhân, đăng nhập và deck/replay tự tạo không được lấy để đóng gói.

**Bước build này chưa được thực thi trong Windows Unity.** Cần thử game độc lập và gửi toàn bộ thư mục build cho người khác; họ không cần Unity.

## Nguồn và bản sao lưu

Template: [MDPro3 của Sherry Chaos](https://code.moenext.com/sherry_chaos/MDPro3), tùy biến bởi LaoDai060204. Giữ [thông tin template](Documentation/MDPRO3_TEMPLATE.md) và [ghi công/giấy phép](THIRD_PARTY_NOTICES.md). Artwork và văn bản thẻ Battle Spirits thuộc các chủ sở hữu, lấy từ [website chính thức](https://www.battlespirits.com/cardlist/). Đây là dự án cộng đồng độc lập.

Backup trước đợt đổi Standard/dọn Master Duel: **`backup/2026-11-04-before-master-duel-cleanup` → `c70747a0`**. Các backup 0.1/0.2 trước đó và `main` được giữ, không force-push hay ghi đè.

Backup trước phần trình diễn thẻ 0.4.0: **`backup/2026-11-05-before-card-summon-animation` → `c19c2d67`**.
