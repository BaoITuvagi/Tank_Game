# Kết quả build server và kiểm thử Java

Ngày thực hiện: **15/10/2026**, múi giờ Asia/Saigon. Người dùng đã xác nhận build server và chạy bộ kiểm thử. Không sửa mã nguồn server/client trong lượt này.

## Kết quả tổng hợp

| Nội dung | Kết quả |
|---|---|
| Shared gameplay contracts | Match |
| Biên dịch | 16 tệp mã chính, 17 tệp kiểm thử; release 21 |
| Maven | BUILD SUCCESS; tiến trình kết thúc mã 0 |
| Lớp kiểm thử | 17 |
| Tests run | 77 |
| Failures / Errors / Skipped | 0 / 0 / 0 |
| Thời gian Maven báo | 01:14 phút; không phải thời gian thuần test hoặc benchmark |
| Hoàn tất | 15:44:39, 15/10/2026, UTC+07:00 |
| Artifact | tank-server-1.0.0.jar (`../build/server/tank-server-1.0.0.jar`; tài liệu/artifact cục bộ không kèm source GitHub) |
| Kích thước JAR | 14.598.385 byte |
| SHA-256 JAR | `6E646F06F6AB75FF7561F95BA6B0840AC559E672384BC8AAF2BBDC41883D8BE7` |

## Môi trường và cách chạy

- Java: Eclipse Adoptium 21.0.8, tại `C:\Java`.
- Maven: 3.9.14, từ bản có sẵn trong Maven wrapper cache của máy.
- Python: 3.10.8.
- Hệ điều hành theo Java/Surefire: Windows 11, version 10.0, amd64.
- Cache dependency dành cho lượt build: `Tank_Game/runtime/maven-repository`.
- Script: `scripts/build-server.ps1`, gọi `python scripts/sync-contracts.py --check` và Maven `clean package`, sau đó xuất JAR vào build/server.
- Dùng `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/build-server.ps1`; bypass chỉ áp dụng tiến trình chạy, không thay execution policy toàn máy.
- Đặt JAVA_HOME/PATH/MAVEN_OPTS/MAVEN_ARGS riêng cho tiến trình. Không sửa biến môi trường lưu lâu dài hoặc tệp script.

Lượt đầu gặp giới hạn mạng khi tải dependency. Sau khi quyền chạy được duyệt, Maven tải được thư viện. JDK báo native encoding Cp1252 và nhận sai đường dẫn workspace có dấu tiếng Việt, làm bước resources không tạo được đầu ra. Lượt thành công dùng junction tạm:

```text
C:\Users\admin\AppData\Local\Temp\tank-game-build-20261015
    → thư mục Tank_Game trong workspace
```

Đường dẫn này trỏ tới cùng mã nguồn, không tạo bản sao hoặc thay đổi gameplay. Script kiểm tra giới hạn đường dẫn build và thực hiện clean chỉ trên thư mục target của dự án. File JAR, log và kết quả vẫn nằm trong Tank_Game. Không sửa công cụ build để che lỗi đường dẫn.

## Kết quả theo lớp

Số liệu đọc lại từ các tệp `TEST-*.xml` của Surefire sau lượt BUILD SUCCESS.

| Lớp | Tests | Failures | Errors | Skipped | Thời gian theo Surefire (s) |
|---|---:|---:|---:|---:|---:|
| AfkRejoinAndHitTest | 9 | 0 | 0 | 0 | 3.305 |
| BaselineTest | 1 | 0 | 0 | 0 | 5.062 |
| DatabaseTest | 3 | 0 | 0 | 0 | 6.349 |
| DeltaTest | 2 | 0 | 0 | 0 | 0.164 |
| FailureTest | 7 | 0 | 0 | 0 | 4.736 |
| GameplayTest | 10 | 0 | 0 | 0 | 0.047 |
| GameplayUpgradeTest | 8 | 0 | 0 | 0 | 0.157 |
| GoldenInteropTest | 1 | 0 | 0 | 0 | 0.014 |
| MatchDurationTest | 3 | 0 | 0 | 0 | 1.206 |
| PlayerRecoveryTest | 4 | 0 | 0 | 0 | 2.504 |
| PredictionTest | 1 | 0 | 0 | 0 | 0.018 |
| ProtocolTest | 3 | 0 | 0 | 0 | 0.006 |
| ReliabilityTest | 7 | 0 | 0 | 0 | 6.309 |
| ReviewRegressionTest | 4 | 0 | 0 | 0 | 1.006 |
| RoomFeaturesTest | 6 | 0 | 0 | 0 | 5.163 |
| RoomGameplayUpgradeTest | 4 | 0 | 0 | 0 | 3.126 |
| RoomRefinementTest | 4 | 0 | 0 | 0 | 2.717 |
| **Tổng** | **77** | **0** | **0** | **0** | Không dùng làm số đo hiệu năng game |

## Bằng chứng và tệp sinh ra

- Log build/test của lượt thành công (`../runtime/server-build-test.log`; tài liệu/artifact cục bộ không kèm source GitHub).
- Thư mục XML và TXT Surefire (`../source/server/target/surefire-reports`; tài liệu/artifact cục bộ không kèm source GitHub).
- JAR đã đóng gói (`../build/server/tank-server-1.0.0.jar`; tài liệu/artifact cục bộ không kèm source GitHub).
- golden.tsv (`../source/server/target/interop/golden.tsv`; tài liệu/artifact cục bộ không kèm source GitHub): 4.366 byte.
- delta-trace.tsv (`../source/server/target/interop/delta-trace.tsv`; tài liệu/artifact cục bộ không kèm source GitHub): 530.791 byte.
- movement-trace.tsv (`../source/server/target/interop/movement-trace.tsv`; tài liệu/artifact cục bộ không kèm source GitHub): 88.558 byte.

build/, runtime/ và target/ là các thư mục sinh ra đang được .gitignore bỏ qua. Bản tổng hợp này được lưu trong report/ để giữ nội dung báo cáo; nếu đóng gói bằng chứng cần lấy thêm log/XML trước khi clean hoặc build lại vì chúng có thể bị thay thế.

## Diễn giải log và giới hạn

DatabaseTest cố ý bật database chỉ đọc và cản ghi spool ở test `databaseAndSpoolFailureKeepsResultUntilRecovery`. Vì vậy log có `RESULT NOT SAVED: DB and spool unavailable match=101`; sau đó test kiểm tra giữ pending và phục hồi. Cả 3 test trong lớp đều đạt. PowerShell biểu diễn stderr thành NativeCommandError trong tệp log không làm thay đổi kết quả XML hoặc mã kết thúc 0 của lượt build.

Shade plugin cảnh báo metadata trùng `module-info` và `MANIFEST.MF` giữa dependency. Build vẫn tạo JAR thành công; chưa khởi chạy JAR để xác nhận toàn bộ runtime sau đóng gói.

**Cập nhật sau lượt kiểm thử:** người dùng đã cho phép benchmark. Chính JAR có SHA-256 ghi trên đã được khởi chạy trong 36 lượt socket benchmark, tất cả kết thúc bình thường với 4 client. Xem [kết quả benchmark](benchmarks/network-20261015/results.md). Đây là bằng chứng bổ sung về runtime JAR, không thay đổi lịch sử kết quả build/test ở trên.

BaselineTest chạy luồng chức năng ở mode BASELINE qua socket loopback; không phải benchmark BASELINE vs COMBINED. Các fixture interop được Java tạo thành công nhưng chưa được bộ Unity đọc và xác minh trong lượt này. Chưa chạy Unity, demo nhiều máy, benchmark chịu tải hoặc thực nghiệm băng thông/độ trễ; cần xác nhận riêng trước khi tiếp tục các bước đó.

Đo byte ứng dụng/độ trễ trong benchmark sau đó đã được bổ sung ở liên kết trên. Unity, demo nhiều máy và khả năng chịu tải tối đa vẫn chưa được đánh giá.
