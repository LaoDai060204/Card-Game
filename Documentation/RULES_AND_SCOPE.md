# Luật và phạm vi triển khai

Đối chiếu ngày **27/10/2026**, Battle Spirits Nhật, **Eternal manual v17.1**. Standard có bộ thẻ hợp lệ và trình tự bước khác; bản nguyên mẫu này chọn Eternal theo yêu cầu.

## Các quy tắc được mô hình hóa

| Phần | Hành vi |
| --- | --- |
| Bộ bài | Ít nhất 40 lá; tối đa 3 lá cùng tên. Bộ mẫu gồm 40 lá. |
| Chuẩn bị | 5 Life; Reserve có 3 core thường và 1 Soul Core; rút 4 lá. |
| Đổi bài đầu | Mỗi người một lần, trả toàn bộ 4 lá vào deck, xáo rồi rút lại 4. |
| Lượt | Start → Core → Draw → Refresh → Main → Attack → End. Một số bước xử lý tự động vì bộ mẫu không có hiệu ứng tương tác tại đó. |
| Lượt đầu bên đi trước | Bỏ qua Core và Attack; vẫn Draw. |
| Core Step | Nhận thêm 1 core thường từ Void. |
| Refresh | Hồi phục Spirit; chuyển core trong Trash về Reserve. |
| Phí | Giảm theo symbol cùng màu trên sân, tối đa số biểu tượng giảm trên thẻ; phí không âm. |
| Level | Số core trên Spirit quyết định Lv/BP; xuống dưới yêu cầu Lv1 thì tiêu biến. |
| Attack | Spirit đang hồi phục có thể tấn công ngay lượt triệu hồi; tấn công làm Spirit mệt. |
| Flash | Bên phòng thủ có quyền trước. Hai bên bỏ qua liên tiếp thì sang bước tiếp. Cửa sổ thứ hai chỉ có khi chặn. |
| Block | Spirit đang hồi phục được chặn và chuyển sang mệt. BP bằng nhau thì cả hai bị phá hủy. |
| Core khi rời sân | Core trên Spirit bị phá hủy hoặc tiêu biến về Reserve. |
| Life | Nhận sát thương theo symbol; core Life mất đi chuyển sang Reserve; Life 0 thì thua. |
| Hết deck | Kiểm tra thua ở Start Step của chủ deck; rút lá cuối không lập tức thua. |

Soul Core được theo dõi như một core riêng, không thể nhân bản. Các test kiểm tra tổng core và số lượng Soul Core sau các hành động.

## Những gì còn cần bổ sung

- Bộ mẫu chỉ gồm Spirit có ô hiệu ứng `-`. Không tự suy diễn thực thi từ văn bản thẻ; các thẻ chưa hỗ trợ bị từ chối.
- Chưa có Magic, Nexus, Brave, Burst, Contract, Ultimate, nhiều màu/symbol pha trộn hoặc các keyword. Chỉ số thẻ mẫu một màu đã được lấy từ nguồn chính thức.
- UI bản đầu tự chọn core trả phí từ Reserve rồi trên sân; phí giảm được chốt trước khi trả, kể cả khi Spirit cho symbol sau đó tiêu biến. Có thể chuyển core trong Main Step để chuẩn bị. Chưa có hộp thoại chọn chính xác từng core nguồn hoặc số core đặt lúc triệu hồi; mặc định đặt đủ Lv1 rồi có thể điều chỉnh trong Main Step.
- Chưa có kiểm tra toàn bộ danh sách cấm/hạn chế theo giải đấu, sự kiện, tương tác errata hoặc giải quyết mọi trạng thái hợp lệ của game đầy đủ.
- Hai người chơi dùng chung máy. Các cửa sổ Flash hiện chỉ có thao tác bỏ qua vì bộ mẫu không có hiệu ứng Flash.
- Chưa có AI, đồng bộ mạng, chống gian lận, reconnect, replay hoặc tài khoản.

## Nguồn đối chiếu

- [Tổng quan Eternal](https://www.battlespirits.com/rule/official-rule-manual-eternal/)
- [Điều kiện thắng/thua](https://www.battlespirits.com/rule/official-rule-manual-eternal/page02.html)
- [Chuẩn bị](https://www.battlespirits.com/rule/official-rule-manual-eternal/page03.html)
- [Các bước lượt](https://www.battlespirits.com/rule/official-rule-manual-eternal/page05.html)
- [Core, Draw, Refresh](https://www.battlespirits.com/rule/official-rule-manual-eternal/page06.html)
- [Main và trả phí](https://www.battlespirits.com/rule/official-rule-manual-eternal/page07.html)
- [Attack, Block, Flash](https://www.battlespirits.com/rule/official-rule-manual-eternal/page08.html)
- [Thông báo Eternal v17.1](https://www.battlespirits.com/news/2026/rule-manual-eternal17-revised.php)

Khi thêm thẻ, cần bổ sung code hiệu ứng và test tình huống tương ứng trước khi đặt `implemented: true`.
