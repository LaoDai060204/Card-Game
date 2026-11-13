# Standee nhân vật — 0.7.1

Mặc định trên sân là **GIẤY**: nhân vật đã tách nền đứng trên đế oval có độ dày, cùng mặt thẻ gốc nằm bên dưới. Có mặt trước/sau và cạnh giấy theo vùng alpha, kể cả khoảng trống giữa tay, chân và vũ khí. Đế lấy màu của thẻ. Chuyển động triệu hồi, tấn công và nhận đòn di chuyển cả tấm giấy, không biến nhân vật thành model khối có xương.

Đợt này có **18 ảnh tách cho 19 Spirit Standard đang chơi được**. `26RBS03-071` chưa có ảnh tách vì công cụ tạo ảnh từ chối đầu ra; lá đó vẫn chơi bình thường và chỉ hiện mặt thẻ trong chế độ GIẤY. Lá mẫu `26RBS03-022` cũng bị công cụ từ chối; lá này chưa được triển khai luật, nên không thêm vào deck Standard tự động. Không dùng nền minh họa hình chữ nhật hoặc model đồ chơi thay cho ảnh tách còn thiếu.

Ảnh tách được tạo với **hỗ trợ AI từ artwork của từng mã thẻ**, đã bỏ khung, chữ và nền. Công cụ có thể diễn giải lại chi tiết và phần hình bị che; đây không phải bản cắt pixel chính xác hoàn toàn của artwork. `Picture/Art` và danh mục/luật gốc không bị sửa. Tên, thiết kế và artwork chính thức vẫn giữ nguồn và quyền của chủ sở hữu; ảnh tách không phải model chính thức BANDAI. Xem [sổ nguồn và checksum](../Data/BattleSpirits/standee-sources.json).

## Chạy thử

1. Đóng Unity, cập nhật nhánh `battle-spirits-mdpro3-ui`, mở bằng **6000.0.24f1**, Windows x64.
2. Giữ `Picture/Standee` cùng cấp với `Assets`; Addressables chọn **Use Asset Database**. Mở `Assets/Scenes/Boot.unity`, nhấn Play.
3. Vào **Hoạt cảnh**, chọn mục **GIẤY** để xem một nhân vật có ảnh tách. Trong **Đấu với máy**, giữ bài mở đầu rồi triệu hồi Spirit từ Main bằng nút **TRIỆU HỒI**.
4. Nút **GIẤY / ẢNH** ở khung soi đổi chế độ. GIẤY dùng cutout riêng; ẢNH dùng vùng minh họa có nền như bản trước. Lựa chọn mới mặc định GIẤY, không đọc lại tùy chọn model 3D cũ.
5. Thử chọn/soi/phóng to mặt thẻ, chuyển mức **THẤP / VỪA / CAO**, tấn công/chặn và quay lại menu. Nút chọn vẫn đặt trên mặt thẻ nằm trên sân.

## Tệp và vòng đời

| Phần | Vị trí |
| --- | --- |
| PNG nhân vật trong suốt theo đúng ID | `Picture/Standee/{ID}.png` |
| Nguồn ảnh, alpha, kích thước và SHA-256 | `Data/BattleSpirits/standee-sources.json` |
| Hình học giấy, cạnh và đế | `Assets/BattleSpirits/Runtime/Visuals/BattleCharacterStandee.cs` |
| Shader URP alpha clip | `Assets/BattleSpirits/Art/Resources/BattleSpirits/PaperStandee.shader` |
| Mặt thẻ, tải ảnh và lựa chọn hiển thị | `BattleCardFigure.cs`, `BattleNativeSession.cs`, `BattleNativeDuelView.cs` |

Session tải ảnh theo khóa riêng `standee:{ID}` và giữ cutout đọc được để dựng mesh một lần. Mặt thẻ vẫn dùng cache riêng. Các bản sao cùng ID/texture dùng chung mesh, material và đế; chuyển động không đọc lại alpha mỗi frame. Ảnh rỗng/opaque toàn khung hoặc không đọc được bị từ chối. Mesh trace được giới hạn để không vượt ngân sách hình học; mặt in vẫn dùng texture đầy đủ. Mesh/material được giải phóng khi đóng sân, texture thuộc session. Bước đóng gói Windows sao chép cutout theo từng ID, bỏ qua lá thiếu ảnh tách. Hook đóng gói được đọc mã/phân tích cú pháp, chưa chạy build Unity. Pet, wallpaper, luật, AI và giao thức mạng không đổi.

Nguồn model rig 0.7.0 và tài liệu lịch sử được giữ để tra cứu, nhưng renderer sân không khởi tạo chúng. Không cần Blender để chơi game.

## Bằng chứng kiểm tra

[Ảnh xem thử](Previews/CharacterStandees/) được dựng bằng Blender từ **mesh production xuất bởi harness**, với ánh sáng studio. Đây không phải ảnh chụp hoặc bằng chứng Play/render Unity Windows. Báo cáo [0.7.1](battle-spirits-standee-validation.json) ghi số kiểm tra thực tế; chưa xác minh shader, input, pixel hoặc FPS trên Editor Windows.

```bash
python Tools/BattleSpiritsAssets/check_standee_sources.py
dotnet run --project Tests/BattleSpirits/Presentation/BattleSpirits.Presentation.Tests.csproj -c Release -- /path/to/Card-Game
```

Backup trước đợt này: `backup/2026-11-13-before-character-standees` → `05c3b9ff53a10f499c7e7e34a77477b8b32bd640`. Bản Yu-Gi-Oh trên `main` được giữ riêng.
