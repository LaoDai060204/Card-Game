# Sàn đấu và soi thẻ — 0.6.4

Sàn đấu dùng lại form OcgCoreUI và khung CardDescription của MDPro3. Khung cũ được lưu ngoài mép trái (`Window` ở x ≈ -420); controller Battle Spirits nay đặt khung này vào vùng nhìn thấy, sửa viewport cuộn và thay các chỉ số Yu-Gi-Oh bằng thông tin Battle Spirits.

- Bên trái có toàn bộ ảnh thẻ, mã/tên, loại/màu, Cost, giảm Cost, Symbol, các mức Lv/Core/BP và hiệu ứng gốc. Cuộn phần hiệu ứng để đọc tiếp. Ảnh dùng mặt thẻ đầy đủ, không dùng ảnh closeup đã cắt.
- Bấm thẻ trên tay chỉ chọn và soi. Khi đúng lượt/bước, nút **TRIỆU HỒI / DÙNG MAGIC** mới đánh lá đã chọn. Thay đổi tay bài xóa lựa chọn cũ; popup Magic cũng kiểm tra lại mã và vị trí trước khi gửi hành động.
- Bấm lá trên sân để chọn và cập nhật BP/Lv/Core/Soul Core/trạng thái hiện tại. Soi lá đối thủ bỏ lựa chọn tấn công/chặn trước đó của bạn. Không có thông tin bài kín của đối thủ trong khung này.
- **PHÓNG TO** hoặc bấm ảnh bên trái để xem toàn bộ ảnh gốc lớn. **ĐÓNG**, Esc, chuột phải hoặc nút quay lại đóng ảnh trước khi thoát trận. Trong lúc phóng to, các callback đánh bài/tấn công/replay phía sau bị khóa; focus chuyển vào nút Đóng và khôi phục khi đóng. Xem ảnh không dừng thời gian của server hay đổi luật.
- Góc nhìn sân cao hơn, thẻ trên sân ít lá được tăng kích thước có giới hạn. Ảnh quái nổi vẫn hiện cùng mặt thẻ và hoạt ảnh triệu hồi; kích thước/vị trí được điều chỉnh để bớt che mặt thẻ. Spirit/Nexus đông được xếp theo vùng mở.
- Trạng thái lượt chuyển lên trên; tay bài, Life và các nút hành động có vùng riêng. Thống kê trên mặt thẻ dùng nền tối, co chữ/cắt phần vượt khung khi cửa sổ nhỏ; thông tin đầy đủ luôn ở khung soi.
- Ảnh sân giữ tỷ lệ thực của vùng hiển thị, render target có cạnh dài tối đa 1920 và chờ kích thước ổn định 0,12 giây trước khi tạo lại. Các vùng bấm dùng cùng hình chữ nhật và được chiếu lại khi đổi cỡ cửa sổ. Ô sân/tay bài được tái sử dụng; ảnh không tải lại chỉ vì có thông báo trạng thái mới.

Luật, AI, dữ liệu thẻ, artwork, mạng và lựa chọn pet/wallpaper giữ nguyên. Văn bản hiệu ứng Nhật được giữ đúng nguồn; tính năng soi không tự triển khai hiệu ứng chưa hỗ trợ.

## Thử trên Unity Windows

1. Đóng Unity, cập nhật `battle-spirits-mdpro3-ui`. Mở Unity **6000.0.24f1**, Windows x64, Addressables **Use Asset Database**, chạy từ **Boot**, kiểm tra phiên bản **0.6.4**.
2. Vào Solo bằng deck Standard. Giữ 4 lá, bấm một lá: ảnh và thông tin phải hiện bên trái, tay bài chưa giảm. Đến Main, bấm **TRIỆU HỒI** để đánh lá đó.
3. Bấm Spirit/Nexus trên sân, chuyển Core hoặc để BP/trạng thái đổi; khung trái phải cập nhật theo đúng instance. Chọn lá đối thủ rồi xác nhận hành động tấn công/chặn cũ không còn được chọn.
4. Mở **PHÓNG TO**, kiểm tra đúng mặt thẻ, đúng tỷ lệ và chữ đọc được ở độ phân giải gốc. Thử chuột và Submit/gamepad: chỉ nút Đóng hoạt động, không đánh bài phía sau. Đóng rồi tiếp tục chơi.
5. Thử Game view 16:9 và 4:3, đổi kích thước cửa sổ; sân không kéo dãn, các vùng Life/Reserve/Trash/Deck còn nhìn được, nút bấm thẻ khớp mặt thẻ. Với sân đông, soi từng lá để đọc đầy đủ.
6. Mở lại trận/replay/preview, thử ngắt mạng khi đang soi; không giữ hành động của lá cũ hay hiển thị bài kín đối thủ.

[Báo cáo kiểm tra](battle-spirits-duel-readability-validation.json) phân biệt các kiểm tra mã, hình học và fixture prefab với việc render/input thật. Chưa chạy Unity Windows, chưa đo FPS hay xác nhận độ nét/font bằng ảnh render.

Backup: `backup/2026-11-13-before-duel-readability` → `0599f41f506da400c2e884f786cf277dbde297ab`.
