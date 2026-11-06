# Ngọc Core và tỷ lệ ảnh thẻ — 0.5.1

Theo lựa chọn ngày 06/11/2026, các ảnh ngọc được dùng để hiển thị **Core trên sân**: Core thường mặc định xanh, Soul Core đỏ. Life, Reserve, Trash và Core trên Spirit đều dùng ảnh ngọc có mặt hướng về camera thay cho khối cầu đơn giản. Màu ngọc không tạo loại tài nguyên mới và không thay đổi số Core, trả phí hay luật Standard.

Vào **Cài đặt → Ngoại hình ngọc Core** để chọn xanh, tím, lục, vàng hoặc trắng cho Core thường. Soul Core luôn đỏ để phân biệt. Đây là lựa chọn hiển thị cục bộ; hai máy LAN có thể dùng ngoại hình khác nhau mà trạng thái trận vẫn giống nhau. Ảnh được lấy từ bộ skin đã có; nguồn và khác biệt độ phân giải với ảnh inline nằm trong [skin-sources.json](../Data/BattleSpirits/skin-sources.json).

Với ảnh thẻ, đã xác nhận một lỗi bố cục: vùng ảnh trên tay bị kéo ngang (khoảng 0,96 so với tỷ lệ gốc khoảng 0,687 ở cửa sổ 16:9). Bản này tách vùng artwork và nhãn mã thẻ, giữ tỷ lệ gốc bằng `FitInParent`; tỷ lệ được cập nhật từ texture thực tế, cả khi lấy lại từ cache hoặc dùng vùng UV minh họa.

Ảnh tải từ PNG vẫn giữ kích thước gốc, ví dụ **813×1184**. Bộ tải đặt rõ Bilinear, Clamp và anisotropy 8. Không sửa hay phóng lại các PNG chính thức. Camera UI Battle Spirits được giữ render scale 1,0 và tắt dynamic resolution; scale của cảnh 3D cũ vẫn theo thiết lập riêng. Kiểm tra mã cho thấy URP UI đã lưu scale 1 từ trước, nên không khẳng định mọi hiện tượng mờ trong ảnh chụp đều do giảm render scale.

Khi thử trong Unity, đặt **Game View Scale = 1x**, dùng kích thước Game View đủ lớn và mở chi tiết thẻ để đọc chữ nhỏ. Ảnh đã thu nhỏ để vừa bàn đấu hoặc ảnh chụp màn hình phóng lớn không thể hiện đủ chi tiết của PNG gốc.

Các kiểm tra tự động dùng API Unity thay thế, không phải kiểm tra pixel hoặc phát hiện bằng mắt trong Editor thật. **Chưa chạy Play/render/build trong Unity Windows.** Xem [báo cáo kiểm tra](battle-spirits-core-gem-quality-validation.json).

Backup trước thay đổi: `backup/2026-11-06-before-core-gem-rendering` → `e7687540`. Unity vẫn là **6000.0.24f1**, Standard có **19 thẻ hỗ trợ**, giao thức LAN **3**.
