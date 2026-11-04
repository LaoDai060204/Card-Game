# Luật và phạm vi Battle Spirits 0.2.0

Unity **6000.0.24f1**, Windows x64, giao diện tiếng Việt, **Battle Spirits Nhật — Eternal**. Mốc luật là Eternal manual v17.1; dữ liệu thẻ được đối chiếu ngày 31/10/2026. Không thay bằng Saga hoặc Standard.

Danh sách booster mới nhất được kiểm tra lại ngày 04/11/2026: BS76 vẫn là đợt Eternal đã nhập, 26RBS03 là đợt Standard mới hơn đã nhập. Việc kiểm tra danh sách phát hành không thay ngày truy cập của các bản ghi thẻ gốc.

## Phần đang chơi được

Danh mục có 261 thẻ chính thức; 21 thẻ được bật cho trận tự động: 16 Spirit không hiệu ứng và năm thẻ BS01 có registry rõ ràng. `CardDefinition.IsPlayable` kiểm tra metadata/nhóm luật; một cờ `implemented=true` trên thẻ lạ không tự tạo code hiệu ứng.

- Bộ bài ít nhất 40 lá, giới hạn theo tên gốc và dữ liệu hạn chế đã xác minh. Có ba bộ mẫu 40 lá cho Solo.
- Mở đầu: 5 Life, 3 Core thường và 1 Soul Core trong Reserve, 4 lá trên tay; mỗi người được đổi toàn bộ bài đầu một lần.
- Start → Core → Draw → Refresh → Main → Attack → End. Người đi trước bỏ Core và Attack ở lượt đầu, vẫn Draw. Các bước không có lựa chọn được tự chuyển.
- Trả Cost và giảm phí theo symbol; đặt Core duy trì Level; chuyển Core trong Main, trao đổi Soul/Core đồng thời và xử lý tiêu biến khi Spirit xuống dưới Lv1. Soul Core được theo dõi riêng và kiểm tra bảo toàn.
- Spirit hồi phục có thể tấn công ngay lượt triệu hồi. Tấn công/chặn làm mệt. Bên phòng thủ có Flash trước; hai lần bỏ qua liên tiếp chuyển bước. Nếu chặn, có Flash lần hai trước khi so BP.
- Tấn công mất Spirit hoặc Spirit chặn biến mất trong Flash vẫn xử lý hết quyền Flash theo luật, sau đó giải quyết phần trận đấu còn hợp lệ. So BP bằng nhau làm cả hai bị phá hủy.
- Sát thương theo symbol; Core Life mất đi về Reserve. Life về 0 thì thua. Hết Deck kiểm tra ở Start Step của chủ deck.
- Nhận thua và kết thúc trận; replay Solo lưu seed/deck/thao tác cùng fingerprint danh mục và schema để dựng lại trận phù hợp.

| Thẻ có hiệu ứng đã triển khai | Hành vi được kiểm tra |
| --- | --- |
| BS01-X01 龍皇ジークフリード | Awaken chuyển Core từ nhiều Spirit theo một thao tác; hiệu ứng bị phá hủy ở Lv3 thêm Core Life |
| BS01-097 飛龍ヴァルキュリウス | BP +6000 khi chặn, áp dụng đúng ngữ cảnh chiến đấu |
| BS01-113 侵食されゆく銀世界 | Đặt Nexus; Lv1 không cần Core theo thẻ; giảm phí bằng symbol; hồi Core ở Attack Step đối thủ và thêm Core khi Spirit bị phá hủy ở Lv2 |
| BS01-115 アウェイクン | Chuyển tối đa 3 Core từ Reserve, sau đó rút bài; có thể dùng Main/Flash và chọn 0 Core để chỉ rút bài, kể cả khi không có Spirit |
| BS01-117 ダブルドロー | Main rút 2 lá hoặc Flash BP +2000 trong lượt |

Registry `bs01-effects-v1` giữ thông số và văn bản Nhật đối chiếu với bản in cụ thể. Những hiệu ứng này không được suy ra tự động bằng cách phân tích tùy ý văn bản nhập từ web.

## Giao diện, Solo và mạng

Năm prefab màn hình gốc và ba prefab Parts được đổi controller ngay trong tài nguyên hiện tại; hierarchy, geometry và các tham chiếu artwork gốc có kiểm tra. Các vùng thẻ/nhãn/nút Battle Spirits dùng form đó và cùng hệ thống Program/Servant. Lớp overlay của bản 0.1.0 đã bỏ; Solo hiện là người chơi đấu WindBot.

Executor Battle Spirits trong WindBot nhận `FilteredMatchView`: tay của chính bot, Life/Core, field, discard và số lượng bài công khai. Nó không nhận tay đối thủ, thứ tự deck hoặc seed. Bot được điều phối theo frame, có trì hoãn quyết định, hủy phiên và đường xử lý khi authority từ chối thao tác. Ba profile có mức ưu tiên chiến thuật khác nhau; kết quả thắng baseline ngẫu nhiên không chứng minh năng lực thi đấu.

Server TCP C# dùng cùng Core. Giao thức phiên bản 2, cổng mặc định 7777; danh tính actor lấy từ kết nối. Server xác minh phiên bản, danh mục, deck, lượt/pha và mục tiêu. Snapshot chỉ có tay của người nhận và trạng thái công khai. Mất kết nối trong trận dẫn đến xử lý nhận thua; client mới vào lobby mới, không nhận tay của phiên cũ.

Field/Spirit/Nexus và model boss do dự án tự dựng. Hai boss có ảnh closeup/render từ Blender; Spirit còn lại dùng hình học thay thế theo màu. Mate Master Duel là trang trí, không quyết định luật. Nhạc/hiệu ứng WAV mới do tổng hợp. Menu Hoạt cảnh có màn xem thử không cấp quyền gửi lệnh trận đấu.

## Giới hạn còn lại

- **240 thẻ chỉ tra cứu**. Chưa triển khai toàn bộ Brave, Burst, Contract, Ultimate, các keyword khác và mọi tương tác/errata của Eternal. Standard trong danh mục chưa mặc định hợp lệ ở Eternal.
- Nguồn Core để trả phí vẫn được chọn tự động, chốt giảm phí trước khi trả. Triệu hồi đặt Core tối thiểu và có thể chỉnh trong Main; chưa có bộ chọn chi tiết mọi nguồn Core/Level trong một thao tác trả phí.
- Chưa có tài khoản/ghép trận MyCard cho Battle Spirits, xếp hạng, tự mở cổng router, reconnect tiếp tục trận hoặc replay online đầy đủ.
- Không có model chính thức riêng cho mọi thẻ. Model/closeup boss mới là diễn giải fan-made kiểu low-poly; mặt thẻ vẫn là artwork chính thức.
- Play/render/build và thao tác thiết bị thật trong Windows Unity, LAN hai máy Windows và Internet công khai chưa được kiểm thử trong môi trường phát triển này.

## Nguồn đối chiếu

- [Eternal manual](https://www.battlespirits.com/rule/official-rule-manual-eternal/)
- [Chuẩn bị](https://www.battlespirits.com/rule/official-rule-manual-eternal/page03.html)
- [Trình tự lượt](https://www.battlespirits.com/rule/official-rule-manual-eternal/page05.html)
- [Core, Draw, Refresh](https://www.battlespirits.com/rule/official-rule-manual-eternal/page06.html)
- [Main và trả phí](https://www.battlespirits.com/rule/official-rule-manual-eternal/page07.html)
- [Attack, Block, Flash](https://www.battlespirits.com/rule/official-rule-manual-eternal/page08.html)
- [Danh sách cấm/hạn chế](https://www.battlespirits.com/rule/limited.php)
- [Danh mục thẻ chính thức](https://www.battlespirits.com/cardlist/)

Các HTML fixture của năm thẻ được giữ tại `Tools/BattleSpiritsCards/Fixtures`; bài kiểm tra luật bổ sung nằm trong `Tests/BattleSpirits/Core/AbilityTests.cs`.
