# Kiểm tra Standard và cách trình diễn thẻ

Bản **0.6.0** thêm màn mở đầu theo trình tự Master Duel chính thức: title Battle Spirits → nhấn bắt đầu → menu. [Báo cáo title](battle-spirits-title-validation.json) ghi **35 tình huống startup/title** và **57 luồng UI/session**, gồm 12 kiểm tra mới của luồng chờ bắt đầu và tám kiểm tra view thực với API Unity thay thế. Năm tệp production C# qua ba profile C# 9 không có lỗi cú pháp; Boot, MainMenu đầy đủ, input thiết bị và khóa FPS chỉ được đọc mã/phân tích cú pháp. Chưa xác minh hình ảnh/chuyển cảnh bằng Unity Windows. [Cách chạy thử](BATTLE_SPIRITS_TITLE_SCREEN.md).

Bản **0.5.2** sửa chuyển màn Boot → Main và vòng đời wallpaper. [Báo cáo khởi động](battle-spirits-startup-validation.json) ghi **23 tình huống startup state**, **49 luồng UI/session** (gồm bảy kiểm tra mới cho loader wallpaper), bốn kiểm tra cấu trúc Boot, 9 kiểm tra prefab và 12 kiểm tra tài nguyên đạt. Bảy tệp production C# qua ba profile C# 9 không có lỗi cú pháp. **Boot, Program, UIManager và Servant đầy đủ chỉ được đọc mã/phân tích cú pháp; chưa biên dịch chúng bằng Unity hoặc xác minh chuyển cảnh/pixel/input trên Windows.** [Cách thử](BATTLE_SPIRITS_STARTUP.md).

Bản **0.5.1** sửa hiển thị Core bằng ảnh ngọc, thêm ngoại hình Core thường và giữ đúng tỷ lệ ảnh thẻ. [Báo cáo ngọc/ảnh](battle-spirits-core-gem-quality-validation.json) ghi **42 luồng UI/session đạt**, gồm kiểm tra ảnh gốc 813×1184, bộ lọc/tỷ lệ, cache và các màu Core không đổi trạng thái trận. Guard độ phân giải camera UI được kiểm tra bằng đọc mã; chưa xác minh độ nét thực tế trong Windows Unity. Các kết quả phiên bản trước được giữ bên dưới như baseline.

Bản **0.5.0** khôi phục chrome/bảng màu UI gốc có kiểm soát và thêm skin Battle Spirits. [Báo cáo theme](battle-spirits-theme-validation.json) ghi nhận 38 luồng UI/session, 9 kiểm tra prefab, 12 kiểm tra tài nguyên/restoration, 14 ảnh đúng hash/kích thước/importer; 6 tệp production C# qua ba profile không có lỗi cú pháp. Đây là kiểm tra mã, dữ liệu và API thay thế, **chưa kiểm tra pixel, shader hoặc Play/build trong Windows Unity**. [Hướng dẫn theme](BATTLE_SPIRITS_THEME.md) ghi rõ nguồn ảnh và khác biệt với ảnh inline.

Bản **0.4.0** thêm hoạt ảnh triệu hồi bằng artwork, mặt thẻ trên sân và ảnh minh họa nổi. Kiểm tra mới nằm trong [báo cáo triệu hồi](battle-spirits-summon-validation.json) và [hướng dẫn trình diễn](BATTLE_SPIRITS_SUMMON_PRESENTATION.md). Bộ luật, giao thức, danh mục và artwork gốc không đổi; các kết quả luật/AI/TCP bên dưới là baseline đã chạy cho **0.3.0**, không phải một lượt chạy lại cho tính năng hình ảnh.

Kiểm tra riêng cho 0.4.0 đạt **32 luồng UI/session**, **29 tình huống chuyển động/PCM**, 4.080 mẫu chuyển động và 515 mẫu triệu hồi; 9 tệp production C# qua ba profile C# 9 không có lỗi cú pháp. Hai bộ kiểm tra prefab/closure vẫn đạt 9 + 9; metadata ghi nhận 44 GUID BattleSpirits và 10 source project. Bộ kiểm tra dùng API Unity thay thế và phép chiếu hình học kiểm soát, chưa xác nhận shader/pixel/raycast hoặc Play/build bằng Editor Windows thật.

Ngày tổng hợp **05/11/2026**. Unity mục tiêu **6000.0.24f1**, Windows x64; nhánh `battle-spirits-mdpro3-ui`. Backup trước đợt này: **`backup/2026-11-04-before-master-duel-cleanup` → `c70747a0`**. `main` giữ bản Card-Game/Yu-Gi-Oh.

| Phần kiểm tra | Kết quả |
| --- | --- |
| Core Standard | 17 tình huống mới và 12 trận Standard hoàn chỉnh, gồm Second Main/lượt đầu/pool A/copy limit/Super Unleash |
| Core tương thích | 62 tình huống cũ: 15 gốc + 19 chuyển đổi + 28 hiệu ứng; 96 trận mô phỏng và 24 cặp dispatcher cùng seed |
| Audit engine độc lập | 100 trận, 8.853 trạng thái, 79.677 thao tác thử |
| WindBot | 30 tình huống, 101 trận hoàn chỉnh: 81 Eternal + 20 Standard; 5.984 thao tác authority |
| TCP thực | 23 tình huống: 16 Eternal + 7 Standard; 104 thao tác trong lượt chạy đã ghi nhận, có trận hoàn chỉnh giữa hai client loopback Linux |
| Server console thực | 10 tình huống; mặc định Standard và tùy chọn Eternal, hai client thực cho mỗi host, Ctrl+C dừng đúng |
| Native UI/session | 23 luồng production; fixture prefab thật, Unity API được thay bằng stand-in kiểm soát |
| Prefab/UI art | 9 kiểm tra hierarchy/geometry/GUID/controller và 9 kiểm tra closure/import cho skin mới |
| Parser/dữ liệu | 30 tình huống; 270 bản ghi/ảnh; 19 thẻ Standard hỗ trợ, BS01 không lọt vào deck Standard |
| Catalog cosmetic production | 12 tình huống; 108 pet, 149 wallpaper; allowlist 647 bundle: 184 Mate + 462 Wallpaper + 1 shader chung |
| Chuyển động/âm thanh thuần | 24 tình huống, 4.080 mẫu chuyển động; 9 WAV được kiểm tra hash/PCM/duration/headroom/DC/đầu-cuối |
| Dự án/metadata | 40 GUID trong cây BattleSpirits duy nhất; 10 source project dùng tham chiếu tương đối |
| Tài nguyên render | Checksum `.blend` và ba PNG đúng manifest; kích thước PNG đúng |
| Cú pháp production | 31 tệp C# mới/thay đổi trong Assets so với `c70747a0`, ba profile C# 9; 0 lỗi cú pháp |

Các số trên mô tả harness đã chạy, không phải số tính năng đầy đủ của Standard. Core, Networking và executor AI nhắm **.NET Standard 2.1/C# 9**; test/console dùng SDK .NET 10 ngoài Unity. Người mở game trong Unity không cần SDK .NET 10.

Lượt Roslyn đã phân tích 31 tệp production C# mới/thay đổi trong Assets so với `c70747a0` trên ba profile Editor/Windows, Editor/Android và standalone Windows, với 0 lỗi cú pháp. Đây chỉ là phân tích cú pháp, **không phải biên dịch API Unity**. Các luồng UI production được biên dịch với stand-in cũng không thay thế Editor thật.

TCP action count có thể đổi theo seed/xáo bài. Kết quả 40/40 của AI trước baseline chọn lệnh hợp lệ ngẫu nhiên thuộc kiểm tra Eternal có kiểm soát; không được quảng bá là năng lực thi đấu Standard.

## Phạm vi kiểm tra tài nguyên

- [Manifest cut-in/voice](master-duel-initial-cleanup.json): 64.509 tệp, 4.589.665.895 byte.
- [Manifest external](master-duel-external-cleanup.json): 2.686 tệp bổ sung, 1.865.808.352 byte; pet/wallpaper/shader được so hash trước-sau.
- [Manifest art](master-duel-art-replacement.json): 10.508 asset, 1.676.343.210 byte **không tính `.meta`**; map tham chiếu UI sang skin mới, kiểm tra không còn GUID đã xóa trong Assets.
- [Manifest data](master-duel-data-cleanup.json): 3.648 tệp, 256.724.684 byte; giữ BattleSpirits, `Settings.json`, UI translations và cấu trúc thư mục.

Các số theo manifest có đơn vị khác nhau; không cộng số asset với số tệp Git hoặc `.meta` thành một tổng sai. Những đợt dọn trên chỉ mô tả tài nguyên/dữ liệu; mã template/namespace tương thích cần cho generic UI và pet/wallpaper vẫn được giữ.

Harness native kiểm tra binding từ metadata prefab thật, bộ sưu tập/deck, Solo, lobby, opening, Second Main, chặn thẻ Eternal ở form Standard, replay, preview, quyền khi ngắt mạng, render-target lifecycle và ảnh tải muộn. UnityEngine, TMP, Addressables, tải bundle và thiết bị được thay bằng API kiểm soát. Test cosmetic biên dịch helper production và kiểm tra catalog/path/allowlist; **không giải mã hoặc render bundle bằng Unity**.

Model/menu/closeup đã render bằng Blender 4.3.2/Cycles CPU. Kiểm tra đường cong/PCM độc lập không xác nhận shader, font, chuột/gamepad, AudioSource hoặc giao diện pixel trong Unity Windows.

## Chạy lại

Từ thư mục gốc:

```bash
python Tools/BattleSpirits/validate_conversion.py
python Tools/BattleSpirits/test_native_prefabs.py
python Tools/BattleSpirits/test_ui_art_cleanup.py
python Tests/BattleSpirits/Startup/test_boot_scene.py
dotnet run --project Tests/BattleSpirits/Startup/BattleSpirits.Startup.Tests.csproj -c Release
python Tools/BattleSpiritsCards/catalog.py validate
python -m unittest discover -s Tools/BattleSpiritsCards -p 'test_*.py'
dotnet run --project Tests/BattleSpirits/Core/BattleSpirits.Core.Tests.csproj -c Release -- Data/BattleSpirits/cards.json
dotnet run --project Tests/BattleSpirits/AI/BattleSpirits.AI.Tests.csproj -c Release -- Data/BattleSpirits/cards.json
dotnet run --project Tests/BattleSpirits/Network/BattleSpirits.Network.Tests.csproj -c Release -- Data/BattleSpirits/cards.json
python Tests/BattleSpirits/Network/test_console.py --dotnet /path/to/dotnet
dotnet run --project Tests/BattleSpirits/Presentation/BattleSpirits.Presentation.Tests.csproj -c Release -- /path/to/Card-Game
dotnet run --project Tests/BattleSpirits/Cosmetics/BattleSpirits.Cosmetics.Tests.csproj -c Release -- /path/to/Card-Game
dotnet run --project Tests/BattleSpirits/Visuals/BattleSpirits.Visuals.Tests.csproj -c Release -- /path/to/Card-Game
```

Thay đường dẫn mẫu bằng đường dẫn trên máy. `catalog.py validate --pixels` kiểm tra decoded pixel nếu có ImageMagick; cache WebP nguồn cho phép kiểm tra thêm equality nguồn/PNG. Không gộp kết quả đó thành kiểm tra Unity.

## Cần kiểm tra bằng Windows Unity

1. Mở Boot đúng Editor, Addressables **Use Asset Database**. Kiểm tra màn mở đầu **0.6.0**, wallpaper đã chọn và thao tác nhấn bắt đầu theo [hướng dẫn title](BATTLE_SPIRITS_TITLE_SCREEN.md); thử khởi động lại, chuột, phím và gamepad. Bố cục các form và nhãn nút được giữ.
2. Chọn một trong ba bộ Standard. Giữ/đổi tay đầu, triệu hồi bằng click, Core/Soul, Super Unleash, Main → Attack → Second Main → End; lượt đầu không có Core/Attack/Second Main.
3. Về menu rồi chơi ván tiếp; kiểm tra field/pet và âm thanh không mất hoặc lặp. Thử chọn pet và **Cài đặt → Hình nền**, quay lại menu và khởi động lại để kiểm tra lựa chọn được giữ.
4. Hoạt cảnh vẫn xem hai boss BS01 nhưng không cho đưa chúng vào deck Standard. Lịch sử cho tiến/lùi replay Standard cùng version/catalog.
5. Chạy hai client TCP 7777 cùng máy rồi hai máy Windows cùng LAN; kiểm tra sẵn sàng, trận hoàn chỉnh, mất mạng, tạo phòng mới và sai phiên bản/format.
6. Build bằng Addressables mới vào thư mục đầu ra mới/rỗng; copy allowlist không tự dọn tệp tồn từ build cũ. Kiểm tra đóng gói allowlist và chạy độc lập trên máy sạch trước khi chia sẻ.

**Chưa chạy Editor Play, render/audio Unity, Windows build, LAN hai máy Windows hoặc triển khai Internet công khai trong môi trường này.**
