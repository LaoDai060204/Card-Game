# Màn mở đầu Battle Spirits — 0.6.0

Theo lựa chọn của người dùng, màn vào game đi theo trình tự của **Master Duel chính thức**: hiển thị tên game, chờ thao tác bắt đầu, rồi chuyển sang menu. Tên hiển thị là **Battle Spirits**, phần giao diện dùng tiếng Việt.

## Trình tự

1. **Tải dữ liệu:** tên game hiện trên nền tối, trạng thái và tiến trình nằm phía dưới. Nền kín vẫn xóa sạch chữ cũ như bản sửa 0.5.2.
2. **Màn mở đầu:** khi manager, menu và wallpaper đều sẵn sàng, wallpaper được chọn hiện phía sau tên game; dòng **NHẤN ĐỂ BẮT ĐẦU** sáng nhẹ theo nhịp. Menu và FPS được ẩn.
3. **Vào menu:** bấm chuột trái, chạm màn hình, một phím hoặc A/Start trên gamepad. Tên game mờ dần và menu hiện lên. Input menu chỉ mở sau khi chuyển cảnh hoàn tất.

Màn mở đầu có thời gian tối thiểu để đọc; thao tác trong lúc đang tải hoặc một nút giữ từ trước không tự bỏ qua nó. Đồng hồ hiệu ứng dùng thời gian thực, nên không phụ thuộc `Time.timeScale` của trận đấu.

Các thành phần này dùng **Canvas Boot hiện có**, không tạo Canvas ứng dụng phủ thêm. Tên, đường nét và hiệu ứng được dựng bằng UI trong dự án; wallpaper đã chọn được giữ. Các lỗi khởi động vẫn hiện rõ trên nền kín. Luật, deck, LAN, thẻ, Core và pet không thay đổi trong đợt này.

## Thử trên máy Windows

1. Đóng Unity trước khi cập nhật nhánh `battle-spirits-mdpro3-ui`. Mở lại bằng **Unity 6000.0.24f1**, Windows x64; Addressables Groups dùng **Use Asset Database**.
2. Mở `Assets/Scenes/Boot.unity`, nhấn Play. Chờ tải xong và xác nhận màn mở đầu hiện phiên bản **0.6.0**, chưa hiện các nút menu/FPS.
3. Chờ vài giây mà không bấm: phải ở nguyên màn mở đầu. Bấm chuột hoặc Enter: chuyển mượt sang menu; không mở nhầm **Đấu với máy/Bộ bài** bởi cùng thao tác đó.
4. Dừng Play rồi thử lại với gamepad A/Start. Giữ một nút trong lúc tải, thả ra rồi bấm lại: chỉ lần bấm mới được vào menu.
5. Chọn wallpaper khác trong Cài đặt, khởi động lại để kiểm tra title và menu dùng cùng hình nền. Kiểm tra tùy chọn FPS vẫn giữ đúng sau khi vào menu.
6. Dừng/chạy lại Play vài lần, thử Game View 16:9 và 4:3. Nếu build Windows, build lại Addressables từ source hiện tại vào thư mục đầu ra mới.

[Báo cáo kiểm tra](battle-spirits-title-validation.json) phân biệt các kiểm tra mã/stand-in với kiểm tra Editor thật. Chưa chạy render, thiết bị nhập hay build bằng Unity Windows trong môi trường cloud này.
