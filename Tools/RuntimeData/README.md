# Dữ liệu runtime của Card-Game

Editor mục tiêu của dự án này vẫn là **6000.0.24f1**. Dự án Battle Spirits đang tạm dừng và nằm ở nhánh riêng.

## Puzzle gốc

104 tệp `.lua` được lấy từ `Puzzle/` của:

- Repository: <https://code.moenext.com/mycard/mdpro3-other>
- Commit: `3a7423e6b389c413cc64b157dc213ab67b2ae551`
- Git tree: `4da0f2aaa80190b12b0b001c9753aadad26961be`
- Nguồn, kích thước và checksum từng tệp: `puzzle-source.json`.

Các tệp giữ nguyên nội dung và tên từ nguồn. Có 55 tệp ở gốc, 40 CardEffect, 6 FinalAttack và 3 SpecialWin. Thư mục này phải nằm ở gốc project, cạnh `Assets`.

### Phụ thuộc của các màn WCS cũ

Bộ `Data/locales/en-US/cards.cdb` hiện có 15.019 ID. Có 99 màn đủ ID thẻ tham chiếu trực tiếp; năm màn sau còn thiếu dữ liệu phiên bản cũ và script tương ứng:

| Màn | ID còn thiếu |
| --- | --- |
| `[WCS2006]34_Riddle Challenge` | `511000228` |
| `[WCS2006]35_Interdimensional Invitation` | `511001039` |
| `[WCS2008] Download Puzzle 07` | `511002997` |
| `[WCS2011] Download Puzzle 01` | `511002993` |
| `[WCS2011] Duel Puzzle 04` | `511000229`, `511002995` |

Các ID này không có trong những CDB hoặc `Data/script.zip` đang đi kèm. Không đổi chúng sang thẻ hiện hành vì có thể làm sai lời giải. Chưa tìm được một gói phụ thuộc tương thích đã xác minh từ nguồn gốc.

Game kiểm tra các ID literal trong `Debug.AddCard(...)` trước khi mở màn và hiển thị thông báo tiếng Việt nếu dữ liệu thẻ đang nạp chưa có chúng. Đây là bước kiểm tra dữ liệu cơ bản, không thay thế việc chạy trận hoặc xác minh mọi script/hiệu ứng. Kiểm tra dùng dữ liệu thực tế của game, gồm gói mở rộng đã bật.

```bash
python Tools/RuntimeData/validate_puzzles.py
```

Lệnh kiểm tra checksum, Git tree, đường dẫn Windows, UTF-8 và độ phủ ID trong CDB. Trên Linux có thể thêm `--native-lua` để biên dịch cú pháp bằng Lua của `Assets/Plugins/Linux/libocgcore.so`. Công cụ không thực thi các màn chơi hoặc bắt đầu trận.

## Ánh xạ CID/YDK

`Data/cards_Lite.json` chứa **14.390 ánh xạ**, chỉ giữ hai trường `cid` và `id`. Nguồn YGOCDB có 14.343 mục, được gộp 93 mục ghi đè trong `Data/cards_Alt.json`; một số mục ghi đè thêm CID mới. `cards-source.json` ghi checksum đầu vào và đầu ra.

Nguồn tải `cards.json`: <https://ygocdb.com/api/v0/cards.zip>. Snapshot được ghi bằng SHA-256 trong `cards-source.json`. Không cần tải JSON đầy đủ để chạy bản đã clone vì file lite đã có trong Git.

Để dựng lại từ dữ liệu chính thức đã tải và giải nén:

```bash
python Tools/RuntimeData/build_cards_lite.py --source /path/to/cards.json --source-archive /path/to/cards.zip
```

Quy tắc alternate-art của project luôn được áp dụng khi game nạp ánh xạ, kể cả khi lite đã tồn tại. Nếu lite bị hỏng nhưng `cards.json` hợp lệ có sẵn, game dùng file đầy đủ rồi thử tạo lại cache; lỗi ghi cache không làm mất dữ liệu đã nạp.

## Kiểm tra hồi quy

Các bộ kiểm tra dưới đây dùng .NET SDK 10 riêng cho công cụ; Unity vẫn dùng phiên bản Editor và runtime của chính nó.

```bash
dotnet run --project Tools/Tests/RuntimeData/RuntimeData.Tests.csproj -c Release -- Data/cards_Lite.json
python Tools/Tests/PuzzleRegression.py
python Tools/Tests/validate_network_metadata.py
python Tools/Translations/validate_vietnamese.py
```

Hai script Python kiểm tra C# nhận tham số `--dotnet /path/to/dotnet` nếu SDK không nằm trong PATH. Bộ Cid2Ydk biên dịch tệp production trực tiếp; hai harness còn lại trích các phương thức production. Chúng không thay thế biên dịch toàn bộ project, Play hoặc build Windows trong Unity.

Ngày 28/10/2026 đã qua: **16 tình huống dữ liệu CID**, **14 tình huống Puzzle**, **46 tình huống metadata mạng**; 104 script qua biên dịch cú pháp Lua. Giao diện Puzzle cũng được kiểm tra với danh sách 0/1/2 mục, nút đã bị ẩn và màn chơi trước bị xóa.

Môi trường này không cài Unity Editor: **chưa chạy Play, chưa build Windows, chưa xác minh giải được toàn bộ màn chơi**. Trên Windows, mở `Assets/Scenes/Boot.unity`, Play rồi thử mục Puzzle, vật phẩm/đồng hành, một trận offline và LAN trước khi coi bản này hoàn tất kiểm thử runtime.
