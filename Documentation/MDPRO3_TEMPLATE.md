# LaoDai Card Game

Bản tùy biến tiếng Việt do **LaoDai060204** duy trì, phát triển từ [MDPro3 của Sherry Chaos](https://code.moenext.com/sherry_chaos/MDPro3). Dự án mô phỏng đấu bài Yu-Gi-Oh! bằng Unity, có chơi với máy, tạo phòng LAN, kết nối máy chủ và tích hợp MyCard.

Giao diện có ngôn ngữ **Tiếng Việt**. Tên lá bài và mô tả hiệu ứng dùng dữ liệu tiếng Anh khi chưa có cơ sở dữ liệu tiếng Việt. Tên nhân vật, bộ bài, dịch vụ và tác giả có thể giữ nguyên tên riêng.

Khi mở bản này lần đầu, giao diện tự chuyển sang tiếng Việt một lần; các lựa chọn ngôn ngữ bạn đổi sau đó được giữ lại. Bảng tin MyCard tiếng Trung được ẩn khi chọn tiếng Việt vì nguồn tin này chưa có bản dịch tiếng Việt. Lời thoại AI và tên vật phẩm dùng dữ liệu tiếng Anh hiện có.

Các công cụ Unity Editor, thông báo chẩn đoán và tên AI/lời nhắc còn tiếng Trung trong Puzzle đã được bổ sung tiếng Anh. Mục lịch sử cập nhật có tài nguyên tiếng Anh riêng; bảng tin ở giao diện tiếng Anh dùng nguồn tin tiếng Anh nếu có. Xem [chi tiết bản dịch và kiểm tra](Tools/Translations/ENGLISH_CLEANUP.md).

## Mở dự án trên Windows

1. Cài Git for Windows và Unity Hub.
2. Cài **Unity Editor 6000.0.24f1**, đúng phiên bản trong `ProjectSettings/ProjectVersion.txt`.
3. Tải mã nguồn:

   ```bash
   git clone https://github.com/LaoDai060204/Card-Game.git
   ```

4. Trong Unity Hub, chọn **Add project from disk** và mở thư mục `Card-Game` chứa `Assets`, `Packages`, `ProjectSettings`.
5. Giữ kết nối Internet để Unity tải các package. Chờ import hoàn tất.
6. Trong **File → Build Profiles**, chọn nền tảng **Windows**.
7. Mở `Assets/Scenes/Boot.unity`, rồi nhấn **Play**.

Nếu cần chạy Addressables trực tiếp từ mã nguồn, mở **Window → Asset Management → Addressables → Groups** và chọn **Play Mode Script → Use Asset Database**.

Các thư mục tài nguyên Windows đã được bổ sung vào kho:

- `Platforms/StandaloneWindows64`
- `Picture/Art`, `Picture/Closeup`, `Picture/DIY`
- `Sound`
- `Puzzle` và `Data/cards_Lite.json`

Giữ các thư mục này cùng cấp với `Assets`, `Data`, `Packages`, `ProjectSettings`. Windows Editor đọc chúng từ thư mục gốc dự án. Các nền tảng Android, Linux, macOS, iOS và Switch chưa được bổ sung tài nguyên trong bản này.

Để xuất ứng dụng Windows với cấu hình IL2CPP hiện tại, cài thêm **Windows Build Support (IL2CPP)** cho đúng bản Unity và bộ công cụ C++ cùng Windows SDK của Visual Studio. Kho này không kèm một bản `.exe` đã được kiểm thử.

## Màn chơi Puzzle

Thư mục `Puzzle` có **104 tệp màn chơi gốc**, được kiểm tra với commit `3a7423e6b389c413cc64b157dc213ab67b2ae551` của [mdpro3-other](https://code.moenext.com/mycard/mdpro3-other):

- 55 tệp ở thư mục gốc, gồm các thử thách WCS.
- 40 tệp trong `CardEffect`, 6 trong `FinalAttack`, 3 trong `SpecialWin`.

Đặt `Puzzle` cùng cấp với `Assets`, mở scene **Boot**, Play rồi vào mục **Puzzle / Thử thách giải thế cờ**. Không đặt các tệp này trong `Assets` hoặc `StreamingAssets`.

Trong bộ dữ liệu đi kèm, 99 màn có đủ các ID thẻ được tham chiếu trực tiếp. **5 màn WCS cũ cần 6 ID thẻ phiên bản cũ chưa được cung cấp**; game sẽ báo ID thiếu khi mở các màn đó. Tệp gốc được giữ nguyên, không đổi sang ID của lá bài có hiệu ứng khác. Nếu bạn bổ sung gói mở rộng tương thích, việc kiểm tra sẽ dùng dữ liệu thẻ đang được game nạp.

Chi tiết nguồn, các màn thiếu phụ thuộc và các kiểm tra có thể chạy lại nằm trong [Tools/RuntimeData](Tools/RuntimeData/README.md). Kiểm tra ID và cú pháp Lua không chứng minh tất cả màn đã chơi hoặc giải thành công trong Unity.

`Data/cards_Lite.json` chứa ánh xạ CID/YDK gọn, đã gộp `cards_Alt.json`. File này giúp bản clone mới mở vật phẩm/đồng hành mà không cần tự tải `cards.json`. Bộ nạp có xử lý tệp thiếu, tệp hỏng và lỗi ghi cache.

## Chơi với người khác

- **Single Duel**: đấu 1v1 một trận.
- **Match**: đấu 1v1 theo loạt trận.
- **Tag**: đấu đội 2v2, tổng cộng bốn người.
- Có thể tạo phòng tại máy mình, tham gia bằng IP/cổng, hoặc sử dụng các chức năng MyCard.

Để thử trong cùng mạng LAN, một máy tạo phòng ở mục tạo máy chủ; máy còn lại nhập IP nội bộ và cổng của máy đó trong mục kết nối. Mã hiện tại bắt đầu tìm cổng ở `7911` và chọn cổng trống tiếp theo nếu cổng này đã được dùng. Windows Firewall cần cho phép kết nối đến ứng dụng tạo phòng. Kết nối qua Internet còn phụ thuộc cấu hình mạng của máy chủ. Chức năng chơi mạng chưa được kiểm thử thực tế trong môi trường này.

## Khởi động trận đấu với AI

WindBot dùng ngôn ngữ **dữ liệu thẻ**, nên giao diện tiếng Việt vẫn đọc `Data/locales/en-US/cards.cdb`. Không cần tạo hoặc sao chép một CDB vào `vi-VN`. Thư mục `Expansions` có thể chưa tồn tại nếu bạn chưa cài gói mở rộng.

Bản sửa khởi động Solo đã bỏ lệnh chờ bàn phím/thoát process khi thiếu CDB. Lỗi AI sẽ được ghi vào Console và hiện thông báo, thay vì gọi `Console.ReadKey()` trong Unity. PLAY lặp khi máy chủ đang khởi tạo/kết thúc sẽ được chặn; thao tác dừng máy chủ chạy ở luồng nền để tránh chặn luồng giao diện.

Nếu bản cũ đã treo ở `Can't find cards database file` / `Press any key to quit...`, đóng Editor đang treo, cập nhật mã rồi mở lại bằng Unity **6000.0.24f1**. Xem [kiểm tra khởi động Solo](Tools/RuntimeData/SOLO_STARTUP.md) để biết các kiểm tra đã chạy và giới hạn còn lại.

## Cập nhật và giữ bản cũ

Trước mỗi đợt chỉnh sửa, lưu các thay đổi cần giữ vào Git, tạo một nhánh backup và push nhánh đó lên GitHub. Bản trước đợt Việt hóa này được lưu tại:

[`backup/2026-10-25-before-vietnamese-ui`](https://github.com/LaoDai060204/Card-Game/tree/backup/2026-10-25-before-vietnamese-ui)

Nhánh backup lưu các tệp đã được commit. Cấu hình cá nhân, bộ bài tự tạo và một số thư mục runtime có thể nằm trong `.gitignore`, nên cần sao lưu riêng nếu chưa được Git theo dõi. Đóng Unity trước khi chuyển nhánh.

Để cập nhật bản tùy biến, dùng các thay đổi trong kho `LaoDai060204/Card-Game`. Các gói phát hành MDPro3 gốc thuộc dự án nền; chép trực tiếp chúng vào đây có thể ghi đè phần tùy biến.

## Nguồn gốc và ghi công

Mã nền được nhập từ nhánh `master` của [MDPro3](https://code.moenext.com/sherry_chaos/MDPro3), tại commit `e177cd8d53092ae9249e37b38d3cef0040e41095`. Đây là bản nhập nội dung mã nguồn; lịch sử Git và các nhánh khác của kho gốc không được chuyển đầy đủ.

| Thư mục | Nguồn |
| --- | --- |
| `Platforms/StandaloneWindows64` | [mdpro3-assetbundles](https://code.mycard.moe/sherry_chaos/mdpro3-assetbundles) |
| `Picture/Art` | [hd-arts](https://code.mycard.moe/mycard/hd-arts) |
| `Picture/Closeup` | [ygopro2-closeup](https://code.mycard.moe/mycard/ygopro2-closeup) |
| `Picture/DIY` | Dữ liệu phát hành chính thức qua [mdpro3-other](https://code.moenext.com/mycard/mdpro3-other) |
| `Sound` | Gói MyCard chính thức `mdpro3-sound`, phiên bản `ea4ad1eb`; liên kết kho được README gốc nêu trả về 404 khi nhập dữ liệu |

Các tài nguyên Master Duel, Cross Duel và Duel Links cùng tên gọi Yu-Gi-Oh! thuộc các chủ sở hữu tương ứng. Xem [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) và mục **Giới thiệu trò chơi** để biết thêm về các tác giả, cộng tác viên và thông báo đi kèm.

## Công cụ phát triển

Chỉnh cột `vi-VN` trong `Tools/Translations/translations.csv`, rồi xuất và kiểm tra bản dịch bằng Python 3:

```bash
python Tools/Translations/validate_vietnamese.py --write
```

Chạy cùng lệnh không có `--write` để chỉ kiểm tra. Công cụ kiểm tra khóa dịch giao diện, placeholder, ID thông báo trận đấu, cấu hình AI và liên kết Addressables của phần giới thiệu. Đây là kiểm tra dữ liệu; vẫn cần mở Unity để kiểm tra hiển thị và chạy game.

Tệp cấu hình URP đã bỏ riêng mục `UniversalRenderPipelineEditorAssets` không có trong bản Editor 6000.0.24f1. Các thay đổi khác bạn tự thực hiện trên Windows chỉ có trong kho sau khi được commit/push hoặc chuyển sang môi trường này.

Registry OpenUPM dùng `https://package.openupm.com`. Các lượt tải tùy chọn có giới hạn thời gian và thông báo URL lỗi đã loại bỏ thông tin đăng nhập/query. Nguồn dịch thẻ phát hành trước `ElderLich/TransSuperpre` hiện đã ngừng cung cấp gói tải; game giữ nguyên thẻ đã cài và bỏ qua kiểm tra nguồn đó. URL tùy chỉnh vẫn được giữ lại, và việc xác minh chứng chỉ HTTPS vẫn được bật.

- `Tools/Translations`: bảng và công cụ xuất dữ liệu dịch giao diện.
- `Tools/DumpShaders`: dữ liệu phục vụ thay thế shader khi đóng gói tài nguyên.
- `Tools/YGO Classes`: mã và công cụ biên dịch các thư viện YGOPro phụ thuộc.
- QuickBMS: công cụ xử lý dữ liệu ID vật phẩm của Master Duel.

Các bản dịch MDPro3 gốc được cộng tác tại [bảng dịch của dự án](https://docs.google.com/spreadsheets/d/1BbSTxgobDqLyHL7De6uFSqfGiK9WIH3no5fQPW2Xwls/edit?usp=sharing). Dữ liệu WindBot gốc liên quan đến [YGOMobile](https://github.com/fallenstardust/YGOMobile-cn-ko-en).
