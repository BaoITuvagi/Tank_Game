# Build, chạy và cấu hình

Đối chiếu ngày **2026-10-23** trên working tree dựa trên HEAD `e788610bdb0e2cd9fbd3f0cce54d9d29e815947e`, có thay đổi chưa commit. Các lệnh dưới đây được kiểm tra bằng cách đọc script/mã nguồn; đợt tài liệu này không chạy build hoặc test.

Mục lục:

- [Build server](#build-server)
- [Chạy và dừng server](#run-server)
- [Build và phân phối client](#build-client)
- [Build và phân phối launcher](#build-launcher)
- [Kết nối qua máy cục bộ, LAN hoặc VPN](#connect)
- [Cấu hình server và công cụ](#configuration)
- [Demo nhiều client trên cùng máy](#local-demo)
- [Log và xử lý lỗi](#troubleshooting)

Chạy lệnh PowerShell từ **thư mục gốc repository**. Cách cài phần mềm nằm trong [README](../README.md); các kiểm tra cần chạy sau khi sửa mã ở [kiểm thử](testing.md#matrix).

Sau khi merge hoặc áp dụng stash có thay đổi code/map/contract, build lại **cả server và client**, rồi khởi động lại server và mở client mới. Các thao tác Git không cập nhật JAR hay `build/client` đã có. Server đọc map từ source, còn bản client dùng map đã đóng gói trong `TankBattle_Data/StreamingAssets`; hai bản khác byte khiến client rời phòng khi bắt đầu trận. Khi phân phối, thay toàn bộ thư mục client cùng đợt build. Các nút **Chiến dịch** và **BXH chiến dịch** xuất hiện trong cột thao tác của sảnh ở bản client đã build từ code tương ứng.

<a id="build-server"></a>
## Build server

Cần **JDK 21**, **Maven 3.9.x** và **Python 3**; dự án không yêu cầu thêm thư viện Python. Kiểm tra công cụ trong cửa sổ đang dùng:

```powershell
java -version
mvn -version
python --version
```

Java phải là 21 và Maven phải đang dùng JDK 21. Nếu chưa tìm đúng, đặt [đường dẫn công cụ](#configuration), rồi build:

```powershell
./scripts/build-server.ps1
```

[build-server.ps1](../scripts/build-server.ps1) chạy `python scripts/sync-contracts.py --check`, sau đó Maven `clean package` gồm JUnit test và đóng gói JAR có dependency. Script mặc định làm sạch `source/server/target`, rồi xuất bản `build/server/tank-server-1.0.0.jar` qua file tạm. Kết quả test ở `source/server/target/surefire-reports/`; fixture Java/C# ở `source/server/target/interop/`. Không dùng `--check` để cập nhật generated code: quy trình sửa contract nằm ở [shared contracts](protocol.md#shared-contracts).

Script có `-JavaHome <thu-muc-JDK>` và `-BuildDirectory <duong-dan>` khi cần chọn riêng; output khác `target` chỉ được chấp nhận trong cây `runtime/` hoặc `build/`, với tên thư mục cuối bắt đầu bằng `server-`. Dùng đường dẫn mặc định cho quy trình thông thường và xem [thứ tự tạo fixture/test](testing.md#prerequisites) trước khi tùy chỉnh output.

Nếu nhận JAR đã build từ dự án, có thể đặt vào `build/server/tank-server-1.0.0.jar` rồi chuyển sang [chạy server](#run-server). Máy chỉ chạy JAR cần JDK 21, không cần Maven, Python hoặc Unity. Các file cấu hình và map mà server sử dụng vẫn phải có ở đường dẫn đã cấu hình.

<a id="run-server"></a>
## Chạy và dừng server

```powershell
./scripts/start-server.ps1
```

[start-server.ps1](../scripts/start-server.ps1) chọn `config/server.local.properties` nếu có; nếu không, dùng `config/server.properties`. Chọn rõ một file bằng:

```powershell
./scripts/start-server.ps1 -Config config/server.properties
```

Giữ PowerShell mở trong lúc chơi. Dòng khởi động từ [Server.start](../source/server/src/main/java/tank/Server.java) có dạng `Tank server COMBINED TCP=8888 UDP=8889` với mode/cổng thực tế. Dừng bằng **Ctrl+C**; [Main.main](../source/server/src/main/java/tank/Main.java) cài shutdown hook gọi `Server.close`.

Trước khi chạy JVM, [resolve-server-artifact.ps1](../scripts/resolve-server-artifact.ps1) tạo bản JAR bất biến trong `runtime/server-artifacts/<SHA256>/tank-server-1.0.0.jar`. Build mới không thay code mà server đang chạy đã nạp; hãy dừng rồi khởi động lại để dùng bản mới.

Server dùng chung giữ tài khoản và lịch sử cho mọi client trong SQLite, mặc định `runtime/tank.db`. Lần đầu server tạo database mới; lần sau dùng lại dữ liệu đó. Server khác có database riêng và không tự đồng bộ. Máy chỉ chạy client không giữ database tài khoản. Hành vi lưu dữ liệu/spool và lịch sử ở [dữ liệu](data.md#persistence).

<a id="build-client"></a>
## Build và phân phối client

Cần Unity Editor **6000.3.24f1**, license đã kích hoạt và **Windows Build Support**. Đóng Editor đang mở `source/client` trước khi chạy:

```powershell
./scripts/build-client.ps1
```

[build-client.ps1](../scripts/build-client.ps1) tìm Editor qua [resolve-unity-editor.ps1](../scripts/resolve-unity-editor.ps1), chạy batchmode và gọi [BuildClient.Build → BuildTo](../source/client/Assets/Editor/BuildClient.cs). Bản Windows 64 bit được ghi vào `build/client/TankBattle.exe`; log ở `runtime/unity-build.log`.

**Tác dụng phụ cần kiểm tra:** `BuildTo` tạo lại `Assets/Scenes/Main.unity` thành scene rỗng có camera, lưu scene và ghi `PlayerSettings` (company/product, cửa sổ mặc định 1280×800, Windowed, chạy nền, cho resize). Sau build, kiểm tra diff scene/project settings, chỉ giữ thay đổi thuộc phạm vi được cho phép và bảo toàn công việc có sẵn. Script này không phải kiểm tra chỉ đọc.

Build tự validate Combat FX và chép giấy phép sang `ThirdParty/CookedFX-LICENSE.pdf` nhờ [CombatFxBuildValidation.OnPreprocessBuild, OnPostprocessBuild](../source/client/Assets/Editor/CombatFxImporter.cs). Nếu thiếu/sai asset, dùng [hướng dẫn khôi phục](client.md#restore-combat-fx).

Mở `build/client/TankBattle.exe` để chơi. Khi chuyển sang máy khác, chép **toàn bộ thư mục `build/client`**, gồm `.exe`, thư mục Data, Unity runtime và `ThirdParty`; chỉ chép `.exe` không đủ. Máy chơi bản client đã build không cần Java, Maven, Python hoặc Unity Editor. Người phát hành cần cung cấp client/server cùng đợt build để khớp [giao thức và nội dung](protocol.md#shared-contracts).

Để chạy trong Editor, mở `source/client`, scene `Assets/Scenes/Main.unity` rồi Play theo [bootstrap client](client.md#bootstrap).

<a id="build-launcher"></a>
## Build và phân phối launcher

Launcher từ 1.2.0 tự cập nhật EXE trước game bằng `launcher-manifest.json`, độc lập với manifest game. Build bằng `./scripts/build-launcher.ps1`; dùng `-ReleaseBaseUrl` trỏ tag GitHub Releases cố định khi phát hành. Gửi `build/launcher-win64.zip` cho người chơi lần đầu, đồng thời upload EXE có version và manifest riêng do script sinh ra. Latest phải phục vụ cả hai manifest và các asset mà chúng trỏ tới. Xem [hướng dẫn launcher](../source/launcher/README.md) về cấu hình, output, rollback và kiểm tra.

Người chơi launcher cũ tải lại bản có cơ chế tự cập nhật một lần. Các lần sau tự tải/mở lại; giữ `launcher.json`, shortcut và game trong `%LocalAppData%\TankBattle`. Thư mục đặt EXE cần quyền ghi; không yêu cầu quyền admin. Lỗi tải/thay file giữ bản hiện tại; lỗi khởi động bản mới thì helper khôi phục backup. Build server hoặc client không cập nhật các file phát hành này.

<a id="connect"></a>
## Kết nối tới server cố định

Client luôn kết nối tới **`13.75.2.41`**, TCP **8888**, UDP **8889**. Đã bỏ màn hình chọn kết nối; cấu hình `connection.json` cũ trên máy không được đọc. Server cần cho phép cả hai cổng qua firewall. Để tạo bản client dùng server khác, sửa `ServerHost`, `ServerTcpPort`, `ServerUdpPort` trong `TankApp.cs` rồi build lại. Smoke/test harness vẫn cấu hình endpoint riêng để kiểm thử cục bộ.

Đăng ký tài khoản, đăng nhập, tạo/tham gia phòng, rồi tất cả sẵn sàng để chủ phòng bắt đầu. Cần ít nhất hai người và mỗi cửa sổ dùng một tài khoản riêng. Khi tạo phòng có thể chọn **Thường** hoặc **Nhập vai**; chế độ cố định cho phòng. Phòng Nhập vai yêu cầu mỗi người chọn quốc gia khác nhau trước khi ready, tải/đồng bộ loadout rồi đếm ngược ba giây. Xem [vòng đời phòng](architecture.md#lifecycle) và [kỹ năng](gameplay.md#skills).

Sau kết quả, có thể trở lại cùng phòng để ready và chơi trận tiếp. Cách điều khiển ở [gameplay](gameplay.md#controls); cách thử timeout/vào lại và khớp phiên bản/map ở [kiểm tra mạng thủ công](testing.md#manual).

<a id="configuration"></a>
## Cấu hình server và công cụ

| File | Cách dùng |
|---|---|
| [config/server.properties](../config/server.properties) | Thiết lập mặc định được chia sẻ trong repository. |
| `config/server.local.properties` | File riêng, bị Git bỏ qua. Khi cần, sao chép toàn bộ `server.properties` sang tên này rồi sửa. Script tự ưu tiên file local; **không ghép** nó với file mặc định. |
| [config/demo.properties](../config/demo.properties) | Demo loopback với cổng và dữ liệu riêng; xem [demo cục bộ](#local-demo). |
| [config/tools.example.ps1](../config/tools.example.ps1) | Mẫu tùy chọn lưu đường dẫn công cụ trên máy phát triển. Sao chép thành `config/tools.local.ps1`, thay `REPLACE_WITH_...`, rồi nạp bằng `. ./config/tools.local.ps1`; file riêng không tự được nạp. |

Giá trị mặc định và ý nghĩa vận hành:

| Key | Mặc định | Ý nghĩa |
|---|---|---|
| `bind` | `0.0.0.0` | Nhận kết nối trên các địa chỉ mạng của máy server. |
| `tcp.port`, `udp.port` | `8888`, `8889` | Cổng điều khiển TCP và gameplay UDP; đổi cổng thì cập nhật client và firewall. |
| `tick.rate`, `snapshot.rate` | `30`, `30` | [Config constructor](../source/server/src/main/java/tank/Config.java) yêu cầu cả hai là 30 Hz. |
| `max.players` | `8` | Số người mặc định/tối đa theo cấu hình; phạm vi hợp lệ 2–8. |
| `max.sessions`, `max.rooms` | `320`, `40` | Giới hạn phiên TCP và số phòng; phạm vi tương ứng 1–320 và 1–40. |
| `network.mode` | `COMBINED` | Mode truyền: `BASELINE`, `INPUT_REDUNDANCY`, `DELTA`, `COMBINED`; mô tả tại [transport](protocol.md#transport). |
| `database.path` | `runtime/tank.db` | Tài khoản và lịch sử trên máy server. |
| `spool.path` | `runtime/spool` | Dữ liệu chờ persistence; xem [persistence](data.md#persistence). |
| `metrics.path` | `runtime/server.csv` | Metrics CSV; xem [ý nghĩa metrics](evaluation.md#metrics). |
| `log.path` | `runtime/server.log` | Log console/lỗi được ghi nối tiếp bởi `Main.main`. |
| `map.path` | `source/client/Assets/StreamingAssets/map.json` | Map cơ sở. `Server` còn tải `map-large.json` cùng thư mục, hoặc đường dẫn `map.large.path` nếu đặt riêng. Định dạng và map thực dùng ở [map](gameplay.md#maps). |
| `benchmark`, `match.ticks` | `false`, `5400` | Chế độ thử nghiệm và thời lượng mặc định bằng tick. `match.ticks` hợp lệ 1–65535; giá trị khác 5400 yêu cầu `benchmark=true`. Đây không thay thế các lựa chọn thời lượng phòng trong UI. |
| `impairment.enabled`, `impairment.seed` | `false`, `20261007` | Bật mô phỏng mạng và seed. |
| `impairment.delay.ms`, `impairment.jitter.ms` | `0`, `0` | Độ trễ và jitter giả lập, không âm. |
| `impairment.loss`, `impairment.duplicate`, `impairment.reorder` | `0`, `0`, `0` | Tỷ lệ mất, lặp, đảo thứ tự, trong khoảng 0–1. |
| `impairment.burst.period.ms`, `impairment.burst.duration.ms` | `0`, `0` | Chu kỳ/thời gian burst, không âm. Kịch bản đánh giá ở [evaluation](evaluation.md#scenarios). |

Đường dẫn tương đối được tính từ gốc repository vì script chuyển về đó trước khi chạy. Giữ credentials, database, spool, log, `build/`, `runtime/` và cấu hình riêng ngoài Git theo [.gitignore](../.gitignore). Không chạy hai server trên cùng cổng hoặc cùng database. Giữ các mục còn lại nếu chưa có nhu cầu thay đổi; không đổi dependencies, Unity version hay project settings chỉ để xử lý cấu hình máy.

`JAVA_HOME` trỏ tới **thư mục JDK 21 chứa `bin`**; `UNITY_EDITOR_PATH` trỏ tới **file `Editor/Unity.exe`**, không phải Unity Hub. Ví dụ chỉ áp dụng cho PowerShell hiện tại:

```powershell
$env:JAVA_HOME='<DUONG_DAN_THU_MUC_JDK_21>'
$env:Path=(Join-Path $env:JAVA_HOME 'bin')+';'+$env:Path
$env:UNITY_EDITOR_PATH='<DUONG_DAN_DEN_EDITOR_UNITY_EXE>'
```

Giữ dấu nháy và thay placeholder bằng đường dẫn thật. `start-server.ps1` còn nhận `-Java <java.exe>`; build/test client nhận `-Unity <Unity.exe>`. Resolver kiểm tra executable tồn tại, còn người chạy phải bảo đảm đúng JDK/Editor. Người chỉ chơi bản client đã build không cần các biến này.

<a id="local-demo"></a>
## Demo nhiều client trên cùng máy

Sau khi build server/client, chạy:

```powershell
./scripts/start-server.ps1 -Config config/demo.properties
```

[demo.properties](../config/demo.properties) đặt `bind=127.0.0.1`, TCP **23888**, UDP **23889**, tối đa 16 phiên/4 phòng và `benchmark=true`. Database, spool, CSV và log nằm riêng trong `runtime/demo/`. Đây là demo chỉ trên cùng máy; cấu hình bind loopback không cho máy LAN kết nối.

Mở ít nhất hai cửa sổ `build/client/TankBattle.exe`, nhập host/cổng trên, dùng tài khoản khác nhau, tạo/tham gia cùng phòng. Với Nhập vai, chọn quốc gia khác nhau, tất cả ready rồi chủ phòng bắt đầu. Hoàn tất một trận, kiểm tra kết quả/lịch sử và chơi lại trong cùng phòng theo [checklist thủ công](testing.md#manual).

Client ghi nhớ host/cổng vừa lưu trong dữ liệu riêng của người dùng. Khi quay lại server thường, sửa lại các giá trị tương ứng. Đóng client và Ctrl+C server demo khi xong; giữ dữ liệu demo riêng nếu cần đối chiếu, không đưa vào Git.

<a id="troubleshooting"></a>
## Log và xử lý lỗi

| Triệu chứng | Kiểm tra/cách xử lý |
|---|---|
| Không tìm thấy Java/Maven/Python hoặc Java sai phiên bản | Kiểm tra `java -version`, `mvn -version`, `python --version` trong chính PowerShell đang dùng; sửa `JAVA_HOME`/`PATH`, mở lại shell sau khi đổi biến hệ thống. |
| Không tìm thấy JAR | Build server trước hoặc đặt bản JAR đã build vào `build/server/tank-server-1.0.0.jar`. |
| Không tìm thấy Unity | Cài đúng 6000.3.24f1 hoặc đặt `UNITY_EDITOR_PATH`/`-Unity` tới Editor executable. |
| Unity thiếu license hoặc không build Windows | Đăng nhập/kích hoạt license trong Unity Hub, mở Editor một lần; kiểm tra Windows Build Support và đóng Editor trước script. |
| Package Manager không kết nối | Kiểm tra mạng, firewall hoặc phần mềm bảo vệ có chặn Unity/Package Manager; không đổi package để bỏ qua nguyên nhân môi trường. |
| Unity báo project đang mở | Đóng Editor dùng cùng `source/client`, rồi chạy lại build/test tuần tự. |
| Thiếu/sai Combat FX hoặc sprite | Kiểm tra clone đủ asset và `.meta`; dùng [khôi phục Combat FX](client.md#restore-combat-fx) và giữ importer settings. |
| Đăng nhập được nhưng không vào trận | Kiểm tra UDP, firewall, cổng, phiên bản nội dung/map và lỗi chuẩn bị trong client. TCP hoạt động không chứng minh UDP hoạt động. |
| Không kết nối được | Kiểm tra server đang chạy, host/cổng thật, LAN/VPN/Radmin, firewall và `bind`; không nhập `0.0.0.0` thay IP server. |
| Tài khoản đang đăng nhập ở cửa sổ khác | Dùng tài khoản khác cho từng client hoặc đóng phiên cũ. |
| `RATE_LIMITED` | Chờ theo thông báo client rồi thử lại; demo có cấu hình riêng. Không dùng `benchmark=true` như mặc định cho server chơi chung. |
| Không khớp giao thức/nội dung/map | Dùng server và client cùng đợt build; đồng bộ contract đúng quy trình và giữ nguyên byte map. Xem [shared contracts](protocol.md#shared-contracts). |
| Cổng hoặc database đang dùng | Dừng server cũ; chọn cổng và đường dẫn dữ liệu riêng khi chạy nhiều môi trường. |
| Unity test thiếu file `.tsv` interop | Build server với test đầy đủ trước khi chạy Unity; xem [điều kiện test](testing.md#prerequisites). |

| File/thư mục | Nội dung |
|---|---|
| `runtime/server.log` / `runtime/demo/server.log` | Log server mặc định/demo, hoặc theo `log.path`. |
| `runtime/server.csv` / `runtime/demo/server.csv` | Metrics mặc định/demo, hoặc theo `metrics.path`. |
| `runtime/unity-build.log` | Log batch build client. |
| `runtime/unity-tests.log`, `runtime/unity-tests.xml` | Log và kết quả NUnit của script test client. |
| `source/server/target/surefire-reports/` | Kết quả JUnit khi dùng build directory mặc định. |

Những file này được tạo khi chạy, không cần có sẵn sau clone. Một log hoặc bản build cũ không xác nhận working tree mới đã pass; cách ghi bằng chứng ở [testing](testing.md#evidence).
