# Kết quả benchmark socket Tank Battle

Đã xác minh **36 lượt hợp lệ**, 12 nhóm cấu hình. Các giá trị bảng là mean ± sample SD giữa các lượt; p95 được tính riêng từng lượt trước khi tổng hợp.

## Lưu lượng, input thiếu và thời gian xử lý

| Profile | Mode | n | Server TX ứng dụng (KiB/s) | Input thiếu (%) | Tick p95 (µs) | RTT p95 (ms) |
|---|---|---:|---:|---:|---:|---:|
| clean | BASELINE | 3 | 54.70 ± 0.02 | 0.000 ± 0.000 | 697.3 ± 126.8 | 0.62 ± 0.21 |
| clean | INPUT_REDUNDANCY | 3 | 54.71 ± 0.02 | 0.000 ± 0.000 | 786.9 ± 93.9 | 0.77 ± 0.10 |
| clean | DELTA | 3 | 31.99 ± 0.01 | 0.000 ± 0.000 | 1002.7 ± 24.6 | 0.71 ± 0.03 |
| clean | COMBINED | 3 | 31.98 ± 0.01 | 0.000 ± 0.000 | 1104.2 ± 197.8 | 1.04 ± 0.41 |
| loss5 | BASELINE | 3 | 52.45 ± 0.90 | 5.240 ± 0.208 | 387.3 ± 2.6 | 0.79 ± 0.09 |
| loss5 | INPUT_REDUNDANCY | 3 | 54.69 ± 0.02 | 0.000 ± 0.000 | 375.3 ± 34.5 | 0.83 ± 0.06 |
| loss5 | DELTA | 3 | 29.81 ± 0.72 | 5.374 ± 0.153 | 665.3 ± 26.7 | 0.83 ± 0.08 |
| loss5 | COMBINED | 3 | 31.93 ± 0.12 | 0.011 ± 0.019 | 649.1 ± 15.1 | 0.80 ± 0.01 |
| delay50_jitter20_loss5 | BASELINE | 3 | 53.23 ± 1.02 | 5.196 ± 0.190 | 377.5 ± 18.3 | 148.55 ± 1.42 |
| delay50_jitter20_loss5 | INPUT_REDUNDANCY | 3 | 53.33 ± 0.43 | 0.100 ± 0.067 | 385.8 ± 34.7 | 145.69 ± 3.07 |
| delay50_jitter20_loss5 | DELTA | 3 | 31.93 ± 0.41 | 5.029 ± 0.234 | 681.5 ± 97.0 | 138.19 ± 4.58 |
| delay50_jitter20_loss5 | COMBINED | 3 | 30.41 ± 1.33 | 0.211 ± 0.227 | 621.7 ± 23.6 | 141.57 ± 6.15 |

## Chi phí server và đồng bộ

| Profile | Mode | Delta (%) | Encode p95 (µs) | CPU server (% một lõi) | Snapshot age p95 (ms) |
|---|---|---:|---:|---:|---:|
| clean | BASELINE | 0.00 ± 0.00 | 40.6 ± 5.0 | 9.12 ± 1.64 | 0.48 ± 0.12 |
| clean | INPUT_REDUNDANCY | 0.00 ± 0.00 | 42.7 ± 5.8 | 8.51 ± 0.82 | 0.46 ± 0.07 |
| clean | DELTA | 96.80 ± 0.00 | 353.9 ± 8.9 | 15.44 ± 1.00 | 0.48 ± 0.02 |
| clean | COMBINED | 96.80 ± 0.00 | 384.7 ± 63.9 | 16.30 ± 0.40 | 0.65 ± 0.28 |
| loss5 | BASELINE | 0.00 ± 0.00 | 44.7 ± 2.9 | 10.93 ± 0.54 | 0.65 ± 0.11 |
| loss5 | INPUT_REDUNDANCY | 0.00 ± 0.00 | 43.3 ± 2.3 | 9.82 ± 0.38 | 0.61 ± 0.03 |
| loss5 | DELTA | 96.80 ± 0.00 | 316.4 ± 15.9 | 15.35 ± 0.35 | 0.66 ± 0.06 |
| loss5 | COMBINED | 96.80 ± 0.00 | 344.9 ± 17.2 | 16.94 ± 2.45 | 0.60 ± 0.04 |
| delay50_jitter20_loss5 | BASELINE | 0.00 ± 0.00 | 56.5 ± 5.0 | 11.52 ± 0.56 | 76.58 ± 2.33 |
| delay50_jitter20_loss5 | INPUT_REDUNDANCY | 0.00 ± 0.00 | 47.1 ± 7.4 | 10.24 ± 0.72 | 77.13 ± 0.85 |
| delay50_jitter20_loss5 | DELTA | 96.80 ± 0.00 | 338.3 ± 35.2 | 15.63 ± 1.41 | 79.58 ± 1.59 |
| delay50_jitter20_loss5 | COMBINED | 96.80 ± 0.00 | 313.7 ± 19.5 | 17.08 ± 0.76 | 76.45 ± 1.61 |

## Đối chiếu với BASELINE

- `clean`: COMBINED giảm 41.54% byte ứng dụng server đưa vào transport; input thiếu 0.000% → 0.000%.
- `loss5`: COMBINED giảm 39.12% byte ứng dụng server đưa vào transport; input thiếu 5.240% → 0.011%.
- `delay50_jitter20_loss5`: COMBINED giảm 42.87% byte ứng dụng server đưa vào transport; input thiếu 5.196% → 0.211%.

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
