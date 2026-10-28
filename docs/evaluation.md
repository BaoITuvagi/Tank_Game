# Thực nghiệm và bằng chứng

Đối chiếu thiết bị đo và harness trong source ngày **2026-10-23**, working tree dựa trên HEAD `e788610bdb0e2cd9fbd3f0cce54d9d29e815947e`, có thay đổi chưa commit. Các kịch bản dưới đây là phương pháp đề xuất cho báo cáo; **chưa được thực hiện trong tác vụ tài liệu này**.

Mục lục: [Tái lập](#reproducibility) · [Chỉ số](#metrics) · [Kịch bản](#scenarios) · [Kết quả](#results) · [Đóng góp](#contributions) · [Nguồn](#sources).

<a id="reproducibility"></a>
## Tái lập

Mỗi đợt đo cần ghi: ngày, mục tiêu, commit và patch/chỉ dấu working tree, hash bản build, JDK/Unity/OS, CPU/RAM, máy vật lý hay VM, client/server cùng máy hay khác máy, LAN/VPN, mode mạng, mode phòng, map/content hash, số người/phòng, thời lượng, seed và toàn bộ cấu hình impairment. Không ghi credentials hoặc dữ liệu người chơi vào báo cáo.

Build và checks trước khi đo theo [testing](testing.md#matrix); chạy trên cấu hình/dữ liệu thử nghiệm theo [deployment](deployment.md#local-demo). Raw CSV, JSON và log ứng dụng có thể nằm trong `runtime/` theo quy ước ignore. Khi lập báo cáo, lưu bộ dữ liệu đã loại thông tin riêng cùng biểu đồ và mô tả cách xử lý ở nơi nhóm thống nhất để người khác tái lập được; không chỉ dẫn tới file local sẽ mất sau khi clone.

Giữ các biến ngoài mục tiêu so sánh giống nhau. Kế hoạch khởi đầu: trận 180 giây, bỏ 10 giây đầu khi tổng hợp hiệu năng ổn định, ít nhất ba lần lặp cho mỗi cấu hình. Tách số liệu khởi động và phục hồi khỏi cửa sổ ổn định; không bỏ những lần thất bại khỏi bảng kết quả. Đây là quy ước đề xuất, không phải kết quả hoặc tiêu chí đã nghiệm thu của môn học.

<a id="metrics"></a>
## Chỉ số và nguồn thu thập

| Câu hỏi | Nguồn hiện có | Cách diễn giải |
|---|---|---|
| Loop có theo kịp lịch 30 Hz? | [Metrics.row/writeLoop](../source/server/src/main/java/tank/Metrics.java): `tick_us`, `schedule_lag_us`, `overrun`, `encode_us`, `send_us` | Báo p50/p95/p99 và tỷ lệ overrun; so với ngân sách tick khoảng 33,33 ms |
| UDP tại các điểm đo dùng bao nhiêu dữ liệu? | Metrics: `tx_bytes/rx_bytes`, `tx_packets/rx_packets`; [ClientCore](../source/client/Assets/Scripts/ClientCore.cs): `RxBytes/TxBytes`, `RxPackets/TxPackets` | Lấy chênh lệch counter theo thời gian, nêu rõ chiều truyền, đơn vị và phạm vi counter; không gọi đây là tổng lưu lượng TCP/UDP trên mạng |
| Delta và input redundancy có tác dụng gì? | Metrics: `full/delta/fallback/resync`, `input_ontime/missing/late/duplicate/conflict/drop`; [Protocol.Mode](../source/server/src/main/java/tank/Protocol.java) | So sánh cùng workload giữa các mode; tỷ lệ missing là bộ đếm input, không tự đồng nhất với tỷ lệ mất datagram |
| Trạng thái đến client có trễ/dao động? | ClientCore: `LastRttUs`, `LastStateAgeUs`, `LastJitterUs`, `RecoveryUs` | Cần lấy chuỗi mẫu theo thời gian mới tính percentile; smoke JSON hiện không xuất đầy đủ các trường này |
| Client phải phục hồi bao nhiêu? | ClientCore: `Decoded`, `Invalid`, `MissingBase`, `Corrections`, `PredictionResets`, `SequenceGaps` | Counter mô tả sự kiện trong implementation, không phải thước đo trực tiếp cảm giác mượt hoặc mọi dạng packet loss |
| Chạy hết trận và ghi lịch sử? | [SmokePlayer.Save](../source/client/Assets/Scripts/SmokePlayer.cs), [SmokeAcceptance.Passed](../source/client/Assets/Scripts/SmokeAcceptance.cs), log và lịch sử trận | Đọc cả JSON, exit code, match ID, kết quả cuối và lịch sử; smoke đạt không thay thế kiểm tra UI thủ công |
| Đo có làm mất mẫu hoặc mất gói ở hàng đợi? | Metrics: `metrics_drop`, `udp_drop`, `command_depth`, heap/GC/CPU | Nêu mẫu bị mất và tải đo; không coi file CSV thiếu hàng là đường truyền tốt hơn |

`Metrics` ghi các counter mạng toàn server vào từng sample của phòng; không cộng chúng qua các hàng/phòng như các counter độc lập. Heap, GC, CPU và `udp_drop` cũng có phạm vi tiến trình/server. Counter tích lũy cần lấy hiệu, không cộng trực tiếp. Các counter input là tổng của thành viên hiện có: rời/rejoin/reset trận có thể làm tổng giảm, nên chia cửa sổ theo match và membership trước khi tính hiệu. Trường `mode` trong CSV là **network mode**, khác Thường/Nhập vai; bổ sung room mode vào metadata của lần đo. `Metrics` mở file bằng writer mới nên cần tách tên/chép kết quả trước lần chạy tiếp theo.

Các counter byte/gói chỉ đo **UDP tại điểm instrument**, không gồm TCP hoặc overhead IP. `Server.udpSend()` đếm TX sau khi gọi transport nhưng trước khi impairment thực sự giao gói; `Server.route()` không đếm handshake và nhiều gói bị từ chối vào RX. Client có điểm đếm riêng trong `ClientCore.Network`/`SendUdp`; không mặc định các counter hai đầu có cùng phạm vi. Muốn đo tổng băng thông đường truyền cần phép thu thập riêng và ghi rõ công cụ/phương pháp.

Impairment có thể tác động hai chiều UDP. Phải ghi nơi bật, chiều và cấu hình; nếu bật cả server lẫn client, độ trễ quan sát có thể cộng qua nhiều lớp. Source hỗ trợ [UdpImpairment phía client](../source/client/Assets/Scripts/UdpImpairment.cs) và [UdpTransport phía server](../source/server/src/main/java/tank/UdpTransport.java). Không suy ra rằng mọi tham số client đều có cờ CLI hoặc UI tương ứng.

<a id="scenarios"></a>
## Các nhóm kịch bản

| Nhóm | Biến thay đổi | Giữ cố định và bằng chứng cần thu |
|---|---|---|
| So sánh phương án mạng | `BASELINE`, `INPUT_REDUNDANCY`, `DELTA`, `COMBINED` | Cùng map, số người, room mode, thời lượng và impairment; CSV, counter client và hoàn thành trận |
| Tải người chơi | 2, 4, 8 client trong một phòng | Cùng mode mạng; tick time, băng thông, CPU/heap/GC; không gọi 8 client cục bộ là kết quả 8 máy LAN |
| Chất lượng UDP | Không impairment; delay 50 ms/jitter 10 ms; loss 1% và 5%; một nhóm reorder/duplicate riêng | Dùng seed được ghi lại, bật một cấu hình mỗi lần; missing/resync, trạng thái client và hoàn thành trận |
| Mất mạng và vào lại | Ngắt một client khi các client khác vẫn giữ trận RUNNING, rồi reconnect trong giới hạn hiện có | Ghi thời điểm ngắt/phục hồi, đúng match/slot/life, snapshot/buff/deadline, kết quả và lịch sử |
| Chức năng Nhập vai/battlefield | Thường và Nhập vai; chọn đội khác nhau; có cả hai loại battlefield | Theo [kiểm tra thủ công](testing.md#manual), ảnh hoặc smoke JSON; ghi seed và tình huống thực tế, không suy luận từ ảnh đơn lẻ |
| Lỗi lưu dữ liệu | Theo fixture cô lập của [DatabaseTest.spoolAndRetry, databaseAndSpoolFailureKeepsResultUntilRecovery, queueOverflowSpoolsAndStartupRecovers](../source/server/src/test/java/tank/DatabaseTest.java) | Bằng chứng transaction/spool/retry và thông báo; không gây lỗi trên database đang sử dụng |

Các thiết lập impairment là tỷ lệ/xấp xỉ cấu hình của wrapper, không đảm bảo toàn bộ mạng thực tế có đúng tỷ lệ đó. Đọc giới hạn và điều kiện rejoin trong [protocol](protocol.md#rejoin) trước khi chọn thời lượng ngắt.

Chạy harness bằng [ví dụ smoke host/join](testing.md#smoke). `SmokeResult` xuất JSON acceptance, không xuất chuỗi mẫu RTT/jitter/recovery, bandwidth hoặc toàn bộ counter client; cần bổ sung phương pháp thu mẫu trong tác vụ thực nghiệm sau này để tính các percentile client nêu trên. Các test [DeltaTest](../source/server/src/test/java/tank/DeltaTest.java), [PredictionTest](../source/server/src/test/java/tank/PredictionTest.java), [NetworkRejoinTest](../source/server/src/test/java/tank/NetworkRejoinTest.java), [ReliabilityTest](../source/server/src/test/java/tank/ReliabilityTest.java) và [BattlefieldTests](../source/client/Assets/Tests/Editor/BattlefieldTests.cs) là điểm tìm hiểu workload/assertion; sự tồn tại của chúng không xác nhận kết quả lần chạy hiện tại.

<a id="results"></a>
## Kết quả và trạng thái bằng chứng

| Hạng mục | Trạng thái trong đợt tài liệu này | Còn cần |
|---|---|---|
| Đối chiếu tài liệu với implementation | Đọc source, script và định nghĩa tests theo chủ đề | Không thay cho runtime verification |
| Java/Unity suites | Không chạy | Lần chạy có ngày, revision/working tree, lệnh và kết quả gốc |
| Smoke multiplayer, LAN/VPN, playtest | Chưa thực hiện | Bản build phù hợp, môi trường và kết quả có thể tái lập |
| Số liệu latency/tải/băng thông và so sánh mode | Chưa thực hiện | Raw samples, số lần lặp, xử lý dữ liệu và bảng/biểu đồ |
| Khẳng định cải thiện hoặc cân bằng kỹ năng | Chưa đủ bằng chứng | Baseline rõ ràng và phép đo/playtest phù hợp |

Không nhập kết quả từ log cũ hoặc `plan/` vào bảng đã đạt nếu chưa xác định đúng bản build, working tree và kịch bản. Không tạo số liệu minh họa trông như kết quả đo thật. Khi có dữ liệu, thay trạng thái bằng liên kết bằng chứng và mô tả phạm vi kết luận.

<a id="contributions"></a>
## Baseline và đóng góp

`network.mode=BASELINE` là một phương án trong implementation để so sánh giao thức; nó **không xác định nguồn gốc code** hay chứng minh đóng góp của nhóm so với phần kế thừa.

| Nội dung có thể phân tích | Bằng chứng implementation | Cần trước khi viết thành đóng góp |
|---|---|---|
| FULL/DELTA, input redundancy và phục hồi | [Room.snapshot](../source/server/src/main/java/tank/Room.java), [ClientCore.GenerateInput/Reconcile](../source/client/Assets/Scripts/ClientCore.cs), [protocol](protocol.md) | Xác định phần kế thừa/phần nhóm viết; thực nghiệm so sánh mode |
| Authoritative simulation và prediction | [World](../source/server/src/main/java/tank/World.java), [GameState](../source/client/Assets/Scripts/GameState.cs), [architecture](architecture.md) | Giải thích quyết định, giới hạn và bằng chứng đồng bộ |
| Lưu kết quả có spool/retry | [Database](../source/server/src/main/java/tank/Database.java), [data](data.md#persistence) | Nguồn gốc thay đổi và kịch bản lỗi có kết quả |
| Nhập vai, item và battlefield qua mạng | [gameplay](gameplay.md), [protocol](protocol.md#snapshot) | Tách nội dung game khỏi đóng góp kỹ thuật mạng; chứng minh tính nhất quán và phục hồi |

Chưa có bảng xác nhận tác giả/phạm vi kế thừa trong bộ docs này. Không suy ra tính mới từ tên tính năng hoặc từ việc Git chỉ có một mốc lịch sử. Dùng [report-guide](report-guide.md#checklist) khi chuẩn bị báo cáo.

<a id="sources"></a>
## Nguồn và cách cập nhật

Nguồn chỉ số: `Metrics.row/writeLoop`, `Room.loop/snapshot`, `ClientCore`, `ClientCore.Network`, `UdpTransport`, `UdpImpairment` và `SmokePlayer.Save` đã dẫn ở trên. Thiết lập và log: [deployment](deployment.md#configuration). Quy tắc nghiệm thu chức năng: [testing](testing.md#evidence).

Cập nhật tài liệu này khi thực sự có phép đo mới hoặc định nghĩa chỉ số thay đổi; giữ cả hạn chế và lần chạy thất bại. Kết quả nghiên cứu cần trích nguồn riêng cho lý thuyết/giải pháp liên quan, không lấy docs nội bộ làm thay thế toàn bộ tài liệu tham khảo học thuật.
