# Kiểm tra Standard và cách trình diễn thẻ

Bản **0.7.0** có 19 model skinned 3D theo đúng ID/artwork Standard, shader ánh sáng/rim/specular, bóng tiếp xúc mềm, BP rõ hơn và ba mức render. **124 luồng UI/session đạt**, gồm 15 kiểm tra mới: mesh khối/normal/winding/bone weight, bind pose dưới parent có TRS, cache/dọn tài nguyên, mọi pose với giá trị biên, 3D/ảnh/fallback, quyền dữ liệu công khai, cấu hình/quality resize, cinematic đáp đúng vùng sân và reveal model không đổi luật, status không che tay bài, BP và gallery Standard. Đã xuất đúng geometry production và render cả 19 model bằng Blender; ảnh xem trước là studio review, không phải Unity. 11 tệp production × ba profile C# 9 không lỗi cú pháp; 9 prefab, 12 tài nguyên/restoration, 4 cấu trúc Boot, 2 PNG và metadata 67 GUID đạt. **Chưa biên dịch shader hoặc Play/render/input/build Unity Windows; chưa đo FPS/frame time.** [Hướng dẫn và ảnh](BATTLE_SPIRITS_3D_SPIRITS.md), [manifest](../Tools/BattleSpiritsAssets/spirit-model-manifest.json), [báo cáo](battle-spirits-3d-spirit-validation.json).

Bản **0.6.5** sửa nền khung soi trắng và chỉnh phần nhìn theo phong cách Master Duel: chữ sáng trên nền xanh đen, tay bài quạt/nâng khi chọn hoặc rê chuột, HUD gọn và sân navy/slate. **109 luồng UI/session đạt**, gồm bảy kiểm tra mới về màu/tương phản khai báo, mặt thẻ đầy đủ, vùng bấm đứng yên khi hover, lựa chọn đúng mã, tay đông/resize, vị trí hướng dẫn và vật liệu sân dùng lại. Hai kiểm tra PNG nguồn xác nhận khung là mặt nạ trắng cần tô tối, còn nút có tâm đen. 9 kiểm tra prefab, 12 tài nguyên/restoration, 4 cấu trúc Boot và metadata conversion đạt; hai tệp production × ba profile C# 9 không lỗi cú pháp. **Chưa render/input/build Unity Windows hoặc đo FPS; phép kiểm tra màu không đo pixel màn hình Unity.** [Cách thử](BATTLE_SPIRITS_MASTER_DUEL_STYLE.md) và [báo cáo](battle-spirits-master-duel-style-validation.json).

Bản **0.6.4** chỉnh camera/ảnh sân theo vùng hiển thị, phục hồi khung soi thẻ native bị ẩn ngoài màn hình, xem mặt thẻ gốc/phóng to và tách thao tác soi khỏi đánh bài. **102 luồng UI/session đạt**, gồm 18 kiểm tra mới: geometry từ prefab thật, ảnh đúng mã, BP/Core cập nhật, lựa chọn cũ, popup Magic, focus/khóa callback khi phóng to, nút quay lại, pooling, lưng bài đối thủ và resize/render-target lifecycle. Ba tỷ lệ sân và trường hợp sân đông được kiểm tra bằng phép chiếu/API thay thế. 5 tệp production × 3 profile C# 9 không lỗi cú pháp; 9 kiểm tra prefab, 12 tài nguyên/restoration, 4 cấu trúc Boot và metadata conversion đạt. **Chưa chạy render/input hoặc build bằng Unity Windows, chưa đo FPS/độ nét thực.** [Cách chơi/thử](BATTLE_SPIRITS_DUEL_READABILITY.md) và [báo cáo](battle-spirits-duel-readability-validation.json).

Bản **0.6.3** gom bản sao trong danh sách deck, thêm badge/viền/trạng thái, ba cỡ thẻ và sửa tương phản ô nhập/tab. **84 luồng UI/session đạt**, gồm chín kiểm tra mới và các kiểm tra cũ được giữ theo cách hiển thị gom nhóm. Lưu deck vẫn giữ đủ bản sao; đổi mật độ không tạo lại ô/tải ảnh/tính lại truy vấn trong harness. Tương phản được kiểm tra bằng màu trạng thái khai báo, không phải phép đo pixel. **Chưa chạy Unity Windows.** [Hướng dẫn](BATTLE_SPIRITS_CARD_BROWSER.md) và [báo cáo](battle-spirits-card-browser-polish-validation.json).

Bản **0.6.2** sửa lỗi thực của prefab: ô tìm kiếm thiếu vùng raycast/targetGraphic, collection grid bị tắt và label rỗng độc lập không theo kết quả. Bố cục runtime giãn ba khung theo SafeArea, phóng lớn preview và sửa vùng cuộn. Fixture nay đọc enabled/raycast, các tham chiếu TMP/ScrollRect, layout và anchor/size từ prefab. **75 luồng UI/session đạt**, gồm tám kiểm tra mới cho những lỗi này và hợp đồng hình học 16:9/rộng/4:3. Đây vẫn là kiểm tra bằng API thay thế; **chưa thực thi GraphicRaycaster, gõ phím, layout pass hoặc render trong Unity Windows**. [Hướng dẫn](BATTLE_SPIRITS_DECK_INPUT_LAYOUT.md) và [báo cáo](battle-spirits-deck-input-layout-validation.json).

Bản **0.6.1** dùng lại luồng Boot → menu MDPro3 và tối ưu Bộ bài. [Báo cáo Bộ bài](battle-spirits-deck-optimization-validation.json) ghi **67 luồng UI/session**, gồm 10 tình huống mới kiểm tra pool, kết quả tìm kiếm, lá trùng, ảnh tải muộn và bản nháp khi mở lại. Sau khi pool sẵn sàng, tám lần refresh cùng truy vấn và 12 vòng phân trang không tạo thêm GameObject. Các kiểm tra cấu trúc đạt 9 prefab, 12 tài nguyên, 4 scene Boot; năm tệp C# qua ba profile C# 9 không có lỗi cú pháp. **Chưa đo FPS hoặc chạy Play/render bằng Unity Windows.** [Cách thử bản hiện tại](BATTLE_SPIRITS_DECK_OPTIMIZATION.md). Các kết quả title 0.6.0 bên dưới là kiểm tra helper lưu lại; Boot hiện tại không dùng title đó.

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

1. Mở Boot đúng Editor, Addressables **Use Asset Database**. Xác nhận menu MDPro3 **0.6.3**; thử số lượng lá trùng, cỡ thẻ, tìm kiếm/hover/tab và lưu deck theo [hướng dẫn hiện tại](BATTLE_SPIRITS_CARD_BROWSER.md).
2. Chọn một trong ba bộ Standard. Giữ/đổi tay đầu, triệu hồi bằng click, Core/Soul, Super Unleash, Main → Attack → Second Main → End; lượt đầu không có Core/Attack/Second Main.
3. Về menu rồi chơi ván tiếp; kiểm tra field/pet và âm thanh không mất hoặc lặp. Thử chọn pet và **Cài đặt → Hình nền**, quay lại menu và khởi động lại để kiểm tra lựa chọn được giữ.
4. Hoạt cảnh vẫn xem hai boss BS01 nhưng không cho đưa chúng vào deck Standard. Lịch sử cho tiến/lùi replay Standard cùng version/catalog.
5. Chạy hai client TCP 7777 cùng máy rồi hai máy Windows cùng LAN; kiểm tra sẵn sàng, trận hoàn chỉnh, mất mạng, tạo phòng mới và sai phiên bản/format.
6. Build bằng Addressables mới vào thư mục đầu ra mới/rỗng; copy allowlist không tự dọn tệp tồn từ build cũ. Kiểm tra đóng gói allowlist và chạy độc lập trên máy sạch trước khi chia sẻ.

**Chưa chạy Editor Play, render/audio Unity, Windows build, LAN hai máy Windows hoặc triển khai Internet công khai trong môi trường này.**
