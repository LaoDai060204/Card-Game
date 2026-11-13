# Phong cách sàn đấu — 0.6.5

Bản này chỉnh phần nhìn của sàn Battle Spirits theo phong cách Master Duel: nền tối, thông tin gọn, mặt thẻ đầy đủ, tay bài xếp quạt và điểm nhấn sáng. Tiếp tục dùng form native OcgCoreUI, bộ luật Standard và tài nguyên Battle Spirits.

## Những thay đổi

- **Sửa khung trắng:** `ChromePanel.png` là mặt nạ trắng có alpha, không có màu nền tối sẵn. 0.6.4 tô trắng khi sprite tồn tại, làm tên/chỉ số/hiệu ứng trắng chìm trên nền. Nay khung luôn được tô xanh đen; tên, metadata và hiệu ứng có chữ sáng, trạng thái dùng xám sáng/lime. `ChromeButton` có tâm đen nên giữ cách tô trắng của nút.
- **Khung soi gọn:** chia vùng tên, ảnh, Cost/Lv/BP, trạng thái và phần hiệu ứng cuộn bằng các nền tối riêng; viền/đường phân cách mảnh, nút Phóng to/Triệu hồi ở đáy. Toàn bộ mặt thẻ và chức năng zoom vẫn được giữ.
- **Tay bài dạng quạt:** dùng trọn mặt thẻ, bỏ mã nhỏ dưới từng lá; mã vẫn hiện trong khung soi. Lá được chọn hoặc rê chuột được nâng, phóng nhẹ và đưa ra trước; các lá khác nghiêng nhẹ. Chỉ phần hình chuyển vị trí, vùng bấm đứng yên để tránh hover nhấp nháy. Rê chuột không thay lựa chọn đánh bài hoặc tự gửi lệnh.
- **HUD tối:** trạng thái lượt và Life có nền tối, điểm màu mỗi bên. Dòng gợi ý bước nằm trên sân, dưới tay đối thủ, để không che Core trong Reserve/Trash. Bước mở đầu ghi rõ **GIỮ 4 LÁ / ĐỔI 4 LÁ**.
- **Sân navy/slate:** nền Spirit/Nexus có màu phân biệt với bàn, viền nhiều lớp và đường cyan nhẹ. Họa tiết playmat Standard được giảm độ sáng bằng vật liệu; ảnh nguồn, các màu Core/Soul Core, mặt thẻ và lưng bài không sửa. Vật liệu và chi tiết trang trí được tạo một lần mỗi sân, dùng chung và dọn cùng sân.

Giữ cách soi an toàn của 0.6.4: bấm lá trên tay để xem; **TRIỆU HỒI / DÙNG MAGIC** mới đánh lá được chọn. Ở bước mở đầu cần chọn **GIỮ 4 LÁ** hoặc **ĐỔI 4 LÁ** trước khi đến Main. Pet, wallpaper, hoạt ảnh triệu hồi, luật, AI, mạng và danh mục thẻ không đổi.

## Kiểm tra trên máy Windows

1. Đóng Unity, cập nhật nhánh `battle-spirits-mdpro3-ui`, mở Unity **6000.0.24f1**, Windows x64. Chọn Addressables **Use Asset Database**, chạy từ **Boot**, xác nhận **0.6.5**.
2. Vào Solo và bấm thẻ: khung trái phải có nền tối, tên/Cost/Lv/BP/hiệu ứng nhìn được. Đọc tiếp bằng cuộn; thử Phóng to rồi Esc/Đóng.
3. Rê chuột từ mép dưới qua nhiều lá: lá nâng lên ổn định; ra ngoài trở lại vị trí cũ. Chọn một lá rồi rê lá khác: lựa chọn và thông tin lá đã chọn không tự đổi.
4. Chọn Giữ/Đổi 4 lá, vào Main, bấm Triệu hồi. Soi cả lá trên sân, xác nhận Core và trạng thái cập nhật. Dòng hướng dẫn trên cùng không che Reserve/Trash.
5. Thử tay bài đông và Game view 16:9/4:3/cửa sổ nhỏ: lá không mất bản sao, đúng mã khi chọn/đánh, khung thông tin không trắng lại. Kiểm tra cả chuột, bàn phím và gamepad thực.

[Báo cáo](battle-spirits-master-duel-style-validation.json) ghi kiểm tra production bằng API thay thế, phép chiếu hình học và pixel của PNG nguồn. Đây chưa phải ảnh render của Unity; chưa xác nhận input, font/shader, độ nét hay FPS trên Windows.

Backup: `backup/2026-11-13-before-master-duel-duel-style` → `e401183904a96795340894b037731907d20cfd70`.
