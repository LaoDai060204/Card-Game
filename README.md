# LaoDai Battle Spirits — Eternal

Nhánh chuyển đổi từ Card-Game/MDPro3 sang **Battle Spirits Nhật, thể thức Eternal**, giữ Unity **6000.0.24f1** và giao diện tiếng Việt. Menu chính, hình nền, font và tài nguyên ngoại hình của template được dùng lại; dữ liệu thẻ, bộ bài, luật và giao thức trận đấu dùng hệ thống Battle Spirits riêng.

**Phạm vi bản này:** có tra cứu thẻ, xây/lưu bộ bài, hai người chơi chung máy, tạo/tham gia phòng TCP qua LAN hoặc địa chỉ Internet và lịch sử trận cục bộ. Engine hiện hỗ trợ **16 Spirit không hiệu ứng** để chơi trọn một trận bằng hai bộ luyện tập 40 lá. **240 thẻ mới còn lại hiện chỉ để tra cứu**; việc tải được tên, ảnh và mô tả không có nghĩa hiệu ứng đã được lập trình. Chưa có AI hoặc toàn bộ khả năng của Eternal.

Bản Yu-Gi-Oh đang chạy được vẫn ở nhánh `main`. Bản này ở `battle-spirits-mdpro3-ui`.

## Mở và kiểm tra

Đóng Unity trước khi chuyển nhánh. Lưu các chỉnh sửa cục bộ cần giữ; nếu Git báo file bị thay đổi, cất chúng bằng commit hoặc stash trước khi chuyển.

```bash
git fetch origin
git switch battle-spirits-mdpro3-ui
git pull --ff-only origin battle-spirits-mdpro3-ui
```

1. Mở thư mục dự án bằng **Unity 6000.0.24f1**, chọn Windows.
2. Giữ `Data`, `Picture`, `Platforms` và `Sound` cùng cấp với `Assets` như bản template.
3. Trong Addressables Groups, chọn **Play Mode Script → Use Asset Database** nếu đang chạy trực tiếp từ mã nguồn.
4. Mở `Assets/Scenes/Boot.unity`, nhấn Play.
5. Vào **Bộ bài** để tra cứu, hoặc **Chơi cùng máy** để dùng hai bộ luyện tập có sẵn. Chế độ này có màn che khi chuyển người điều khiển.

Menu xem pet, ngoại hình và hoạt cảnh vẫn dùng tài nguyên template. Bàn đấu Battle Spirits có trình tải riêng cho field/Mate hiện có. Pet là yếu tố trang trí; ảnh thẻ Battle Spirits không tự tạo ra mô hình 3D chính thức của quái vật.

## Thẻ mới và bộ bài

Đối chiếu nguồn chính thức ngày **30/10/2026**: **256 thẻ và 256 ảnh PNG** đã đóng gói.

| Nhóm | Số thẻ | Phát hành | Trạng thái trong bản này |
| --- | ---: | --- | --- |
| Bộ luyện tập BS01/BS02 | 16 | Các bộ đầu tiên | Chơi được: Spirit không hiệu ứng |
| [BS76 — 永皇の輝き](https://www.battlespirits.com/product/booster/booster-76.php) | 123 | 30/05/2026 | Đợt Eternal đã phát hành; hiện tra cứu |
| [26RBS03 — 絶界の覇者](https://www.battlespirits.com/product/booster/booster-26rbs03.php) | 117 | 17/10/2026 | Đợt Standard mới hơn; hiện tra cứu, không mặc định xem là hợp lệ trong Eternal |

BS77 và 26RBS04 đã được công bố nhưng chưa phát hành vào ngày đối chiếu, nên không được đưa vào nhóm thẻ đã phát hành. Đây là tập các bộ đã nhập, không phải toàn bộ lịch sử thẻ Battle Spirits.

Trong Bộ bài có tìm theo tên/mã/hệ, lọc màu, loại, Cost, BP, bộ phát hành và trạng thái hỗ trợ. Bộ bài lưu riêng tại `Data/BattleSpirits/Decks`, không dùng YDK, Main/Extra/Side hoặc CDB của Yu-Gi-Oh. Eternal cần ít nhất 40 lá; giới hạn số bản theo tên gốc và dữ liệu hạn chế/ngoại lệ đã được xác minh. Những thẻ chưa hỗ trợ không được đưa vào trận tự động.

Tên, thông số và hiệu ứng gốc tiếng Nhật được giữ để đối chiếu. Nguồn, ngày truy cập, checksum HTML/ảnh và phép chuyển WebP→PNG giữ nguyên pixel nằm trong `Data/BattleSpirits/card-sources.json`. Danh sách hạn chế nằm trong `Data/BattleSpirits/banlist.json`.

```bash
# Kiểm tra dữ liệu và ảnh đã đóng gói; không cần mạng.
python Tools/BattleSpiritsCards/catalog.py validate

# Đối chiếu nguồn chính thức; chỉ báo thay đổi.
python Tools/BattleSpiritsCards/catalog.py check
```

Xem [công cụ cập nhật thẻ](Tools/BattleSpiritsCards/README.md) trước khi nhập đợt mới. Luôn backup trước khi cập nhật. Công cụ không tự đánh dấu hiệu ứng mới là đã được lập trình.

## Chơi LAN và Internet

Trong **Chơi mạng**, một máy tạo phòng ở cổng TCP **7777**; máy khác nhập IPv4 của máy chủ và đúng cổng. Cả hai chọn bộ bài hợp lệ, bấm sẵn sàng; người tạo phòng bắt đầu trận. Khi thử cùng một máy có thể dùng `127.0.0.1`; máy khác phải dùng IP LAN của máy chủ.

Windows Firewall cần cho phép Unity/game nhận kết nối trong mạng riêng. Có thể kiểm tra cổng từ PowerShell trên máy tham gia:

```powershell
Test-NetConnection 192.168.1.10 -Port 7777
```

Kết nối Internet dùng cùng giao thức và server, nhưng máy chủ phải có địa chỉ/cổng truy cập được từ ngoài mạng nhà. Bản này hỗ trợ kết nối trực tiếp; không có dịch vụ tài khoản/ghép trận MyCard cho Battle Spirits. Chưa triển khai tự mở cổng router, khôi phục trận sau mất kết nối hoặc dịch vụ xếp hạng.

Có thể chạy server riêng ngoài Unity theo [hướng dẫn server](Tools/BattleSpiritsServer/README.md). Server xác minh phiên bản và fingerprint danh mục; hai máy cần cùng phiên bản dữ liệu. Server chỉ gửi tay bài của chính người nhận cùng trạng thái công khai, không gửi tay bài, thứ tự deck hay seed của đối thủ.

## Luật và kiểm tra

Engine dùng C# thuần, tách khỏi Unity. Các thẻ hiện chơi được hỗ trợ Life, Reserve/Trash, Core/Soul Core, giảm phí, Level/BP, trình tự lượt Eternal, đổi bài đầu, tấn công, hai cửa sổ Flash, chặn, tiêu biến/phá hủy và thắng/thua. Xem [phạm vi triển khai](Documentation/BATTLE_SPIRITS_SCOPE.md).

```bash
dotnet run --project Tests/BattleSpirits/Core/BattleSpirits.Core.Tests.csproj -c Release -- Data/BattleSpirits/cards.json
dotnet run --project Tests/BattleSpirits/Network/BattleSpirits.Network.Tests.csproj -c Release
python -m unittest discover -s Tools/BattleSpiritsCards -p 'test_*.py'
```

Các lệnh `dotnet` dùng SDK 10 bên ngoài Unity. Mã runtime vẫn nhắm C# 9/.NET Standard 2.1 cho Editor 6000.0.24f1. Xem [kết quả kiểm tra](Documentation/BATTLE_SPIRITS_VALIDATION.md) để phân biệt các bài kiểm tra thực sự đã chạy với Play/render/build trong Unity Windows chưa được xác minh.

## Nguồn và bản cũ

Template: [MDPro3 của Sherry Chaos](https://code.moenext.com/sherry_chaos/MDPro3), được tùy biến bởi LaoDai060204. Ghi nhận nguồn, giấy phép và tài nguyên Master Duel vẫn được giữ trong [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Thông tin bản template trước chuyển đổi nằm tại [MDPRO3_TEMPLATE.md](Documentation/MDPRO3_TEMPLATE.md).

Thẻ và hình Battle Spirits lấy từ [website chính thức BANDAI](https://www.battlespirits.com/cardlist/), không phải artwork tự sáng tác. Tên và quyền đối với các tài nguyên thuộc các chủ sở hữu tương ứng. Đây là dự án cộng đồng độc lập.

Backup trước chuyển đổi: `backup/2026-10-30-before-battle-spirits-conversion`. Snapshot công việc đang làm: `backup/2026-10-30-battle-spirits-conversion-wip`. Không force-push hoặc ghi đè các nhánh backup.
