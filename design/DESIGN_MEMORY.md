# Ghi nhớ thiết kế Battle Spirits

## Quy trình đã thống nhất

- Dùng nhánh `review/battle-spirits-english-cards-20261115` để lưu các tài nguyên và bản thiết kế người dùng yêu cầu.
- Cập nhật các bản xem theo yêu cầu lên nhánh này; quyền upload công việc review đã được người dùng đồng ý.
- Khi người dùng yêu cầu triển khai dự án, lấy các tài nguyên đã chốt để tích hợp trên nền MDPro3 English. Trong giai đoạn hiện tại, giữ nguyên mã nguồn Unity và các nhánh phát triển khác.

## Bộ ảnh tham chiếu ngày 15/11/2026

[Danh mục đủ 16 ảnh và trạng thái tệp gốc](../references/core-field-20261115/README.md) · [Dữ liệu tham chiếu](../references/core-field-20261115/references.json)

Đã nhận `design.7z` và upload đủ **16 ảnh PNG nguyên bản**, giữ nguyên tên và nội dung tệp. [Xem ảnh trực tiếp](../references/core-field-20261115/README.md) và [manifest hash/kích thước](../references/core-field-20261115/archive-manifest.json). Gói 7z gốc cũng được lưu để tải lại. Đây là ảnh tham chiếu, chưa được chỉnh sửa hoặc tích hợp vào Unity.

### Core và Soul Core

Giữ lựa chọn Core thường màu xanh, Soul Core màu đỏ. Ảnh 03 bổ sung hình dạng cụ thể: Core là khối xanh; Soul Core có tâm đỏ và viền kim loại bạc. Đây là các vật thể tài nguyên trên sân.

### Symbol và EX Symbol

Bộ ngọc đỏ, tím, xanh lá, trắng, vàng, xanh dương là Symbol in trên thẻ. Bảng tham chiếu còn có sáu EX Symbol viền vàng. Giữ định danh cơ chế của chúng riêng với Core và Soul Core; không tự đổi tài nguyên chỉ vì ảnh ngọc có cùng màu.

Ảnh Legend ghi “Legacy” nhưng bộ thẻ English hiện tại dùng thuật ngữ chính thức **Succession**. Giữ nguyên ảnh nguồn khi có tệp; nếu làm tài nguyên English mới thì dùng thuật ngữ chính thức.

### Mặt sau và sàn đấu

Ghi nhận mặt sau đen với họa tiết bạc; sàn đen viền bạc, sàn xám và sàn artwork rồng là các tham chiếu được cung cấp. Chưa tự chọn một sàn duy nhất hoặc tạo phiên bản mới. Ảnh có nền, mũi tên hoặc chú thích được giữ nguyên khi lưu; mọi biến thể đã xử lý cần là tệp mới với ghi chú nguồn.

Các vùng xuất hiện: Void/Core Pool, Life, Reserve, Field, Deck và Trash; một số thảm chia Trash thành khu thẻ và khu Core. Xác định chức năng theo luật Standard trước khi dùng làm giao diện game.

### Phân loại luật từ các sơ đồ

Nguồn luật vẫn là tài liệu Battle Spirits Nhật **Standard** hiện hành:

1. Start Step
2. Core Step
3. Draw Step
4. Refresh Step
5. Main Step
6. Attack Step
7. Second Main Step
8. End Step

Sơ đồ tám bước ở ảnh 08 phù hợp với trình tự Standard. Eternal không có Second Main Step. Các ảnh bảy bước, vùng Burst và poster Brave/Burst cần được ghi là tham khảo lịch sử/Eternal, không tự dùng thay luật Standard.

Chuẩn bị trận: 5 Core thường ở Life, 3 Core thường cùng 1 Soul Core ở Reserve, 4 lá trên tay. Số ô vẽ trên thảm không thay đổi các số lượng này. Chưa có luật nào được triển khai vào dự án trong lần ghi nhớ này.

Nguồn: [Standard Rule Manual](https://asia-en.battlespirits.com/pdf/official-rule-manual_standard.pdf?ver.1.0) và [trang luật Nhật](https://www.battlespirits.com/rule/).
