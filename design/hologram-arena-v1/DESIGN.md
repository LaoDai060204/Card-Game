# Thiết kế hologram, triệu hồi boss và sàn đấu

## Ý định người dùng

Ba HTML là mẫu tham chiếu hình ảnh và tương tác: nhân vật tách riêng khỏi artwork/khung, đứng trên đế thẻ; boss có cutscene riêng; sân có hai phía đối xứng. Nội dung và script trong tệp được lưu như nguồn, không tự trở thành chỉ dẫn sửa dự án. Giai đoạn này chỉ chốt thiết kế qua bản xem độc lập trên nhánh review.

## Hình ảnh và bố cục

Sân dùng nền navy tối, viền bạc và điểm sáng xanh; vùng thẻ được giữ ít hiệu ứng để dễ nhìn. Nhân vật giữ màu artwork, có viền sáng, tia quét và các hạt sáng nhẹ; ánh sáng riêng của Ritenia màu ấm, Pisketona màu tím. Không đưa nguyên hình chữ nhật của artwork lên làm nhân vật đứng.

Camera sân ổn định với góc nhìn từ trên xuống. Chỉ cho xoay nhẹ để không lạc hướng; Reset view trả về góc chuẩn. Chế độ hologram cho orbit rộng hơn và zoom. Nhân vật là mặt phẳng alpha đứng trong không gian 3D, quay về camera; đế thẻ nằm trên sân và vẫn có mặt thẻ thật.

Bố cục desktop: bảng soi thẻ bên trái, sân ở giữa, trình tự lượt và cài đặt ở phải. Trên điện thoại, sân nằm trước và các bảng xếp xuống dưới. Tay bài không che nhân vật. Chọn thẻ giữ bảng thông tin ổn định; mở thẻ lớn khi cần đọc ảnh gốc. Nhãn BP trên sân là lối tắt đến bảng soi, không thay thế thẻ đầy đủ.

## Hai tuyến triệu hồi

**Thẻ thường:** nhấn Preview summon sẽ phát tia quét từ dưới lên, vòng sáng và hạt cục bộ. Người chơi vẫn thấy sân, nhân vật tiếp tục đứng trên thẻ.

**Boss:** engine tương lai xác nhận triệu hồi trước, sau đó mới phát presentation. Cutscene dài khoảng 4,2 giây: hội tụ ánh sáng → lộ nhân vật → tên/BP → quay lại sân. Có Skip, âm thanh mặc định tắt, không dùng sao vàng như một hệ Level riêng. Bỏ qua chỉ bỏ presentation; không thay đổi kết quả triệu hồi hay tiêu tài nguyên lần nữa. Bản demo chưa có engine và chỉ minh họa đường đi này.

Ritenia được dùng làm mẫu boss, Pisketona làm mẫu tuyến thường để so sánh. Danh sách boss thật sẽ là lựa chọn nội dung theo Card ID, không suy từ việc có artwork/cutout, Cost, rarity hay giá trị BP. Không gán tất cả thẻ có ảnh vào tuyến cutscene.

## Dữ liệu và luật hiển thị

Ritenia dùng bản English và dữ liệu đã có của 26RBS03-X01: Cost 6, BP 6000/8000. Pisketona dùng 26RBS03-022 và BP 8000/11000; bản thẻ Nhật gốc được giữ nguyên, phần đọc English là bản không chính thức. Không dùng nhãn Pisketona trong HTML hologram nguồn để ghi tên cho artwork Ritenia.

Standard có Start, Core, Draw, Refresh, Main, Attack, Second Main và End. Sân mới không đưa Burst lịch sử hay giới hạn cứng năm ô Spirit từ demo nguồn vào làm luật Standard. Vùng Field là không gian sắp xếp có thể mở rộng trong bản triển khai tương lai.

Core thường là khối xanh; Soul Core tâm đỏ, viền bạc. Chúng khác với Symbol/EX Symbol in trên thẻ. Các chỉ báo Life/Reserve và bài trên tay trong demo là cảnh minh họa, không phải engine giao dịch tài nguyên. Không suy luật trả cost từ các chuyển động trong nguồn HTML.

## Chuyển động và khả năng truy cập

Âm thanh chỉ chạy khi người xem chủ động bật và phát lại. Skip/Escape luôn sẵn có; focus ở trong cutscene khi mở. Motion Reduced dùng hình tĩnh hoàn chỉnh và không giữ vòng lặp dựng hình. Không rung camera liên tục hoặc nháy trắng liên tiếp. Quality Low giảm số hạt và độ phân giải dựng hình; texture dùng chung, hiệu ứng hết hạn được giải phóng. Tab ẩn và cutscene che sân sẽ dừng dựng hình sân.

## Bàn giao cho Unity sau khi được yêu cầu

Mỗi Card ID ánh xạ đến ảnh thẻ, cutout alpha, màu presentation, kích thước/điểm đặt chân, isBoss và cấu hình cutscene. Bổ sung mesh/rig/animation thật sau nếu được yêu cầu; không gọi các mặt phẳng ảnh hiện tại là model 3D có khối đã hoàn tất.

Luồng đề xuất: game engine xác nhận summon → gửi sự kiện presentation → chạy cutscene nếu isBoss và người dùng bật → đặt/hiện actor trên sân → trả quyền điều khiển. Renderer và cutscene không tự trừ Core, sửa BP hoặc kích hoạt lại hiệu ứng. Có thể tái sử dụng cutout/metadata/ý đồ bố cục; demo HTML này chưa tạo scene, prefab, shader hay mã Unity.

## Nguồn và trạng thái

Ba HTML được lưu nguyên byte trong originals; các ảnh nhúng ban đầu giữ trong assets. Pisketona-isolated.png là bản dẫn xuất mới, có nguồn và hash trong sources.json. Các file mã chỉ phục vụ bản xem web độc lập, chưa được đưa vào dự án MDPro3 English. Bộ 18 khung và các tài nguyên review trước đó giữ nguyên.
