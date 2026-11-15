# 16 ảnh tham chiếu Core, Symbol, mặt sau và sàn đấu

**Trạng thái: đã ghi nhận nội dung 16 ảnh; chưa lưu được tệp ảnh gốc.** Ảnh được gửi dạng hình chèn trong tin nhắn, còn các tệp nhị phân chưa xuất hiện trong môi trường làm việc. Cần người dùng đính kèm ZIP chứa các ảnh gốc để lưu đúng ảnh đã gửi, giữ nguyên byte và ghi SHA-256.

Không có ảnh thay thế hoặc ảnh tái tạo được dùng để giả làm tệp gốc. Các tên bên dưới là nhãn tham chiếu dự kiến, chưa phải đường dẫn tệp ảnh đã upload.

| Thứ tự | Nhãn tham chiếu | Nội dung | Vai trò / lưu ý |
| ---: | --- | --- | --- |
| 01 | `symbol-blue` | Viên ngọc Symbol xanh dương | Printed Blue Symbol; preserve its distinction from a physical regular Core. |
| 02 | `card-back-black-silver` | Mặt sau Battle Spirits đen với hoa văn bạc | Card-back artwork reference; retain the original border and proportions when original bytes are supplied. |
| 03 | `core-and-soul-core` | Core xanh và Soul Core tâm đỏ, viền bạc | Regular Core and Soul Core resource-piece shapes; matches the existing blue/red preference. |
| 04 | `playmat-dragon-gold-frame` | Sàn đấu artwork rồng, khung vàng | Illustrated field and zone-layout reference; format provenance not established from this screenshot alone. |
| 05 | `setup-example-numbered` | Sơ đồ chuẩn bị trận đấu đánh số 1–5 | Void, Deck, five Life Cores, three regular Cores plus one Soul Core in Reserve, four-card Hand. |
| 06 | `symbol-green` | Viên ngọc Symbol xanh lá | Printed Green Symbol. |
| 07 | `deck-core-soul-core-guide` | Hướng dẫn bộ bài, Core và Soul Core | Deck stack/card back and physical resource pieces; Japanese labels: Deck; Core and Soul Core. |
| 08 | `playmat-black-standard-eight-steps` | Sàn đen viền bạc với 8 bước của lượt | Standard-compatible turn sequence, including Second Main Step. |
| 09 | `playmat-grayscale-burst-reference` | Sàn xám có vùng Burst và 7 bước | Historical/Eternal visual reference; do not use its sequence as the current Standard rules. |
| 10 | `symbol-purple` | Viên ngọc Symbol tím | Printed Purple Symbol. |
| 11 | `symbol-red` | Viên ngọc Symbol đỏ | Printed Red Symbol; distinct from the red-centered Soul Core resource piece. |
| 12 | `quick-rules-legacy-eternal` | Poster luật English có Brave, Burst và 7 bước | Older/Eternal quick-rules reference; not the governing Standard ruleset. |
| 13 | `card-anatomy-legacy-example` | Sơ đồ giải thích các vùng thông tin trên thẻ | Cost, reductions, card type/name/family, levels, effects, symbols and card details; the older example is not proof of Standard legality. |
| 14 | `symbol-and-ex-symbol-legend` | Bảng 6 Symbol thường và 6 EX Symbol | Normal and gold-framed EX Symbols in six colors; screenshot Legacy wording is not the canonical English keyword Succession. |
| 15 | `symbol-white` | Viên ngọc Symbol trắng/bạc | Printed White Symbol. |
| 16 | `symbol-yellow` | Viên ngọc Symbol vàng | Printed Yellow Symbol. |

## Ghi nhớ để làm tiếp

- **Core thường:** màu xanh, theo hình khối Core ở ảnh 03. **Soul Core:** tâm đỏ với viền bạc ở cùng ảnh. Đây là hai loại tài nguyên.
- Các viên ngọc sáu màu và các phiên bản viền vàng là **Symbol / EX Symbol trên thẻ**. Màu xanh hoặc đỏ giống nhau không có nghĩa là chúng là cùng một loại vật thể với Core hoặc Soul Core.
- Mặt sau đen/hoa văn bạc và các sàn đấu được lưu làm tham chiếu hình ảnh; chưa tạo texture trong suốt, bản thiết kế mới hay đưa vào Unity.
- Standard hiện hành có **8 bước**, gồm **Second Main Step**. Sàn đen ảnh 08 có trình tự này.
- Ảnh 09 và 12 có thông tin lịch sử/Eternal như Burst, Brave hoặc 7 bước. Dùng làm tham khảo hình ảnh và phân loại riêng; luật Standard lấy từ tài liệu chính thức.
- Chuẩn bị trận theo tài liệu Standard: **5 Core thường ở Life; 3 Core thường + 1 Soul Core ở Reserve; 4 lá trên tay**. Sáu ô in trên một số thảm không đổi số Life khởi đầu thành sáu.
- Bảng Symbol dùng chữ “Legacy”; thuật ngữ English chính thức đã dùng trong bộ thẻ là **Succession**.
- Sơ đồ giải phẫu thẻ cũ chỉ giúp nhận diện các vùng thông tin, không xác nhận lá minh họa được phép chơi Standard.

## Khi nhận ZIP ảnh gốc

Lưu ảnh nguyên bản, nối từng tệp với ID trong [references.json](references.json), ghi hash và kích thước thật. Khi chưa có tệp, giữ `original_file` và `original_sha256` là `null`, không đánh dấu đã upload. Cập nhật phần mô tả trạng thái sau khi lưu thành công.

[Ghi nhớ thiết kế](../../design/DESIGN_MEMORY.md) · [Quay lại trang review](../../README.md)

Nguồn luật: [Official Standard Rule Manual](https://asia-en.battlespirits.com/pdf/official-rule-manual_standard.pdf?ver.1.0) · [Luật chính thức Nhật](https://www.battlespirits.com/rule/)
