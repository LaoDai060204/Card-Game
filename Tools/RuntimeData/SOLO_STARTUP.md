# Kiểm tra khởi động Solo

Lỗi được xác nhận từ log Windows ngày 28/10/2026: kết nối localhost thành công, WindBot tìm CDB theo giao diện `vi-VN`, báo không tìm thấy rồi dừng ở `Press any key to quit...`. Trong chế độ nhúng vào Unity, nhánh lỗi này gọi `Console.ReadKey()` và `Environment.Exit()` của cả process.

## Thay đổi

- Dùng `Language.GetDataConfig()` để giao diện Việt đọc CDB tiếng Anh có sẵn. Đường dẫn CDB tùy chỉnh vẫn được giữ.
- Thiếu CDB trở thành lỗi có thể bắt và thông báo; bỏ thao tác chờ console hoặc thoát Unity.
- Cho phép chưa có thư mục `Expansions`.
- Chỉ đọc `DeckAttribute` của các Executor hợp lệ; không khởi tạo mọi attribute của toàn bộ Assembly-CSharp trên luồng AI.
- Danh sách Executor được công bố đầy đủ khi nhiều bot khởi tạo đồng thời. Nếu không có Executor Normal để chọn ngẫu nhiên, báo lỗi thay vì lặp vô hạn.
- Worker AI chạy nền, có ranh giới bắt lỗi và không khởi động muộn sau khi game đã dừng.
- Chặn khởi động máy chủ trùng; dừng native server trên worker nền và giữ trạng thái bận cho đến khi phiên cũ kết thúc. Không dùng `Thread.Abort` cho luồng máy chủ.

## Tên AI vượt giới hạn giao thức

Log tiếp theo ngày 29/10/2026 xác nhận WindBot đã vượt qua bước đọc dữ liệu nhưng dừng khi gửi `Horus the Black Flame Dragon LV8`: `WriteUnicode` báo tên quá dài cho trường có độ dài 20. Trường tên `PlayerInfo` chỉ có 40 byte: tối đa 19 đơn vị UTF-16 và một ký tự NUL kết thúc. Bản Debug của bộ ghi chủ động báo lỗi khi vượt giới hạn này.

`GameClient` rút gọn riêng tên gửi qua mạng, không cắt đôi cặp surrogate của ký tự Unicode. Tên đầy đủ trong cấu hình, `Username`, bộ bài và lời thoại vẫn được giữ. Quy tắc kiểm tra độ dài trong `BinaryExtensions` và kích thước gói tin không thay đổi. Cách xử lý này áp dụng cho mọi tên AI dài, kể cả khi chọn AI ngẫu nhiên.

## Kiểm tra đã chạy

| Bộ kiểm tra | Kết quả |
| --- | --- |
| WindBot startup, resolver, Executor discovery | 16 tình huống thành công |
| Solo launcher, worker error boundary, PLAY lặp | 8 tình huống thành công |
| Vòng đời server với native stub | 12 tình huống và 24 race dừng ngay thành công |
| P/Invoke production + `libygoserver.so` đi kèm trên Linux | 3 lần TCP handshake, 2 lần khởi động lại, 8 race dừng ngay thành công |
| Gửi tên AI và tuần tự ba gói tin khởi động | Debug và Release đều qua 552 trường hợp: 14 kiểm tra biên/serializer và 538 cấu hình AI, trong đó 125 tên quá dài |

Các harness biên dịch các class/phương thức production theo C# 9. Probe native dùng mã `YgoServer.cs` production và thư viện Linux đi kèm trong thư mục tạm, không chạy Unity Editor. Nhánh AI dùng CDB Việt thiếu, CDB Anh có sẵn, thư mục mở rộng không tồn tại và các đường dẫn tùy chỉnh đều có kiểm tra.

Kiểm tra tên AI biên dịch nguyên các class production `GameClient`, `WindBotInfo`, `GamePacketFactory`, `BinaryExtensions` và enum `CtosMessage`. Transport giả lập thu các gói tin để kiểm tra giới hạn 19/20, tiếng Việt, cặp surrogate, ký tự NUL, tên đầy đủ, bộ bài, mật khẩu và phiên bản giao thức. Đây là kiểm tra serialization, không phải kết nối mạng hay chạy Unity.

```bash
python Tools/Tests/WindBotStartup.py
python Tools/Tests/WindBotHandshake.py
python Tools/Tests/SoloLaunchRegression.py
python Tools/Tests/YgoServerLifecycle.py
```

Các lệnh dùng .NET SDK 10; có thể thêm `--dotnet /path/to/dotnet` nếu SDK không có trong PATH. Trên Linux, thêm `--native-linux` vào lệnh YgoServerLifecycle để chạy probe với thư viện native thật.

## Kiểm tra trên Windows cần thực hiện

Môi trường phát triển không có Unity Editor: chưa xác minh Play, render hoặc toàn bộ trận đấu trong Windows Unity. Kiểm tra native Linux không thay thế kiểm tra `ygoserver.dll` trên Windows.

Đóng Editor cũ đang treo, cập nhật mã, mở `Assets/Scenes/Boot.unity` trong **6000.0.24f1** rồi vào Đấu với máy và PLAY. Xác nhận AI vào phòng, chọn bộ bài của bạn nếu cần và bắt đầu trận. Thử quay lại, tạo trận tiếp theo và dừng Play.

Nếu vẫn gặp lỗi, lấy đoạn cuối log quanh `WindBot starting...` và dòng lỗi mới. Tránh gửi các dòng chứa access token của Unity ở phần đầu log.
