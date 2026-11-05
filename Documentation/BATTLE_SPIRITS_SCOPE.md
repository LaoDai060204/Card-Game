# Luật và phạm vi Battle Spirits Standard 0.3.0

Unity **6000.0.24f1**, Windows x64, giao diện tiếng Việt. Chủ dự án đã chọn **Standard theo các PDF được cung cấp**, giữ bố cục UI và thay đồ họa Master Duel, ngoại trừ pet/wallpaper cùng phụ thuộc cần giữ. Bản cập nhật này không đổi sang Saga. Core vẫn có chế độ Eternal riêng để kiểm tra tương thích; UI và Solo/LAN mặc định dùng Standard rõ ràng.

Nguồn Standard là **Official Rules/Manual v1.0**. Mùa được xác minh **01/04/2026–31/03/2027 chỉ dùng block A**; metadata/importer không tự coi thẻ BS01 là Standard. [Đối chiếu PDF](BATTLE_SPIRITS_RULE_SOURCES.md) ghi trang, điều khoản, phiên bản và SHA-256. Ngày mở rộng danh mục Standard là **04/11/2026**; provenance giữ ngày truy cập gốc từng thẻ.

## Pool chơi được

Danh mục có **270 thẻ chính thức**. Active Standard có **19 Spirit** đã xác minh block, tên/thông số, symbol và xử lý luật:

- **16 Spirit không có hiệu ứng**.
- **Ba Spirit Super Unleash**: `26RBS03-052`, `26RBS03-061`, `26RBS03-071`. Giữ ngưỡng Core thường in trên thẻ, thêm lựa chọn Lv2 bằng Soul Core đúng biểu tượng của từng thẻ.

**251 thẻ không dùng trong trận Standard hiện tại**: 21 thẻ Eternal đã có code tương thích và 230 thẻ chưa có triển khai luật trong cả hai thể thức. Các thẻ đó vẫn có ảnh/văn bản để tra cứu; việc có dữ liệu, cờ `implemented` hoặc không có dòng hiệu ứng không tự chứng minh thẻ được chơi.

`IsPlayableFor(format)` kiểm tra cả metadata đúng thể thức và registry/baseline được hỗ trợ. `DeckValidator` áp dụng tên gốc và hạn chế đúng pool; lệnh server tiếp tục xác minh mọi thao tác. Hai boss BS01 trong menu Hoạt cảnh là **xem thử tài nguyên**, không phải thẻ Standard.

## Luật đã xử lý cho pool hiện có

- Bộ bài ít nhất 40 lá, tối đa ba lá cùng tên và hạn chế đã xác minh. Ba bộ Standard mẫu 40 lá được xuất từ API AI production.
- Mở đầu: 5 Life, Reserve có 3 Core thường + 1 Soul Core, tay 4 lá. Theo thứ tự đi trước, mỗi người có thể đổi cả tay một lần.
- **Start → Core → Draw → Refresh → Main → Attack → Second Main → End**. Người đi trước bỏ Core, Attack và Second Main trong lượt đầu, vẫn Draw. Các bước không có quyết định được tự chuyển.
- Main/Second Main cho triệu hồi và chuyển Core, tăng Level, trao đổi Soul/Core đồng thời. Từ Second Main không được quay lại Attack. Nút kết thúc Main/Attack chuyển đến Second Main ở các lượt phù hợp.
- Trả Cost theo symbol giảm phí đã hỗ trợ; đặt Core tối thiểu duy trì Spirit. Spirit dưới Lv1 tiêu biến. Soul Core theo dõi riêng và bảo toàn; **không có quy tắc mọi Spirit có Soul đều Lv2**.
- Spirit hồi phục có thể tấn công ngay lượt triệu hồi. Attack/block làm mệt; chỉ Spirit phù hợp mới tấn công/chặn.
- Bên phòng thủ có Flash trước, luân phiên quyền; hai lần bỏ qua liên tiếp kết thúc cửa sổ. Có chặn thì có Flash sau chặn; không chặn thì nhận sát thương theo symbol. BP bằng nhau phá hủy cả hai.
- Attacker/blocker biến mất trong Flash không tạo BP comparison hoặc sát thương không hợp lệ; xử lý quyền Flash còn lại theo phần luật đã kiểm tra.
- Core Life mất đi về Reserve; Life về 0 thì thua. Hết Deck kiểm tra ở Start Step. Refresh hồi phục bài và chuyển Core Trash về Reserve.
- Nhận thua, kết thúc trận và replay Solo: record chứa phiên bản, thể thức, fingerprint danh mục, seed/deck/thao tác. Replay Eternal/phiên bản cũ không tự trở thành replay Standard.

Code năm hiệu ứng BS01 trước đây vẫn được kiểm tra ở **Eternal**; chúng không mở khóa Standard và không được dùng để giả lập thẻ Standard mới.

## UI, tài nguyên, Solo và mạng

Năm form và ba Parts gốc giữ hierarchy, geometry và GUID; ảnh/material/font game cũ được thay bằng skin tự tạo và font Source Han Sans độc lập. Không có Canvas ứng dụng overlay. Thư mục rỗng giữ bằng `.gitkeep`; manifest ghi đường dẫn chính xác đã xóa/thay để kiểm tra lại.

Pet/wallpaper là ngoại lệ được giữ: **108 pet và 149 wallpaper** có bundle cài sẵn; Mate/Wallpaper, shader nhị phân chung và `SE_MATE` phục vụ các tài nguyên này. Các tên namespace/lớp serialized và mã template tổng quát cần tương thích vẫn tồn tại. Bootstrap BS dùng catalog cosmetic độc lập, bỏ eager-load UI/outduel/nhân vật/CDB/CID cũ. Build dùng allowlist thay vì chép toàn bộ Master Duel.

Executor WindBot nhận `FilteredMatchView`, chỉ có tay bot và thông tin công khai. Bot xử lý Second Main, chỉ triệu hồi thẻ hỗ trợ đúng format, ưu tiên Core/Soul theo Super Unleash đã xác minh, có trì hoãn theo frame và fallback khi authority từ chối. Kết quả trước baseline ngẫu nhiên không chứng minh năng lực thi đấu.

Server TCP dùng cùng Core, cổng **7777**, giao thức **3**. Hello/welcome/state mang thể thức; server từ chối sai format, protocol và danh mục. Actor lấy từ kết nối, không từ payload. Snapshot không gửi tay đối thủ/thứ tự deck/seed. Client mới sau mất kết nối vào lobby mới, không nhận tay của phiên cũ.

Field/model và WAV mới do dự án tự tạo. Các Spirit dùng hình học thay thế theo màu; hai model BS01 low-poly vẫn chỉ là diễn giải fan-made. Pet trang trí không quyết định luật.

## Giới hạn còn lại

- Chưa triển khai toàn bộ hiệu ứng Standard, Soul Magic, Succession, Brave/Burst/Contract/Ultimate, Heavy Exhaust và các vùng/chuỗi giải quyết cần cho những thẻ đó. Các thẻ chưa có code bị khóa khỏi trận tự động.
- Hệ thống chưa phải bộ giải quyết mọi trigger/pending-state/errata của toàn bộ card pool. Các kiểm tra hiện tại áp dụng cho nhóm đã hỗ trợ, không chứng minh mọi tương tác tương lai.
- Nguồn Core trả phí được chọn tự động; giảm phí được chốt trước khi trả. Triệu hồi đặt Core tối thiểu rồi điều chỉnh ở Main; chưa có UI chọn mọi tổ hợp nguồn Core/Level trong một bước trả phí.
- Chưa có MyCard/account/ghép trận/xếp hạng, tự mở cổng, reconnect tiếp tục trận hoặc replay online đầy đủ.
- Floor Rules là quản lý giải đấu. Chưa áp dụng timer/penalty/tiebreak của giải vào Solo/LAN; không coi khuyến nghị 30 phút là luật bắt buộc.
- Chưa có model chính thức riêng cho từng lá, chưa có kéo thả đặt bài. **Chưa kiểm tra Play/render/build Windows Unity, LAN hai máy Windows hoặc Internet công khai trong môi trường này.**
