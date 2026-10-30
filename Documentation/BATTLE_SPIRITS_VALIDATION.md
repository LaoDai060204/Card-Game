# Kiểm tra bản chuyển đổi

Ngày 30/10/2026; Unity mục tiêu **6000.0.24f1**, Windows x64. Nhánh `battle-spirits-mdpro3-ui` phát triển từ `db9007d1`; `main` vẫn giữ bản Card-Game/Yu-Gi-Oh. Xem [phạm vi luật](BATTLE_SPIRITS_SCOPE.md) trước khi đánh giá số thẻ chơi được.

| Phần đã kiểm tra | Kết quả |
| --- | --- |
| Luật và dữ liệu hỗ trợ | 15 tình huống gốc, 19 tình huống chuyển đổi; 96 trận mô phỏng và 24 cặp trận cùng seed |
| Kiểm tra độc lập engine | 100 trận, 8.324 trạng thái và 74.916 thao tác thử |
| TCP với danh mục 256 thẻ | 16 tình huống, 47 thao tác; có một trận hoàn chỉnh giữa hai client |
| Server console thật | 5 tình huống: hai client, trạng thái mở đầu, nhận thua, dừng bằng Ctrl+C |
| Luồng trình bày | 26 tình huống với mã production và Unity stand-in; có deck, hotseat, replay cục bộ, ảnh tải muộn, cache, vòng đời field/pet, chat, xử lý phím Esc và layer/camera UI |
| Công cụ dữ liệu | 18 tình huống parser; 256 bản ghi/ảnh và metadata nguồn được kiểm tra |
| Ảnh thẻ | 256 PNG khớp pixel với 256 WebP nguồn trong cache; import thử tái tạo đúng các bản ghi và byte ảnh |
| Dự án/asset | 23 GUID mới duy nhất, 6 source project tham chiếu bằng đường dẫn tương đối |
| Cú pháp production | 20 tệp C# 9, ba bộ ký hiệu Editor/Windows, Editor/Android và standalone Windows |
| Bản dịch template | 823 khóa Anh và 823 khóa Việt, placeholder và Addressables đã kiểm tra |

Core và Networking production biên dịch với **.NET Standard 2.1/C# 9**. Các test và server console dùng SDK .NET 10 ở ngoài Unity; server pin Newtonsoft.Json 13.0.3. Các bài mạng chạy TCP thật trên loopback Linux, không phải giả lập truyền gói tin.

Harness trình bày biên dịch sáu class presentation, MainMenuUI và các class rules/network thật. UnityEngine, TMP, Addressables, DOTween và tải AssetBundle được thay bằng đối tượng kiểm soát. Các đường dẫn trang trí, tên nút menu và API MDPro3 cũng được đối chiếu source/prefab thật. Những kiểm tra này không xác nhận hình ảnh, shader, kích thước chữ hoặc thao tác chuột trong Unity Editor thật.

Các lỗi đã sửa khi tích hợp: nguồn sprite Addressables bị nhả sớm; ảnh cũ tải xong ghi đè lựa chọn mới; cache ảnh không giới hạn; board/tween chưa được dọn khi đổi màn; UI camera chưa được kế thừa; thao tác còn bật sau mất kết nối; hàng đợi kết nối cũ làm hỏng kết nối mới; hướng pet khi xem replay.

Sau khi người dùng thử Play, đã sửa thêm lỗi `InvalidOperationException` tại `BattleSpiritsApp.Update`: cấu hình dự án dùng Input System (`activeInputHandler: 1`), nhưng phím Esc còn đọc qua `UnityEngine.Input` cũ. Mã hiện dùng `Keyboard.current.escapeKey.wasPressedThisFrame`, có kiểm tra bàn phím null. Harness đã đổi API Input cũ từ trả false sang ném lỗi để tái hiện cấu hình thực tế: source cũ thất bại, source sửa vượt qua 21 tình huống. Ba tình huống bổ sung kiểm tra không có bàn phím, chưa nhấn Esc và nhấn Esc để đóng overlay/trả quyền nhập liệu; có gọi Update lại khi overlay đã đóng. Đây là kiểm tra luồng bằng stand-in, chưa phải kiểm tra thiết bị nhập liệu trong Unity thật.

Đã sửa lỗi bấm Chơi cùng máy/Chơi mạng/Bộ bài chỉ còn hình nền: `Camera_UI` trong `Main.unity` có culling mask `1073741856` (layer 5 và 30), còn các GameObject UI tạo bằng code mặc định ở layer 0. Unity không tự kế thừa layer khi gọi `SetParent`. Toàn bộ UI Battle Spirits hiện lấy layer từ UI cha ngay lúc tạo, gồm overlay, chữ, nút, ảnh thẻ, danh sách được tạo lại và hình trang trí tải bất đồng bộ. Canvas con giữ camera, sorting layer và additional shader channels của Canvas gốc. Sân/pet 3D tiếp tục dùng camera riêng ở layer 31.

Harness hiện đọc layer, camera mask và thiết lập Canvas trực tiếp từ `Main.unity`. Năm tình huống bổ sung kiểm tra các màn từ menu, trang trí tải muộn, đổi trang bộ bài và ảnh bàn đấu; cả 26 tình huống đạt. Chạy source trước sửa (`0a32535e`) với harness mới thất bại đúng ở overlay layer 0 bị camera loại bỏ. Kiểm tra này đối chiếu cấu hình và luồng tạo đối tượng, chưa xác nhận render bằng Unity Editor thật.

## Chạy lại

Từ thư mục gốc dự án:

```bash
python Tools/BattleSpirits/validate_conversion.py
python Tools/BattleSpiritsCards/catalog.py validate
python -m unittest discover -s Tools/BattleSpiritsCards -p 'test_*.py'
dotnet run --project Tests/BattleSpirits/Core/BattleSpirits.Core.Tests.csproj -c Release -- Data/BattleSpirits/cards.json
dotnet run --project Tests/BattleSpirits/Network/BattleSpirits.Network.Tests.csproj -c Release -- Data/BattleSpirits/cards.json
python Tests/BattleSpirits/Network/test_console.py --dotnet /path/to/dotnet
dotnet run --project Tests/BattleSpirits/Presentation/BattleSpirits.Presentation.Tests.csproj -c Release -- /path/to/Card-Game
```

Thay `/path/to/dotnet` và `/path/to/Card-Game` bằng đường dẫn trên máy bạn. Kiểm tra pixel bổ sung dùng `catalog.py validate --pixels` và cần ImageMagick. Không cần ImageMagick hoặc SDK .NET 10 để mở game trong Unity.

## Cần kiểm tra trên Windows Unity

Chưa chạy Editor Play, render, build Windows, trận LAN giữa hai máy Windows hoặc triển khai Internet công khai trong môi trường phát triển này.

Mở Boot bằng đúng Editor, kiểm tra menu cũ, bộ lọc/bố cục thẻ, font và ảnh; chơi hết một trận bằng hai bộ luyện tập. Kiểm tra field/Mate, chuyển người, quay lại menu và replay. Sau đó thử tạo/tham gia phòng TCP 7777 từ hai máy cùng dữ liệu, chơi hết trận và quay lại để tạo trận mới. Màn hình mạng hiện dùng thao tác quay lại/tạo lại phòng cho ván tiếp theo.

Không coi 240 thẻ chỉ tra cứu là thẻ đã có hiệu ứng tự động. Các khả năng chưa có của Eternal, AI, tài khoản/ghép trận và reconnect tiếp tục trận vẫn được ghi trong tài liệu phạm vi.
