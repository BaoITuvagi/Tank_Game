# Yêu cầu xây dựng web quản lý kết nối mạng cho Tank_Game

## 1. Mục tiêu

Đọc mã nguồn Tank_Game và xây dựng một ứng dụng web quản trị chạy trực tiếp trên máy server game. Admin sử dụng tài khoản admin để theo dõi các IP và tài khoản kết nối tới server, quản lý quy tắc truy cập, gửi thông báo một chiều và ngắt kết nối khi cần thiết.

Đây là phần mềm quản lý kết nối mạng phục vụ đồ án môn Lập trình mạng. Chức năng phải làm việc với kết nối thực tế của game, không chỉ hiển thị dữ liệu giả lập hoặc các bản ghi CRUD độc lập với server.

## 2. Bối cảnh dự án

- Server game viết bằng Java; client viết bằng Unity/C#.
- TCP mặc định cổng 8888 phục vụ tài khoản, phòng chơi và điều phối trận.
- UDP mặc định cổng 8889 phục vụ input, trạng thái trận và ping/pong.
- Server quản lý phiên kết nối, tài khoản đã đăng nhập, phòng chơi và trạng thái trận; dữ liệu tài khoản và lịch sử trận nằm trong SQLite trên server.
- Server là nơi quyết định và thực thi thao tác quản trị. Web phải tích hợp với trạng thái thật và cơ chế xử lý đồng thời hiện có của server.
- Kiểm tra lại mã nguồn trước khi thiết kế; các thông tin trên là bối cảnh, không thay thế việc đọc mã.

## 3. Các quyết định đã chốt

1. **Đơn vị quản lý là cặp IP + tài khoản.** IP phải gắn với tài khoản để truy vết hành vi vi phạm. Khi phát hiện một tài khoản gian lận, Admin có thể ban cả IP mà tài khoản đó sử dụng.
2. **Phạm vi Admin là quản lý IP và kết nối.** Không tạo, sửa hoặc xóa tài khoản người chơi. Việc quản lý tài khoản vẫn thuộc server game. Admin chỉ can thiệp vào phòng/trận thông qua ngắt kết nối client.
3. **Tin nhắn là thông báo một chiều từ Admin tới client**, dùng để cảnh báo vi phạm, thông báo lỗi hoặc thông báo vận hành. Không xây dựng chat hai chiều.
4. **Ứng dụng có giao diện web, chạy trên máy server game.** Mặc định truy cập quản trị tại máy server; việc cho phép quản trị từ máy khác không thuộc yêu cầu bắt buộc.
5. **Truy cập bằng tài khoản admin.** Các API quản trị phải kiểm tra phiên đăng nhập và quyền admin.

## 4. Mô hình quản lý IP và tài khoản

- Dùng IP thực tế server quan sát được từ kết nối client, không tin IP do client tự khai báo.
- Không coi IP là mã định danh duy nhất của tài khoản: một IP có thể có nhiều tài khoản, một tài khoản có thể xuất hiện ở nhiều IP theo thời gian.
- Hiển thị và lưu được mối liên hệ IP–tài khoản; phân biệt phiên đang hoạt động với lịch sử đã kết thúc.
- Với client đã kết nối TCP nhưng chưa đăng nhập, vẫn hiển thị IP và trạng thái “Chưa đăng nhập”. Sau đăng nhập, bổ sung tài khoản tương ứng.
- Cổng nguồn, mã phiên và mã trận là thông tin của phiên; không dùng cổng nguồn làm khóa của quy tắc ban IP.
- Ban IP tác động đến mọi tài khoản sử dụng IP đó. Giao diện phải nêu rõ phạm vi tác động và số phiên hiện tại bị ảnh hưởng trước khi Admin xác nhận.

## 5. Chức năng bắt buộc

### 5.1. Đăng nhập quản trị

- Có trang đăng nhập và thao tác đăng xuất.
- Tài khoản admin có cơ chế khởi tạo/cấu hình được hướng dẫn rõ ràng; không hardcode mật khẩu vào mã nguồn hoặc giao diện.
- Phân biệt quyền admin với tài khoản người chơi. Không mặc định cho tài khoản game quyền quản trị.
- Lưu mật khẩu admin dưới dạng băm an toàn; quản lý thời hạn phiên và vô hiệu hóa phiên khi đăng xuất.

### 5.2. Tổng quan và danh sách kết nối

- Hiển thị số phiên đang kết nối, số IP đang kết nối, số tài khoản đã đăng nhập, số người đang chơi và số IP đang bị ban.
- Danh sách kết nối cập nhật tự động, không yêu cầu tải lại toàn bộ trang.
- Thông tin mỗi phiên gồm: mã phiên, IP, tài khoản nếu có, thời điểm kết nối, lần hoạt động gần nhất, trạng thái TCP/UDP và phòng/mã trận nếu có.
- Phân biệt rõ các trạng thái: chưa đăng nhập, ở sảnh, trong phòng chờ, chuẩn bị trận, đang chơi, xem kết quả và đã ngắt kết nối; ánh xạ theo trạng thái thật của mã nguồn.
- Có tìm kiếm theo IP/tài khoản và lọc theo trạng thái; phân trang khi cần.
- Hiển thị RTT hoặc chỉ số mạng nếu dữ liệu thực tế đã có; không tạo số đo giả khi chưa thu thập được.

### 5.3. Chi tiết IP–tài khoản và lịch sử

- Xem các phiên của một cặp IP–tài khoản, thời điểm xuất hiện đầu/cuối và lý do kết thúc phiên nếu có.
- Từ một IP xem được các tài khoản đã sử dụng; từ một tài khoản xem được các IP đã sử dụng.
- Hiển thị trạng thái ban hiện tại, lý do ban và các thao tác quản trị liên quan.
- Lịch sử cần được lưu bền vững để tra cứu sau khi khởi động lại server.
- Chỉ hiển thị thông tin tài khoản cần thiết cho quản lý kết nối; không cung cấp thao tác sửa/xóa tài khoản hoặc dữ liệu xác thực.

### 5.4. CRUD quy tắc quản lý truy cập theo IP

CRUD áp dụng cho **quy tắc ban IP**, không phải tạo hoặc sửa socket/phiên kết nối đang hoạt động.

- **Create:** thêm quy tắc ban một IP cụ thể, gồm IP và lý do. Có thể thao tác từ danh sách kết nối hoặc nhập IP thủ công.
- **Read:** xem danh sách và chi tiết quy tắc; tìm kiếm theo IP, trạng thái hoặc lý do.
- **Update:** sửa lý do hoặc thay đổi trạng thái áp dụng của quy tắc. IP là đối tượng đích; nếu cần đổi IP, tạo quy tắc mới để giữ rõ lịch sử.
- **Delete:** xóa quy tắc, tương đương bỏ ban IP đó. Lưu nhật ký thao tác trước khi xóa; không xóa lịch sử quản trị.
- Quy tắc phải được lưu bền vững và tiếp tục có hiệu lực sau khi server khởi động lại.
- Khi ban có hiệu lực, từ chối kết nối mới từ IP đó và ngắt tất cả phiên hiện tại của IP, kể cả phiên chưa đăng nhập; dừng tiếp nhận input UDP của các phiên đã bị ngắt.
- Bỏ ban chỉ cho phép kết nối lại, không tự khôi phục phiên hoặc đưa người chơi trở lại phòng.
- Phạm vi bắt buộc là IP cụ thể; CIDR, dải IP và ban có thời hạn là tính năng mở rộng, không bắt buộc.

### 5.5. Ngắt kết nối

- Cho phép ngắt một phiên cụ thể hoặc tất cả phiên của một IP, có thể nhập lý do.
- Phân biệt rõ “Ngắt kết nối” với “Ban IP”: ngắt kết nối không cấm client kết nối lại.
- Yêu cầu xác nhận trước khi ngắt hoặc ban; hiển thị IP, tài khoản và phạm vi ảnh hưởng.
- Khi ngắt client đang ở phòng hoặc trong trận, server phải thực hiện đầy đủ quy trình rời phòng/mất kết nối hiện có: giải phóng phiên và token, dừng input, cập nhật thành viên/chủ phòng và kết quả theo luật game hiện tại.
- Không cho Admin sửa trực tiếp trạng thái trận, điểm số, máu, vị trí hoặc kết quả thi đấu.
- Nếu phiên đã đóng trước khi thao tác được thực thi, trả về trạng thái phù hợp và cập nhật giao diện, tránh báo thành công sai.

### 5.6. Thông báo một chiều

- Gửi tới một phiên, tất cả phiên của một IP, một phòng hoặc toàn bộ client đang kết nối.
- Cho nhập nội dung và loại thông báo như thông tin, cảnh báo hoặc lỗi.
- Client phải hiển thị thông báo rõ ràng cả ở menu và trong trận; không tự ngắt kết nối chỉ vì nhận thông báo.
- Ưu tiên tận dụng kênh TCP và cơ chế NOTICE nếu phù hợp; chỉ mở rộng giao thức khi cần, đồng bộ thay đổi giữa Java và C#.
- Kiểm tra nội dung rỗng và giới hạn kích thước phù hợp giao thức.
- Web hiển thị kết quả gửi và số phiên được đưa vào hàng đợi gửi. Phân biệt kết quả này với việc người dùng đã đọc; không báo “đã đọc” nếu không có cơ chế xác nhận.
- Với thao tác ngắt/ban, không phụ thuộc vào việc thông báo đã tới client mới được thực thi.

### 5.7. Nhật ký quản trị

- Ghi nhận đăng nhập/đăng xuất admin, tạo/sửa/xóa quy tắc ban, ngắt kết nối và gửi thông báo.
- Mỗi bản ghi gồm thời gian, admin thực hiện, loại thao tác, IP/tài khoản/phiên đích khi có, lý do hoặc nội dung và kết quả.
- Có giao diện tra cứu theo thời gian, IP, tài khoản và loại thao tác.
- Không ghi mật khẩu, token phiên game hoặc bí mật xác thực vào log/API/giao diện.

## 6. Yêu cầu tích hợp và kỹ thuật

- Web và các API quản trị chạy trên máy server; cổng web phải cấu hình được và không xung đột cổng TCP/UDP của game. Mặc định chỉ bind giao diện quản trị vào loopback.
- Đọc mã nguồn và chọn cách tích hợp phù hợp; không bắt buộc một framework web cụ thể. Hạn chế thêm dịch vụ vận hành không cần thiết.
- Dữ liệu trạng thái lấy từ server đang chạy; không đọc log để suy đoán trạng thái trực tuyến và không để trình duyệt truy cập trực tiếp SQLite.
- Thao tác ảnh hưởng phòng/trận phải đi qua cơ chế xử lý lệnh và đồng bộ của server, không sửa các cấu trúc dùng chung tùy tiện từ luồng HTTP.
- Không để truy vấn web hoặc ghi lịch sử chặn vòng lặp game. Dùng hàng đợi hữu hạn và xử lý lỗi/quá tải rõ ràng.
- Bảo vệ tất cả API quản trị; nếu dùng cookie, cấu hình thuộc tính cookie phù hợp và bảo vệ thao tác thay đổi dữ liệu khỏi CSRF. Kiểm tra đầu vào, dùng truy vấn tham số hóa và hiển thị thông báo dưới dạng văn bản an toàn.
- Giao diện tiếng Việt, thời gian hiển thị nhất quán, có trạng thái tải/rỗng/lỗi và thông báo kết quả thao tác.
- Khi mất kết nối tới dịch vụ quản trị hoặc dữ liệu không còn mới, giao diện phải thể hiện rõ, không tiếp tục trình bày dữ liệu cũ như trạng thái hiện tại.
- Giữ nguyên chức năng game hiện có ngoài những thay đổi cần thiết để tích hợp quản trị.

## 7. Ngoài phạm vi

- CRUD tài khoản người chơi; sửa mật khẩu hoặc cấp quyền tài khoản game.
- Ban tài khoản độc lập với IP; mục tiêu bắt buộc là ban IP gắn với tài khoản được quản lý.
- Tự động phát hiện gian lận. Admin là người quyết định cảnh báo, ngắt hoặc ban.
- Quản lý router, cấu hình mạng LAN, quét mạng hoặc điều khiển máy client.
- Chat hai chiều, xác nhận đã đọc, chỉnh sửa gameplay hoặc quản trị phòng ngoài thao tác ngắt kết nối.
- Không coi một IP là bằng chứng duy nhất xác định một người chơi, thiết bị hoặc hành vi gian lận.

## 8. Tiêu chí nghiệm thu

1. Người chưa đăng nhập admin không xem được dữ liệu hoặc gọi API quản trị; đăng xuất làm mất quyền truy cập.
2. Client kết nối thật xuất hiện trên web; đăng nhập làm rõ cặp IP–tài khoản; thay đổi trạng thái sảnh/phòng/trận được cập nhật tự động.
3. Nhiều client cùng IP được phân biệt theo phiên và tài khoản, đồng thời xem được dưới cùng IP.
4. Thêm, xem, sửa và xóa quy tắc ban hoạt động đúng; quy tắc còn hiệu lực sau khi khởi động lại server.
5. Ban một IP ngắt mọi phiên hiện tại thuộc IP đó, chặn kết nối mới và không làm ngắt các IP khác. Sau bỏ ban, client có thể kết nối lại.
6. Ngắt riêng một phiên không ban IP; client khác tiếp tục hoạt động; phòng/trận được cập nhật đúng theo luật hiện có.
7. Thông báo riêng, theo IP, theo phòng và toàn server đến đúng nhóm client, hiển thị được khi đang chơi và không tạo chức năng chat trả lời.
8. Lịch sử IP–tài khoản và nhật ký quản trị tra cứu được sau khi khởi động lại; không lộ mật khẩu/token.
9. Thao tác với phiên vừa mất kết nối, IP không hợp lệ, nội dung thông báo rỗng hoặc dữ liệu quá giới hạn được xử lý rõ ràng.
10. Build và các kiểm tra phù hợp với phần thay đổi chạy thành công; kiểm thử tích hợp có client thực hoặc bộ kiểm thử giao thức, không chỉ kiểm tra giao diện giả lập.

## 9. Kết quả cần bàn giao khi triển khai

- Mã nguồn web, API quản trị và các thay đổi cần thiết ở server/client.
- Cấu hình mẫu và hướng dẫn khởi tạo admin, build, khởi chạy, truy cập web, lưu dữ liệu và kiểm thử.
- Mô tả ngắn kiến trúc tích hợp, luồng theo dõi kết nối, luồng ban/ngắt và luồng gửi thông báo để phục vụ báo cáo môn học.
- Báo cáo những gì đã kiểm chứng, các giới hạn còn lại và các giả định đã dùng.

Các chi tiết chưa được ấn định như framework, cách cập nhật trực tiếp và cấu trúc bảng dữ liệu được lựa chọn dựa trên mã nguồn, miễn đáp ứng phạm vi và tiêu chí nghiệm thu trên.
