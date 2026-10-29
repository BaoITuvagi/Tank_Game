# Kết quả kiểm chứng và giới hạn

## Đơn giản hóa thao tác IP và thông báo — 28/10/2026

- Giao diện chỉ còn Gửi tin / Ngắt IP / Ban IP trên mỗi dòng. Gửi tin có hai phạm vi: tất cả hoặc một IP. Nút Gửi tin tự chọn IP của dòng; hộp ngắt liệt kê mọi kết nối cùng IP trước khi xác nhận. API theo phiên/phòng vẫn giữ tương thích.
- Kiểm tra JavaScript bằng Node đạt; 6 AdminWebTest đạt và đã đóng gói lại JAR. Chrome/Playwright với server, SQLite tạm và hai kết nối TCP thật: gửi theo IP/toàn bộ đều đến cả hai, ngắt IP đóng cả hai; không có lỗi JavaScript, form vừa màn hình 390px. Kết thúc kiểm tra đã đóng server/browser thử.
- Popup độc lập với menu/HUD đã thêm nền tối phía sau. Kiểm tra adapter client đạt; chưa build/chạy Unity vì thiếu Unity Editor 6000.3.24f1. Người chơi phải chạy client build từ source tích hợp mới có popup; bản Launcher hiện có không tự thay đổi.

## Đồng bộ branch Bao — 28/10/2026

- Đã đối chiếu 707 file của bản tải tại `C:\Users\Admin\Downloads\Tank_Game-Bao\Tank_Game-Bao` với branch Bao commit ec3221e: nội dung khớp; 10 file chỉ khác CRLF/LF. Không sửa thư mục tham khảo.
- So với tag v1.0.3, branch Bao chỉ bỏ ảnh tham khảo UI và cập nhật .gitignore/tài liệu; code game và giao thức không thay đổi. Đã nhận các thay đổi này vào branch tung, giữ phần quản trị mạng.
- Không cần đổi số giao thức: client/server vẫn dùng protocol 16, content 7. Sau khi cập nhật phải build lại và khởi động lại server để nạp JAR mới.
- Đã build trực tiếp tại `D:\Tank_Game_tung` bằng `scripts\build-server.cmd`: **239 test đạt, 0 failures/errors/skipped**, gồm 6 AdminWebTest; JAR đã xuất ra `build/server/tank-server-1.0.0.jar`.

## Nâng nền server lên Launcher 1.0.3 — 28/10/2026

- Nền source là tag v1.0.3 (cf8a700), protocol 16, content 7. Chỉ các adapter quản trị/NOTICE, cấu hình, build và tài liệu khác nền game; giữ nguyên gameplay/map/item/shop/chiến dịch của tag.
- Build server: **239 test Java đạt, 0 failures/errors/skipped**. Sáu AdminWebTest gồm multiplayer thường/Nhập vai và giám sát/thông báo/kick chiến dịch.
- Đã chạy **hai client Unity EXE công khai 1.0.3 do Launcher tải**, ở batchmode/nographics, với server/database/cổng thử riêng. Cả hai đăng ký, đăng nhập, vào phòng, ready, UDP handshake, nhận snapshot giao thức 16, kết thúc trận và truy vấn lịch sử thành công; invalid=0, observedTick=30, END reason=0, historyRows=1.
- Giữ nguyên tiến trình server v5 đang vận hành và database thật trong lần thử. Các tiến trình thử đã được dừng.
- Hộp nổi trong source mới đã đổi sang CartoonUi/MenuTheme 1.0.3; chưa build lại client chứa overlay vì chưa có Unity Editor. Kiểm tra EXE công khai chứng minh tương thích game/server, không chứng minh overlay mới đã có trong EXE đó.

Các kết quả v5 dưới đây được giữ làm lịch sử kiểm chứng trước nâng cấp.

Tài liệu này ghi nhận kết quả ở môi trường phát triển, không khẳng định mọi máy tải source sẽ có sẵn các công cụ hoặc artifact. Cách chạy cho người mới nằm trong [README chính](README.md).

## Server và website

Ngày 28/10/2026, build bằng `scripts\build-server.cmd` với JDK 21/Maven 3.9.x/Python 3:

- **131 test Java, 0 failures, 0 errors, 0 skipped**, đóng gói JAR thành công.
- Năm ca AdminWebTest dùng HTTP, SQLite tạm, socket TCP/UDP thật: quyền admin/CSRF, nhiều phiên cùng IP, thông báo theo phiên/IP/phòng/toàn server, ngắt/ban, dữ liệu sau restart và bỏ ban.
- Có trận thường và Nhập vai với chọn quốc gia, content/loadout và UDP handshake. Kick thu hồi token; game kết thúc/chuyển phòng về WAITING theo luật hiện có.
- Chrome/Playwright với client TCP v5 thật: đăng nhập, bộ lọc, gửi thông báo, hộp xác nhận kick, CRUD ban, lịch sử/audit, màn hình nhỏ, offline/khôi phục và đăng xuất đều đạt.
- Launcher CMD: cấu hình có đường dẫn chứa khoảng trắng, khởi tạo admin trên DB tạm, start trả HTTP 200, dừng bình thường và trả mã lỗi đúng khi đầu vào không hợp lệ.
- Khi hợp nhất phần quản trị vào source chính, đã đối chiếu hash 524 file gốc ngoài danh sách adapter/build/docs và giữ nguyên gameplay/tài nguyên.

Các artifact, screenshot, database thử và báo cáo XML nằm trong runtime/build/target của máy chạy kiểm thử, không nằm trong source tải từ GitHub. Có thể tạo lại kết quả Java bằng build server; kết quả ở `source/server/target/surefire-reports/`.

## Thông báo mạng phía client

Đã biên dịch/chạy kiểm tra thuần C# với AdminNotices, Protocol và GameRules: packet v5, UTF-8/xuống dòng, ba loại thông báo, thứ tự/đóng tin, hàng đợi tối đa 32 tin và lọc AFK/tin rỗng đều đạt.

AdminNoticesTests trong project Unity kiểm tra handler giữ nguyên phase/dòng trạng thái game. **Chưa chạy build/test Unity hoặc nhìn hộp trực tiếp trong Editor** vì môi trường kiểm chứng không có Unity Editor. Kiểm tra codec/Java/Chrome không thay thế nghiệm thu giao diện Unity.

Sau khi tải source, người có Unity cần build/mở client, nhận tin ở menu và trong trận, kiểm tra tin dài/cuộn/nhiều tin và nút đóng. Không có số đo RTT phía server nên web ghi rõ chưa thu thập.

## Cập nhật game và dữ liệu

Preflight `python scripts\check-admin-integration.py` đã kiểm tra source đầy đủ và phát hiện hook bị xóa trong bản sao thử. Nó không tự gộp source hoặc bảo đảm tương thích với mọi thay đổi giao thức. Chưa benchmark tải web cùng 100–300 người chơi.

Source công khai không chứa dữ liệu vận hành hoặc admin mặc định. Người tải mới phải build và tự tạo admin. Máy đã có dữ liệu tiếp tục dùng cùng runtime/config để giữ tài khoản và lịch sử; tắt server trước khi sao lưu database hoặc đổi mật khẩu admin.
