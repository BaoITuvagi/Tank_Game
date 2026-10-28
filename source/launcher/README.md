# Tank Battle Launcher

Launcher Windows x64 viết bằng C#/WinForms, tách khỏi client Unity. Người chơi tải launcher một lần; launcher tự tải và cài các bản client tiếp theo. Bản đầu cập nhật bằng **toàn bộ ZIP client**, chưa tải chênh lệch từng file.

## Build launcher

Cần .NET SDK 9.x. Build mặc định tải runtime pack từ NuGet lần đầu và tạo bản độc lập, người chơi **không cần cài .NET, Java hay Unity**.

Launcher là tùy chọn phát hành client, không cần để chạy website. Các ví dụ dùng CMD tại gốc repository trong source đã clone/giải nén. Trước tiên hoàn tất [hướng dẫn chạy server/web](../../README.md).

```cmd
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-launcher.ps1
```

Kết quả: `build/launcher/Launcher.exe`, `build/launcher/launcher.json` và `build/launcher-win64.zip` để gửi người chơi. Giải nén ZIP trước khi chạy. Giữ `launcher.json` cạnh file EXE. Cờ ExecutionPolicy chỉ áp dụng cho tiến trình lệnh này.

URL mặc định trong script trỏ tới repository game gốc bên dưới. Nếu phát hành từ repository của bạn, phải truyền -ManifestUrl tương ứng; clone source không tự tạo release hay cập nhật URL này:

```text
https://github.com/BaoITuvagi/Tank_Game/releases/latest/download/manifest.json
```

Nếu phát hành ở repository khác hoặc máy chủ HTTPS riêng:

```cmd
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-launcher.ps1 -ManifestUrl "https://example.com/tank/manifest.json"
```

Build thành công không đồng nghĩa đã có bản phát hành online. Trước khi có `manifest.json` ở URL cấu hình, launcher sẽ báo lỗi kiểm tra cập nhật; đây không phải lỗi cài game.

## Phát hành client lần đầu và các lần tiếp theo

1. Đồng bộ luật và build client (đóng Unity Editor đang mở cùng project):

   ```cmd
   python scripts/sync-contracts.py
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-client.ps1
   ```

2. Đóng gói **toàn bộ** `build/client`, chọn số phiên bản mới và URL đúng với tag định tạo:

   ```cmd
   python scripts/package-client.py --version 1.0.0 --base-url https://github.com/BaoITuvagi/Tank_Game/releases/download/v1.0.0
   ```

3. Tạo GitHub Release tag `v1.0.0`, đính kèm hai file trong `build/releases/1.0.0`: `TankBattle-1.0.0-win64.zip` và `manifest.json`. Lần đầu đính kèm thêm `build/launcher-win64.zip` để người chơi tải. Chọn release ổn định, đánh dấu là Latest; không dùng draft/prerelease cho kênh này. Repository/asset phải truy cập được công khai, không yêu cầu người chơi đăng nhập. Có thể dùng repository phát hành công khai riêng nếu cần giữ kín mã nguồn.

4. Kiểm tra URL manifest mặc định truy cập được. Người chơi mở `Launcher.exe`; launcher tải client vào `%LOCALAPPDATA%/TankBattle`, sau đó bấm **Chơi game**. IP/cổng game vẫn nhập trong Cài đặt như trước; nơi tải bản cập nhật độc lập với server TCP/UDP.

5. Bản sau dùng `1.0.1`, tag `v1.0.1` và base URL tương ứng. Không cần gửi launcher lại. Không sửa nội dung release đã phát hành; mỗi thay đổi phải tăng phiên bản. Nếu cần quay lại gameplay cũ, build nội dung cũ dưới một số phiên bản mới.

Nếu dùng máy chủ HTTPS riêng, tải ZIP lên trước, tải manifest lên sau cùng để người chơi không nhận thông tin về gói chưa tồn tại. Không đưa database, log, mã nguồn server hoặc khóa bí mật vào ZIP client. Script chỉ lấy nội dung thư mục client và bỏ debug symbol/log/thư mục backup Unity.

## Cách cài và khôi phục khi lỗi

- Xác minh dung lượng và SHA-256 trước khi giải nén. HTTPS bắt buộc cả khi chuyển hướng tải.
- Chặn đường dẫn thoát thư mục, đường dẫn Windows không hợp lệ, symlink, file trùng và ZIP quá lớn. Giới hạn ZIP 4 GiB, giải nén 8 GiB.
- Cài vào `versions/<version>-<sha256>`, sau đó thay `current.json`. Không ghi đè phiên bản đang chạy. Hủy tải/lỗi trước khi chuyển phiên bản giữ bản hiện tại.
- Giữ các phiên bản cũ; chưa tự dọn dẹp để tránh xóa game đang chạy. Khi cần giải phóng dung lượng, đóng game/launcher và xóa các thư mục phiên bản cũ **không được current.json tham chiếu**.
- Chỉ một launcher chạy trong phiên Windows; khóa file bảo vệ cài đặt đồng thời. Nút Chơi được bật nếu có bản đã cài, kể cả khi mất mạng cập nhật. Server game vẫn phải truy cập được để chơi online.
- Nếu metadata cài đặt hỏng, kiểm tra cập nhật để tạo lại. Nếu file game bên trong một phiên bản bị hỏng, phát hành bản mới hoặc đóng ứng dụng rồi gỡ thư mục bản hỏng và kiểm tra cập nhật lại.
- SHA-256 phát hiện gói sai/hỏng; manifest hiện được tin cậy qua HTTPS, chưa có chữ ký nhà phát hành hay cơ chế tự cập nhật launcher. Nếu đổi logic launcher trong tương lai, cần phát hành launcher mới.

## Tương thích server

Launcher này quản lý phát hành client, **chưa bổ sung bước server kiểm tra phiên bản phát hành**. Protocol hiện có kiểm tra phiên bản gói tin và hash map, nhưng chưa đủ để khẳng định hai bản có cùng luật gameplay. Client mở trực tiếp cũng có thể bỏ qua launcher.

Khi thay `source/shared/game-rules.json`, giao thức hoặc map, build và triển khai client/server cùng đợt. Trước khi mở rộng phát hành công khai, nên thêm kiểm tra compatibility ID tại server để từ chối client cũ rõ ràng. Không chuyển các luật dùng cho mô phỏng client sang chỉ sửa server.

Server Java tiếp tục dùng script hiện tại và database riêng; cập nhật launcher không thay server hoặc database.

## Kiểm thử

```cmd
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-launcher.ps1
python scripts/test-package-client.py
rem Khi da dong goi ban that: kiểm tra tải và cài cả bản Unity đó qua HTTP loopback.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-launcher.ps1 -ReleaseDirectory build/releases/1.0.0
```

Test C# dùng HTTP loopback thực, ZIP và thư mục cài đặt riêng trong `runtime/launcher-tests`; kiểm tra cài mới/cập nhật, lỗi SHA, sai dung lượng, ZIP không an toàn, hủy, cài đồng thời và giữ bản cũ. Không mở game giả trong fixture.

Có thể thử launcher với kho cập nhật localhost (không dùng chế độ này khi phát hành):

```cmd
python scripts/package-client.py --version 1.0.0 --base-url http://127.0.0.1:8090 --allow-loopback-http
python -m http.server 8090 --bind 127.0.0.1 --directory build/releases/1.0.0
rem Trong mot cua so CMD khac:
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-launcher.ps1 -ManifestUrl http://127.0.0.1:8090/manifest.json -AllowLoopbackHttp
```

Nếu chỉ debug trên máy đã có .NET Desktop Runtime 9, dùng `-FrameworkDependent`. Kết quả riêng ở `build/launcher-framework-dependent`; không gửi bản này cho người chơi chưa cài runtime.
