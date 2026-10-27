# Battle Spirits — Eternal

Dự án Unity riêng dành cho LaoDai060204, phát triển từ cách tổ chức giao diện và trận đấu của template MDPro3/Card-Game. Đây là bản nguyên mẫu chơi hai người trên cùng máy, dùng luật cơ sở Battle Spirits Nhật, thể thức Eternal v17.1 và giao diện tiếng Việt.

## Mở dự án

1. Cài **Unity Editor 6000.6.7f1**, revision `b22767f04f04`, qua Unity Hub. Chọn Windows Build Support nếu cần build Windows.
2. Trong Hub, chọn **Add → Add project from disk**, chọn thư mục `Battle-Spirits` chứa `Assets`, `Packages`, `ProjectSettings`.
3. Chờ Unity hoàn tất tải package và import tài nguyên. Project dùng URP **17.6.0**, Input System **1.20.1**, UI Toolkit và C# 9; các phiên bản được ghi rõ trong `Packages/manifest.json`.
4. Chọn menu **Tools → Battle Spirits → Open Arena**, hoặc mở `Assets/BattleSpirits/Scenes/Arena.unity`.
5. Nhấn **Play**. Mỗi người giữ hoặc đổi toàn bộ 4 lá mở đầu, sau đó thao tác theo hướng dẫn trên giao diện.

Các thẻ mẫu, font và mô hình đã nằm trong project. Trận mẫu không cần tài khoản hoặc tải dữ liệu từ máy chủ của game cũ.

Khi triệu hồi, bản mẫu tự chọn core trả phí từ Reserve rồi từ các Spirit trên sân. Spirit mất core duy trì sẽ tiêu biến; kiểm tra lượng core trước khi xác nhận.

## Nội dung bản đầu

- Hai bộ bài luyện tập 40 lá, tối đa 3 lá cùng tên, lấy từ **16 Spirit chính thức không có hiệu ứng**; hình và chỉ số có nguồn trong `Documentation/official-card-sources.json`.
- Life, core thường, Soul Core, giảm phí theo màu/symbol, triệu hồi và điều chỉnh core để lên level.
- Trình tự lượt Eternal; người đi trước vẫn rút bài ở lượt đầu, bỏ qua Core và Attack Step ở lượt đó.
- Tấn công, hai cửa sổ Flash khi có chặn, chặn, so BP, mất Life, thắng/thua và kiểm tra hết deck ở Start Step.
- Bàn đấu 3D; mô hình rồng tự tạo có rig và animation `Idle`, `Summon`, `Attack`; hiệu ứng triệu hồi, di chuyển và âm thanh được tạo trong project.

Mô hình dùng Generic rig, `Animator` và `PlayableGraph`. Có thể xem [ảnh mẫu model](Documentation/ModelPreview.png) trước khi mở Editor.

**Phạm vi hiện tại:** Spirit không có hiệu ứng, chơi cùng máy. Magic, Nexus, Brave, Burst, Contract, các keyword/hiệu ứng đặc biệt, AI và multiplayer Internet chưa được triển khai. Mô hình rồng là mẫu minh họa chung, chưa phải model đúng theo từng lá bài. Xem [phạm vi luật](Documentation/RULES_AND_SCOPE.md) trước khi mở rộng.

## Cấu trúc

```text
Assets/BattleSpirits/
  Runtime/Core/          Luật C# thuần, không phụ thuộc Unity
  Runtime/Presentation/  UI Toolkit, bàn đấu, animation, âm thanh
  Resources/Data/        Catalog JSON đã kiểm chứng
  Resources/CardArt/     Hình thẻ và bản ghi nguồn/checksum
  Resources/Models/      FBX dùng trực tiếp trong Unity
  Resources/UI/Fonts/    Font hỗ trợ tiếng Việt và tiếng Nhật
  Editor/                Tạo scene, cấu hình render và import model
  Scenes/                Scene Arena
Tests/Core/              Test luật độc lập với Unity
Tools/Art/               File Blender gốc và script tạo mô hình
Documentation/           Luật tham khảo, phiên bản và nguồn tài nguyên
```

## Kiểm tra và cập nhật

```bash
python Tools/validate_project.py
dotnet run --project Tests/Core/BattleSpirits.Core.Tests.csproj -c Release -- Assets/BattleSpirits/Resources/Data/cards.json
dotnet run --project Tests/Audit/BattleSpirits.Audit.csproj -c Release
```

Để đối chiếu lại chỉ số/hiệu ứng với trang thẻ chính thức mà không thay đổi catalog:

```bash
python Tools/check_card_updates.py --id BS01-001
```

Bỏ `--id` để kiểm tra toàn bộ thẻ đã đóng gói. Công cụ chỉ báo thay đổi; việc thêm thẻ hoặc cập nhật hành vi vẫn cần kiểm tra luật và test tương ứng.

Lệnh test dùng .NET SDK 10 bên ngoài Unity; không cần cài SDK đó để chơi trong Editor. Unity vẫn biên dịch script theo cấu hình của Editor 6000.6.

Trong Unity, mở **Window → General → Test Runner → EditMode → Run All** để kiểm tra việc import catalog, ba clip animation, hình thẻ và font. Đây là nhóm kiểm tra tích hợp cần Editor thật.

Để build, chọn **Tools → Battle Spirits → Build Windows x64**.

Trước mỗi đợt chỉnh sửa, lưu một commit và nhánh `backup/YYYY-MM-DD-mo-ta`. Nếu đã cấu hình remote, push và xác minh nhánh backup trước khi sửa. Thực hiện thay đổi trên nhánh tính năng rồi kiểm tra trong đúng Editor. Không tự động nâng tất cả package lên phiên bản bất kỳ.

Kết quả test C# và kiểm tra dữ liệu không thay thế việc mở **Play** hoặc build trong Unity. Môi trường tạo bản nguyên mẫu này chưa cài Unity Editor; xem `Documentation/VALIDATION.md` để biết các kiểm tra thực sự đã chạy.

## Nguồn

- Luật chính thức: <https://www.battlespirits.com/rule/official-rule-manual-eternal/>
- Danh sách thẻ: <https://www.battlespirits.com/cardlist/>
- Unity: <https://unity.com/releases/editor/whats-new/6000.6.7f1>
- Template gốc: <https://code.moenext.com/sherry_chaos/MDPro3>
- Bản template đang dùng: <https://github.com/LaoDai060204/Card-Game>

Nguồn và quyền của hình thẻ, font được ghi riêng trong [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
