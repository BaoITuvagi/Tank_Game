# Tank Battle Launcher

Ứng dụng Windows x64 dùng WinForms/.NET 9. Người chơi tải **`TankBattle-launcher-win64.zip`** từ [Releases](https://github.com/BaoITuvagi/Tank_Game/releases/latest), giải nén và mở **`TankBattle.exe`** trong thư mục có quyền ghi. Cũng có thể tải trực tiếp EXE; không cần tạo cấu hình hoặc cài .NET. Ứng dụng tự cập nhật trước khi kiểm tra game; game đã cài trong `%LocalAppData%\TankBattle` vẫn dùng được.

Tên `Launcher.exe` cũ vẫn được hỗ trợ để luồng tự cập nhật và shortcut hiện có tiếp tục hoạt động. Nếu có `launcher.json` riêng bên cạnh EXE, ứng dụng ưu tiên cấu hình đó; nếu không có, nó dùng cấu hình HTTPS được nhúng khi build.

## Giao diện

Ứng dụng **1.2.2** có tên cửa sổ **Tank Battle**, giữ giao diện olive/kem, khung cắt góc và nút **CHƠI GAME** vàng. Logo, nền chiến trường và font Bungee/Nunito được nhúng trực tiếp từ `Assets/Resources/MenuArt` của client vào EXE, không cần thư mục ảnh/font bên cạnh. Hai giấy phép OFL và `NOTICE.txt` cũng được nhúng trong EXE. Không thay ảnh nguồn, importer hay `.meta` của Unity.

Cửa sổ dùng thanh tiêu đề Windows chuẩn như cửa sổ game, có đủ nút thu nhỏ, phóng to/khôi phục và đóng; màu thanh tiêu đề theo thiết lập Windows. Vùng nội dung mặc định 1120×660 ở 100% DPI, tối thiểu 900×600, hỗ trợ kéo cạnh, kéo thanh tiêu đề, nhấp đúp và điều khiển nút bằng Tab/Enter/Space. Logo được giữ đúng tỷ lệ, chỉ bỏ vùng trong suốt bao quanh; hai dòng tiêu đề đo theo đường viền ký tự thật, gồm cả dấu tiếng Việt, rồi xếp tuần tự với khoảng cách riêng để không đè lên logo hoặc nhau khi đổi kích thước. Khối logo, tiêu đề và lời giới thiệu căn trái, cân giữa theo chiều dọc trong vùng ảnh với khoảng đệm trên/dưới; không còn dải ba nhãn chế độ. Footer hiển thị phiên bản game thực tế đã cài và phiên bản launcher riêng.

Vùng trạng thái hiển thị kiểm tra/cập nhật launcher, kiểm tra/tải/cài game, phần trăm tiến độ, hủy và lỗi. Chỉ cho chơi khi có bản game hợp lệ và không đang cập nhật; lỗi hoặc hủy trả lại khả năng chơi bản đã cài. Khi bàn giao cho helper, nút hủy bị khóa. Nội dung thông báo xuống dòng và vùng thao tác tự tăng chiều cao; thông báo quá dài được rút gọn trên màn hình, nội dung đầy đủ vẫn có trong mô tả accessibility.

Nút chơi là hành động chính màu vàng, icon play và chữ Bungee căn giữa thành một cụm; nút kiểm tra cập nhật/hủy là hành động phụ nền olive. Cả hai cấp nút dùng viền mực, bóng nhẹ, phản hồi hover/nhấn, trạng thái khóa và viền focus bàn phím; icon vẽ vector thay ký tự của font hệ điều hành. Khi đang cập nhật, nút chơi hiện icon tải xuống cùng nhãn khóa.

Người dùng Launcher 1.2.0/1.2.1 nhận bản mới qua kênh cập nhật, giữ tên file và cấu hình đang dùng. Người tải mới sử dụng `TankBattle.exe`.

## Build và phát hành

Chạy PowerShell tại repo root, cần .NET SDK có thể build `net9.0-windows`:

```powershell
./scripts/build-launcher.ps1 -ReleaseBaseUrl 'https://github.com/BaoITuvagi/Tank_Game/releases/download/launcher-v1.2.2'
```

Version mặc định lấy từ project (`1.2.2`); có thể truyền `-LauncherVersion` để chỉ định version phát hành. Giá trị này được dùng cho assembly/file version, User-Agent và manifest. Mỗi thay đổi đã phát hành cần số phiên bản lớn hơn; không thay asset cùng version để ép cập nhật. `ManifestUrl` và `LauncherManifestUrl` được nhúng vào EXE theo tham số build.

Output:

| File | Mục đích |
|---|---|
| `build/TankBattle-launcher-win64.zip` | Gói tải thủ công cho người chơi, chỉ chứa `TankBattle.exe`. |
| `build/launcher/TankBattle.exe` | Có thể chạy trực tiếp, đồng thời là asset của bộ tự cập nhật. |
| `build/launcher/launcher-manifest.json` | Manifest riêng cho launcher: `schemaVersion`, `version`, `packageUrl`, `sha256`, `size`. Giới hạn EXE 512 MiB. |

Release **latest** gồm bốn asset: `TankBattle-launcher-win64.zip`, `TankBattle.exe`, `launcher-manifest.json` và `manifest.json` của game. Ưu tiên `ReleaseBaseUrl` trỏ tag cố định. Khi game không đổi, giữ `packageUrl` của `manifest.json` trỏ ZIP ở release game cũ; không upload thêm ZIP game trùng. Không xóa ZIP ở URL mà manifest vẫn dùng. Hai gói “Source code” do GitHub tự sinh, không phải asset do script upload. Không đưa output vào Git; build local không tự phát hành.

`-FrameworkDependent` chỉ dành cho phát triển, yêu cầu .NET 9 Desktop Runtime, có output/ZIP riêng và không tạo manifest cho kênh tự cập nhật. Không trộn bản này vào asset phát hành standalone.

## Cấu hình và hành vi

`launcher.json` là cấu hình tùy chọn, giữ `manifestUrl` của game và `launcherManifestUrl`. Khi thiếu trường thứ hai, ứng dụng lấy `launcher-manifest.json` cùng thư mục URL với manifest game. File tùy chỉnh sai định dạng được báo lỗi, không bị âm thầm thay bằng mặc định. Chỉ dùng HTTPS; `allowLoopbackHttp: true`/`-AllowLoopbackHttp` chỉ cho HTTP loopback khi kiểm thử. Kiểm tra mọi redirect, dung lượng và SHA-256 trước khi dùng file tải xuống.

Khi mở hoặc bấm **Kiểm tra cập nhật**, launcher so phiên bản theo ba thành phần số. Nếu có bản mới, giao diện hiện tiến độ, tự đóng và mở lại rồi tiếp tục kiểm tra game. Có thể hủy khi đang tải; giai đoạn bàn giao cho helper không cho hủy. Bản chạy trực tiếp từ `dotnet run`/build không bundle không dùng làm helper; dùng script publish single-file để kiểm thử luồng đầy đủ.

Helper là bản sao launcher hiện tại, chạy ẩn từ `%LocalAppData%\TankBattle\launcher-updates\<hash đường dẫn launcher>`. Nó đợi launcher gốc thoát, dùng file staging trên cùng ổ đĩa đích và `File.Replace` để giữ bản dự phòng. `launcher.json`, shortcut và thư mục game không bị thay. Cơ chế không xin quyền admin, không đóng game đang chạy và không thực thi shell script.

Launcher mới xác nhận sau khi khởi tạo giao diện, trước các thao tác mạng. Không xác nhận trong 30 giây hoặc khởi động lỗi thì helper đóng đúng tiến trình launcher mới do nó tạo, khôi phục bản cũ và mở lại với tự cập nhật launcher được bỏ qua trong lần khởi động đó. Nút kiểm tra thủ công vẫn cho thử lại. Lỗi cập nhật launcher không chặn kiểm tra game hoặc chơi bản game đã cài.

Giao dịch được ghi dưới `launcher-updates`; bản sao lưu cạnh EXE có dạng `<tên EXE>.update-<token>.bak`. Lần mở tiếp theo xử lý giao dịch gián đoạn: giữ bản mới đã xác nhận, hoặc nhờ helper khôi phục bản cũ chưa xác nhận. File đang khóa được giữ lại để thử dọn sau. Khi lỗi không thể khôi phục tự động, giữ các file và xem `last-error.txt`; tải `TankBattle.exe` vào thư mục mới rồi chép cấu hình riêng sang nếu có, hoặc khôi phục bản `.bak` sau khi đóng ứng dụng. Game trong LocalAppData vẫn được dùng lại. Không xóa thủ công backup khi helper đang chạy.

## Kiểm tra

```powershell
./scripts/test-launcher.ps1
```

Chương trình kiểm tra không thêm package: publish hai fixture single-file phiên bản 1.2.0/1.3.0, dùng coordinator production và HTTP loopback tại cổng ngẫu nhiên. Nó kiểm contract/transport, cài game sau khi dùng chung transport, thay file bằng tiến trình thật, rollback, khôi phục giao dịch gián đoạn và quyền ghi. Thư mục/log của từng lần chạy nằm trong `runtime/launcher-checks/`; game và cấu hình người chơi thật không được dùng. Fixture không thay thế kiểm tra giao diện WinForms trên bản launcher thật.

`./scripts/test-launcher.ps1 -IncludeUiSmoke` còn publish hai bản WinForms với define **chỉ dành cho kiểm thử** `LAUNCHER_TEST`, tự cập nhật 1.2.0 → 1.3.0 rồi cập nhật game qua loopback. Define này bắt buộc thư mục dữ liệu riêng qua `TANK_LAUNCHER_TEST_ROOT`; bản phát hành bình thường bỏ qua biến đó. Thêm `-KeepUi` để giữ cửa sổ cho kiểm tra trực quan/cancel theo đường dẫn marker mà script in ra. Bài kiểm tra ACL chỉ sửa quyền tạo file của đúng thư mục fixture rồi khôi phục DACL; môi trường sandbox chặn đặt ACL cần chạy bài này bằng tài khoản có quyền thay DACL trên thư mục thử nghiệm, không cần sửa quyền thư mục hệ thống.

Kiểm tra thủ công trên hai bản standalone: cập nhật thành công và giao diện mở lại; trạng thái lỗi/cancel; launcher mới tiếp tục cập nhật game; mở hai launcher; thử mạng offline và thư mục không có quyền ghi. Không đánh dấu đã kiểm tra UI chỉ từ kết quả fixture.

Với bản 1.2.2, kiểm tra cả tên mới `TankBattle.exe`, tên cũ `Launcher.exe`, cấu hình mặc định nhúng và cấu hình tùy chỉnh. Kiểm tra thêm logo/nền, tiếng Việt, nhãn phiên bản, tiến độ/nút hủy; thử Tab/Space, thu nhỏ/khôi phục, kích thước tối thiểu 900×600 và DPI 100%/150%. Giữ cửa sổ bằng `-IncludeUiSmoke -KeepUi` để xem bản đã tự cập nhật; fixture chỉ là game giả, không dùng nút chơi của fixture để đánh giá Unity.
