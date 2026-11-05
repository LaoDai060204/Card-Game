# Triệu hồi bằng artwork — Standard 0.4.0

Bản này giữ Unity **6000.0.24f1**, bộ luật Standard, danh mục 270 thẻ và **19 thẻ Standard chơi được** của 0.3.0. Giao thức LAN vẫn là **3**. Thay đổi nằm ở cách trình diễn và chọn thẻ trên bàn đấu.

Khi một lệnh triệu hồi/đặt Nexus được chấp nhận, bàn đấu hiện hiệu ứng khoảng **1,95 giây**: làm tối nền, quét sáng, xoay/phóng to mặt thẻ, nâng ảnh minh họa rồi đưa lá bài xuống đúng vị trí trên sân. Tên và mã thẻ đi cùng artwork. Bấm **BỎ QUA** để kết thúc phần trình diễn và tiếp tục thao tác.

Sau hiệu ứng, mặt thẻ nằm trên sân và ảnh minh họa của cùng lá nổi phía trên, hướng về camera. Vùng minh họa được lấy từ ảnh gốc bằng UV, giữ tỷ lệ và **giữ cả nền artwork**. Đây là cách dựng ảnh 2.5D; chưa phải cutout quái trong suốt hoặc model 3D. Không thêm lại cut-in hay đồ họa triệu hồi Master Duel. Các PNG mặt thẻ chính thức và checksum nguồn được giữ nguyên.

Người chơi chọn lá bài ngay tại vị trí của nó trên sân. Vùng bấm dùng vị trí chiếu của lá bài từ camera sân, thay các thumbnail mặt thẻ từng phủ riêng trên vùng field. Pet và wallpaper vẫn được giữ như 0.3.0.

## Luồng trình diễn

- `BattleNativeSession` phát sự kiện trình diễn từ trạng thái đã được engine/server chấp nhận. Cập nhật nhãn hoặc vẽ lại UI không tự phát lại triệu hồi.
- `BattleSummonPresentation` dùng root UI bàn đấu hiện có, hàng đợi có giới hạn và texture do session cung cấp; không tạo Canvas ứng dụng mới.
- `BattleCardFigure` hiển thị mặt thẻ và vùng minh họa; `CardArtwork.shader` lấy vùng UV/alpha mềm. Renderer mượn texture trong cache, giải phóng mesh/material/carrier của chính nó khi đổi ván hoặc thoát.
- Khi trình diễn, thao tác và polling WindBot cục bộ tạm chờ. Server TCP và trạng thái trận không bị tạm dừng. Bỏ qua hoạt ảnh không sửa kết quả lệnh hay hoàn lại thao tác.
- Trở về menu, ngắt kết nối hoặc hủy màn hình sẽ dọn phần trình diễn đang chờ. Mở replay không tự phát mọi hiệu ứng cũ; bước tiến đơn có thể trình diễn sự kiện của bước đó.

Source model low-poly và hai closeup BS01 tự dựng trước đây vẫn ở repository như tài nguyên lịch sử. Màn **Hoạt cảnh** hiện dùng cách trình diễn artwork; các thẻ BS01 trong đó vẫn không hợp lệ cho deck Standard.

## Thử trong Windows Unity

1. Mở đúng Editor, chọn Addressables **Use Asset Database**, chạy `Assets/Scenes/Boot.unity`; menu phải hiện **0.4.0**.
2. Vào **Đấu với máy**, chọn bộ Standard mẫu, bấm **GIỮ 4 LÁ**. Đến Main, bấm một Spirit đủ Core để triệu hồi.
3. Kiểm tra tên/mã, mặt thẻ phóng to và ảnh nổi đều thuộc chính lá vừa gọi; sau hiệu ứng, lá đó còn trên sân và chọn được.
4. Triệu hồi thêm, thử **BỎ QUA**, chuyển Core, tấn công/chặn và Spirit bị phá hủy. Thao tác phải được trả lại sau khi hiệu ứng kết thúc hoặc bị bỏ qua.
5. Về menu và chơi ván thứ hai; không được còn ảnh/vệt sáng của ván trước. Thử **Hoạt cảnh** để xem lại trình diễn.
6. Chạy hai client LAN; kiểm tra cả triệu hồi của mình và đối thủ hiện đúng artwork, không làm lộ tay bài chưa công khai.

Kiểm tra mã/stand-in và đường cong không xác nhận pixel, shader, font, raycast hoặc hoạt ảnh thật trong Editor. **Chưa chạy Play/render/build bằng Windows Unity trong môi trường phát triển này.** Xem [báo cáo kiểm tra](BATTLE_SPIRITS_VALIDATION.md) để phân biệt kết quả tự động với những bước thử cần thực hiện trên máy Windows.

Backup trước thay đổi: **`backup/2026-11-05-before-card-summon-animation` → `c19c2d67`**. Phạm vi luật và hiệu ứng chưa hỗ trợ vẫn theo [BATTLE_SPIRITS_SCOPE.md](BATTLE_SPIRITS_SCOPE.md).
