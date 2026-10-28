# Cấu hình Tank Battle

Hướng dẫn chính nằm tại [cấu hình và môi trường chạy](../docs/deployment.md#configuration): lựa chọn file, local override đầy đủ, cổng, database/log, Java/Unity và cấu hình thử nghiệm.

- [Cài đặt phần mềm](../README.md#cài-đặt-môi-trường).
- [Build và chạy server](../docs/deployment.md#build-server).
- [Kết nối client qua LAN/VPN](../docs/deployment.md#connect).
- [Thử multiplayer cục bộ](../docs/deployment.md#local-demo).

Giữ cấu hình riêng của máy trong file local đã được ignore; không đưa credentials hoặc database vào Git. File cấu hình mẫu vẫn ở thư mục này, không có thay đổi trong tác vụ tổ chức tài liệu.

## Website quản trị mạng

Web chạy cùng server tại 127.0.0.1:8080 theo các khóa admin.* trong server.properties. Cấu hình local là bản đầy đủ; cần giữ các khóa admin.* nếu tạo local. Khởi tạo admin và chạy server phải trỏ cùng config/database. Xem [ADMIN_WEB.md](../ADMIN_WEB.md).
