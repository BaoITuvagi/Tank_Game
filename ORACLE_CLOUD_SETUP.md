# Chạy Tank Battle trên Oracle Cloud Free Tier

Hướng dẫn dành cho Oracle Cloud Infrastructure (OCI) Always Free. Không cần thẻ tín dụng trả phí; tài khoản Free Tier cấp VM miễn phí vĩnh viễn. Người chơi vẫn dùng Launcher. Các bước cài đặt bên trong VM giống [VPS_SETUP.md](VPS_SETUP.md) từ bước 3.

Tài nguyên Always Free cho VM:

| Tài nguyên | Miễn phí vĩnh viễn |
|---|---|
| VM ARM (Ampere A1) | Tổng 4 OCPU + 24 GB RAM (chia được nhiều VM) |
| VM AMD (E2.1.Micro) | 1 VM, 1 OCPU, 1 GB RAM |
| Boot volume | 200 GB tổng |
| Outbound data | 10 TB/tháng |

Dùng VM ARM Ampere A1 vì mạnh hơn (có thể cấu hình 2 OCPU + 12 GB RAM cho game), hoặc VM AMD E2.1.Micro nếu muốn đơn giản. Server Java chạy được cả ARM lẫn x64; JAR là cross-platform. Bản hướng dẫn này dùng ARM A1 với Ubuntu 24.04.

Các lệnh CMD chạy trên Windows; các lệnh bash chạy sau khi SSH vào VM. Oracle Cloud tạo SSH key pair khi tạo VM; lưu file private key an toàn, không đưa lên GitHub.

## 1. Đăng ký Oracle Cloud Free Tier

Vào [cloud.oracle.com/free](https://www.oracle.com/cloud/free/) → Start for Free.

- Chọn Home Region gần người chơi: **Singapore**, **Seoul**, **Tokyo** hoặc **Osaka**. Không thể đổi Home Region sau khi tạo tài khoản. Always Free chỉ khả dụng ở Home Region.
- Điền thông tin, xác minh email.
- Nhập thẻ (Visa/Mastercard) để xác minh danh tính; Oracle không tính phí và không tự nâng cấp trả phí trừ khi bạn chủ động chuyển. Tài khoản Free Tier giới hạn ở Always Free; không thể vô tình vượt hạn mức.

Sau khi tạo xong, đăng nhập tại [cloud.oracle.com](https://cloud.oracle.com).

## 2. Tạo Virtual Machine

### 2.1. Tạo VM

Vào menu ☰ → Compute → Instances → Create instance.

| Mục | Giá trị |
|---|---|
| Name | `tank-server` |
| Compartment | để mặc định (root) |
| Placement → Availability domain | để mặc định |

**Image and shape:**

- Bấm **Edit** → Change image → chọn **Ubuntu** → **Canonical Ubuntu 24.04** (aarch64 cho ARM, x86_64 cho AMD). Bấm Select image.
- Bấm **Change shape** → **Ampere** (ARM) → **VM.Standard.A1.Flex**:
  - Number of OCPUs: **2** (hoặc 1 nếu muốn dành cho VM khác)
  - Memory: **12** GB (hoặc 6 nếu dùng 1 OCPU)
  - Kiểm tra dòng "Always Free eligible"

Hoặc chọn AMD: **VM.Standard.E2.1.Micro** (1 OCPU, 1 GB RAM, Always Free).

**Networking:**

- Virtual cloud network: tạo mới hoặc chọn có sẵn
- Subnet: public subnet
- Public IPv4 address: **Assign a public IPv4 address**

**Add SSH keys:**

- Chọn **Generate a key pair** → **Save Private Key** (lưu file `.key`) và **Save Public Key**
- Hoặc chọn **Upload public key files** nếu đã có SSH key

Bấm **Create**. Đợi VM chuyển trạng thái RUNNING (1-2 phút).

### 2.2. Lấy thông tin kết nối

Vào trang Instance vừa tạo → ghi lại **Public IP Address** (ví dụ `152.xxx.xxx.xxx`). Username mặc định cho Ubuntu là `ubuntu`.

## 3. Mở cổng trong Security List

Oracle Cloud mặc định chặn mọi cổng trừ SSH (22). Cần mở TCP 8888 và UDP 8889.

Vào menu ☰ → Networking → Virtual Cloud Networks → chọn VCN của VM → chọn Public Subnet → chọn **Default Security List** → Add Ingress Rules:

**Rule 1 — Game TCP:**

| Mục | Giá trị |
|---|---|
| Source Type | CIDR |
| Source CIDR | `0.0.0.0/0` |
| IP Protocol | TCP |
| Destination Port Range | `8888` |

**Rule 2 — Game UDP:**

| Mục | Giá trị |
|---|---|
| Source Type | CIDR |
| Source CIDR | `0.0.0.0/0` |
| IP Protocol | UDP |
| Destination Port Range | `8889` |

Bấm Add Ingress Rules. Không mở cổng 8080.

## 4. Đóng gói trên Windows

Giống [VPS_SETUP.md bước 2](VPS_SETUP.md#2-đóng-gói-trên-windows). CMD ở máy đang có source:

```cmd
cd /d D:\Tank_Game_tung
scripts\build-server.cmd
```

Chỉ tiếp tục khi BUILD SUCCESS:

```cmd
if not exist runtime mkdir runtime
tar -czf runtime\tank-vps.tar.gz build/server/tank-server-1.0.0.jar config/server.properties source/client/Assets/StreamingAssets
scp -i "duong-dan\ssh-key.key" runtime\tank-vps.tar.gz ubuntu@IP_VM:/tmp/tank-vps.tar.gz
```

Thay `duong-dan\ssh-key.key` bằng đường dẫn file private key đã tải ở bước 2. Thay `IP_VM` bằng Public IP của VM. Username Oracle Cloud Ubuntu mặc định là `ubuntu`, không phải `azureuser`.

Nếu SSH báo lỗi quyền file key trên Windows, sửa trong PowerShell:

```powershell
icacls "duong-dan\ssh-key.key" /inheritance:r /grant:r "$($env:USERNAME):(R)"
```

## 5. Đăng nhập và cài Java

CMD Windows:

```cmd
ssh -i "duong-dan\ssh-key.key" ubuntu@IP_VM
```

Các lệnh sau chạy trên Ubuntu VM:

```bash
sudo apt update
sudo apt install -y openjdk-21-jre-headless
java -version
```

Kết quả Java phải là 21. Cài JRE aarch64 (ARM) hoặc amd64 tùy shape đã chọn; `apt` tự chọn đúng gói.

## 6. Cài bộ server

Giống [VPS_SETUP.md bước 4](VPS_SETUP.md#4-cài-bộ-server):

```bash
sudo useradd --system --user-group --home-dir /opt/tank --shell /usr/sbin/nologin tank
sudo mkdir -p /opt/tank
sudo tar -xzf /tmp/tank-vps.tar.gz -C /opt/tank
sudo mkdir -p /opt/tank/runtime
sudo chown -R tank:tank /opt/tank
cd /opt/tank
cat config/server.properties
```

Kiểm tra cấu hình có `admin.bind=127.0.0.1`, `tcp.port=8888`, `udp.port=8889`. Không đổi `admin.bind` thành IP public.

## 7. Tạo admin trên VM

Chạy khi server chưa được mở:

```bash
cd /opt/tank
sudo -u tank /usr/bin/java -cp build/server/tank-server-1.0.0.jar tank.AdminSetup config/server.properties admin
```

Nhập và xác nhận mật khẩu ít nhất 12 ký tự.

## 8. Cho server tự chạy

Tạo service giống [VPS_SETUP.md bước 6](VPS_SETUP.md#6-cho-server-tự-chạy):

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

Phải thấy active (running). Nếu lỗi: `sudo journalctl -u tank -n 80 --no-pager`.

## 9. Firewall bên trong VM

Oracle Cloud Security List đã mở cổng ở bước 3. Bật iptables/ufw bên trong VM:

```bash
# Oracle Ubuntu dùng iptables mặc định, cần mở cổng game:
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 8888 -j ACCEPT
sudo iptables -I INPUT 7 -m state --state NEW -p udp --dport 8889 -j ACCEPT
sudo netfilter-persistent save
```

Nếu VM không có `netfilter-persistent`:

```bash
sudo apt install -y iptables-persistent
sudo netfilter-persistent save
```

Cần mở cổng ở cả Security List (Oracle Console) và iptables (bên trong VM).

## 10. Mở web quản trị từ Windows

Mở CMD mới, giữ cửa sổ này chạy:

```cmd
ssh -N -o ExitOnForwardFailure=yes -o ServerAliveInterval=30 -i "duong-dan\ssh-key.key" -L 127.0.0.1:18080:127.0.0.1:8080 ubuntu@IP_VM
```

Mở trình duyệt: http://127.0.0.1:18080 và đăng nhập admin vừa tạo.

## 11. Chơi qua Launcher

Trong game điền:

- IP server: Public IP của Oracle VM, không nhập http/https.
- TCP: 8888.
- UDP: 8889.

## So sánh Oracle Cloud Free Tier với các phương án khác

| Điểm | Oracle Free Tier | Azure Student | VPS trả phí |
|---|---|---|---|
| Chi phí | **Miễn phí vĩnh viễn** | $100 credit / 12 tháng | Trả hàng tháng |
| Cấu hình | Lên tới 4 OCPU + 24 GB RAM | B1s: 1 vCPU + 1 GB (nếu có) | Tùy gói |
| TCP/UDP | Hỗ trợ đầy đủ | Hỗ trợ đầy đủ | Hỗ trợ đầy đủ |
| Kiến trúc | ARM (Ampere) hoặc AMD | x64 | x64 |
| Public IP | Có sẵn, miễn phí | Tính phí Static IP | Thường có sẵn |
| Bandwidth | 10 TB/tháng miễn phí | Tính phí outbound | Tùy gói |
| Rủi ro | Không hết credit | Hết $100 = dừng | Không trả = dừng |

## Sau khi thử thành công

- Xem log: `sudo journalctl -u tank -f` (Ctrl+C thoát xem log).
- Dừng: `sudo systemctl stop tank`. Chạy lại: `sudo systemctl start tank`.
- Sao lưu: dừng service, sao lưu `/opt/tank/runtime`, rồi start lại.
- Update: dừng service, thay JAR và tài nguyên, start lại.
- Oracle Free Tier VM chạy 24/7 không tính phí. Không cần auto-shutdown để tiết kiệm.
- HTTPS: dùng tên miền, Caddy reverse proxy và sửa kiểm tra Origin/cookie trong AdminWeb; xem ghi chú cuối [VPS_SETUP.md](VPS_SETUP.md).

Tài liệu tham khảo: [Oracle Cloud Free Tier](https://www.oracle.com/cloud/free/), [Always Free resources](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm), [OCI Security Lists](https://docs.oracle.com/en-us/iaas/Content/Network/Concepts/securitylists.htm), [VPS_SETUP.md](VPS_SETUP.md).
