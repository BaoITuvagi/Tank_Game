# Tank Battle Multiplayer

## Tải launcher và chơi game

Dành cho người chơi trên **Windows 64 bit**: không cần clone repository hay cài công cụ lập trình.

1. Bấm **Releases** ở bên phải trang repository, rồi mở bản phát hành mới nhất: [Tank Battle Releases](https://github.com/BaoITuvagi/Tank_Game/releases/latest).
2. Trong mục **Assets**, bấm tải **[TankBattle-launcher-win64.zip](https://github.com/BaoITuvagi/Tank_Game/releases/latest/download/TankBattle-launcher-win64.zip)**.
3. Giải nén ZIP, mở **TankBattle.exe**, chờ launcher tải game rồi bấm **CHƠI GAME**.

## Phát triển và vận hành server

Bản tích hợp quản trị mạng này đã đồng bộ branch **Bao**, commit **ec3221e** (nền game v1.0.3). Working tree bổ sung chat, chọn ba map multiplayer và bão phủ toàn map; phiên bản hiện tại dùng giao thức **21**, nội dung **7** với hash được sinh lại, cần server và client build cùng đợt. Quản trị mạng được ghép bổ sung; các thay đổi UI/asset và bản đồ đang có được mô tả tại [context](docs/context.md). Xem [ADMIN_WEB.md](ADMIN_WEB.md), web mặc định http://127.0.0.1:8080.

Các lệnh CMD chạy tại thư mục gốc repository:

```cmd
scripts\build-server.cmd
scripts\init-admin.cmd -Name admin
scripts\start-server.cmd
```

Chỉ tạo admin lần đầu hoặc khi cần đặt lại mật khẩu; server phải dừng khi khởi tạo. Client/server dùng giao thức 18; các bản cũ không kết nối được. Chat, chọn map và hộp nổi admin cần client được build từ source tích hợp này; website/server không tự thay file client trong Launcher. Build local chưa cập nhật bản phân phối Launcher.

**Chọn map nhiều người:** trong **Tạo phòng**, chọn thẻ bản đồ trước khi xác nhận. Cả Thường và Nhập vai có **Tiền Đồn Xanh** (map hiện tại, mặc định), **Pháo Đài Cát** (sa mạc vàng, công sự sa thạch, sân giữa có bốn cổng) và **Sông Băng** (tuyết trắng xanh, công sự phủ tuyết, sông lạnh với các lối vượt tuyết nén). Mỗi thẻ có hình xem trước địa hình thật, cùng diện mạo với trong trận; ba map khác nhau cả bối cảnh lẫn bố cục. Map giữ cố định cho phòng đó, kể cả trận tiếp theo; muốn đổi map hãy tạo phòng mới. Tên map hiện trong danh sách phòng, phòng chờ và chi tiết lịch sử.

**Chat trong trận Thường/Nhập vai:** bấm tab Chat ở phần trên sidebar bên phải rồi bấm ô nhập; Enter gửi, Esc hủy. Xếp hạng/Chat dùng chung vùng thông tin ở mọi kích thước, mặc định chọn Xếp hạng; Chat có số tin chưa đọc. Cụm kỹ năng/túi vật phẩm phía dưới giữ nguyên vị trí khi đổi tab. Tên có màu theo xe của người gửi; khi đang gõ, xe ngừng nhận điều khiển và tự bắn tắt. Khi vào lại cùng trận, khôi phục tối đa 100 tin gần nhất; không lưu chat sau trận. Xem [giao diện](docs/client.md) và [giao thức chat](docs/protocol.md#match-chat).

Trong trận Nhập vai và Chiến dịch, **chuột phải vào ô kỹ năng** để xem mô tả; Đóng, Esc hoặc bấm ra ngoài để đóng. HUD giữ tên và trạng thái hồi chiêu; kỹ năng Nhập vai có viền xanh rút dần, kỹ năng Chiến dịch chỉ ghi số lượt còn lại. Mở khung mô tả khóa điều khiển và tắt tự bắn; trận multiplayer vẫn tiếp tục. **Cài đặt** cho đổi phím bằng cách nhập tên; riêng phím mũi tên có thể nhấn trực tiếp trong ô, hiển thị **↑ ↓ ← →**. Bấm Lưu để áp dụng.

Muốn gửi game một lần và tự cập nhật cho người chơi: xem [launcher](source/launcher/README.md). Từ bản 1.2.0, launcher tự tải và mở lại khi có phiên bản launcher mới; người dùng bản cũ cần tải bản hỗ trợ một lần. Build local chưa tự phát hành các asset/manifest lên GitHub Releases.

Tank Battle là game xe tăng nhiều người chơi, gồm server Java và client Unity. Người chơi có thể đăng ký tài khoản, tạo hoặc tham gia phòng, đấu với nhau và xem lịch sử trận đấu.

Mỗi phiên chơi sử dụng **một server chung**, lưu tài khoản và lịch sử của tất cả người chơi trong database SQLite. Các máy người chơi chạy client và kết nối đến server qua LAN hoặc VPN/Radmin. Bạn cũng có thể chạy server và client trên cùng máy để thử nghiệm.

Dự án đang được phát triển. Repository cung cấp **mã nguồn**, không kèm file chạy, database hay dữ liệu người chơi. Nếu tải mã nguồn để phát triển, bạn cần build để tạo bản chạy được. Nếu chỉ muốn chơi, làm theo hướng dẫn tải launcher ở đầu trang.

## Phạm vi dự án

- Tài khoản, phòng chờ, trận Thường/Nhập vai, chiến đấu, kỹ năng Q, item, Red Zone/Bombstorm, bão cát phủ toàn map giảm 50% tốc độ bắn, bão tuyết phủ toàn map giảm 50% tốc độ di chuyển và lịch sử trận.
- Server Java mô phỏng gameplay; client Unity hiển thị, nhận điều khiển và đồng bộ qua TCP/UDP.
- Bộ docs mô tả implementation và nguồn kiểm chứng; kết quả thực nghiệm mạng chưa được xác nhận trong đợt tổ chức tài liệu này.

## Bạn cần cài gì?

**Build** nghĩa là tạo bản chạy được từ mã nguồn. Chọn trường hợp phù hợp để tránh cài những phần mềm không cần thiết:

| Mục đích | Phần mềm cần có trên Windows |
|---|---|
| Tự build và chạy server từ repository | JDK 21, Maven 3.9.x, Python 3 |
| Mở hoặc build client từ repository | Unity Hub, Unity Editor **6000.3.24f1**, module **Windows Build Support** và license Unity đã kích hoạt |
| Chạy server đã được build sẵn | JDK 21 |
| Chơi bằng client đã được build sẵn | Không cần Java, Maven, Python hoặc Unity Editor |

Người chỉ chạy server không cần Unity. Người chơi bản client đã build xem [cách kết nối](docs/deployment.md#connect).

## Cài đặt môi trường

### Java và công cụ build server

Cài **JDK 21**, sau đó kiểm tra trong CMD:

```cmd
java -version
```

Kết quả cần hiển thị phiên bản 21. Nếu không tìm thấy Java, đang dùng phiên bản khác hoặc `JAVA_HOME` đang trỏ tới JDK khác, chọn rõ JDK 21 cho cửa sổ hiện tại:

```cmd
set "JAVA_HOME=C:\duong-dan\jdk-21"
set "PATH=%JAVA_HOME%\bin;%PATH%"
java -version
```

Thay đường dẫn ví dụ bằng thư mục JDK thật trên máy bạn, giữ dấu nháy kép. Chọn thư mục chứa thư mục con `bin`, không chọn trực tiếp file `java.exe`. Khi mở CMD mới, cần đặt lại nếu chưa lưu biến môi trường trong Windows.

Nếu tự build server từ mã nguồn, cài thêm:

- **Python 3:** bật tùy chọn thêm Python vào `PATH` khi cài. Dự án không cần thêm thư viện Python.
- **Maven 3.9.x:** giải nén Maven và thêm thư mục `bin` của Maven vào biến `Path` của Windows. Tìm “environment variables” trong Windows, mở phần biến môi trường của người dùng, chọn `Path` → `Edit` → `New` để thêm đường dẫn. Mở lại CMD sau khi lưu.

Kiểm tra trước khi build:

```cmd
python --version
mvn -version
```

Maven cần hiển thị phiên bản 3.9.x và đang dùng Java 21.

### Unity để mở hoặc build client

Cài Unity Hub, đăng nhập và kích hoạt license trên máy. Trong Hub, cài Editor **6000.3.24f1** kèm module **Windows Build Support**. Nếu Editor đã được cài, dùng mục Add modules trong Hub để bổ sung module còn thiếu.

Script tự tìm đúng Editor tại vị trí cài Unity Hub thông thường. Nếu script báo không tìm thấy Editor, chọn đường dẫn riêng:

```cmd
set "UNITY_EDITOR_PATH=C:\duong-dan\Editor\Unity.exe"
```

Thay đường dẫn ví dụ bằng đường dẫn Unity Editor thật trên máy bạn, giữ dấu nháy kép. Không chọn file chạy Unity Hub.

Tại Unity Hub, thêm project `source/client`. Tài nguyên game cần thiết đã có trong repository và được Unity import; giữ nguyên `.meta` và giấy phép. Nếu thiếu/hỏng, xem [khôi phục tài nguyên](docs/client.md#assets).

## Đọc tiếp

| Mục đích | Tài liệu |
|---|---|
| Nắm nhanh phạm vi, kiến trúc và hiện trạng | [Context dự án](docs/context.md), [kiến trúc và source map](docs/architecture.md) |
| Build, chạy, cấu hình và kết nối LAN/VPN | [Triển khai](docs/deployment.md) |
| Luật chơi, kỹ năng, item, battlefield | [Gameplay](docs/gameplay.md) |
| Giao thức, đồng bộ và dữ liệu | [Protocol](docs/protocol.md), [SQLite và lịch sử](docs/data.md) |
| Unity, UI, tài nguyên và preview | [Client](docs/client.md) |
| Kiểm tra và đo đạc | [Testing](docs/testing.md), [thực nghiệm](docs/evaluation.md) |
| Chuẩn bị báo cáo môn Lập trình mạng | [Hướng dẫn báo cáo](docs/report-guide.md) |

## Chế độ chiến dịch

Sau đăng nhập, chọn **Chiến dịch** ở màn chọn chế độ, chọn quốc gia rồi bắt đầu chuyến giải cứu. Chiến dịch dùng cùng phong cách cartoon và khung header/bản đồ/cột HUD bên phải với multiplayer; bộ luật theo lượt và bố cục map vẫn riêng. Thanh máu nằm trên xe; sidebar giữ quãng đường, số trùm, vàng và các thao tác cần thiết. Tên khu vực hiện ngắn khi đến nơi rồi mờ đi, không nằm thường trực bên phải; số lượt và số địch đã hạ chỉ dành cho kết quả, không chiếm HUD trong trận. Ô Khiên/Đòn ×2 hiện số lần chặn/đòn cường hóa còn lại. Ba nút Bắn/Kỹ năng/Tiếp tế luôn nằm ở cuối cột HUD. Thẻ **Tiếp tục chuyến đi** chỉ tiếp tục run còn trong phiên đăng nhập hiện tại. Có thể xem sảnh multiplayer khi còn run, nhưng phải kết thúc và lưu chuyến đi trước khi tạo/tham gia phòng.

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

Mỗi chuyến đi bắt đầu với **0 vàng**. Kết thúc chuyến đi, vàng còn lại sau mua vật phẩm được cộng vào **vàng tài khoản** khi lưu kết quả. Huy hiệu vàng có đồng xu, số dư và nút **+**: bấm số vàng để xem công dụng, bấm + để xem cách kiếm thêm. Trong trận **Nhập vai**, bấm **E** để mở shop theo hướng dẫn sidebar: 6 loại, **50 vàng/loại**, mỗi loại mua tối đa một lần mỗi đợt. Item mua cất vào túi ở sidebar, **bấm icon để dùng sau**, kể cả lúc shop đóng. Túi giữ qua chết/hồi sinh/vào lại và chỉ dùng trong trận hiện tại. Vẫn được mua sau khi đã nhặt item; nhặt trên map có hiệu lực ngay. Cứ **40 giây** có một đợt item và shop mở **20 giây**, không phụ thuộc thời lượng trận; trận 3 phút có các đợt ở giây **40/80/120/160**. Item xuất hiện ngẫu nhiên trong vòng tròn giữa map (bán kính 25% cạnh ngắn) và biến mất khi hết đợt. Nút **Đóng**, E hoặc Esc đóng shop; mở shop không tạm dừng trận. Xem [luật vật phẩm](docs/gameplay.md#items).

Vàng dùng chung biểu tượng **một đồng xu** trên bản đồ, ví, cửa hàng và kết quả; nhặt xu thành công có âm thanh xác nhận theo cài đặt Âm thanh. Sáu icon item Nhập vai ghép từ thùng/đạn Kenney cùng bộ với xe, kèm hình chức năng thống nhất trên map/shop/túi.

UI chiến dịch dùng chung theme `CartoonUi`, font Bungee/Nunito và xe nhìn từ trên xuống của multiplayer. Scenery và địch/boss cartoon bổ sung ở `CampaignCartoon`; tài nguyên pixel/Theme Wood/Sprout gốc vẫn được giữ cùng tệp ghi công và [điều khoản Sprout](source/client/Assets/Resources/CampaignUI/Sprout-License.txt).
