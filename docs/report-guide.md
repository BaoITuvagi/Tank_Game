# Hướng dẫn dùng docs để viết báo cáo

Đối chiếu ngày **2026-10-23**. Nội dung dự án dựa trên working tree có thay đổi chưa commit, mốc HEAD `e788610bdb0e2cd9fbd3f0cce54d9d29e815947e`. Đây là bản đồ tài liệu và checklist chuẩn bị, chưa phải báo cáo hoàn chỉnh hoặc xác nhận đủ điều kiện nộp bài.

Mục lục: [Nguồn yêu cầu](#requirements) · [Ánh xạ báo cáo](#outline) · [Checklist](#checklist) · [Phần còn thiếu](#gaps) · [Nguồn tham khảo](#references).

<a id="requirements"></a>
## Nguồn yêu cầu môn học

Người dùng sẽ tự thêm nguyên bản vào `docs/final-project/`. Hiện ba file chưa có trong repo, nên các đường dẫn dưới đây là vị trí dự kiến, chưa tạo link:

| File dự kiến | Vai trò và mục cần đọc |
|---|---|
| `final-project/Instruction.md` | Yêu cầu chính; mục 8 cấu trúc báo cáo, 9 thực nghiệm, 10 Compilatio, 12 cấu trúc project, 13 README, 14 nộp source |
| `final-project/Topics.md` | Danh sách chủ đề; chủ đề 2 “Phát triển game mạng” phù hợp với loại dự án này |
| `final-project/README.md` | Mẫu nội dung nộp bài; không thay thế README Tank Battle ở gốc |

Các yêu cầu trên đã được đọc từ bộ tài liệu gốc bên ngoài repo khi tổ chức docs. Khi bổ sung bản gốc, giữ đúng tên file thực tế, kiểm tra lại nội dung/mục và cập nhật bảng thành link tương đối. Không sửa nguyên bản để phù hợp với implementation. Nếu bộ tài liệu mới khác bản đã đọc, đối chiếu lại checklist trước khi dùng cho bản nộp.

Yêu cầu đáng chú ý trong `Instruction.md`: báo cáo theo dạng technical/research paper, có kết quả đo thật, phần novelty/contributions kèm bằng chứng, trích dẫn và kiểm tra Compilatio; quy định chính ghi **Similarity + Plagiarism ≤ 20%**. Cần dùng đúng quy định giảng viên xác nhận khi nộp. Cấu trúc thư mục có thể thay đổi nếu dễ đọc/chạy; báo cáo PDF được mẫu hướng tới `report/report.pdf`. Tác vụ docs này không tạo PDF hay thư mục báo cáo rỗng.

<a id="outline"></a>
## Ánh xạ các phần báo cáo

| Phần báo cáo theo mục 8 | Nguồn chính trong dự án | Phần người viết cần bổ sung |
|---|---|---|
| Title, Abstract, Keywords | [context](context.md#overview), [evaluation](evaluation.md#results) | Tên nhóm/thành viên; tóm tắt kết quả chỉ sau khi có số liệu |
| Introduction, Problem Definition | [context](context.md), [gameplay](gameplay.md), [architecture](architecture.md#components) | Bối cảnh, bài toán mạng, giả định, mục tiêu và phạm vi đã thống nhất |
| Background / Related Work | [protocol](protocol.md), [nguồn tham khảo](#references) | Nguồn học thuật/chính thức về giải pháp liên quan, trích dẫn và so sánh |
| Proposed Approach, System Architecture | [architecture](architecture.md#data-flow), [concurrency](architecture.md#concurrency) | Giải thích quyết định và tradeoff, xuất sơ đồ ở dạng yêu cầu của bản nộp |
| Network Communication Design | [protocol](protocol.md#transport), [TCP](protocol.md#tcp), [snapshot](protocol.md#snapshot), [rejoin](protocol.md#rejoin) | Sequence cụ thể, request/response, định dạng và xử lý lỗi tiêu biểu |
| Implementation | [source map](architecture.md#source-map), [client](client.md), [data](data.md), [gameplay](gameplay.md) | Chọn module/thuật toán quan trọng; không chép toàn bộ source vào báo cáo |
| Experimental Setup | [evaluation](evaluation.md#reproducibility), [testing](testing.md#prerequisites), [deployment](deployment.md) | Môi trường thực tế, cấu hình/hash, workload, seed và số lần lặp |
| Results, Discussion | [evaluation](evaluation.md#metrics), [kết quả](evaluation.md#results) | CSV/JSON/log/ảnh thật, biểu đồ, phương pháp xử lý và giải thích giới hạn |
| Novelty and Contributions | [bảng đóng góp](evaluation.md#contributions) | Baseline kế thừa, phần nhóm bổ sung, tác giả/nguồn và bằng chứng |
| Limitations, Future Work | [context](context.md#status), [evaluation](evaluation.md#results) | Giới hạn quan sát được và hướng phát triển, không trình bày đề xuất như tính năng đã có |
| Conclusion, References, Appendix | Các phần đã có bằng chứng; [nguồn tham khảo](#references) | Kết luận đúng phạm vi dữ liệu; danh mục trích dẫn; protocol/schema/config/tests chi tiết |

Sơ đồ Mermaid trong docs là nguồn chỉnh sửa. Nếu bản PDF cần PNG/SVG thì xuất từ sơ đồ đó và kiểm tra hình trước khi nộp, không vẽ lại thành một nguồn thiết kế song song.

<a id="checklist"></a>
## Checklist chuẩn bị bài nộp

- [ ] Bổ sung và đối chiếu ba tài liệu môn học, chủ đề đăng ký chính thức, nhóm, lớp, giảng viên, hạn và cách nộp.
- [ ] Chốt phiên bản source/build/working tree dùng cho báo cáo; lưu patch nếu có thay đổi chưa commit.
- [ ] Người khác cài đặt và chạy được theo [README](../README.md) và [deployment](deployment.md); các dependency/asset/giấy phép được khai báo.
- [ ] Có kết quả Java/Unity và kiểm tra thủ công phù hợp kèm lệnh, ngày, revision và file kết quả theo [testing](testing.md#evidence).
- [ ] Có ít nhất một nhóm thực nghiệm mạng phù hợp yêu cầu môn học, raw data và cách tổng hợp theo [evaluation](evaluation.md#scenarios).
- [ ] Có bảng baseline/phần kế thừa/phần nhóm thực hiện, đóng góp và bằng chứng; không xem thay đổi hình ảnh đơn thuần là đóng góp mạng.
- [ ] Có phân tích ưu/nhược điểm, lỗi/thất bại, giới hạn và hướng phát triển.
- [ ] Có tài liệu tham khảo được trích trong nội dung, attribution của code/thư viện/tài nguyên.
- [ ] Đối chiếu checklist README của môn học với cách tổ chức README gọn hiện tại.
- [ ] Viết, kiểm tra và xuất báo cáo hoàn chỉnh; kiểm tra Compilatio theo quy định học phần.
- [ ] Đóng gói source, cấu hình mẫu, dữ liệu mẫu nếu cần, tests, kết quả và sơ đồ; không đưa credentials/database người dùng vào bài nộp.

Checklist để trống vì tác vụ này chỉ tổ chức tài liệu; việc có code hoặc docs không tự làm một mục đạt.

<a id="gaps"></a>
## Phần còn thiếu và khác biệt cần giữ rõ

**README gốc được giữ gọn theo yêu cầu người dùng:** mô tả dự án, cài đặt và link đọc tiếp. Mục 13 của `Instruction.md` còn yêu cầu architecture, configuration, database setup, run server/client, test, experimental results, novelty, limitations và references. Chi tiết tương ứng nằm trong docs; chưa có xác nhận rằng chỉ dẫn link là đủ đáp ứng mọi mục của mẫu nộp. Khi chuẩn bị bài nộp, đối chiếu với giảng viên và quyết định bản trình bày phù hợp; không tự mở rộng README trong tác vụ này.

Chưa xác nhận được chủ đề đã đăng ký, danh sách nhóm, baseline kế thừa, tác giả từng phần, tài liệu tham khảo học thuật, hạn/link nộp cuối cùng hoặc kết quả Compilatio. Không điền các thông tin này bằng suy đoán.

Chưa chạy thực nghiệm hay tạo báo cáo PDF. Kết quả test/log lịch sử hoặc các phương án trong `plan/` không mặc nhiên áp dụng cho working tree hiện tại. Xem trạng thái bằng chứng tại [evaluation](evaluation.md#results).

<a id="references"></a>
## Nguồn tham khảo và attribution

Nguồn implementation có thể truy ngược qua [architecture source map](architecture.md#source-map). Phiên bản thư viện lấy từ [pom.xml](../source/server/pom.xml) và [manifest Unity](../source/client/Packages/manifest.json); phiên bản Editor lấy từ [ProjectVersion.txt](../source/client/ProjectSettings/ProjectVersion.txt). Nguồn thông số và sinh contract ở [protocol](protocol.md#shared-contracts); schema ở [data](data.md#schema).

Nguồn/giấy phép Kenney và Cooked FX được dẫn tại [client — tài nguyên](client.md#assets). Giữ attribution trong bản build/báo cáo theo giấy phép. Khi dùng kiến thức hoặc hình/bảng từ bên ngoài, ghi tác giả, tiêu đề, URL/định danh và thời điểm truy cập; chỉ thêm tài liệu đã thực sự đọc và trích trong nội dung. Chưa có danh mục related work hoàn chỉnh trong đợt này.
