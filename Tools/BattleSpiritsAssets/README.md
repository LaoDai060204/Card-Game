# Tài nguyên giao diện, trình diễn và âm thanh Battle Spirits

Nhóm này chứa sân, mã trình diễn thẻ, sound bank và công cụ kiểm tra nguồn ảnh. Từ 0.4.0, quái trên sân dùng minh họa của đúng lá bài. Từ 0.5.0, UI dùng lại một phần chrome gốc cùng ảnh Battle Spirits có nguồn riêng; các ảnh đó không được coi là tài nguyên tự tạo. Mặt thẻ chính thức được quản lý riêng bởi [công cụ thẻ](../BattleSpiritsCards/README.md).

Từ **0.7.1**, mặc định dùng **standee giấy từ ảnh nhân vật trong suốt**, với mặt trước/sau, cạnh và đế oval. Có 18 cutout Standard, một lá chơi được còn thiếu ảnh tách. [Hướng dẫn và giới hạn](../../Documentation/BATTLE_SPIRITS_CHARACTER_STANDEES.md), [sổ nguồn PNG](../../Data/BattleSpirits/standee-sources.json). Ảnh có hỗ trợ AI; không phải bản tách pixel chính xác hoàn toàn. Model rig bên dưới là lịch sử 0.7.0, không còn khởi tạo trên sân mặc định.

Từ **0.7.0**, 19 Spirit Standard có model skinned 3D tự dựng theo artwork, tạo từ C# tại runtime. [Manifest mới](spirit-model-manifest.json) ghi từng ID/nguồn ảnh/hash/thông số mesh và ảnh dựng thử từ đúng geometry. [Hướng dẫn](../../Documentation/BATTLE_SPIRITS_3D_SPIRITS.md) mô tả chế độ 3D/ẢNH, các mức chất lượng và giới hạn kiểm tra. Bộ model này khác hai model boss low-poly cũ trong `visual-manifest.json`.

| Tài nguyên | Vị trí |
| --- | --- |
| Bảng màu/chrome và texture skin | `Assets/BattleSpirits/Runtime/Visuals/BattleTheme.cs` |
| Biểu tượng màu, mặt sau, playmat và chrome | `Assets/BattleSpirits/Art/Resources/BattleSpirits/Skin` |
| Provenance/checksum ảnh skin | `Data/BattleSpirits/skin-sources.json` |
| Sân và bố trí thẻ trong Unity | `Assets/BattleSpirits/Runtime/Visuals/BattleFieldVisuals.cs` |
| Mặt thẻ trên sân + ảnh minh họa nổi | `Assets/BattleSpirits/Runtime/Visuals/BattleCardFigure.cs` |
| Cache mesh, rig và chuyển động 3D | `Assets/BattleSpirits/Runtime/Visuals/BattleSpiritModel.cs` |
| 19 profile theo anatomy/artwork từng lá | `Assets/BattleSpirits/Runtime/Visuals/BattleSpiritModelProfiles.cs` |
| Ánh sáng/màu đỉnh của model | `Assets/BattleSpirits/Art/Resources/BattleSpirits/SpiritModel.shader` |
| Trình diễn triệu hồi trên UI bàn đấu | `Assets/BattleSpirits/Runtime/NativeUI/BattleSummonPresentation.cs` |
| Đường cong trình diễn triệu hồi | `Assets/BattleSpirits/Runtime/Visuals/BattleSummonMotion.cs` |
| Hình học sân và nguồn model cũ | `Assets/BattleSpirits/Runtime/Visuals/BattleSpiritGeometry.cs` |
| Đường cong triệu hồi/tấn công/chặn/nhận đòn/phá hủy/thắng-thua | `Assets/BattleSpirits/Runtime/Visuals/BattleVisualMotion.cs` |
| Render menu | `Picture/Art/bs-menu.png` |
| Closeup hai boss | `Picture/Closeup/BS01-X01.png`, `Picture/Closeup/BS01-097.png` |
| Scene Blender có mesh và keyframe | `Tools/BattleSpiritsAssets/Source/OriginalSpirits.blend` |
| Nhạc/SFX PCM16 | `Sound/BattleSpirits/*.wav` |

Mặt thẻ nằm ngang trên sân. Trong chế độ ẢNH, vùng minh họa của cùng ảnh được lấy bằng UV và hiển thị nổi hướng về camera. Mã renderer giữ tỷ lệ artwork và mượn texture từ cache của session, không ghi lại hoặc sửa PNG nguồn. Vùng ảnh nổi **vẫn có nền minh họa**, không phải ảnh quái đã tách nền. Trình diễn triệu hồi khoảng 1,95 giây có quét sáng, phóng to/xoay thẻ, nâng ảnh và đưa thẻ về sân; với standee mới, bước đáp còn khởi động chuyển động hiện phần giấy. Xem [cách hoạt động và thử trong Editor](../../Documentation/BATTLE_SPIRITS_SUMMON_PRESENTATION.md).

Hai model boss cũ là **diễn giải fan-made kiểu low-poly**, không phải model chính thức của BANDAI hay bản trích từ game thương mại. Source geometry, render và `.blend` vẫn được lưu, nhưng không được dùng làm quái trên sân trong 0.4.0. Tệp `.blend` nằm ngoài Assets để tránh yêu cầu Blender khi Unity import; mở/chơi game không cần Blender.

`visual-manifest.json` ghi checksum của hai PNG RGBA 1024×1024, menu 1920×1080 và scene `.blend`. Ảnh được render từ geometry gốc bằng Blender 4.3.2/Cycles CPU, không phải chỉnh sửa artwork mặt thẻ chính thức. Scene có keyframe chuyển động cánh/triệu hồi; đường cong runtime Unity là mã riêng, không tự import các action Blender.

Sound bank gồm loop nhạc 16 giây và tám cue `summon`, `boss_summon`, `attack`, `core`, `damage`, `destroy`, `victory`, `defeat`. Âm thanh được tổng hợp từ hàm sóng và noise xác định, không dùng bản ghi bên thứ ba. Thông số, peak/DC và SHA-256 nằm trong `Sound/BattleSpirits/manifest.json`.

Manifest áp dụng **CC0-1.0 cho phần geometry/render và âm thanh tự tạo** được nêu trong đó. Quyền đối với tên, thiết kế và artwork chính thức Battle Spirits, cùng tài nguyên Master Duel dùng lại, vẫn theo [THIRD_PARTY_NOTICES](../../THIRD_PARTY_NOTICES.md).

## Ảnh giao diện 0.5.0

[Sổ nguồn skin](../../Data/BattleSpirits/skin-sources.json) ghi 14 ảnh: sáu biểu tượng màu thẻ chính thức, Core/Soul Core, mặt sau BS, playmat Standard và mẫu xám, cùng ba chrome gốc. Biểu tượng chính thức là bản 47×47; mặt sau là bản mirror 350×490 có khác dòng bản quyền so với ảnh tham khảo 500×730. JPEG playmat được trích nguyên byte từ PDF chính thức; chrome được sao chép nguyên byte từ template. Các ảnh này không thuộc CC0 của geometry/render/audio tự tạo.

Sân dùng viền và logo của playmat Standard, không đưa chỉ dẫn Burst/lượt Eternal từ mẫu xám lên thay luật. Xem [phạm vi giao diện](../../Documentation/BATTLE_SPIRITS_THEME.md). Kiểm tra file, metadata và hash:

```bash
python Tools/BattleSpiritsAssets/check_skin_sources.py
```

Công cụ này không xác nhận render Unity hay cấp quyền mới đối với artwork.

## Tạo lại

Từ thư mục gốc dự án, với Python 3 và Blender 4.3.2:

```bash
python Tools/BattleSpiritsAssets/audio.py
blender --background --python Tools/BattleSpiritsAssets/render_originals.py -- --project /path/to/Card-Game --samples 72
```

Lệnh render ghi lại các PNG menu/closeup, `.blend` và manifest cũ; nó không tạo ảnh minh họa nổi mới cho thẻ và không thay PNG artwork chính thức. Chạy kiểm tra dữ liệu/chuyển động:

```bash
dotnet run --project Tests/BattleSpirits/Visuals/BattleSpirits.Visuals.Tests.csproj -c Release -- /path/to/Card-Game
```

Kiểm tra thuần xác nhận đường cong, PCM, file/hash và giới hạn số học. Render Blender, kiểm tra PCM và Unity stand-in không xác nhận shader, chuyển động hoặc phát AudioSource trong Windows Unity thật.

Xuất đúng mesh runtime 0.7.0 để dựng ảnh kiểm tra (ngoài Assets):

```bash
BATTLE_SPIRITS_MODEL_EXPORT=/tmp/bs-models dotnet run --project Tests/BattleSpirits/Presentation/BattleSpirits.Presentation.Tests.csproj -c Release -- /path/to/Card-Game --export-models-only
blender --background --python Tools/BattleSpiritsAssets/render_spirit_model_preview.py -- --models /tmp/bs-models/production-models.json --output /tmp/bs-models/renders --samples 48
```

Preview dùng ánh sáng/nền studio riêng để xem geometry, đọc color-space từ cấu hình dự án và không sửa mặt thẻ gốc. Không cần chạy các lệnh này để mở/chơi game trên Windows.

Từ 0.5.1, ảnh ngọc trong bộ skin cũng hiển thị Core trên sân: xanh cho Core thường, đỏ cho Soul; tím/lục/vàng/trắng chỉ đổi ngoại hình Core thường. Xem [cách chọn và kiểm tra ảnh thẻ](../../Documentation/BATTLE_SPIRITS_CORE_GEMS_AND_ART_QUALITY.md).

Xuất và xem đúng mesh giấy 0.7.1 (ảnh studio Blender, không phải render Unity):

```bash
python Tools/BattleSpiritsAssets/check_standee_sources.py
BATTLE_SPIRITS_STANDEE_EXPORT=/tmp/bs-paper dotnet run --project Tests/BattleSpirits/Presentation/BattleSpirits.Presentation.Tests.csproj -c Release -- /path/to/Card-Game --export-standees-only
blender --background --python Tools/BattleSpiritsAssets/render_character_standee_preview.py -- --standees /tmp/bs-paper/production-standees.json --output /tmp/bs-paper/renders --gallery
```

Kiểm tra PNG cần Pillow. Công cụ chỉ đọc RGBA/hash; không tách nhân vật hay chỉnh pixel. Cutout mới được tạo bằng công cụ ảnh có tham chiếu artwork gốc, rồi lưu riêng trong `Picture/Standee`.
