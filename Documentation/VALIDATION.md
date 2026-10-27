# Kết quả kiểm tra bản nguyên mẫu

Ngày đối chiếu: **27/10/2026**. Editor mục tiêu: **Unity 6000.6.7f1**, revision `b22767f04f04`.

## Đã thực hiện

- Core C# biên dịch thành công với **.NET Standard 2.1**, giới hạn ngôn ngữ **C# 9**.
- **15 nhóm test luật độc lập** thành công: chuẩn bị/mulligan, lượt đầu, giảm phí, trả phí bằng core trên sân, Soul Core, level/tiêu biến, Flash, chặn/so BP, Life/thắng, hết deck, giới hạn deck và mô phỏng trận.
- Trong bộ test chính: **64 trận dữ liệu kiểm thử + 32 trận dùng catalog chính thức**, tổng **4.686 lệnh hợp lệ**.
- Audit độc lập dùng cả 16 thẻ: **100 trận hoàn tất**, **8.335 trạng thái** và **75.015 lần thử hành động**. Kiểm tra core/Soul Core, bảo toàn từng ID thẻ, actor không hợp lệ, giới hạn phase và trạng thái không thay đổi khi lệnh bị từ chối.
- Roslyn đọc **10 file C# trong Assets** theo C# 9, không có lỗi cú pháp. Đây là kiểm tra cú pháp, không phải biên dịch toàn bộ API Unity.
- Đối chiếu catalog 16 thẻ với nguồn chính thức, SHA-256 của 16 hình PNG, GUID script trong scene và font có glyph tiếng Việt/tiếng Nhật.
- Blender export/import lại FBX: 943 vertex, 1.578 triangle, 13 bone, 3 animation `Idle`, `Summon`, `Attack`; kiểm tra rig, weight, thay đổi pose và chân trong animation tấn công.
- Công cụ kiểm tra cập nhật thẻ đã đọc lại trang chính thức cho `BS01-001` và trả về không thay đổi.

## Chưa thực hiện

Môi trường tạo project **không cài Unity Editor**. Chưa xác minh trực tiếp:

- Import package/URP/FBX/font trong Editor 6000.6.7f1.
- Các test EditMode tích hợp của Unity.
- Play, hình ảnh UI/bàn đấu và binding animation trong Unity.
- Build và chạy file Windows x64.

Không coi kết quả test C# hoặc Blender là bằng chứng rằng Unity đã Play/build thành công.

## Chạy lại

Tại thư mục project, với Python 3 và .NET SDK 10:

```bash
python Tools/validate_project.py
dotnet run --project Tests/Core/BattleSpirits.Core.Tests.csproj -c Release -- Assets/BattleSpirits/Resources/Data/cards.json
dotnet run --project Tests/Audit/BattleSpirits.Audit.csproj -c Release
```

Trong Unity: **Window → General → Test Runner → EditMode → Run All**. Sau đó mở scene Arena, Play, thử mulligan, triệu hồi, chuyển core, tấn công/chặn/nhận Life và tạo trận mới. Kiểm tra **Tools → Battle Spirits → Build Windows x64** khi đã cài module Windows Build Support.

Model nguồn và script tái tạo có trong `Tools/Art/`. Các bản ghi chi tiết nằm trong `Documentation/ValidationReports/`.
