# Tài nguyên Battle Spirits tự tạo

Nhóm này tạo field, hình học Spirit/boss, hoạt ảnh và sound bank cho bản thay thế trực tiếp. Mặt thẻ chính thức được quản lý riêng bởi [công cụ thẻ](../BattleSpiritsCards/README.md).

| Tài nguyên | Vị trí |
| --- | --- |
| Geometry field/Spirit/boss trong Unity | `Assets/BattleSpirits/Runtime/Visuals/BattleSpiritGeometry.cs`, `BattleFieldVisuals.cs` |
| Đường cong triệu hồi/tấn công/chặn/nhận đòn/phá hủy/thắng-thua | `Assets/BattleSpirits/Runtime/Visuals/BattleVisualMotion.cs` |
| Render menu | `Picture/Art/bs-menu.png` |
| Closeup hai boss | `Picture/Closeup/BS01-X01.png`, `Picture/Closeup/BS01-097.png` |
| Scene Blender có mesh và keyframe | `Tools/BattleSpiritsAssets/Source/OriginalSpirits.blend` |
| Nhạc/SFX PCM16 | `Sound/BattleSpirits/*.wav` |

Hai model boss là **diễn giải fan-made kiểu low-poly** gắn với mã thẻ, không phải model chính thức của BANDAI hay bản trích từ game thương mại. Các Spirit khác dùng geometry thay thế theo màu. Runtime Unity dùng mã C# để dựng model và chạy đường cong; không cần Blender để mở/chơi game. Tệp `.blend` nằm ngoài Assets để tránh yêu cầu Blender khi Unity import.

`visual-manifest.json` ghi checksum của hai PNG RGBA 1024×1024, menu 1920×1080 và scene `.blend`. Ảnh được render từ geometry gốc bằng Blender 4.3.2/Cycles CPU, không phải chỉnh sửa artwork mặt thẻ chính thức. Scene có keyframe chuyển động cánh/triệu hồi; đường cong runtime Unity là mã riêng, không tự import các action Blender.

Sound bank gồm loop nhạc 16 giây và tám cue `summon`, `boss_summon`, `attack`, `core`, `damage`, `destroy`, `victory`, `defeat`. Âm thanh được tổng hợp từ hàm sóng và noise xác định, không dùng bản ghi bên thứ ba. Thông số, peak/DC và SHA-256 nằm trong `Sound/BattleSpirits/manifest.json`.

Manifest áp dụng **CC0-1.0 cho phần geometry/render và âm thanh tự tạo** được nêu trong đó. Quyền đối với tên, thiết kế và artwork chính thức Battle Spirits, cùng tài nguyên Master Duel dùng lại, vẫn theo [THIRD_PARTY_NOTICES](../../THIRD_PARTY_NOTICES.md).

## Tạo lại

Từ thư mục gốc dự án, với Python 3 và Blender 4.3.2:

```bash
python Tools/BattleSpiritsAssets/audio.py
blender --background --python Tools/BattleSpiritsAssets/render_originals.py -- --project /path/to/Card-Game --samples 72
```

Lệnh render ghi lại PNG, `.blend` và manifest; hình học runtime nằm trong C# nên khi đổi mẫu cần đồng bộ cả hai phần nếu muốn chúng giống nhau. Chạy kiểm tra dữ liệu/chuyển động:

```bash
dotnet run --project Tests/BattleSpirits/Visuals/BattleSpirits.Visuals.Tests.csproj -c Release -- /path/to/Card-Game
```

Kiểm tra thuần xác nhận đường cong, PCM, file/hash và giới hạn số học. Render Blender, kiểm tra PCM và Unity stand-in không xác nhận shader, chuyển động hoặc phát AudioSource trong Windows Unity thật.
