# Kết quả kiểm chứng và giới hạn

Tài liệu này ghi nhận kết quả ở môi trường phát triển, không khẳng định mọi máy tải source sẽ có sẵn các công cụ hoặc artifact. Cách chạy cho người mới nằm trong [README chính](../README.md).

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
