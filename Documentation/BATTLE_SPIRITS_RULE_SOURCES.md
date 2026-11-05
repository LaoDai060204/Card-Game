# Đối chiếu tài liệu luật Battle Spirits

Ngày đọc tài liệu: **04/11/2026**; cập nhật trạng thái triển khai **05/11/2026**. Chủ dự án đã xác nhận chuyển sang **Battle Spirits Nhật — Standard theo các PDF này**. Bản 0.3.0 áp dụng những phần được nêu trong [phạm vi triển khai](BATTLE_SPIRITS_SCOPE.md); không coi việc đọc PDF là đã hỗ trợ toàn bộ hiệu ứng.

## Tài liệu được cung cấp

Các số trang dưới đây là **trang PDF, bắt đầu từ 1**. Tên tệp và SHA-256 nhận diện đúng bản được đọc; các PDF không được chép vào repository. Ngày cập nhật là ngày ghi trong tài liệu, không phải khẳng định đây là bản mới nhất trên website ở mọi thời điểm.

| Tên tệp được gửi | Tên/phiên bản ghi trong tài liệu | Ngày cập nhật ghi trong tài liệu | Số trang |
| --- | --- | --- | ---: |
| `comprehensive-rules-standard.pdf` | Battle Spirits Standard Official Rules, Ver.1.0 | 01/04/2026 | 24 |
| `official-rule-manual_standard.pdf` | Official Rule Manual [Standard], Ver.1.0 | Không ghi ngày cập nhật riêng ở trang đầu | 45 |
| `floor-rule-forasia.pdf` | BANDAI CARD GAMES Floor Rules, Ver.2.2.0 | 23/10/2026 | 20 |

```text
comprehensive-rules-standard.pdf
48fe6950040eaf5bf860b965d62996f9d0a1bb75ee9eca4b4c9a9407405b4303

official-rule-manual_standard.pdf
10f31ad75cc119597c6ccc90d325861fcc5fca78df050db708ecfe265aaeb914

floor-rule-forasia.pdf
7bf82d9bd32d20b330206a0a086749959c45ba597eeb5f69618996205f6ef59e
```

## Những khác biệt Standard đã đối chiếu

| Nội dung | Căn cứ trong PDF | Áp dụng trong 0.3.0 |
| --- | --- | --- |
| Thẻ hợp lệ | Comprehensive §1-1-1, trang 1; §2-13-1-2-1, trang 5. Manual trang 6 quy định giai đoạn 01/04/2026–31/03/2027 chỉ dùng block chữ **A**. | 19 Spirit Standard đã được xác minh block A/bản in/thông số/symbol; validator, bot và server dùng pool đó. 21 thẻ Eternal tương thích không được đổi thành Standard. Metadata/importer kiểm tra mùa và hạn chế. |
| Main lần hai | Comprehensive §7-8, trang 14; Manual trang 17–18. Manual trang 18 nói rõ Eternal không có Second Main Step. | Core, UI và bot có `Main → Attack → Second Main → End`; Main actions hợp lệ ở cả hai Main, không được quay lại Attack từ Second Main. API Eternal riêng vẫn không có bước này. |
| Ngoại lệ lượt đầu | Comprehensive §7-3-3, trang 13; §7-7-2 và §7-8-2, trang 14; Manual trang 18. | Đã xử lý người đi trước bỏ Core, Attack và Second Main ở lượt đầu, vẫn Draw; có regression cho ngoại lệ này. |
| Super Unleash | Comprehensive §3-2-5-5 và §3-3-3-4, trang 6; §12-2-1-1, trang 22. | Đã xử lý cho ba Spirit block A có biểu tượng đã xác minh, giữ ngưỡng Core thường. Chưa hỗ trợ mọi loại thẻ có keyword này và không áp dụng Soul Core → Lv2 cho mọi Spirit/Nexus. |
| Soul Magic | Comprehensive §12-2-1-2, trang 22. | Cần lựa chọn trả phí bằng riêng Soul Core khi thỏa điều kiện symbol được thẻ yêu cầu. Việc theo dõi Soul Core như một Core thường chưa triển khai cơ chế này. |
| Succession | Comprehensive §11-1/11-2/11-3, trang 20–21; §12-2-1-3, trang 22. | Cần chọn thẻ có EX symbol trong Trash để đưa sang Removed Area trước khi tính giảm phí bằng symbol trên sân; không được dùng quá số reduction symbol của thẻ. Cần trạng thái vùng bài và lệnh authority tương ứng. |

Danh mục 270 thẻ không đồng nghĩa toàn bộ hiệu ứng đã được lập trình. Bản 0.3.0 chỉ bật 19 thẻ Standard; 251 bản ghi không dùng trong trận Standard hiện tại. Validator, bộ mẫu, bot, giao thức 3, fingerprint và replay đã mang thể thức rõ ràng; thẻ có dữ liệu nhưng chưa có code vẫn bị khóa.

## Phần luật nền có thể đối chiếu chung

| Nội dung | Căn cứ trong Comprehensive | Ý nghĩa triển khai |
| --- | --- | --- |
| Bộ bài | §6-1, trang 11–12; xem thêm Manual trang 6 | Ít nhất 40 lá; không có giới hạn trên trong luật chơi nền. Tối đa ba lá cùng tên, có phân biệt số định danh cùng tên; tiếp tục áp dụng cấm/hạn chế hợp lệ theo thể thức. Giới hạn kích thước gói tin không phải luật giới hạn số lá. |
| Mở đầu | §6-2-1-3 đến §6-2-1-9, trang 12 | Năm Core Life; Reserve có ba Core thường và một Soul Core; tay bốn lá. Theo thứ tự đi trước, mỗi người có thể đổi toàn bộ tay một lần. Thiết lập hiện tại phù hợp với phần này. |
| Start/Core/Draw/Refresh | §7-2 đến §7-5, trang 12–13 | Deck rỗng được xét ở Start Step. Core Step nhận một Core từ Void, trừ lượt đầu người đi trước. Draw một lá. Refresh hồi phục bài trên sân và chuyển Core trong Trash về Reserve. |
| Chuyển Core ở Main | §7-6-1-2-2, trang 13 | Cho phép Reserve ↔ bài trên sân, bài trên sân ↔ bài trên sân, và đổi đồng thời Soul Core với Core thường. UI cần biểu diễn đủ các nguồn/đích mà engine hỗ trợ. |
| Flash và chặn | §8-1-2 đến §8-1-4, trang 14–15 | Bên phòng thủ có quyền Flash trước; luân phiên; hai lần bỏ qua liên tiếp kết thúc cửa sổ. Nếu không chặn thì không có cửa sổ Flash sau chặn. |
| Giải quyết trận đánh | §8-1-5 đến §8-1-7, trang 15 | BP bằng nhau phá hủy cả hai. Nếu attacker hoặc blocker đã rời sân tại lúc so BP thì không so BP. Tấn công không bị chặn làm mất Life theo symbol. |
| Main/Flash của Magic | §9-1-7-1-1 đến §9-1-7-1-3, trang 17 | `[Main]` dùng ở Main; `[Flash]` dùng ở Main của mình hoặc cửa sổ Flash. Không mặc định mọi Magic đều có cùng cách dùng. |
| Giải quyết một phần | §1-4-2 và §1-4-5-1, trang 2 | Thực hiện phần có thể thực hiện; “tối đa” cho phép chọn 0 trừ khi có giới hạn dưới. Vẫn cần phân biệt điều kiện kích hoạt và các liên từ trong hiệu ứng. |
| Thua ngay | §1-3-7 và §1-3-8, trang 2 | Life về 0 hoặc Deck rỗng ở đầu Start Step kết thúc game ngay, không chờ các hiệu ứng vừa được kích hoạt. |
| Trạng thái chờ rời sân | §10-2-2, trang 19–20 | Destruction/Depletion/return có pending state; hiệu ứng liên tục và trigger được giải quyết theo quy tắc trước khi chuyển vùng. Không thể coi việc xóa ngay đối tượng khỏi sân là đã hỗ trợ mọi hiệu ứng. |
| Trạng thái bài | §5-5, trang 9; §5-14/5-16, trang 10–11 | Refresh, Exhaust và Heavy Exhaust là các trạng thái riêng. Biến boolean Exhausted hiện tại không đủ cho thẻ có Heavy Exhaust. |

Đối chiếu một luật nền không chứng minh toàn bộ card effect hoặc tương tác đã được hỗ trợ. [Báo cáo kiểm tra](BATTLE_SPIRITS_VALIDATION.md) phân biệt những harness đã chạy với Play/build/render thật trong Unity Windows.

## Floor Rules và chế độ giải đấu

Floor Rules trang 2 xác định đây là quy định quản lý sự kiện chính thức/được cấp phép tại Nhật và châu Á, có thể được thay bởi điều lệ sự kiện cụ thể. Tài liệu này không yêu cầu mọi trận Solo hoặc phòng LAN phải bật chế tài của trọng tài hay đồng hồ giải đấu.

- **Trang 3:** giữ kín tay bài/Deck, công bố rõ thao tác và trạng thái. Đây là căn cứ phù hợp cho lọc snapshot theo người nhận và lịch sử thao tác công khai.
- **Trang 6:** hạn chế sử dụng mã thẻ mới phụ thuộc loại sự kiện và lãnh thổ. Quy tắc chờ bảy ngày áp dụng cho sự kiện chính thức/nhóm giải được nêu; phần bổ sung Battle Spirits cho phép dùng ngay ở sanctioned tournaments. Không tự cấm mọi thẻ mới bảy ngày trong Solo/LAN.
- **Trang 13:** best-of-one, 30 phút là thể thức **được khuyến nghị** cho sanctioned tournaments có thời gian; không phải đồng hồ bắt buộc của luật Core.
- **Trang 13–14:** điều lệ có thể cho cả hai thua khi hết giờ hoặc dùng quy trình xác định thắng được nêu. Nếu triển khai lựa chọn thứ hai, phải giữ đủ chuỗi dưới đây, không chỉ so Life.

Quy trình hết giờ được nêu cho Battle Spirits, khi sự kiện chọn áp dụng:

1. Người có nhiều Core Life hơn thắng; nếu bằng nhau **hoặc cả hai có từ 5 Life trở lên**, chuyển bước tiếp.
2. Người có nhiều lá trong Deck hơn thắng; nếu bằng, tiếp tục.
3. Người có nhiều lá tổng cộng trong Hand, Field, Trash, Removed Area và Deck hơn thắng; nếu bằng, tiếp tục.
4. Người có **ít** Core tổng cộng trên Field, Reserve và Trash hơn thắng, loại trừ các vị trí được tài liệu nêu như Life/Count/Grandwalker hoặc Grandstone Nexus; nếu bằng, tiếp tục.
5. Một ván kéo-búa-bao xác định người thắng.

Các quy định vật lý về sleeve, token, thao tác trọng tài và hình phạt không được tự suy thành hành vi Core của trận Solo. Floor Rules §8, trang 12, về lật Leader trước khi bắt đầu ghi rõ dành cho **ONE PIECE/Fusion World**, không áp dụng sang Battle Spirits.

## Trạng thái áp dụng trong repository

Bản 0.3.0 dùng Standard sau lựa chọn rõ ràng của chủ dự án. Core có 17 tình huống Standard bổ sung, gồm Second Main, ngoại lệ lượt đầu, hành động Main, cấm quay lại Attack, block A, BS01 bị từ chối và Super Unleash đúng thẻ. UI/AI/TCP có kiểm tra riêng; replay/client khác phiên bản hoặc thể thức bị từ chối.

**Soul Magic, Succession, Heavy Exhaust và toàn bộ chuỗi hiệu ứng phức tạp chưa được triển khai.** Các thẻ cần chúng vẫn không được dùng trong trận tự động. Không biến Floor Rules thành timer hay hình phạt mặc định của Solo/LAN. [Báo cáo kiểm tra](BATTLE_SPIRITS_VALIDATION.md) ghi rõ chưa thử Windows Unity Play/build/render thật.
