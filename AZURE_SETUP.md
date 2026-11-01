# Chạy Tank Battle và web trên Azure (Student)

Hướng dẫn dành cho tài khoản Azure for Students ($100 credit, 365 ngày). Sử dụng Azure Virtual Machine B1s (miễn phí 750 giờ/tháng) chạy Ubuntu 24.04 LTS x86_64. Người chơi vẫn dùng Launcher. Các bước cài đặt bên trong VM giống [VPS_SETUP.md](VPS_SETUP.md) từ bước 3.

App Service và các dịch vụ web-only của Azure không chạy được server game này vì chỉ hỗ trợ HTTP/HTTPS, không hỗ trợ TCP/UDP socket trực tiếp (TCP 8888, UDP 8889).

Các lệnh CMD chạy trên Windows; các lệnh bash chạy sau khi SSH vào VM. Nếu dùng SSH key, Azure tạo file `.pem` khi tạo VM; lưu file này an toàn và không đưa lên GitHub.

## 1. Tạo Virtual Machine trên Azure Portal

Đăng nhập [portal.azure.com](https://portal.azure.com) bằng tài khoản student. Tìm Virtual machines → Create → Azure virtual machine.

| Mục | Giá trị |
|---|---|
| Subscription | Azure for Students |
| Resource group | Tạo mới: `TankBattle-RG` |
| Virtual machine name | `tank-server` |
| Region | Southeast Asia (Singapore) hoặc East Asia (Hong Kong), chọn nơi gần người chơi |
| Availability options | No infrastructure redundancy required |
| Security type | Standard |
| Image | Ubuntu Server 24.04 LTS - x64 Gen2 |
| Size | **Standard_B1s** (1 vCPU, 1 GiB RAM) — kiểm tra dòng "Eligible for free services" |
| Authentication type | SSH public key |
| Username | `azureuser` |

Chọn sai size sẽ mất phí ngay. B1s được miễn phí 750 giờ/tháng trong 12 tháng đầu.

Tab Disks: Standard SSD, 64 GB.

Tab Networking: tạo Virtual network mới, Public IP chọn **Static**, NIC network security group chọn **Advanced** → tạo NSG mới với các inbound rule:

| Priority | Name | Port | Protocol | Action |
|---|---|---|---|---|
| 300 | SSH | 22 | TCP | Allow |
| 310 | Game-TCP | 8888 | TCP | Allow |
| 320 | Game-UDP | 8889 | UDP | Allow |

Không mở cổng 8080 trong NSG. Web quản trị chỉ qua SSH tunnel.

Review + Create → Create. Tải file SSH key `.pem` khi được nhắc.

Sau khi VM tạo xong, vào trang VM → Overview, ghi lại Public IP address. Tùy chọn: vào phần DNS name, nhập label (ví dụ `tankbattle`) để có địa chỉ `tankbattle.southeastasia.cloudapp.azure.com`; người chơi có thể dùng tên này thay IP.

## 2. Đóng gói trên Windows

Giống [VPS_SETUP.md bước 2](VPS_SETUP.md#2-đóng-gói-trên-windows). CMD ở máy đang có source:

```cmd
cd /d D:\Tank_Game_tung
scripts\build-server.cmd
```

Chỉ tiếp tục khi BUILD SUCCESS:

```cmd
if not exist runtime mkdir runtime
tar -czf runtime\tank-vps.tar.gz build/server/tank-server-1.0.0.jar config/server.properties source/client/Assets/StreamingAssets
scp -i "duong-dan\tank-server_key.pem" runtime\tank-vps.tar.gz azureuser@IP_VM:/tmp/tank-vps.tar.gz
```

Thay `duong-dan\tank-server_key.pem` bằng đường dẫn file `.pem` đã tải ở bước 1. Thay `IP_VM` bằng Public IP của VM.

Nếu SSH báo lỗi quyền file `.pem` trên Windows, sửa trong PowerShell:

```powershell
icacls "duong-dan\tank-server_key.pem" /inheritance:r /grant:r "$($env:USERNAME):(R)"
```

## 3. Đăng nhập và cài Java

CMD Windows:

```cmd
ssh -i "duong-dan\tank-server_key.pem" azureuser@IP_VM
```

Các lệnh sau chạy trên Ubuntu VM:

```bash
sudo apt update
sudo apt install -y openjdk-21-jre-headless ufw
java -version
```

Kết quả Java phải là 21. Không cần Maven, Python hay Unity trên VM.

## 4. Cài bộ server

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

## 5. Tạo admin trên VM

Chạy khi server chưa được mở:

```bash
cd /opt/tank
sudo -u tank /usr/bin/java -cp build/server/tank-server-1.0.0.jar tank.AdminSetup config/server.properties admin
```

Nhập và xác nhận mật khẩu ít nhất 12 ký tự. Admin này độc lập với máy cá nhân.

## 6. Cho server tự chạy

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

## 7. Firewall bên trong VM

Azure NSG đã mở cổng ở bước 1, nhưng vẫn bật ufw bên trong VM:

```bash
sudo ufw allow 22/tcp
sudo ufw allow 8888/tcp
sudo ufw allow 8889/udp
sudo ufw enable
sudo ufw status
```

Cần mở cổng ở cả NSG (Azure Portal) và ufw (bên trong VM).

## 8. Mở web quản trị từ Windows

Mở CMD mới, giữ cửa sổ này chạy:

```cmd
ssh -N -o ExitOnForwardFailure=yes -o ServerAliveInterval=30 -i "duong-dan\tank-server_key.pem" -L 127.0.0.1:18080:127.0.0.1:8080 azureuser@IP_VM
```

Mở trình duyệt: http://127.0.0.1:18080 và đăng nhập admin vừa tạo.

## 9. Chơi qua Launcher

Trong game điền:

- IP server: Public IP của Azure VM hoặc DNS name (ví dụ `tankbattle.southeastasia.cloudapp.azure.com`), không nhập http/https.
- TCP: 8888.
- UDP: 8889.

## 10. Tiết kiệm credit Azure

Tổng chi phí ước tính ~$9/tháng (disk + IP), đủ chạy ~10 tháng với $100.

**Auto-shutdown:** Vào VM → Auto-shutdown → bật, đặt giờ tắt hàng ngày (ví dụ 23:00). VM tắt không tính phí compute.

**Deallocate khi không dùng:** Stop VM từ Azure Portal (nút Stop) hoặc CLI sẽ deallocate và ngừng tính phí compute. Không dùng `sudo shutdown` từ bên trong VM vì Azure vẫn giữ trạng thái Stopped và tính phí.

```cmd
rem Tắt và giải phóng (không tính phí compute):
az vm deallocate --resource-group TankBattle-RG --name tank-server
rem Bật lại:
az vm start --resource-group TankBattle-RG --name tank-server
```

**Theo dõi chi phí:** Vào Cost Management + Billing → Cost analysis. Đặt Budget alert tại $80 để nhận cảnh báo.

**Dynamic IP:** Nếu chấp nhận IP thay đổi khi deallocate/start, chọn Dynamic thay Static để tiết kiệm ~$3.65/tháng. Dùng DNS name Azure để người chơi không cần nhập lại IP.

## Sau khi thử thành công

- Xem log: `sudo journalctl -u tank -f` (Ctrl+C thoát xem log).
- Dừng: `sudo systemctl stop tank`. Chạy lại: `sudo systemctl start tank`.
- Sao lưu: dừng service, sao lưu `/opt/tank/runtime`, rồi start lại.
- Update: dừng service, thay JAR và tài nguyên, start lại. Không thay JAR khi JVM đang chạy.
- HTTPS: dùng tên miền, Caddy reverse proxy và sửa kiểm tra Origin/cookie trong AdminWeb; xem ghi chú cuối [VPS_SETUP.md](VPS_SETUP.md).

Tài liệu tham khảo: [Azure for Students](https://azure.microsoft.com/en-us/free/students/), [Azure VM B1s free tier](https://azure.microsoft.com/en-us/pricing/details/virtual-machines/linux/), [Azure NSG](https://learn.microsoft.com/en-us/azure/virtual-network/network-security-groups-overview), [VPS_SETUP.md](VPS_SETUP.md).
