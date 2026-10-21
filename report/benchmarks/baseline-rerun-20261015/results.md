# Kết quả benchmark socket Tank Battle

Đã xác minh **9 lượt hợp lệ**, 3 nhóm cấu hình. Các giá trị bảng là mean ± sample SD giữa các lượt; p95 được tính riêng từng lượt trước khi tổng hợp.

## Lưu lượng, input thiếu và thời gian xử lý

| Profile | Mode | n | Server TX ứng dụng (KiB/s) | Input thiếu (%) | Tick p95 (µs) | RTT p95 (ms) |
|---|---|---:|---:|---:|---:|---:|
| clean | BASELINE | 3 | 54.72 ± 0.01 | 0.000 ± 0.000 | 503.9 ± 22.3 | 0.46 ± 0.04 |
| loss5 | BASELINE | 3 | 54.04 ± 0.72 | 5.263 ± 0.341 | 266.0 ± 3.0 | 0.54 ± 0.02 |
| delay50_jitter20_loss5 | BASELINE | 3 | 52.50 ± 0.25 | 5.085 ± 0.502 | 272.4 ± 11.9 | 146.12 ± 2.97 |

## Chi phí server và đồng bộ

| Profile | Mode | Delta (%) | Encode p95 (µs) | CPU server (% một lõi) | Snapshot age p95 (ms) |
|---|---|---:|---:|---:|---:|
| clean | BASELINE | 0.00 ± 0.00 | 33.0 ± 2.0 | 7.09 ± 1.17 | 0.34 ± 0.01 |
| loss5 | BASELINE | 0.00 ± 0.00 | 33.5 ± 0.5 | 9.45 ± 2.71 | 0.46 ± 0.03 |
| delay50_jitter20_loss5 | BASELINE | 0.00 ± 0.00 | 35.0 ± 1.0 | 9.68 ± 1.54 | 78.26 ± 1.26 |

## Đối chiếu với BASELINE

Bộ dữ liệu này chỉ chạy BASELINE. Không có phép đối chiếu cải tiến trong cùng lượt; không tự gộp với bộ benchmark bốn mode trước đó.


## Phạm vi và giới hạn

- 4 client Java headless, một phòng, IPv4 loopback; server ở JVM riêng. Không chạy rendering/prediction Unity.
- Profile clean tắt impairment; loss5 chèn loss 5%; profile còn lại chèn loss 5%, delay 50 ms và jitter ±20 ms mỗi chiều tại server.
- Trận chính thức 30 giây, warm-up 5 giây, 3 seed; pilot 6 giây ở thư mục khác không gộp vào kết quả chính thức.
- Rate server lấy tick 150 làm mốc và đo tick 151–899 (749 tick/lượt). Dùng chênh lệch counter và thời gian wall-clock.
- TX là frame ứng dụng trước impairment, không gồm header mạng và không phải số byte thực tới client. RX chấp nhận tại server có trong runs.csv.
- RTT chỉ lấy ping/pong nhận được; snapshot age phụ thuộc offset ước lượng và không bằng độ trễ một chiều chính xác.
- Cùng seed không đồng nghĩa từng gói gặp cùng sự kiện loss giữa các mode, vì số lượng/thời điểm gói có thể khác.
- Các thế giới có thể diễn tiến khác vì input mất; đây là tác động thực của cơ chế mạng, không phải mọi snapshot có nội dung byte giống hệt.
- Warm-up ngắn, n=3 và máy không được cô lập khỏi tác vụ nền; chưa tuyên bố ý nghĩa thống kê, tải tối đa hoặc kết quả LAN/Internet.
- Nếu không có RESYNC thì không suy ra thời gian phục hồi từ giá trị 0; chưa có benchmark burst loss riêng.

## Dữ liệu tái lập

[Kết quả từng lượt](runs.csv), [bảng tổng hợp](summary.csv), [JSON tổng hợp](summary.json), [hash tệp](hashes.json), [môi trường](environment.json), [phương pháp và mã công cụ](../../../scripts/benchmark/README.md).

Mỗi thư mục con giữ trial.json, server.properties, server.csv, clients.json và log. Analyzer dừng nếu lượt không thành công, thiếu mẫu hoặc mẫu số input không khớp.
