# Model 3D và trình diễn sân — 0.7.0

19 Spirit Standard đang chơi được có model skinned 3D riêng, tự dựng dựa trên hình dáng/màu/phụ kiện của artwork. Đây là geometry có chiều sâu, normal, màu đỉnh, mười khớp và chuyển động; không còn dùng tấm ảnh phẳng làm quái cho những lá đã hỗ trợ. Mặt lá bài vẫn hiện trên sân và giữ ảnh nguồn nguyên vẹn.

[Xem bảng 19 model dựng thử](Previews/SpiritModels/production-model-contact-sheet.jpg). Đây là render Blender từ đúng mesh production, với ánh sáng và nền studio để kiểm tra hình dáng; **không phải ảnh chụp Unity**. Model có phong cách stylized nhẹ, không phải model chính thức của Battle Spirits hoặc model trích từ Master Duel/Duel Links. [Danh mục, nguồn và thông số](../Tools/BattleSpiritsAssets/spirit-model-manifest.json).

## Cách dùng

1. Đóng Unity, cập nhật nhánh `battle-spirits-mdpro3-ui`. Mở Unity **6000.0.24f1**, Windows x64; Addressables **Use Asset Database**; chạy từ **Boot**, xác nhận **0.7.0**.
2. Chọn deck Standard, vào Solo, chọn **GIỮ 4 LÁ / ĐỔI 4 LÁ**. Đến Main, bấm lá trên tay rồi **TRIỆU HỒI**. Màn phóng to thẻ kết thúc bằng lá đáp vào sân; model hiện lên sau đó.
3. Nút **3D / ẢNH** ở đầu khung soi đổi giữa model và minh họa nổi. Model chỉ có cho 19 ID trong danh mục; thẻ khác/Nexus giữ ảnh nổi riêng, không được thay bằng quái chung. Không phải toàn bộ 270 thẻ đều có model.
4. Nút **THẤP / VỪA / CAO** cạnh đó đổi tỷ lệ render **0,8× / 1× / 1,35×** vùng hiển thị, với giới hạn cạnh dài **1280 / 1920 / 2560**. Mức CAO lấy mẫu ở độ phân giải cao hơn rồi thu về vùng hiển thị; mặc định VỪA. Không thêm MSAA hoặc hứa mức FPS. Các nút lưu lựa chọn nhưng không đổi luật, số Core hoặc gửi lệnh trận đấu.
5. Bấm mặt lá bài dưới model để soi; BP được in lớn hơn, Lv/Core gọn bên dưới. Khung trái vẫn cho đọc ảnh gốc và hiệu ứng đầy đủ. Status được dời về chân khung trái để không vẽ lên tay bài.

Game tạo mesh từ mã C# theo đúng ID khi cần, không cần Blender trên máy Windows và không tải model lúc vào trận. Mỗi model có một SkinnedMeshRenderer, một rig riêng; các bản sao cùng mã dùng chung mesh/material. Mesh và material được dọn theo vòng đời sân; đổi 3D/ẢNH tái sử dụng model đã tạo. Hình học mỗi model dưới 12.000 tam giác; số thực nằm trong manifest. Animation cập nhật transform và property block, không tạo lại mesh mỗi frame.

Để xem nhanh mà không cần đấu, vào menu **HOẠT CẢNH**, chọn mục bắt đầu bằng **3D · mã thẻ**, rồi thử **TRIỆU HỒI / TẤN CÔNG / PHÒNG THỦ**. Hai mục **ẢNH · BS01…** cũ vẫn dùng artwork và thuộc Eternal, không được thêm vào deck Standard.

## Hình ảnh và hoạt ảnh

- Model giữ điểm nhận diện của từng lá: bò xương, pháp sư cầm trượng, nữ satyr thổi kèn, thỏ tai rủ, sứa, cá đao, rối đấu kiếm, thú ăn kiến, sâu giáp… Các hình dáng được đối chiếu artwork từng mã.
- Chuyển động idle/triệu hồi/tấn công/chặn/nhận đòn/biến mất/thắng-thua dùng rig và đường cong thời gian. Floor card vẫn thể hiện trạng thái exhausted; model đổi tư thế nhẹ.
- Shader cho model dùng normal, màu đỉnh, ánh sáng chính/fill, rim và highlight. Bóng tiếp xúc mềm dưới chân là hiệu ứng tính bằng shader; không thêm atlas bóng realtime hoặc bloom nặng.
- Sân thêm phản sáng/radial nhẹ và chi tiết nền cố định. Chữ bên đối thủ được quay về người xem. Các màu Core/Soul Core, pet, wallpaper, ảnh gốc, bộ luật và giao thức mạng giữ nguyên.
- Điểm đáp cinematic được chuyển từ tọa độ riêng của ảnh sân sang tọa độ màn hình; khung soi bên trái không còn làm lá đáp lệch. Việc reveal model chỉ tua lại chuyển động model, không phát lại sự kiện luật/âm thanh hoặc di chuyển mặt thẻ đã đáp.

Tham khảo trình bày từ trang chính thức [Master Duel](https://www.konami.com/yugioh/masterduel/us/en/) và [Duel Links](https://www.konami.com/yugioh/duel_links/en/). Chỉ dùng để nghiên cứu bố cục/phối cảnh và cách làm nổi thông tin; không tải/trích model game. Nguồn artwork chính thức của từng lá và checksum nằm trong manifest/catalog hiện có.

## Kiểm tra còn cần trên Windows

Thử triệu hồi, tấn công/chặn, đổi Core, đổi chế độ 3D/ẢNH và các mức chất lượng; kiểm tra BP, màu quái, không có vật liệu hồng và không che Core. Thử resize, replay, ngắt mạng và mở lại trận; mặt thẻ/rig phải đúng ID. Đo FPS/frame time bằng Unity Profiler trên máy đích nếu cần so sánh hiệu năng.

[Báo cáo kiểm tra](battle-spirits-3d-spirit-validation.json) ghi kiểm tra mesh/rig, trạng thái UI, tài nguyên và render Blender. Chưa có Unity Windows trong môi trường này, nên shader/render thực, input thiết bị và bản build vẫn cần xác nhận ở Editor.

Backup: `backup/2026-11-13-before-3d-spirit-presentation` → `8f8a6cad29feaf5ec667e9af447ca6d3b7b29584`.
