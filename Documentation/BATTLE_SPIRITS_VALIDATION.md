# Kiểm tra bản thay thế trực tiếp 0.2.0

Ngày tổng hợp **04/11/2026**. Unity mục tiêu **6000.0.24f1**, Windows x64; nhánh `battle-spirits-mdpro3-ui`. Snapshot trước kiểm tra cuối: `88d80baa`, đã lưu ở `backup/2026-11-04-native-replacement-wip`. `main` giữ Card-Game/Yu-Gi-Oh tại `db9007d1`.

| Phần kiểm tra | Kết quả |
| --- | --- |
| Core | 62 tình huống: 15 gốc + 19 chuyển đổi + 28 hiệu ứng; 96 trận mô phỏng và 24 cặp dispatcher cùng seed |
| Audit engine độc lập | 100 trận, 8.853 trạng thái, 79.677 thao tác thử |
| WindBot Battle Spirits | 24 tình huống, 81 trận hoàn chỉnh, 4.793 thao tác authority; Normal thắng 40/40 trước baseline chọn lệnh hợp lệ ngẫu nhiên |
| TCP thực với 261 thẻ | 16 tình huống, 48 thao tác trong lượt chạy này; có trận hoàn chỉnh giữa hai client loopback Linux |
| Server console thực | 5 tình huống, hai client TCP, mở đầu/nhận thua và dừng Ctrl+C |
| Native UI/session | 21 luồng production, fixture từ prefab thật, Unity API được thay bằng stand-in kiểm soát |
| Prefab gốc | 9 kiểm tra hierarchy/geometry/art/GUID, controller mới và gỡ callback cũ cho năm màn + ba Parts |
| Parser/dữ liệu | 24 tình huống; 261 bản ghi/ảnh, 16 vanilla + 5 thẻ hiệu ứng được bật, 240 tra cứu |
| Chuyển động/âm thanh thuần | 24 tình huống, 4.080 mẫu chuyển động; 9 WAV được kiểm tra hash/PCM/duration/headroom/DC/đầu-cuối |
| Dự án/metadata | 40 GUID tài nguyên chuyển đổi duy nhất; 9 source project dùng tham chiếu tương đối |
| Tài nguyên render | Checksum của source `.blend` và ba PNG đúng manifest; kích thước PNG đúng |
| Cú pháp production | 33 tệp production thay đổi trong Assets so với `9d1d1055`, C# 9, ba profile Editor/Windows, Editor/Android và standalone Windows; không có lỗi cú pháp |

Các con số trên mô tả những harness đã chạy, không phải số tính năng đầy đủ của Battle Spirits. Chi tiết phạm vi thẻ ở [BATTLE_SPIRITS_SCOPE.md](BATTLE_SPIRITS_SCOPE.md).

Số thao tác TCP là số đo của lượt chạy ngày 04/11/2026 và có thể đổi theo seed/xáo bài. Kiểm tra danh sách booster mới nhất cùng ngày vẫn trả BS76 (Eternal) và 26RBS03 (Standard); dữ liệu/ảnh thẻ gốc giữ ngày đối chiếu 31/10/2026.

Core, Networking và executor WindBot nhắm **.NET Standard 2.1/C# 9**. Các test/server console dùng SDK .NET 10 bên ngoài Unity. Người mở game trong Unity không cần SDK .NET 10.

Lượt Roslyn ba profile chỉ phân tích cú pháp các tệp C# đã nêu, không biên dịch API Unity. Việc biên dịch luồng native production bên dưới dùng stand-in và cũng không thay thế biên dịch trong Editor thật.

Harness native biên dịch controller màn hình, session, Core/Network, WindBot và Visuals production. Nó dùng metadata prefab thật để kiểm tra tìm control/binding, tìm thẻ/phân trang, deck, Solo, lobby, lệnh mở đầu, replay, preview boss, quyền thao tác khi ngắt kết nối, vòng đời render target và ownership giữa ảnh mặt thẻ/closeup. UnityEngine, TMP, Addressables, tải bundle và thiết bị được thay bằng API kiểm soát. Bài này không xác nhận pixel, shader, font, kích thước chữ, chuột/gamepad hoặc AudioSource trong Editor thật.

Model/menu/closeup mới đã được dựng và render bằng **Blender 4.3.2/Cycles CPU**, có manifest ảnh và source `.blend`. Đây là kiểm tra tài nguyên tạo ra; render Blender không thay thế render Unity. Các đường cong động và WAV được kiểm tra riêng, chưa kiểm tra phát âm thanh thật trên Windows.

Các lỗi được xử lý trong đợt ghép: đường dẫn Filter/Host không trùng tên GO thật; callback của SelectionToggle; spinner cũ; authority UI sau mất kết nối; render target của form Duel được cache qua ván mới; ảnh tải muộn; replay hỏng ở thao tác phía sau; số học đồng hồ chuyển động rất lớn. Core có kiểm tra tiếp tục Flash khi Spirit biến mất và trao đổi Soul/Core đồng thời.

## Chạy lại

Từ thư mục gốc:

```bash
python Tools/BattleSpirits/validate_conversion.py
python Tools/BattleSpirits/test_native_prefabs.py
python Tools/BattleSpiritsCards/catalog.py validate
python -m unittest discover -s Tools/BattleSpiritsCards -p 'test_*.py'
dotnet run --project Tests/BattleSpirits/Core/BattleSpirits.Core.Tests.csproj -c Release -- Data/BattleSpirits/cards.json
dotnet run --project Tests/BattleSpirits/AI/BattleSpirits.AI.Tests.csproj -c Release -- Data/BattleSpirits/cards.json
dotnet run --project Tests/BattleSpirits/Network/BattleSpirits.Network.Tests.csproj -c Release -- Data/BattleSpirits/cards.json
python Tests/BattleSpirits/Network/test_console.py --dotnet /path/to/dotnet
dotnet run --project Tests/BattleSpirits/Presentation/BattleSpirits.Presentation.Tests.csproj -c Release -- /path/to/Card-Game
dotnet run --project Tests/BattleSpirits/Visuals/BattleSpirits.Visuals.Tests.csproj -c Release -- /path/to/Card-Game
```

Thay các đường dẫn mẫu bằng đường dẫn trên máy. Pixel đối chiếu WebP/PNG có thể kiểm tra thêm bằng `catalog.py validate --pixels` khi có cache nguồn và ImageMagick; kết quả đó không được gộp thành kiểm tra Unity.

## Cần kiểm tra bằng Windows Unity

1. Mở Boot bằng đúng Editor, Addressables **Use Asset Database**. Kiểm tra menu, bản 0.2.0, Bộ bài và các bộ lọc/ảnh/font trong form gốc.
2. Chơi Solo bằng ba bộ mẫu; kiểm tra lựa chọn Main/Flash, Magic, chuyển Core/Soul, Nexus, thắng/thua và phản hồi AI.
3. Về menu, mở lại Solo, chơi ván tiếp theo; kiểm tra field/pet, render target và âm thanh không bị mất hoặc lặp.
4. Mở Hoạt cảnh, xem cả hai boss; mở Lịch sử và tiến/lùi replay.
5. Chạy hai client TCP 7777 cùng máy, rồi hai máy Windows cùng LAN. Kiểm tra chọn deck, sẵn sàng, trận hoàn chỉnh, mất kết nối và tạo ván mới.
6. Build vào thư mục riêng, dùng bundle Addressables mới. Kiểm tra bước đóng gói tài nguyên và chạy độc lập bản Windows trước khi chia sẻ.

**Chưa chạy Editor Play, render Unity, Windows build, LAN hai máy Windows hoặc triển khai Internet công khai trong môi trường này.**
