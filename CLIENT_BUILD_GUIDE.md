# Hướng dẫn Build Client Tank Battle và Tùy biến Popup Thông báo Admin

Tài liệu này dành cho thành viên phụ trách Client (Unity). Hướng dẫn cách chỉnh sửa giao diện hộp thoại nổi (Popup thông báo từ Web Admin) và quy trình build ra file chạy `.exe` mới cho người chơi.

---

## 1. Cơ chế hoạt động của Popup Admin

1. Quản trị viên gửi thông báo từ **Admin Web** (qua API `/api/notice`).
2. Server gửi gói tin mạng `NOTICE` (Opcode 70) xuống Client người chơi gồm:
   - `Code`: `200` (Thông tin - Info), `201` (Cảnh báo - Warning), `202` (Lỗi - Danger).
   - `Text`: Nội dung thông báo.
3. Client bắt sự kiện tại `AdminNoticeChannel.Publish(...)` và chuyển tới component `AdminNoticeOverlay`.
4. `AdminNoticeOverlay` tự động vẽ một hộp thoại nổi (Modal Popup) đè lên màn hình (kể cả khi đang ở Sảnh hay đang trong Trận đấu).

---

## 2. Nơi chỉnh sửa giao diện Popup

File mã nguồn điều khiển toàn bộ giao diện và hành vi của Popup:
👉 **`source/client/Assets/Scripts/AdminNoticeOverlay.cs`**

Bảng màu và phong cách đồ họa (Cartoon UI Palette) dùng chung của game:
👉 **`source/client/Assets/Scripts/CartoonUi.cs`**

### Các mục bạn có thể tùy biến trong `AdminNoticeOverlay.cs`:

#### A. Kích thước và vị trí hộp thoại (Dòng 56 - 57)
```csharp
// Chiều rộng tối đa 640px, chiều cao 400px, tự co giãn theo màn hình:
float width = Mathf.Min(640, canvasWidth - 32), height = Mathf.Min(400, canvasHeight - 32);
Rect box = new Rect((canvasWidth - width) / 2, (canvasHeight - height) / 2, width, height);
```

#### B. Màu sắc viền & màu chủ đề theo loại tin (Dòng 58)
```csharp
// 202: Lỗi (Đỏ/Danger), 201: Cảnh báo (Vàng/Gold), 200: Thông tin (Xanh/Ready)
Color accent = notice.Code == 202 ? CartoonUi.Danger : notice.Code == 201 ? CartoonUi.Gold : CartoonUi.Ready;
```
*Các màu có sẵn trong `CartoonUi`: `CartoonUi.Gold`, `CartoonUi.Danger`, `CartoonUi.Ready`, `CartoonUi.PanelDark`, `CartoonUi.Cream`...*

#### C. Tiêu đề và nội dung (Dòng 65 - 70)
```csharp
string title = notice.Code == 202 ? "Lỗi từ quản trị viên" 
             : notice.Code == 201 ? "Cảnh báo từ quản trị viên" 
             : "Thông báo từ quản trị viên";
```

#### D. Nút đóng hoặc chuyển tin tiếp theo (Dòng 80 - 82)
```csharp
if (GUI.Button(new Rect(box.xMax - 160, box.yMax - 52, 136, 36), notices.PendingCount > 0 ? "Tin tiếp theo" : "Đóng", theme.Primary)) {
    notices.Dismiss();
    scroll = Vector2.zero;
}
```

---

## 3. Môi trường cần chuẩn bị để Build

- **Unity Editor:** Đúng phiên bản **`6000.3.24f1`** (Unity 6).
- **Module:** Đã cài **Windows Build Support (IL2CPP hoặc Mono)** trong Unity Hub.
- **License:** Unity Personal/Student đã kích hoạt.

---

## 4. Cách Build Client ra file `.exe`

### Cách 1: Build bằng dòng lệnh tự động (Khuyên dùng - Nhanh nhất)
1. Đóng Unity Editor (nếu đang mở project).
2. Mở CMD hoặc PowerShell tại thư mục gốc của repository:
```cmd
scripts\build-client.cmd
```
*(Hoặc trong PowerShell: `./scripts/build-client.ps1`)*

Script sẽ tự động tìm Unity Editor, nạp scene và build file chạy hoàn chỉnh vào:
👉 **`build/client/TankBattle.exe`** cùng toàn bộ thư mục dữ liệu đi kèm.

---

### Cách 2: Mở và Build thủ công trong Unity Editor
1. Mở **Unity Hub** → Bấm **Add** → Chọn thư mục **`source/client`**.
2. Mở project bằng Unity Editor `6000.3.24f1`.
3. Mở scene chính: `Assets/Scenes/Main.unity`.
4. Trên thanh menu Unity, vào menu:
   - **File** → **Build Settings...**
   - Đảm bảo Platform đang chọn là **Windows, Mac, Linux** (Target Platform: **Windows**, Architecture: **x86_64**).
   - Bấm **Build** → Chọn thư mục xuất ra (ví dụ: `build/client/`).
*(Hoặc dùng menu script có sẵn: gọi `BuildClient.Build()` qua Editor).*

---

## 5. Phân phối cho người chơi

Sau khi build xong, **phải gửi trọn bộ thư mục `build/client/`** (bao gồm file `.exe`, thư mục `TankBattle_Data/`, `UnityPlayer.dll`...), không được chỉ gửi riêng mỗi file `.exe`.

Người chơi chỉ cần mở `TankBattle.exe`, vào phần Cài đặt mạng điền:
- **Server IP:** `13.75.2.41` (IP server Azure của nhóm)
- **TCP:** `8888`
- **UDP:** `8889`

Khi có thông báo từ Admin Web, hộp thoại nổi mới sẽ lập tức xuất hiện ngay trên màn hình game!
