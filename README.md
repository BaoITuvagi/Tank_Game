# Tank Battle Multiplayer

Muốn gửi game một lần và tự cập nhật cho người chơi: xem [hướng dẫn launcher và phát hành client](source/launcher/README.md).

Tank Battle là game xe tăng nhiều người chơi, gồm server Java và client Unity. Người chơi có thể đăng ký tài khoản, tạo hoặc tham gia phòng, đấu với nhau và xem lịch sử trận đấu.

Mỗi phiên chơi sử dụng **một server chung**, lưu tài khoản và lịch sử của tất cả người chơi trong database SQLite. Các máy người chơi chạy client và kết nối đến server qua LAN hoặc VPN/Radmin. Bạn cũng có thể chạy server và client trên cùng máy để thử nghiệm.

Dự án đang được phát triển. Repository cung cấp **mã nguồn**, không kèm file chạy, database hay dữ liệu người chơi. Sau khi tải về, bạn cần build để tạo bản chạy được, hoặc sử dụng bản đã được người khác build từ dự án này.

## Bạn cần cài gì?

**Build** nghĩa là tạo bản chạy được từ mã nguồn. Chọn trường hợp phù hợp để tránh cài những phần mềm không cần thiết:

| Mục đích | Phần mềm cần có trên Windows |
|---|---|
| Tự build và chạy server từ repository | JDK 21, Maven 3.9.x, Python 3 |
| Mở hoặc build client từ repository | Unity Hub, Unity Editor **6000.3.24f1**, module **Windows Build Support** và license Unity đã kích hoạt |
| Chạy server đã được build sẵn | JDK 21 |
| Chơi bằng client đã được build sẵn | Không cần Java, Maven, Python hoặc Unity Editor |

Người chỉ chạy server không cần Unity. Người chỉ chơi bản client đã build có thể chuyển thẳng đến mục **Chạy client và kết nối**.

## Cài đặt môi trường

### Java và công cụ build server

Cài **JDK 21**, sau đó kiểm tra trong PowerShell:

```powershell
java -version
```

Kết quả cần hiển thị phiên bản 21. Nếu không tìm thấy Java, đang dùng phiên bản khác hoặc `JAVA_HOME` đang trỏ tới JDK khác, chọn rõ JDK 21 cho cửa sổ hiện tại:

```powershell
$env:JAVA_HOME='<DUONG_DAN_THU_MUC_JDK_21>'
$env:Path=(Join-Path $env:JAVA_HOME 'bin')+';'+$env:Path
java -version
```

Thay phần `<...>` bằng thư mục JDK trên máy bạn, giữ dấu nháy. Chọn thư mục chứa thư mục con `bin`, không chọn trực tiếp file `java.exe`. Khi mở PowerShell mới, cần đặt lại nếu chưa lưu biến môi trường trong Windows.

Nếu tự build server từ mã nguồn, cài thêm:

- **Python 3:** bật tùy chọn thêm Python vào `PATH` khi cài. Dự án không cần thêm thư viện Python.
- **Maven 3.9.x:** giải nén Maven và thêm thư mục `bin` của Maven vào biến `Path` của Windows. Tìm “environment variables” trong Windows, mở phần biến môi trường của người dùng, chọn `Path` → `Edit` → `New` để thêm đường dẫn. Mở lại PowerShell sau khi lưu.

Kiểm tra trước khi build:

```powershell
python --version
mvn -version
```

Maven cần hiển thị phiên bản 3.9.x và đang dùng Java 21.

### Unity để mở hoặc build client

Cài Unity Hub, đăng nhập và kích hoạt license trên máy. Trong Hub, cài Editor **6000.3.24f1** kèm module **Windows Build Support**. Nếu Editor đã được cài, dùng mục Add modules trong Hub để bổ sung module còn thiếu.

Script tự tìm đúng Editor tại vị trí cài Unity Hub thông thường. Nếu script báo không tìm thấy Editor, chọn đường dẫn riêng:

```powershell
$env:UNITY_EDITOR_PATH='<DUONG_DAN_DEN_EDITOR_UNITY_EXE>'
```

Thay phần `<...>` bằng đường dẫn file `Editor/Unity.exe` trên máy bạn, giữ dấu nháy. Không chọn file chạy Unity Hub.

## Build và chạy server

Từ mã nguồn vừa tải về, tạo bản server bằng:

```powershell
./scripts/build-server.ps1
```

Chờ lệnh hoàn tất và báo build thành công. File server được tạo tại `build/server/tank-server-1.0.0.jar`.

Nếu đã có server build sẵn từ dự án này, đặt file `.jar` vào đúng vị trí trên trong thư mục dự án và bỏ qua bước build.

Khởi động server:

```powershell
./scripts/start-server.ps1
```

Giữ cửa sổ này mở trong lúc chơi. Server dùng thiết lập mặc định trong `config/server.properties`. Nếu cần đổi cổng hoặc nơi lưu database, xem [hướng dẫn cấu hình](config/README.md).

**Database nằm trên máy chạy server:** tất cả tài khoản và lịch sử được lưu tại `runtime/tank.db`. Khi chưa có database, server tự tạo một database rỗng; các lần chạy sau dùng lại dữ liệu đã lưu. Máy chỉ chạy client không giữ database tài khoản.

Nếu bạn chạy một server khác để thử nghiệm, server đó có database độc lập, không tự đồng bộ với server khác.

Dừng server bằng Ctrl+C. Sau khi cập nhật bản server, khởi động lại để dùng bản mới.

## Chạy client và kết nối

### Chạy trong Unity Editor

Trong Unity Hub, thêm project ở thư mục **`source/client`**, mở bằng Editor 6000.3.24f1. Mở scene `Assets/Scenes/Main.unity`, rồi bấm Play.

### Build thành ứng dụng Windows

Đóng Unity Editor đang mở cùng project, rồi chạy trong PowerShell tại thư mục gốc dự án:

```powershell
./scripts/build-client.ps1
```

Bản client nằm trong `build/client`. Mở `build/client/TankBattle.exe` để chơi. Khi chuyển sang máy khác, sao chép **toàn bộ thư mục client**, gồm file `.exe`, thư mục Data và các file đi kèm; chỉ chép file `.exe` sẽ không đủ.

Nếu đã nhận bản client build sẵn, mở `TankBattle.exe` trong thư mục đó và làm theo bước kết nối bên dưới.

### Kết nối đến server

Mở **Cài đặt** trong client:

- Server đang chạy trên cùng máy: nhập `127.0.0.1`.
- Server chạy trên máy khác: nhập IPv4 của máy server. Người chạy server có thể dùng `ipconfig` để xem IPv4 của mạng LAN đang dùng; nếu chơi qua Radmin, dùng IP Radmin của máy server.
- Giữ TCP **8888** và UDP **8889**, trừ khi server dùng cổng khác.

Các máy cần ở cùng LAN hoặc cùng mạng VPN/Radmin. Máy server cần cho phép TCP 8888 và UDP 8889 qua firewall. Nếu Windows hỏi quyền truy cập mạng khi chạy Java/server, cho phép trên mạng dùng để chơi.

Sau khi kết nối, đăng ký tài khoản, đăng nhập, tạo hoặc tham gia phòng. Trận đấu cần ít nhất hai người và tất cả phải ready để bắt đầu. Client ghi nhớ lựa chọn kết nối trên máy bạn; lựa chọn này không phải database tài khoản.

Khi tạo phòng, chọn **Thường** (mặc định) hoặc **Nhập vai**. Chế độ được giữ cố định cho phòng đó. Phòng Nhập vai yêu cầu mỗi người chọn một quốc gia khác nhau trước khi ready. Sau khi tải và đồng bộ quốc gia/kỹ năng Q, trận đếm ngược 3 giây rồi bắt đầu.

Nhập vai có kỹ năng Q theo quốc gia. Chết xóa hiệu ứng nhưng giữ hồi chiêu. Người đứng hạng nhất giành Hộp Đen trên màn hình kết quả; đây là phần thưởng kể chuyện trong trận. Các thông số kỹ năng vẫn cần được đánh giá qua playtest về công dụng và cân bằng.

### Demo cục bộ với bảy xe mô phỏng

Sau khi build server và client, mở `DemoRoleplay.bat` ở thư mục gốc. Cần JDK 21 trong `JAVA_HOME` hoặc `PATH`. Script mở server loopback TCP 23888/UDP 23889, một cửa sổ game và bảy client mô phỏng. Demo tự tạo tài khoản thử, chọn quốc gia và sẵn sàng; bạn chơi bằng WASD, chuột, Space, Shift, Q. Mỗi trận kéo dài ba phút, HP 3 và dùng map tám người.

Dữ liệu demo nằm riêng trong `runtime/demo/`, cấu hình ở `config/demo.properties`; không dùng `runtime/tank.db` hoặc ghi đè thiết lập kết nối đã lưu của client. Bảy client mô phỏng tự thoát sau trận; đóng cửa sổ server khi xong. Không mở lại launcher khi server demo cũ còn chạy. Đây là đối thủ theo kịch bản phục vụ trình diễn và kiểm thử, chưa phải bằng chứng về cân bằng hoặc cảm giác chơi với người thật.

## Điều khiển

| Phím/thao tác | Chức năng |
|---|---|
| WASD hoặc ↑/↓/←/→ | Di chuyển |
| Chuột | Ngắm |
| Click trái hoặc Space | Bắn |
| Shift | Tăng tốc |
| Q (Nhập vai) | Kỹ năng quốc gia |
| Chuột phải trong arena | Bật/tắt tự bắn |
| F11 hoặc Alt+Enter | Đổi fullscreen |

## Lỗi thường gặp

| Thông báo hoặc hiện tượng | Việc cần kiểm tra |
|---|---|
| Không tìm thấy Java/Maven/Python | Kiểm tra mục Cài đặt môi trường; mở lại PowerShell nếu vừa đổi biến môi trường |
| Không tìm thấy file server `.jar` | Build server trước hoặc đặt bản build sẵn vào đúng đường dẫn |
| Không tìm thấy Unity | Cài đúng phiên bản hoặc đặt `UNITY_EDITOR_PATH` |
| Unity báo thiếu license | Đăng nhập/kích hoạt license trong Unity Hub, mở Editor một lần rồi thử lại |
| Package Manager không kết nối | Kiểm tra mạng và việc firewall/phần mềm bảo vệ có chặn Unity/Package Manager hay không |
| Client không kết nối được | Kiểm tra server đã chạy, IP, cổng, LAN/VPN và firewall |
| Cổng hoặc database đang được dùng | Đóng server cũ; không chạy hai server cùng cổng hoặc cùng database |

Log được tạo khi chạy để giúp tìm lỗi. Log server/build/test Unity nằm trong `runtime/`; kết quả test Java nằm trong `source/server/target/surefire-reports/`. Không cần có sẵn những file này trước khi chạy.

## Dành cho người sửa mã nguồn

| Thư mục | Nội dung |
|---|---|
| `source/server` | Server Java, schema SQLite và test regression |
| `source/client` | Project Unity, mã client và tài nguyên game kèm `.meta` |
| `source/shared` | Luật gameplay dùng chung giữa Java và C# |
| `config` | Thiết lập server và mẫu thiết lập môi trường riêng |
| `scripts` | Các lệnh build, chạy và kiểm thử |

Client và server cần dùng cùng đợt build (giao thức v5, nội dung v5). Phòng mặc định 100 HP, cho phép 1–500 HP; đạn thường gây 20 HP và thanh máu xe chỉ hiển thị độ đầy, không có con số. Mọi trận dùng map lớn 80 × 80 với vị trí xuất phát ngẫu nhiên, phân bố cân đối; có thể sẵn sàng và bắt đầu trận tiếp theo ngay trong cùng phòng. Bộ kỹ năng Q và hồi chiêu được đồng bộ từ `source/shared/roleplay-content.json`; luật nền ở `source/shared/game-rules.json`. Sau khi thay đổi nội dung, đồng bộ Java/C# và content hash bằng:

```powershell
python scripts/sync-contracts.py
```

Chi tiết contract và kiểm thử packet ở [source/shared/README.md](source/shared/README.md).

Sau đó build lại cả server và client. Build server tự chạy test Java. Muốn chạy test Unity, build server trước để tạo dữ liệu kiểm thử cần thiết, đóng Editor đang mở cùng project rồi chạy:

```powershell
./scripts/test-client.ps1
```

Database, bản build, cache, log và cấu hình riêng được `.gitignore` bỏ qua. Khi thay đổi tài nguyên Unity, giữ file `.meta` đi kèm để bảo toàn tham chiếu.

Hình ảnh game của Kenney dùng giấy phép CC0, giữ tại [Kenney-LICENSE.txt](source/client/Assets/Resources/ArenaArt/Kenney-LICENSE.txt).


## Chế độ chiến dịch

Từ menu, chọn **Chiến dịch**, chọn quốc gia rồi bắt đầu chuyến giải cứu. Chiến dịch dùng bộ luật, bố cục map và giao diện riêng.

- **WASD / phím mũi tên:** di chuyển; đi vào ô có địch để cận chiến.
- **I:** bắn theo hướng xe, tầm 4 ô; hồi sau 5 lượt hợp lệ.
- **O:** kỹ năng quốc gia. Phím J không còn dùng trong chiến dịch.
- **P:** mở/đóng shop; Esc đóng shop. Mua vật phẩm không tiêu lượt hoặc hồi chiêu.

| Quốc gia | Máu | Giáp giảm mỗi đòn | Cận chiến | Đạn I | Kỹ năng O | Hồi O |
|---|---:|---:|---:|---:|---|---:|
| Mỹ | 100 | 0 | 20 | 40 | Không kích 70 sát thương, bán kính 2 ô quanh địch gần nhất trong 12 ô | 24 lượt |
| Liên Xô | 140 | 3 | 28 | 32 | Nhận 3 khiên và xung kích 45 sát thương quanh xe trong 1 ô | 20 lượt |
| Ý | 85 | 0 | 16 | 48 | Pháo 120 sát thương xuyên đội hình và khiên hỗ trợ, theo hướng xe trong 8 ô; dừng ở vật cản | 16 lượt |

Shop và vật phẩm hồi máu theo giới hạn riêng của từng xe. Khiên chặn trọn một đòn; kỹ năng Liên Xô có thể dùng để chuẩn bị phòng thủ trước giao tranh. Các lượt bắn hoặc kỹ năng bị từ chối không làm địch hành động.

UI chiến dịch sử dụng Theme Wood và Sprout Lands UI của Cup Nooble. Tệp ghi công và điều khoản Sprout nằm trong [CampaignUI/Sprout-License.txt](source/client/Assets/Resources/CampaignUI/Sprout-License.txt).
