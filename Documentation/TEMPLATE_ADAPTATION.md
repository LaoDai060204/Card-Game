# Cách dùng template Card-Game

Template tham chiếu là `LaoDai060204/Card-Game` tại commit `6d8997a306cc2402f8b96aed40f04be2ae20229a`, bắt nguồn từ MDPro3.

| Template tham chiếu | Cách áp dụng cho dự án mới |
| --- | --- |
| `Servant` và màn hình giao diện | Tách phần hiển thị khỏi phần xử lý trận, dựng lại UI bằng UI Toolkit. |
| Card view và hoạt ảnh của trận | Thẻ có ID dữ liệu riêng; instance trên sân có ID riêng và presentation phản ứng theo sự kiện. |
| `InterString`, cấu hình ngôn ngữ | Giao diện tiếng Việt; giữ tên Nhật và nguồn chính thức trong catalog. |
| Model/AssetBundle theo nền tảng | Dùng FBX, PNG và font đã có trong project để import bằng Editor mới. |
| `PercyOCG`, `ocgcore`, YGOSharp | Thay bằng core Battle Spirits C# thuần có test độc lập. |
| Server và giao thức Yu-Gi-Oh! | Dành một tầng mạng mới cho Battle Spirits khi triển khai multiplayer. |

Khởi động và trạng thái trận được tách khỏi downloader/tin tức/dữ liệu Yu-Gi-Oh! để bản mẫu hoạt động với bộ tài nguyên đã đóng gói. Project không phụ thuộc các DLL luật hoặc tài nguyên nhiều GB của trò chơi cũ.

Luật và các API của Battle Spirits được viết cho dự án mới. Việc tham khảo kiến trúc không biến các tài nguyên bên thứ ba thành tài nguyên tự sáng tác; nguồn thẻ và font được ghi tại `THIRD_PARTY_NOTICES.md`.
