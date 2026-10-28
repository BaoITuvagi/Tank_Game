# Benchmark socket Tank Battle

Đây là công cụ/số liệu benchmark lịch sử, không cần để chạy website quản trị. Khi giao thức game thay đổi, cần xác minh công cụ tương thích trước khi đo lại.

`NetworkBenchmark.java` là bộ phát tải độc lập, dùng class Protocol/World/DeltaCodec từ JAR đã build. Server thật chạy trong JVM con riêng; 4 client Java headless giao tiếp TCP/UDP qua loopback. Công cụ không gọi Room.tick trực tiếp, không giả lập kết quả và không sửa game/server JAR.

## Phạm vi

Đo server/giao thức dưới tải một phòng bốn người; không đo rendering, input-to-photon, FPS hoặc prediction C# của Unity. Không coi đây là benchmark tải tối đa hoặc mạng LAN/Internet thực.

Các client thực hiện REGISTER/LOGIN, CREATE/JOIN, READY, UDP handshake, PING/PONG, PREPARED, nhận START, gửi INPUT/ACK, giải mã FULL/DELTA và kiểm tra END. Input lặp theo tick/slot, đổi hướng mỗi 30 tick, giữ nút bắn và thay đổi góc ngắm. Input tick mục tiêu được neo vào snapshot/time, có lead dựa trên RTT và cửa sổ tối đa 30 tick, theo ý tưởng ClientCore. Không có mô phỏng prediction hoặc UI trong công cụ này.

## Thiết kế lượt chính thức ngày 15/10/2026

- 4 mode: BASELINE, INPUT_REDUNDANCY, DELTA, COMBINED.
- 3 profile: clean; loss5 (mất 5%, không chèn delay); delay50_jitter20_loss5 (mất 5%, delay 50 ms và jitter đều trong ±20 ms mỗi chiều tại wrapper server).
- Impairment chỉ bật ở server; không chèn thêm ở client. Độ trễ thực còn có scheduling/loopback, không được gọi delay cấu hình là RTT đo được.
- 3 repeat, seed `20261015 + repeat`, 30 giây mô phỏng/trận, 5 giây warm-up.
- Tổng 36 lượt, chạy tuần tự. Thứ tự mode được xáo trộn tái lập trong mỗi cặp profile/repeat để giảm thiên lệch thứ tự.
- Một JVM server mới cho mỗi lượt, `-Xms128m -Xmx512m`; database, metrics và cổng loopback riêng, không dùng runtime/tank.db.
- Một lượt phải có đủ 4 client END(reason=0), không disconnected, last snapshot không chậm quá 30 tick so với kết thúc và không có lỗi giải mã. Nếu không đạt, dừng, giữ dữ liệu lỗi; không âm thầm bỏ mẫu.
- Pilot riêng 4 lượt × 6 giây, warm-up 2 giây, không gộp với kết quả chính thức.

Đây là benchmark thăm dò với 3 lần lặp và thời lượng ngắn hơn kế hoạch 180 giây/5 lần ban đầu trong bản thảo. Báo cáo phải ghi thông số thực tế; chưa đủ để tuyên bố ý nghĩa thống kê hoặc tải tối đa.

## Cách chạy

Chạy CMD tại gốc repository đã clone/giải nén (nơi có source, scripts, config), với JDK 21 trong PATH và JAR đã build. Chỉ chạy java sau khi javac báo thành công:

```cmd
if not exist runtime\benchmark-tools mkdir runtime\benchmark-tools
javac -encoding UTF-8 -cp "build\server\tank-server-1.0.0.jar" -d runtime\benchmark-tools scripts\benchmark\NetworkBenchmark.java
java -cp "runtime\benchmark-tools;build\server\tank-server-1.0.0.jar" tank.NetworkBenchmark report/benchmarks/new-output-directory 30 3
python scripts\benchmark\analyze.py report/benchmarks/new-output-directory
```

Phải kiểm tra exit code biên dịch trước khi chạy. Thư mục đầu ra phải chưa tồn tại; công cụ từ chối ghi đè. Với pilot, dùng một thư mục khác và tham số `6 1 pilot`. Trong môi trường JDK nhận sai đường dẫn tiếng Việt, dùng junction tạm không dấu trỏ tới project như đã ghi trong report/server_test_results.md.

Để chỉ chạy một mode, truyền tên mode ở tham số cuối, ví dụ `report/benchmarks/baseline-new 30 3 BASELINE`. Khi đó có 9 lượt (3 profile × 3 repeat), giữ nguyên lịch input và cách đo. Bỏ tham số này vẫn chạy cả bốn mode. Không ghi đè hoặc gộp mặc định lượt chạy lại vào bộ đối chứng 36 lượt trước.

## Dữ liệu và phân tích

Mỗi lượt giữ server.properties, trial.json, server.csv, clients.json, server.log và console.log. Các tài khoản `bench_0`…`bench_3` và database là dữ liệu thử tổng hợp. Không dùng tài khoản người chơi thật.

Analyzer kiểm tra:

1. Trial thành công, đủ client và không invalid/disconnected.
2. Đủ từng dòng metrics từ tick warm-up+1 tới trước tick cuối, không metrics_drop.
3. Chênh lệch input_ontime + input_missing bằng số client × số tick đo.
4. Phân tích mỗi lượt trước, sau đó tính mean và sample SD giữa 3 lượt; không coi từng tick là lần lặp độc lập.

Tick warm-up (150 với lượt chính thức) làm mốc bộ đếm. Cửa sổ đo chính là tick 151…899, 749 tick; loại tick 900 vì END có thể tới client trước khi Metrics.row hoàn tất, client đóng sớm có thể làm số đếm thành viên của dòng cuối giảm. Rate được tính bằng chênh lệch bộ đếm chia thời gian wall-clock giữa mốc tick 150 và tick 899.

Các percentile được nội suy tuyến tính trên mẫu theo lượt. Giá trị p95 trong bảng tổng hợp là mean của p95 từng lượt, không phải p95 gộp toàn bộ mẫu. RTT đo bằng 4 timestamp, mỗi client khoảng một ping/giây sau giai đoạn chuẩn bị; snapshot age dùng offset ước lượng, không phải one-way delay tuyệt đối. Snapshot/ping được lấy sau warm-up; các khoảng thu mẫu này không hoàn toàn trùng cửa sổ counter server.

`server_offered_udp_Bps` là byte frame ứng dụng server đưa vào transport, được đếm trước impairment, không phải byte thực tới client hoặc băng thông IP/Ethernet. `server_accepted_udp_Bps` là byte ứng dụng đi qua kiểm tra ở server sau impairment. Không cộng hai chiều để gọi là băng thông upload client trước mất gói. CPU dùng chênh lệch process_cpu_ns, quy đổi 100% là một lõi; heap là mẫu ở metrics writer, không phải RSS.

Input redundancy chủ động gửi lại input cũ nên số đếm duplicate/late có thể tăng như hệ quả bình thường. Không dùng riêng số late để suy ra tỷ lệ thao tác hiện tại thất bại; input_missing với mẫu số tick xử lý mới là chỉ số chính trong đối chiếu này. Cùng công thức input và seed không bảo đảm diễn tiến thế giới giống nhau sau khi input bị mất. Máy thử không được cô lập khỏi các tác vụ nền; cần tránh quy mọi chênh lệch p95 cho riêng thuật toán.

Đầu ra tổng hợp: runs.csv, summary.csv, summary.json, hashes.json. Giữ raw data và hash cùng báo cáo; không thay số đo chưa có bằng 0. Khi thay công cụ hoặc thiết kế, dùng thư mục lượt mới và mô tả thay đổi.
