# Sổ tay vận hành Server Tank Battle trên Azure

Tài liệu tóm tắt ngắn gọn các lệnh và thao tác thực tế hàng ngày cho server game đã deploy trên Azure.

---

## 1. Thông tin máy chủ của bạn

- **Public IP:** `13.75.2.41`
- **User:** `azureuser`
- **File SSH Key (Windows):** `%USERPROFILE%\.ssh\tank-server_key.pem`
- **Cổng Game TCP:** `8888`
- **Cổng Game UDP:** `8889`
- **Web Quản trị nội bộ:** `8080`

---

## 2. Vào và Thoát máy chủ từ Windows

### Đăng nhập SSH vào máy chủ
Mở CMD hoặc PowerShell trên Windows:
```cmd
ssh -i "%USERPROFILE%\.ssh\tank-server_key.pem" azureuser@13.75.2.41
```

### Thoát khỏi máy chủ
Trong terminal của máy ảo, gõ:
```bash
exit
```
*(Hoặc phím tắt `Ctrl + D`. Server game vẫn chạy ngầm 24/7 sau khi thoát).*

---

## 3. Mở Website Quản trị (Admin Web)

Do web quản trị được bảo mật nội bộ, bạn dùng đường hầm SSH (SSH Tunnel) để truy cập từ trình duyệt máy tính cá nhân.

1. Mở một cửa sổ CMD trên Windows, chạy lệnh sau và **giữ nguyên cửa sổ đó**:
```cmd
ssh -N -L 127.0.0.1:18080:127.0.0.1:8080 -i "%USERPROFILE%\.ssh\tank-server_key.pem" azureuser@13.75.2.41
```
*(Cửa sổ không hiện dấu nhắc mới là bình thường, nghĩa là đường hầm đang thông).*

2. Mở trình duyệt web (Chrome, Edge...) truy cập:
👉 **http://127.0.0.1:18080**

3. Đăng nhập bằng tài khoản `admin` và mật khẩu bạn đã tạo.

---

## 4. Cho người chơi kết nối vào Game

Người chơi mở game bằng **Launcher/Client**, vào phần Cài đặt mạng điền:
- **Địa chỉ IP Server:** `13.75.2.41`
- **Cổng TCP:** `8888`
- **Cổng UDP:** `8889`

---

## 5. Các lệnh quản lý Server thông dụng (Sau khi SSH vào máy ảo)

```bash
# Xem trạng thái server đang chạy hay dừng:
sudo systemctl status tank --no-pager

# Xem log trực tiếp thời gian thực (xem ai kết nối, lỗi nếu có):
sudo journalctl -u tank -f
# (Nhấn Ctrl + C để thoát khỏi màn hình xem log)

# Khởi động lại server:
sudo systemctl restart tank

# Dừng server tạm thời:
sudo systemctl stop tank

# Bật lại server:
sudo systemctl start tank
```

---

## 6. Tiết kiệm Credit Azure (Quan trọng)

Gói Azure Student có $100 credit. Máy ảo `B2ats_v2` tốn ~$9/tháng.

### Khi không chơi hoặc không cần test:
1. Vào [Azure Portal](https://portal.azure.com) → vào máy ảo `tank-server`.
2. Bấm nút **Stop** ở thanh công cụ phía trên.
3. Chờ trạng thái chuyển sang **`Stopped (deallocated)`**.
   *(Lúc này tiền tính cho CPU & RAM sẽ về 0đ. Chỉ tốn phí lưu trữ ổ đĩa rất nhỏ).*

### Khi cần chơi lại:
- Vào Azure Portal bấm nút **Start**.
- Chờ khoảng 30s là server game tự động chạy lại (vì đã cài systemd service tự khởi động).

### Khi kết thúc môn học (Xóa hoàn toàn):
- Vào Azure Portal → **Resource groups** → chọn `TankBattle` → bấm **Delete resource group**.
