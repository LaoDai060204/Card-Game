# Giao diện thẻ trong Bộ bài — 0.6.3

Giữ ba khung MDPro3 và luật Battle Spirits Standard. Đợt này chỉnh độ dễ đọc và cách trình bày thẻ theo ảnh người dùng gửi ở 0.6.2.

- **Bộ bài gọn hơn:** các bản sao có cùng mã được gom vào một ô, có nhãn **×1/×2/×3**. Danh sách lưu vẫn giữ từng bản sao; deck 40 lá không bị rút thành số mã khác nhau. Bấm ô trong deck chỉ bớt **một** bản sao.
- **Nhìn rõ trạng thái:** ô đang chọn có viền lime, mã thẻ có nền tối; danh mục cũng hiện số bản sao đang có trong deck. Thẻ chưa dùng được trong trận được gắn **TRA CỨU** hoặc **NGOÀI POOL** theo điều kiện hiện có. Artwork vẫn giữ màu và tỷ lệ gốc.
- **Cỡ thẻ tùy chọn:** nút **THẺ: VỪA** cạnh phần phân trang chuyển lần lượt giữa **GỌN / VỪA / LỚN**. Lựa chọn được lưu bằng khóa cấu hình ngoại hình `BattleSpiritsDeckDensity`; chỉ đổi cách xếp/kích thước ô, không đổi truy vấn, deck hay tải lại ảnh.
- **Sửa tương phản:** chữ tab luôn sáng, tab đang chọn có gạch lime. Ô tìm kiếm/tên deck dùng nền riêng với màu tối cho normal, hover, focus và press; caret trắng và gợi ý xám rõ. Bộ màu cũ không còn vẽ lại nền trắng hoặc làm mờ gợi ý. TMP quản lý việc hiện lại gợi ý sau khi xóa nội dung.
- **Thanh đầu thoáng hơn:** dành khoảng trống bên phải cho FPS, tránh chồng lên nút Xóa bộ bài.

Badge, viền và nền mã chỉ áp dụng cho ô trong editor. Thẻ trên tay/sân, pet, wallpaper, mạng và dữ liệu luật không đổi. Thao tác thêm vẫn kiểm tra giới hạn cùng tên và pool Standard; gom theo mã không bỏ qua kiểm tra này. Các slot/badge được tái sử dụng và kết quả tải ảnh muộn vẫn theo đúng ID.

## Cách thử trên Windows

1. Đóng Unity, cập nhật nhánh `battle-spirits-mdpro3-ui`. Mở **6000.0.24f1**, Addressables → **Use Asset Database**, chạy từ **Boot** và xác nhận **0.6.3**.
2. Mở deck mẫu 40 lá. Tổng vẫn là 40; mỗi mã một ô có badge số lượng. Bấm một ô có ×3: còn ×2 và tổng giảm một. Lưu rồi mở lại để kiểm tra số bản sao.
3. Chọn thẻ, đổi trang/lọc, quay lại trang cũ. Viền chọn và số lượng phải thuộc đúng mã; tag tra cứu không được dính sang thẻ chơi được.
4. Thử nút **THẺ: ...** ở cửa sổ thường/rộng/4:3; giữ nguyên truy vấn và deck khi đổi cỡ. Mở lại form để kiểm tra lựa chọn được nhớ.
5. Bấm/gõ/rê chuột trên ô tìm kiếm và chuyển tab **CHƠI ĐƯỢC**: chữ phải rõ trên nền tối. Xóa tìm kiếm và tên deck để kiểm tra gợi ý hiện lại; thử Enter với `cost:4`.

[Báo cáo](battle-spirits-card-browser-polish-validation.json) ghi các kiểm tra nguồn, trạng thái UI và độ tương phản màu khai báo bằng API thay thế. Chưa kiểm tra ảnh render, font, hover/raycast hay bàn phím/gamepad thực bằng Unity Windows. Cần các bước trên để xác nhận hình ảnh trên máy người dùng.
