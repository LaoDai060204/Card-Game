# Tìm kiếm và bố cục Bộ bài — 0.6.2

Ảnh người dùng ở 0.6.1 cho thấy một lá trong vùng kết quả, thông báo không có bài vẫn hiện và khung giao diện bó vào giữa cửa sổ rộng. Kiểm tra serialized prefab xác định các lỗi cụ thể:

- **Ô tìm kiếm không có bề mặt nhận click:** TMP_InputField không có `targetGraphic`, các hình nền/chữ thường đều tắt `raycastTarget`. Nút xóa nhỏ là thành phần duy nhất nhận click, nhưng controller ẩn nó khi ô trống. Binding mới bật vùng nền nhập và gán nó làm `targetGraphic`, giữ đúng text/viewport/placeholder đã serialize.
- **Lưới collection bị tắt:** prefab dùng `GridLayoutGroup` có `m_Enabled: 0`; code cũ lấy lại component rồi chỉ đổi số cột/kích thước. Bản sửa bật grid/fitter và đặt Content bám chiều rộng viewport, nên các ô không còn dùng chung vị trí template.
- **Thông báo rỗng sai đối tượng:** `#Text_NoItemMessageTMP` là một node độc lập bên cạnh ScrollRect. Ẩn `NoItemButton` không ẩn nó. Nay label này theo đúng số kết quả tìm kiếm.
- **Bố cục cố định 1920×1080:** ba container cũ căn giữa với chiều rộng cố định, để trống hai bên ở tỷ lệ rộng như ảnh. Runtime hiện kéo giãn các container và phân vùng ba cột theo SafeArea.

## Giao diện hiện tại

Giữ ba form/khung MDPro3 hiện có. Khung trái có ảnh thẻ lớn, mã, Cost/BP/Symbol, hiệu ứng cuộn và nút thêm/bớt; khung giữa có tên deck và danh sách cuộn; khung phải có ba tab, nhập tìm kiếm, bộ lọc và kết quả. Cột thẻ/kích thước ô thay theo chiều rộng. Thanh trên có chữ **BỘ BÀI** và các thao tác có nhãn; nhóm sáu icon ngoại hình và bộ đếm 100 cũ chưa dùng đã được ẩn. Nút lật trang cùng tổng kết quả nằm riêng bên dưới lưới.

Gõ cập nhật sau 150 ms ngừng nhập; **Enter** hoặc nút **TÌM** áp dụng ngay. Tìm theo tên/mã có trong catalog, loại/bộ/hệ/hiệu ứng, `cost:6` hoặc `bp:6000`. Văn bản thẻ gốc vẫn là tiếng Nhật; bản này không thêm bản dịch tên thẻ sang tiếng Việt. Pool và quyền dùng thẻ Standard không đổi.

Bố cục được áp dụng lên hierarchy runtime; file prefab gốc và GUID không bị thay. Không thêm Canvas ứng dụng. Giữ tối ưu pool/cache, deck chưa lưu, kiểm tra thẻ trùng và ảnh tải muộn của 0.6.1.

## Kiểm tra trên Windows Unity

1. Đóng Unity, pull nhánh `battle-spirits-mdpro3-ui`. Mở bằng **6000.0.24f1**, Windows x64; Addressables Groups → **Use Asset Database**. Bản build cần build lại Addressables.
2. Mở **Boot**, Play, xác nhận menu **0.6.2**, vào Bộ bài. Khi có kết quả, không được thấy thông báo không tìm thấy hoặc các lá đè chung một vị trí.
3. Bấm vùng trống giữa ô tìm kiếm, gõ mã thẻ hoặc `cost:4`, bấm Enter. Thử tiếp `bp:6000` và nút TÌM. Mã được in ngay dưới mỗi lá có thể dùng để thử.
4. Gõ chuỗi không khớp: chỉ hiện thông báo rỗng. Xóa lọc: lưới trở lại, thông báo biến mất. Thử các tab và nút đổi màu/sắp xếp.
5. Thử Game View **16:9**, **4:3** và cửa sổ rộng. Ba khung phải dùng chiều rộng cửa sổ, ảnh preview giữ tỷ lệ và lưới cuộn tới những hàng cuối. Thử cuộn phần hiệu ứng dài và danh sách deck.
6. Thêm/bớt/sắp xếp deck, mở lại form và lưu deck. Kiểm tra ô được bấm luôn đúng lá đang hiển thị.

Lượt test cũ chỉ gán text/gọi callback và không nhập các cờ raycast/layout thật từ prefab. Fixture mới giữ loại component, cờ enabled/raycast/interactable, tham chiếu TMP, layout và anchor/size của nguồn. [Báo cáo](battle-spirits-deck-input-layout-validation.json) ghi rõ kiểm tra hợp đồng/khung hình toán học bằng stand-in. Môi trường cloud không có Unity Editor; chưa thực thi GraphicRaycaster, gõ phím, layout pass hay render pixel trên Windows. Các bước trên cần được xác nhận trên máy người dùng.
