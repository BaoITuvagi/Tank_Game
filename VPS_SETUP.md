# Chạy Tank Battle và web trên VPS

Hướng dẫn dành cho VPS mới dùng Ubuntu 24.04 LTS x86_64, có IPv4 public và SSH. Người chơi vẫn dùng Launcher. Máy cá nhân không chạy server game; VPS chạy liên tục. Giai đoạn đầu không cần tên miền: quản trị web qua SSH được mã hóa. Không mở cổng web 8080 ra Internet.

Các lệnh CMD chạy trên Windows; các lệnh bash chạy sau khi SSH vào VPS. Thay `IP_VPS` và `TAI_KHOAN_VPS` bằng thông tin nhà cung cấp cấp cho bạn. Tài khoản SSH cần quyền sudo (thường là ubuntu hoặc root). Nếu dùng khóa SSH, thêm `-i "duong-dan-khoa"` vào lệnh ssh/scp theo hướng dẫn nhà cung cấp.

## 1. Tạo VPS

- Chọn VPS/máy ảo chạy lâu dài, hỗ trợ cả TCP và UDP, Ubuntu 24.04 LTS x86_64, có IPv4 public. Hosting chỉ phục vụ HTML không chạy được server game này.
- Để thử nhóm nhỏ, có thể bắt đầu 2 vCPU, RAM 2 GB; đây là mức khởi đầu ước lượng, chưa phải cấu hình đã kiểm chứng cho tải tối đa. Chọn nơi gần người chơi và kiểm tra chi phí IPv4, lưu trữ, lưu lượng trước khi tạo.
- Lưu IP, tài khoản SSH và cách đăng nhập. Không đưa mật khẩu/khóa riêng lên GitHub hoặc gửi trong chat.
- Nếu có firewall/security group của nhà cung cấp: cho phép SSH TCP 22 (hoặc cổng SSH thực tế), game TCP 8888 và UDP 8889. Không mở TCP 8080. Có thể giới hạn SSH theo IP quản trị nếu IP của bạn cố định.

## 2. Đóng gói trên Windows

CMD ở máy đang có source:

```cmd
cd /d D:\Tank_Game_tung
scripts\build-server.cmd
```

Chỉ tiếp tục khi BUILD SUCCESS. Đóng gói JAR, cấu hình và tài nguyên bản đồ, không kèm database/mật khẩu của máy cá nhân:

```cmd
if not exist runtime mkdir runtime
tar -czf runtime\tank-vps.tar.gz build/server/tank-server-1.0.0.jar config/server.properties source/client/Assets/StreamingAssets
scp runtime\tank-vps.tar.gz TAI_KHOAN_VPS@IP_VPS:/tmp/tank-vps.tar.gz
```

VPS mới sẽ có database mới. Nếu muốn chuyển dữ liệu cũ, dừng server cũ trước và sao lưu/di chuyển database riêng; không chép SQLite đang ghi như một file thông thường. Không cần upload project Unity, Launcher hoặc thư mục source server để chạy JAR.

## 3. Đăng nhập và cài Java

CMD Windows:

```cmd
ssh TAI_KHOAN_VPS@IP_VPS
```

Đối chiếu SSH host fingerprint với thông tin/console của nhà cung cấp khi kết nối lần đầu. Các lệnh sau đây chạy trên Ubuntu VPS:

```bash
sudo apt update
sudo apt install -y openjdk-21-jre-headless ufw
java -version
```

Kết quả Java phải là 21. Không cần Maven, Python hay Unity trên VPS vì đã build ở Windows.

## 4. Cài bộ server

Chạy một lần trên VPS mới:

```bash
sudo useradd --system --user-group --home-dir /opt/tank --shell /usr/sbin/nologin tank
sudo mkdir -p /opt/tank
sudo tar -xzf /tmp/tank-vps.tar.gz -C /opt/tank
sudo mkdir -p /opt/tank/runtime
sudo chown -R tank:tank /opt/tank
cd /opt/tank
cat config/server.properties
```

Kiểm tra cấu hình có:

```properties
bind=0.0.0.0
tcp.port=8888
udp.port=8889
admin.enabled=true
admin.bind=127.0.0.1
admin.port=8080
```

Giữ đường dẫn tương đối đã có: runtime cho dữ liệu, source/client/Assets/StreamingAssets/map.json cho bản đồ. Không đổi admin.bind thành IP public.

## 5. Tạo admin trên VPS

Chạy khi server chưa được mở:

```bash
cd /opt/tank
sudo -u tank /usr/bin/java -cp build/server/tank-server-1.0.0.jar tank.AdminSetup config/server.properties admin
```

Nhập và xác nhận mật khẩu ít nhất 12 ký tự; ký tự không hiển thị khi nhập. Đây là admin mới của VPS, độc lập với máy cũ. Không cần tạo lại sau mỗi lần restart.

## 6. Cho server tự chạy

Tạo service trên VPS:

```bash
sudo tee /etc/systemd/system/tank.service > /dev/null <<'EOF'
[Unit]
Description=Tank Battle server and administration web
Wants=network-online.target
After=network-online.target

[Service]
Type=simple
User=tank
Group=tank
WorkingDirectory=/opt/tank
ExecStart=/usr/bin/java -jar /opt/tank/build/server/tank-server-1.0.0.jar /opt/tank/config/server.properties
Restart=on-failure
RestartSec=5
UMask=0077
NoNewPrivileges=true

[Install]
WantedBy=multi-user.target
EOF
sudo systemctl daemon-reload
sudo systemctl enable --now tank
sudo systemctl status tank --no-pager
```

Phải thấy active (running). Nếu lỗi:

```bash
sudo journalctl -u tank -n 80 --no-pager
```

VPS vẫn chạy game sau khi đóng SSH hoặc tắt máy cá nhân. Restart=on-failure chỉ tự mở lại khi tiến trình lỗi; dừng chủ động bằng systemctl stop thì service không tự bật lại.

## 7. Mở firewall Ubuntu

Nếu SSH dùng cổng khác 22, thay 22 bằng cổng thực tế trước khi bật firewall. Trên VPS mới với cấu hình firewall mặc định:

```bash
sudo ufw allow 22/tcp
sudo ufw allow 8888/tcp
sudo ufw allow 8889/udp
sudo ufw enable
sudo ufw status
```

Nhớ mở game ở cả firewall của nhà cung cấp trong bước 1. Không cần mở 8080, 80 hoặc 443 cho phương án SSH này.

## 8. Mở web từ Windows

Mở một CMD mới, giữ cửa sổ này chạy:

```cmd
ssh -N -o ExitOnForwardFailure=yes -o ServerAliveInterval=30 -L 127.0.0.1:18080:127.0.0.1:8080 TAI_KHOAN_VPS@IP_VPS
```

Sau khi đăng nhập, không hiện dấu nhắc mới là bình thường: SSH đang chuyển tiếp cổng. Mở trình duyệt ở http://127.0.0.1:18080 và đăng nhập admin vừa tạo trên VPS.

Ở đây localhost:18080 chỉ là cửa vào trên máy bạn, chuyển tới localhost:8080 của VPS qua SSH; game và database vẫn nằm trên VPS. Đóng SSH sẽ mất đường vào web, nhưng game trên VPS vẫn chạy. Chỉ các máy quản trị cần đường hầm; máy chơi game không cần SSH.

## 9. Chơi qua Launcher

Mở Launcher như cũ, trong game điền:

- IP server: IPv4 public của VPS, không phải 127.0.0.1 và không nhập http/https.
- TCP: 8888.
- UDP: 8889.

Dùng client tương thích protocol 16/content 7 của server hiện tại. Vào game bằng hai tài khoản trên hai client để thử chơi; web quản trị sẽ thấy các kết nối tới VPS. Server mới không có tài khoản game cũ nếu chưa chuyển database, nên cần đăng ký lại hoặc chuyển dữ liệu trước. Thông báo vẫn dùng giao diện có sẵn trong client Launcher; hướng dẫn VPS này không thêm popup cho client.

## 10. Dùng từ thẻ nhớ

Tạo file MO_WEB_VPS.cmd trên thẻ nhớ bằng Notepad, chọn Save as type: All Files; thay tài khoản và IP:

```cmd
@echo off
echo Giu cua so nay mo. Sau khi dang nhap SSH, mo http://127.0.0.1:18080
ssh -N -o ExitOnForwardFailure=yes -o ServerAliveInterval=30 -L 127.0.0.1:18080:127.0.0.1:8080 TAI_KHOAN_VPS@IP_VPS
pause
```

Cắm vào máy Windows có Internet, trình duyệt và OpenSSH Client, mở file trên rồi mở địa chỉ web. Nếu Windows báo không nhận lệnh ssh, cài OpenSSH Client trong Optional Features của Windows. File không chứa mật khẩu; máy mới vẫn phải có quyền đăng nhập SSH hợp lệ. Launcher/game là phần riêng, vẫn mở theo cách nhóm đang dùng.

## Sau khi thử thành công

- Xem log: sudo journalctl -u tank -f (Ctrl+C chỉ thoát màn hình xem log).
- Dừng: sudo systemctl stop tank. Chạy lại: sudo systemctl start tank. Restart: sudo systemctl restart tank.
- Sao lưu trước khi nâng cấp: dừng service, sao lưu toàn bộ /opt/tank/runtime cùng cấu hình ra nơi khác, rồi start lại. Database không nằm trên thẻ nhớ.
- Khi update: dừng service trước khi thay JAR, giữ nguyên runtime/config, chép JAR và tài nguyên bản đồ cùng phiên bản, rồi start lại. Không thay JAR khi JVM đang chạy vì JVM có thể nạp class sau thời điểm khởi động.
- Muốn chỉ mở đường dẫn HTTPS mà không cần SSH: dùng tên miền, HTTPS reverse proxy và chỉnh phần kiểm tra Origin/cookie của AdminWeb trước. Code hiện tại so Origin với http://Host, nên đặt sau HTTPS nguyên trạng có thể bị 403 khi đăng nhập. Chưa triển khai public HTTPS chỉ bằng việc mở cổng 8080.

Tài liệu tham khảo: [Java 21 trên Ubuntu](https://packages.ubuntu.com/en/openjdk-21-jre-headless), [Ubuntu firewall](https://ubuntu.com/server/docs/how-to/security/firewalls/), [SSH forwarding](https://man.openbsd.org/ssh.1), [systemd service](https://www.freedesktop.org/software/systemd/man/latest/systemd.service.html), [Caddy HTTPS cho giai đoạn tên miền](https://caddyserver.com/docs/quick-starts/https).
