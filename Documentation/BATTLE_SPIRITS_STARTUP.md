# Màn tải và chuyển sang menu

Bản 0.6.0 giữ các sửa lỗi 0.5.2 bên dưới và thêm [màn mở đầu chờ nhấn bắt đầu](BATTLE_SPIRITS_TITLE_SCREEN.md). Sau khi tải xong, người chơi xác nhận ở màn này rồi mới vào menu.

Người dùng xác nhận menu vẫn xuất hiện sau khi chờ; lỗi được báo là chữ có vệt chồng ở màn tải và background xuất hiện trước menu trong lúc chuyển cảnh.

## Nguyên nhân trong source

Scene `Assets/Scenes/Boot.unity` chỉ có một Canvas và một nhãn trạng thái. Ảnh nền toàn màn hình của nó đã bị thay bằng `BS_clear.png`, có alpha bằng 0 trên toàn ảnh. Scene không có camera xóa nền; lớp ảnh trong suốt không vẽ lại vùng chữ cũ khi nhãn hoặc dấu chấm thay đổi. Bản sửa dùng Image đen kín, không phụ thuộc sprite hay vật liệu tùy chỉnh. Font và thanh tiến trình được giữ nguyên.

Trước đây Boot kết thúc ngay khi scene Main tải xong, trong khi Main vẫn đang khởi tạo giao diện và tải wallpaper. Với một wallpaper được chọn, code còn nạp ảnh dự phòng trước khi thay nó bằng bundle đã chọn. Hai trình tự này cho phép thấy nền hoặc trạng thái giao diện chưa hoàn tất.

## Cách hoạt động sau sửa

- Giữ chính Canvas và controller của Boot qua lần tải Main; không tạo Canvas ứng dụng khác. Màn tải nằm phía trên và chặn thao tác phía sau.
- Chỉ bỏ màn tải sau khi manager, menu và wallpaper đều báo sẵn sàng, qua thêm một frame cập nhật bố cục. Chữ lỗi khởi động được giữ ổn định nếu có lỗi; chi tiết exception nằm trong Console/Editor.log.
- Tải wallpaper được chọn trước. Chỉ dùng `Picture/Art/bs-menu.png` khi không có lựa chọn tải được. Nếu ảnh tùy chọn này cũng thiếu, vẫn cho vào menu với nền trung tính và ghi cảnh báo.
- Khi đổi wallpaper liên tiếp hoặc bắt đầu một lần Boot mới, kết quả cũ không được ghi đè lượt tải mới. Màn tải cũ và texture do loader sở hữu được dọn đúng vòng đời.

Luật Standard, mạng, deck, thẻ, hình Core, pet và wallpaper được chọn không bị đổi trong đợt này.

## Cách thử trên Windows

1. Dừng Play và đóng Unity trước khi cập nhật nhánh `battle-spirits-mdpro3-ui`. Dùng Unity **6000.0.24f1**, Windows x64.
2. Trong Addressables Groups, chọn **Play Mode Script → Use Asset Database** để kiểm tra scene và prefab source mới. Bản build phải build lại Addressables.
3. Mở `Assets/Scenes/Boot.unity`, nhấn Play. Màn tải có nền kín; các lần đổi chữ/dấu chấm không để lại chữ cũ. Khi tải xong, nhấn bắt đầu ở màn mở đầu **0.6.0** để hiện menu cùng wallpaper.
4. Thử wallpaper mặc định, một wallpaper giữ lại và lựa chọn không dùng wallpaper. Khởi động lại để kiểm tra mỗi lựa chọn; không nên thấy ảnh rồng dự phòng chớp trước wallpaper đã chọn.
5. Khi đang tải, thử chuột, Enter và gamepad: chưa được mở form phía sau. Khi hiện **NHẤN ĐỂ BẮT ĐẦU**, thao tác mới cho vào menu. Sau đó thử **Đấu với máy**, **Bộ bài**, **Cài đặt**.
6. Dừng Play, rồi nhấn Play lại vài lần. Không được giữ màn tải của lượt cũ hoặc nhân đôi menu.

Game View đang phóng **1.3x** trong ảnh người dùng; có thể thử **1x** để so sánh nét chữ. Việc phóng Game View không phải nguyên nhân đã xác nhận của lớp nền tải trong suốt.

Các kiểm tra tự động được ghi ở [báo cáo khởi động](battle-spirits-startup-validation.json). Môi trường cloud không có Unity Editor; chưa xác minh pixel, input hay chuyển cảnh thực bằng Windows Unity. Cần các bước trên để xác nhận trên máy người dùng.
