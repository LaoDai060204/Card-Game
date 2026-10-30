# Luật và phạm vi Battle Spirits

Bản chuyển đổi dùng **Battle Spirits Nhật — Eternal**, giao diện tiếng Việt, Unity **6000.0.24f1**. Mốc luật là Eternal manual v17.1; bộ thẻ và danh sách hạn chế được đối chiếu nguồn chính thức ngày 30/10/2026. Không thay luật này bằng Saga hoặc Standard.

## Trận đấu hiện hỗ trợ

- Hai bộ luyện tập 40 lá, lấy từ 16 Spirit chính thức không có hiệu ứng. Giới hạn theo tên gốc tiếng Nhật; dữ liệu hạn chế áp dụng cho các bản in cùng tên. Ngoại lệ số bản chỉ được chấp nhận khi metadata đã được xác minh.
- Mở đầu: 5 Life, 3 Core thường và 1 Soul Core trong Reserve; rút 4 lá, mỗi người có một lần đổi toàn bộ bài đầu.
- Start → Core → Draw → Refresh → Main → Attack → End. Một số bước tự động vì bộ thẻ hiện tại không có hiệu ứng tương tác tại đó. Người đi trước bỏ Core và Attack ở lượt đầu, vẫn Draw.
- Nhận Core từ Void; trả phí và giảm phí theo màu/symbol; đặt đủ Core cho Lv1; chuyển Core trong Main để đổi Level/BP. Soul Core là một Core riêng, không được nhân bản.
- Spirit được tấn công ngay lượt triệu hồi nếu đang hồi phục. Tấn công/chặn làm mệt. Bên phòng thủ có quyền Flash trước; hai lần bỏ qua liên tiếp chuyển bước. Cửa sổ Flash thứ hai xuất hiện khi có chặn.
- So BP, bằng nhau thì cả hai bị phá hủy; Spirit xuống dưới Core duy trì Lv1 thì tiêu biến. Core trên Spirit rời sân về Reserve.
- Sát thương theo symbol; Core Life mất đi về Reserve. Life bằng 0 thì thua. Hết deck được kiểm tra ở Start Step của chủ deck, không thua ngay khi rút lá cuối.
- Nhận thua, kết thúc trận và kiểm tra bảo toàn Core/Soul Core. Chế độ chung máy có màn che khi đổi người; lịch sử trận cục bộ lưu seed và chuỗi thao tác để chạy lại với cùng danh mục.

## Mạng và giao diện

Menu, hình nền, font, artwork của nút, field và Mate được dùng lại từ template. Các màn hình Battle Spirits có lớp điều khiển mới để không gọi luật hoặc thông điệp Yu-Gi-Oh. Asset trang trí không tạo hiệu ứng luật.

Server TCP mới dùng cùng engine C# với chế độ cục bộ; LAN và kết nối trực tiếp qua Internet dùng chung giao thức. Server kiểm tra danh mục, bộ bài, người gửi và lượt/pha trước khi nhận thao tác. Mỗi client chỉ nhận tay bài của mình cùng trạng thái công khai. Mất kết nối trong trận dẫn đến xử lý nhận thua; người thay thế không được nhận tay bài của phiên cũ.

## Giới hạn còn lại

Danh mục có 256 thẻ, nhưng 240 thẻ mới hiện là dữ liệu tra cứu. Chưa có engine cho Magic, Nexus, Brave, Burst, Contract, Ultimate và các keyword/hiệu ứng đặc biệt. Dữ liệu nhập không tự trở thành code hiệu ứng; các thẻ chưa hỗ trợ bị từ chối khi bắt đầu trận tự động.

Việc trả phí hiện tự lấy Core từ Reserve rồi trên sân, chốt giảm phí trước khi trả. Triệu hồi mặc định đặt đủ Lv1; người chơi có thể điều chỉnh Core trong Main. Chưa có bộ chọn chi tiết mọi nguồn Core trong cùng một thao tác triệu hồi.

Chưa có AI, tài khoản/ghép trận MyCard cho Battle Spirits, tự mở cổng router, reconnect tiếp tục trận hoặc replay trận online. Dữ liệu hạn chế có ghi nhận cặp thẻ không được đi cùng nhau, nhưng chưa có xử lý mọi tổ hợp đặc biệt của Eternal; các thẻ liên quan chưa được bật chơi. Không coi đây là triển khai toàn bộ luật/errata/thẻ của Eternal.

Field/pet gọi tài nguyên Master Duel hiện có. Chưa có mô hình quái vật 3D chính thức riêng cho từng thẻ Battle Spirits. Play/render/build trong Windows Unity vẫn cần kiểm tra bằng Editor thật.

## Nguồn đối chiếu

- [Eternal manual](https://www.battlespirits.com/rule/official-rule-manual-eternal/)
- [Chuẩn bị](https://www.battlespirits.com/rule/official-rule-manual-eternal/page03.html)
- [Trình tự lượt](https://www.battlespirits.com/rule/official-rule-manual-eternal/page05.html)
- [Core, Draw, Refresh](https://www.battlespirits.com/rule/official-rule-manual-eternal/page06.html)
- [Main và trả phí](https://www.battlespirits.com/rule/official-rule-manual-eternal/page07.html)
- [Attack, Block, Flash](https://www.battlespirits.com/rule/official-rule-manual-eternal/page08.html)
- [Danh sách cấm/hạn chế](https://www.battlespirits.com/rule/limited.php)
- [Danh mục thẻ chính thức](https://www.battlespirits.com/cardlist/)
