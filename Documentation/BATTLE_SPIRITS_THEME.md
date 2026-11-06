# Giao diện và màu sắc — Standard 0.5.1

Bổ sung 0.5.1: ảnh ngọc xanh/đỏ được dùng cho Core trong Life/Reserve/Trash và trên Spirit; bốn màu khác là ngoại hình Core thường. [Ngọc Core và ảnh thẻ](BATTLE_SPIRITS_CORE_GEMS_AND_ART_QUALITY.md) ghi cách chọn và sửa tỷ lệ ảnh.

Theo yêu cầu ngày **06/11/2026**, bản này đưa giao diện về phong cách ban đầu: nền đen/xám, viền bạc/trắng, nút có khung nổi và điểm nhấn xanh lime. Bố cục form, controller Battle Spirits, tiếng Việt và phần triệu hồi 0.4.0 được giữ. Đây là phục hồi một nhóm tài nguyên UI dùng chung và cách phối màu, không phải cam kết toàn bộ pixel hay bàn đấu giống hệt Master Duel.

Unity vẫn là **6000.0.24f1**. Luật Standard, danh mục 270 thẻ, **19 thẻ Standard chơi được** và giao thức LAN **3** không đổi.

## Phần được phục hồi và thay thế

- [Manifest phục hồi](original-ui-style-restoration.json) ghi **141 tài nguyên GUI gốc**, 435.080 byte không tính `.meta`, và **2.867 tham chiếu đồ họa trong 31 tệp**. Việc phục hồi chỉ áp dụng các thuộc tính đồ họa đã kiểm tra; không ghi đè controller, callback, hierarchy, geometry hoặc GUID của form Battle Spirits.
- Các nút mới dùng chrome gốc với màu lime khi chọn/hover. Giữ Source Han Sans cho tiếng Việt. Không đưa lại hình chữ AUTO, biểu tượng Attribute/Rank/Spell-Trap hay các nhãn chỉ có ý nghĩa trong Yu-Gi-Oh.
- Sáu biểu tượng BS dùng cho màu thẻ ở bộ lọc, chi tiết, triệu hồi và sân. Chúng không trở thành sáu loại Core. Core thường và Soul Core vẫn theo luật hiện tại.
- Sân dùng đen/xám và khung bạc, các vùng Life/Reserve/Trash/Deck có khung. Họa tiết viền và logo được lấy từ playmat Standard chính thức bằng UV. Không kéo giãn cả tấm sân một người lên bàn hai người và không đưa các dòng tiếng Nhật hoặc khu Burst của mẫu cũ vào làm luật.
- Điểm nhấn lime cho biết đến quyền quyết định cục bộ, đỏ cho đối thủ; chúng chỉ đọc trạng thái công khai.
- Chồng Deck và tay đối thủ dùng mặt sau BS. Tay đối thủ chỉ lấy `handCount` công khai, hiện tối đa mười ảnh lưng bài và số lá dư; không tải mặt thẻ hay ID bài bí mật.

Pet/wallpaper tiếp tục được giữ. Engine Yu-Gi-Oh, cut-in quái, giọng nhân vật và CDB/CID cũ không được phục hồi. Các manifest dọn tài nguyên 0.3.0 vẫn là hồ sơ lịch sử; manifest phục hồi 0.5.0 ghi rõ ngoại lệ được chủ dự án yêu cầu sau đó.

## Nguồn ảnh và khác biệt với ảnh tham khảo

[skin-sources.json](../Data/BattleSpirits/skin-sources.json) ghi URL, hash, kích thước và nguồn từng tài nguyên trong `Resources/BattleSpirits/Skin`:

| Nhóm | Bản dùng trong dự án |
| --- | --- |
| Sáu viên biểu tượng màu | PNG chính thức 47×47, cùng họa tiết với ảnh tham khảo; không phải các byte ảnh độ phân giải cao đính kèm |
| Chip Core/Soul tham khảo | Hai PNG chip chính thức 47×47; renderer Core 0.5.1 dùng ảnh ngọc xanh/đỏ |
| Mặt sau bài | Bản mirror công khai 350×490, cùng thiết kế BS nhưng khác kích thước và dòng bản quyền nhỏ với ảnh 500×730 tham khảo |
| Sân Standard | JPEG gốc 5877×4181 trích trực tiếp từ PDF chính thức; tài liệu có Second Main |
| Sân xám tham khảo | JPEG gốc 2478×1753 trích từ PDF chính thức; lưu tham khảo, không dùng làm hướng dẫn lượt Standard |
| Ba chrome | Bản sao nguyên byte của `GUI_CommonButtonM`, `_Over` và `GUI_CommonWindowS` từ template |

Không vẽ lại hay thay pixel các file nguồn. TextureImporter giới hạn 2048, Compression None, không mipmap; Unity có thể thu nhỏ texture khi import. Các biểu tượng/back/playmat giữ quyền của chủ sở hữu, không thuộc CC0 của các render và WAV tự tạo. Xem [ghi công](../THIRD_PARTY_NOTICES.md).

## Kiểm tra và thử trong Editor

Các kiểm tra đã chạy cho phần phục hồi gồm **9 kiểm tra prefab**, **12 kiểm tra tài nguyên/import/manifest** và đối chiếu hash/metadata của **14 ảnh skin**. Kết quả integration cuối do [báo cáo kiểm tra](BATTLE_SPIRITS_VALIDATION.md) ghi lại; kiểm tra bằng stand-in không xác nhận hình ảnh thật trong Unity.

1. Đóng Unity, cập nhật nhánh `battle-spirits-mdpro3-ui`; mở đúng Editor và Addressables **Use Asset Database**.
2. Chạy scene Boot, xác nhận menu **0.5.1**. Xem menu, Cài đặt, Solo và Bộ bài: khung/nút có giao diện gốc, không lẫn nhãn Yu-Gi-Oh.
3. Đổi bộ lọc màu rồi xóa bộ lọc; biểu tượng phải đổi đúng màu hoặc ẩn khi chọn tất cả. Chọn thẻ để kiểm tra biểu tượng trong chi tiết.
4. Chơi Solo, kiểm tra mặt sau ở Deck và tay đối thủ, sáu biểu tượng trên sân, lượt quyết định lime/đỏ và hoạt ảnh triệu hồi vẫn chạy.
5. Về menu rồi mở ván thứ hai; thử hai client LAN để xác nhận chỉ số lá công khai của tay đối thủ, không hiện mặt bài.

**Chưa chạy Play/render/build trong Unity Windows thật ở môi trường phát triển này.** Cần kiểm tra trực tiếp shader, màu, font, kích thước, raycast và tay cầm trên Windows trước khi coi giao diện đã được xác nhận bằng hình ảnh.

Backup trước thay đổi: **`backup/2026-11-06-before-original-style-theme` → `035abd48`**.
