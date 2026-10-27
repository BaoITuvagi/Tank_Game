# Tank Battle Multiplayer

Muốn gửi game một lần và tự cập nhật cho người chơi: xem [launcher](source/launcher/).

Tank Battle là game xe tăng nhiều người chơi, gồm server Java và client Unity. Người chơi có thể đăng ký tài khoản, tạo hoặc tham gia phòng, đấu với nhau và xem lịch sử trận đấu.

Mỗi phiên chơi sử dụng **một server chung**, lưu tài khoản và lịch sử của tất cả người chơi trong database SQLite. Các máy người chơi chạy client và kết nối đến server qua LAN hoặc VPN/Radmin. Bạn cũng có thể chạy server và client trên cùng máy để thử nghiệm.

Dự án đang được phát triển. Repository cung cấp **mã nguồn**, không kèm file chạy, database hay dữ liệu người chơi. Sau khi tải về, bạn cần build để tạo bản chạy được, hoặc sử dụng bản đã được người khác build từ dự án này.

## Phạm vi dự án

- Tài khoản, phòng chờ, trận Thường/Nhập vai, chiến đấu, kỹ năng Q, item, Red Zone/Bombstorm và lịch sử trận.
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

Mỗi chuyến đi bắt đầu với **0 vàng**. Kết thúc chuyến đi, vàng còn lại sau mua vật phẩm được cộng vào **vàng tài khoản** khi lưu kết quả. Huy hiệu vàng có đồng xu, số dư và nút **+**: bấm số vàng để xem công dụng, bấm + để xem cách kiếm thêm. Trong trận **Nhập vai**, bấm **E** để mở shop theo hướng dẫn sidebar: 6 loại, **50 vàng/loại**, mỗi loại mua tối đa một lần mỗi đợt. Item mua cất vào túi ở sidebar, **bấm icon để dùng sau**, kể cả lúc shop đóng. Túi giữ qua chết/hồi sinh/vào lại và chỉ dùng trong trận hiện tại. Vẫn được mua sau khi đã nhặt item; nhặt trên map có hiệu lực ngay. Cứ **40 giây** có một đợt item và shop mở **20 giây**, không phụ thuộc thời lượng trận; trận 3 phút có các đợt ở giây **40/80/120/160**. Item xuất hiện ngẫu nhiên trong vòng tròn giữa map (bán kính 25% cạnh ngắn) và biến mất khi hết đợt. Nút **Đóng**, E hoặc Esc đóng shop; mở shop không tạm dừng trận. Xem [luật vật phẩm](docs/gameplay.md#items).

UI chiến dịch sử dụng Theme Wood và Sprout Lands UI của Cup Nooble. Tệp ghi công và điều khoản Sprout nằm trong [CampaignUI/Sprout-License.txt](source/client/Assets/Resources/CampaignUI/Sprout-License.txt).
