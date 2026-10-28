# Các thiết lập có thể chỉnh

Cách cài phần mềm, build và chạy nằm trong [README chính](../README.md). File này giải thích những thiết lập bạn có thể cần thay đổi; nếu thiết lập mặc định phù hợp, cứ dùng nguyên bản.

Người mới clone/tải ZIP nên hoàn tất [hướng dẫn lần đầu](../../README.md). Các lệnh ở đây dùng CMD tại thư mục `Tank_Game_updated` của source đã giải nén, không phụ thuộc ổ đĩa hoặc tên máy.

## Cấu hình server

| File | Dùng để làm gì? |
|---|---|
| `config/server.properties` | Bản thiết lập mặc định của dự án |
| `config/server.local.properties` | Bản riêng của máy chạy server, chỉ tạo khi muốn đổi thiết lập mặc định |

**`server.local.properties` hiện chưa có sẵn.** Nếu cần, sao chép `server.properties` ngay trong thư mục `config`, rồi đặt tên bản sao là `server.local.properties`. Hai file sẽ nằm cạnh nhau.

Script `start-server.cmd` tự chọn bản local nếu file đó tồn tại. Bản local phải chứa đầy đủ thiết lập của bản gốc; script không tự ghép hai file. Nếu muốn chọn rõ bản dùng chung, truyền thêm `-Config config/server.properties` khi chạy script:

```cmd
scripts\start-server.cmd -Config "config\server.properties"
```

Những mục có thể cần chỉnh trong bản local:

| Mục | Ý nghĩa | Mặc định |
|---|---|---|
| `tcp.port` | Cổng kết nối điều khiển của server | `8888` |
| `udp.port` | Cổng gửi dữ liệu gameplay | `8889` |
| `database.path` | Nơi lưu tài khoản và lịch sử trên máy server | `runtime/tank.db` |
| `log.path` | Nơi ghi thông tin hoạt động/lỗi của server | `runtime/server.log` |
| `bind` | Địa chỉ mạng server nhận kết nối | `0.0.0.0`: nhận trên các địa chỉ mạng của máy |
| `admin.enabled` | Bật website quản trị cùng server | `true` |
| `admin.bind` / `admin.port` | Địa chỉ và cổng web | `127.0.0.1` / `8080` |
| `admin.database.path` | Admin, ban, lịch sử kết nối và audit | `runtime/admin.db` |
| `admin.session.minutes` | Thời hạn phiên đăng nhập web | `60` |

Nếu đổi cổng, thông báo cổng mới cho người chơi và mở cổng tương ứng trên firewall. Các đường dẫn như `runtime/tank.db` được tính từ thư mục gốc dự án. Giữ nguyên những mục còn lại nếu chưa cần thay đổi.

`0.0.0.0` là thiết lập lắng nghe của server. Địa chỉ người chơi nhập vào client là IP thật của máy server, theo mục **Kết nối đến server** trong README chính.

## Đường dẫn Java và Unity

| Tên thiết lập | Chỉ tới đâu? | Ai có thể cần chỉnh? |
|---|---|---|
| `JAVA_HOME` | Thư mục cài JDK 21, bên trong có thư mục `bin` | Người chạy hoặc build server, nếu script chưa tìm đúng Java |
| `UNITY_EDITOR_PATH` | File `Editor/Unity.exe` | Người mở/build/test client, nếu script chưa tìm được Editor |

Người chỉ chơi bản client đã build không cần hai thiết lập này. Người chỉ chạy server không cần Unity. Các lệnh thiết lập và kiểm tra nằm ở mục **Cài đặt môi trường** trong README chính.

`config\tools.example.cmd` là mẫu tùy chọn lưu đường dẫn công cụ. Sao chép thành `config\tools.local.cmd`, sửa đường dẫn thật rồi nạp vào CMD hiện tại bằng:

```cmd
call config\tools.local.cmd
```

File riêng được gitignore, không tự được nạp và không dùng để lưu mật khẩu.

## Website quản trị

Server chuẩn bật web tại `127.0.0.1:8080` bằng `admin.enabled=true` trong `server.properties`. File local là cấu hình đầy đủ, không tự kế thừa bản gốc; nếu đã có `server.local.properties` cũ thì cần thêm các khóa `admin.*` theo [ADMIN_WEB.md](../ADMIN_WEB.md). Khởi tạo admin và chạy server phải trỏ cùng file cấu hình, cùng `admin.database.path`.
