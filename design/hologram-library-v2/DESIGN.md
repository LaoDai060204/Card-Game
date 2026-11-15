# Mở rộng thư viện hologram

V2 tiếp nối [thiết kế v1](../hologram-arena-v1/DESIGN.md), không thay đổi quy trình chốt tài nguyên trước khi tích hợp Unity. Bộ bốn deck có 61 thẻ phân biệt: 42 Spirit, 3 Nexus và 16 Magic. Thêm Pisketona từ mẫu người dùng thành 62 thẻ, trong đó 43 có hologram.

## Những gì đã làm

Tạo 41 silhouette mới từ ảnh gốc đúng mã thẻ; giữ nguyên hai mẫu cũ. Giữ tư thế, màu, trang bị và phần cơ thể nhìn thấy; bỏ khung, chữ và nền cảnh. Mỗi PNG có alpha thật và nguồn/hash riêng. Đây là bản dẫn xuất bằng công cụ hình ảnh, không được khẳng định là segmentation pixel chính xác. Ritenia cũ vẫn giữ những mảnh nền còn sót của nguồn cung cấp.

Nexus/Magic được chọn và đọc thông tin như các thẻ khác, nhưng không tự dựng thành quái. Chúng giữ thẻ nằm ngang. Không tạo thêm boss hoặc tự phát cutscene cho các lá X hiếm; Ritenia là mẫu boss đã có.

## Điều khiển và tải tài nguyên

Tìm kiếm tên/mã không phân biệt hoa thường hay dấu tên Roman; lọc màu và loại thẻ hoạt động đồng thời. Chọn nhanh nhiều lá phải giữ lá được chọn cuối, không để phản hồi tải chậm ghi đè. Lỗi tải/giải mã có thông báo và đường thử lại bằng cách chọn thẻ.

Metadata tách khỏi payload ảnh. Chỉ nạp payload của các lá đang xem; giữ tối đa sáu cặp texture và bảo vệ các lá còn dùng trên sân/inspector. Shader chiếu UV theo alpha bounds để căn điểm chân và tỉ lệ, không cắt/sửa PNG gốc. Giới hạn chiều rộng nhân vật để cánh/vũ khí không lấn sang thẻ khác. Motion Reduced có hình tĩnh và không giữ RAF chạy nền.

Mỗi gói màu tự chứa thư viện, code và ảnh của màu đó; giữ thêm hai mẫu v1 làm bootstrap/boss preview. Có gói PNG riêng đủ 43 ảnh cho bước tích hợp tài nguyên sau này. Không chứa mã nguồn Unity hoặc thay đổi các nhánh game.

## Bàn giao sau này

Dùng Card ID làm khóa nối ảnh thẻ, cutout, alpha bounds, màu, tỉ lệ và metadata boss được chốt. Khi triển khai, engine game xác nhận summon trước, renderer mới phát hiệu ứng; code presentation không tự trừ Core, đổi BP hay kích hoạt hiệu ứng gameplay. Các nguồn gốc và lựa chọn dịch English vẫn như thư viện review trước đó.
