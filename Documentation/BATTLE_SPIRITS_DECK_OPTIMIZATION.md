# UI MDPro3 và Bộ bài — 0.6.1

Game vào thẳng **Boot → menu** theo kiểu MDPro3. Nền tải kín và điều kiện chờ manager/menu/wallpaper của 0.5.2 vẫn được giữ để tránh chữ có vệt và background hiện trước menu. Boot không tạo màn title hay đợi nhấn bắt đầu của 0.6.0.

Các form menu, Bộ bài, Solo và Chơi mạng vẫn dùng prefab MDPro3 cùng bố cục/khung/nút template; dữ liệu và luật là Battle Spirits Standard. Lựa chọn wallpaper, pet và dữ liệu cá nhân được giữ.

## Tối ưu Bộ bài

Trước đây mỗi lần tìm kiếm hoặc đổi trang, code xóa rồi tạo lại tối đa 42 ô bộ sưu tập. Mỗi thao tác thêm, bớt hoặc sắp xếp cũng tạo lại toàn bộ ô trong deck. Kết quả lọc và thứ tự sắp xếp được tính lại ngay cả khi chỉ chuyển trang.

Bản này tái sử dụng các ô thẻ, cập nhật nội dung và thao tác của chúng theo lá đang hiển thị. Ảnh hiện tại được giữ khi còn hợp lệ; ảnh cũ tải muộn không được ghi đè lá mới. Kết quả lọc/sắp xếp được giữ cho tới khi tiêu chí thay đổi. Thao tác gõ liên tiếp được gom trong **150 ms** trước khi cập nhật kết quả; nút tìm kiếm và các bộ lọc vẫn áp dụng ngay. Các phần tử của vùng chi tiết đã tìm được được lưu lại để tránh quét cây giao diện nhiều lần.

Giới hạn thẻ cùng tên, kiểm tra pool Standard, lưu deck và loại thẻ được phép dùng trong trận vẫn theo luật hiện có. Đánh giá tự động tập trung vào số đối tượng và yêu cầu ảnh thực trong harness; chưa có số FPS hay thời gian frame từ Unity Windows.

Kiểm tra có kiểm soát ghi nhận: tám lần refresh cùng truy vấn và 12 vòng lật trang tới/lùi không tạo thêm GameObject; refresh giữ nguyên truy vấn không yêu cầu lại ảnh. Pool bộ sưu tập giới hạn **42 ô**, sắp xếp deck giữ lại widget và ảnh hiện có. Tổng cộng **67 luồng UI/session đạt**, gồm 10 tình huống mới cho Bộ bài; tám kiểm tra title cũ chỉ kiểm tra helper lưu lại, không mô tả luồng Boot hiện tại.

## Cách thử

1. Đóng Unity, cập nhật nhánh `battle-spirits-mdpro3-ui`, mở lại bằng **6000.0.24f1**, Windows x64. Addressables Groups dùng **Use Asset Database**.
2. Mở scene **Boot**, nhấn Play. Game tự vào menu **0.6.1**; khung, nút và form theo template MDPro3.
3. Vào **Bộ bài**. Gõ nhanh tên/mã thẻ, thử `cost:6`, `bp:6000`, đổi màu, tab trạng thái và sắp xếp. Kết quả cuối phải đúng; chữ đang gõ không bị mất.
4. Đổi trang liên tiếp, bấm thẻ rồi thêm/bớt nhiều lá, kể cả các lá trùng nhau. Ảnh, phần chi tiết và lá bị xóa phải đúng với ô đang bấm.
5. Tìm một kết quả, xóa tìm kiếm, rồi đổi trang lại. Ô bị ẩn phải hiện đúng lá mới, không giữ ảnh hoặc thao tác cũ.
6. Rời Bộ bài khi đang gõ rồi mở lại; kiểm tra kết quả phù hợp với nội dung ô tìm kiếm. Lưu deck, mở lại và thử chọn deck đó trong Solo.

[Báo cáo kiểm tra](battle-spirits-deck-optimization-validation.json) ghi kết quả và phạm vi từng phép kiểm tra. Cần thử Unity Profiler/Play trên máy Windows để đánh giá độ mượt thực tế.
