# Chế độ chiến dịch

Đăng nhập rồi chọn **Chiến dịch** / **BXH chiến dịch** ở sảnh, ngay bên dưới nút **Lịch sử**. Rời phòng nhiều người trước khi bắt đầu. Chọn một trong ba xe Mỹ, Liên Xô, Ý bằng nút dưới ô thông tin rồi bấm **Vào chiến dịch**. Khi chơi, sân đấu có HUD gọn và thanh máu trên xe/ngay góc màn hình; không có khung hướng dẫn. Cần chạy bản server và client được build cùng đợt có module này.

Tổng thống Đô-na-tri và gia đình bị Vu-a-lo bắt cóc. Tiến về phía bắc 10.000 m và hạ trùm cuối để giải cứu. Mỗi ô dọc là 1 m; điểm là khoảng cách xa nhất đạt được, đi lùi hoặc ngang không cộng điểm. Trùm xuất hiện mỗi 500 m và chặn đường tiến cho tới khi bị hạ. Có 4 mẫu địa hình nối tiếp ngẫu nhiên và 4 biến thể trùm được lặp lại; HP, sát thương và mật độ địch tăng theo khoảng cách. Chỉ giữ cửa sổ map gần người chơi trong bộ nhớ.

| Điều khiển | Tác dụng |
|---|---|
| WASD / mũi tên | Mỗi lần nhấn đi 1 ô; đi vào ô địch đánh cơ bản 20 sát thương |
| I | Bắn theo hướng xe, trúng địch đầu tiên trong 1–2 ô, 40 sát thương; bị địa hình chặn |
| J | Tự chọn địch gần nhất trong 12 ô; không cần chọn bằng chuột |
| Kết thúc & lưu | Kết thúc chuyến đi sớm, chốt điểm và vàng |

Mỗi hành động hợp lệ tiêu tốn một lượt; địch di chuyển 1 ô hoặc đánh nếu đã đứng sát xe khi bắt đầu phản ứng. Không có cập nhật chiến đấu theo thời gian thực. I hồi sau 10 hành động tiếp theo; J sau 50 hành động tiếp theo. Hành động bị chặn hoặc kỹ năng đang hồi/không có mục tiêu không làm địch đi và không giảm hồi chiêu. Kỹ năng được chuyển thể riêng theo lượt: Mỹ oanh tạc 60 sát thương trong bán kính Manhattan 2 ô quanh mục tiêu; Liên Xô 45 sát thương trong bán kính 3 ô; Ý siêu pháo 120 sát thương lên mục tiêu gần nhất. Cả ba giữ tên/chủ đề và tài nguyên xe gốc, có thông số riêng cho chiến dịch.

Địch thường rơi ngẫu nhiên vật phẩm: `+` hồi ngay 25 HP (tối đa 100), `×2` gấp đôi 3 đòn đánh cơ bản tiếp theo, `S` chặn 3 đòn đánh. Đi vào ô vật phẩm để nhặt; nhặt lại buff làm mới số lượt/đòn còn lại. Rương `$` trên đường tiếp tế cho vàng. Màn hình kết thúc hiển thị điểm, vàng chuyến đi, tổng vàng và trạng thái lưu. Bảng xếp hạng riêng lấy thành tích tốt nhất của mỗi tài khoản, tối đa 50 người, xếp điểm giảm dần rồi ID tài khoản để phân hạng ổn định.

## Ranh giới module

- Server: `CampaignRun` chỉ chứa mô phỏng chiến dịch; không phụ thuộc `World`, `Room`, `NationSkills`, `RoleplayMatch` hay map của đấu nhiều người. `CampaignSession` là adapter TCP/đăng nhập, `CampaignStore` sở hữu lưu trữ và phục hồi riêng.
- Client: `CampaignClient`/`RunState` chứa dữ liệu riêng trong namespace `Tank.Campaign`; `TankCampaignMenus` chứa chọn xe, HUD, bàn cờ, kết quả và BXH. Chỉ có hook nhỏ ở menu, vòng cập nhật và bộ nhận TCP. Giữ nguyên `ClientPhase` và giao thức các mode cũ; chiến dịch dùng cặp opcode mới 90/91, phiên bản payload 1, trên kết nối tài khoản sẵn có.
- Dùng chung SQLite và tài khoản, tạo thêm `campaign_runs` và `campaign_profiles`. Không ghi vào `matches`, `match_players` hoặc điểm/xếp hạng nhiều người. Không sửa số vàng từ client: server tự tính mọi lượt, sát thương, quà và khoảng cách.
- Mỗi run có UUID duy nhất. Chốt run và cộng vàng trong cùng transaction; gửi lại hoặc phát lại spool không cộng vàng lần hai. Lỗi ghi SQLite được giữ tại `<spool.path>/campaign/*.json`, retry sau mỗi 5 giây và khi server khởi động lại. UI cho phép lưu lại khi lỗi; không cho bắt đầu run khác cho tới khi run cũ được lưu.
- Không cho tham gia/tạo phòng khi chiến dịch đang chạy hoặc đang chốt kết quả. Đăng xuất/mất kết nối kết thúc chuyến đi và lưu kết quả; lượt chơi dở không được tiếp tục sau đăng nhập lại. Khi tiến trình server bị dừng đột ngột, chỉ kết quả đã chốt ra spool được phục hồi; không có autosave từng bước.

## Kiểm chứng

`CampaignTest` kiểm tra lượt chơi, sát thương, hồi chiêu, 3 kỹ năng, vật phẩm, trùm/giải cứu, bộ nhớ map, chống cộng vàng trùng, tách lịch sử, lỗi SQLite và phục hồi qua khởi động lại, TCP và đổi tài khoản. Test này xuất gói Java ở `source/server/target/interop/campaign-state.bin`; `CampaignTests` của Unity đọc trực tiếp để kiểm tra codec liên ngôn ngữ, Unicode BXH và xóa trạng thái tài khoản. Build/test server trước test Unity theo quy trình dự án.

Preview giao diện trong bản client: `--menu-preview campaign`, `campaign-play`, `campaign-ranking`, `campaign-results`; có thể kết hợp `--capture <đường dẫn PNG>` theo công cụ preview sẵn có. Preview không kết nối hoặc ghi dữ liệu thật.
