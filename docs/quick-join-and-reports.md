# Vào trận nhanh và tố cáo người chơi

Cập nhật ngày 08/11/2026. Các tính năng này cần server và client được build từ source mới; build local không cập nhật server chính hay game do Launcher tải từ GitHub.

Quy trình xử lý mới (cảnh cáo/khóa có thời hạn, gộp đơn, phản hồi và chống lưu trùng) nằm tại [moderation-workflow.md](moderation-workflow.md). Phần kiểm chứng cuối tài liệu này ghi lại đợt tính năng ban đầu.

## Người chơi

- **Vào trận nhanh** nằm cạnh **Tạo phòng** ở sảnh multiplayer. Bấm nút mở bảng chọn chế độ và bản đồ; mặc định cả hai là **Bất kỳ**. Có thể chỉ chọn chế độ, chỉ chọn map hoặc chọn cả hai. Bấm **Tìm phòng** để server xáo trộn danh sách, chọn một phòng thỏa mọi điều kiện, đang chờ, còn chỗ và không có mật khẩu. Server kiểm tra lại trên room thread trước khi thêm thành viên. Không tự tạo phòng, không vào trận đã bắt đầu và không tự bật sẵn sàng.
- Nếu không có phòng phù hợp, bảng chọn giữ mở và thông báo để người chơi đổi điều kiện hoặc tạo phòng; không tự nới điều kiện. Khi phòng đổi trạng thái/đầy trong lúc tìm, server thử phòng khác cùng điều kiện; yêu cầu có giới hạn thời gian để không treo khi room đã dừng.
- Chọn map dùng cùng thẻ ảnh, tên và mô tả từ map thật như **Tạo phòng**, thêm lựa chọn **Bất kỳ bản đồ**. Vùng nội dung cuộn; nút Hủy/Tìm phòng luôn nằm riêng ở cuối bảng.
- Mỗi phòng hiển thị **ID #...** trong danh sách và trong phòng. ID do server cấp bằng bộ đếm SQLite nguyên tử, không trùng trong cùng database và không cấp lại sau restart. Restart vẫn đóng các phòng hiện tại; bộ đếm không có nghĩa lưu lại phòng. Local và server chính dùng database riêng.
- Bảng trận nhanh có ô **ID phòng**. Để trống thì tìm ngẫu nhiên theo bộ lọc; có nội dung thì lập tức đặt mode/map về **Bất kỳ**, làm mờ và vô hiệu hóa lựa chọn. Xóa hết ID cho phép chọn lại. ID phải là số 1–2147483647; không hợp lệ thì không gửi yêu cầu.
- Nhập ID hợp lệ thì chỉ tìm đúng ID đó, ưu tiên hơn mode/map. Phòng mất/đầy/không còn chờ thì báo không có phòng khả dụng, không chuyển sang phòng khác. Phòng có mật khẩu mở form mật khẩu chuẩn; ID không bỏ qua bảo vệ mật khẩu.
- Sau trận, các dòng người chơi khác dưới **Thành tích của bạn** có nút **Tố cáo** màu cam, căn giữa theo chiều dọc ở mép phải. Cột thống kê giữ cùng vị trí giữa các hàng; không cho tự tố cáo.
- Form có sáu lý do, được chọn nhiều mục: AFK/rời trận; phá trận/cố ý bị hạ; ngôn từ xúc phạm; gian lận/dùng phần mềm; tên không phù hợp; lý do khác.
- Các lý do chia hai cột có cùng chiều rộng; hai nút trong mỗi hàng dùng cùng chiều cao, tự xuống dòng khi nhãn dài.
- Mô tả tối đa 500 ký tự và 1.500 byte UTF-8, bắt buộc khi chọn **Lý do khác**. Hủy/Esc đóng form. Các nội dung phía sau form không nhận thao tác.
- **Gửi tố cáo** đợi xác nhận lưu từ server, sau đó tự đóng form, hiện thông báo mã đơn trong 6 giây và đổi nút thành **Đã gửi**. Nếu lỗi/quá hạn có thể gửi lại: server giữ một đơn cho mỗi bộ trận/người gửi/người bị tố cáo.

## Quản trị

Tab **Đơn tố cáo** có tìm kiếm theo tài khoản, tên hoặc mã trận; lọc trạng thái/lý do/ngày và phân trang 25 đơn. Chi tiết giữ tên và tài khoản hai phía, trận, map, chế độ, thời điểm, lý do, mô tả và lịch sử xử lý.

Admin dùng **Chờ xem xét**, **Đang xem xét**, **Bác bỏ** hoặc **Xác nhận vi phạm**. Kết luận vi phạm phải chọn cảnh cáo/khóa có thời hạn, nhập lý do và căn cứ; xem [quy trình và tác động sau khi lưu](moderation-workflow.md). Lịch sử lưu admin, trạng thái, biện pháp, ghi chú và thời gian trong `report_reviews`.

**Chờ xem xét** là đơn nằm trong hàng chờ, chưa được đánh dấu bắt đầu kiểm tra. **Đang xem xét** là admin đã chủ động lưu trạng thái bắt đầu đối chiếu bằng chứng; chỉ mở xem chi tiết chưa tự đổi trạng thái. Cả hai chỉ lưu tiến trình/ghi chú, chưa gửi cảnh cáo hay khóa tài khoản. Web giải thích riêng cho từng lựa chọn.

Chi tiết đơn hiển thị **10 đơn gần nhất về cùng tài khoản**, sắp thời gian mới nhất rồi mã đơn giảm dần, không gồm đơn đang mở. **Xem toàn bộ** dùng truy vấn đúng `target_id`, chia trang 25 đơn, có Trước/Sau và trở lại 10 đơn gần nhất. Không dùng tìm tên gần đúng nên không lẫn đơn mà người đó chỉ là người gửi. API `GET /api/report-related?id=<reportId>&page=0` yêu cầu đăng nhập admin.

Màn hình **Chọn mặt trận** căn giữa toàn bộ nhóm tiêu đề/hai thẻ trong vùng nội dung; tiêu đề, mô tả và trạng thái trên mỗi thẻ căn giữa.

Biểu đồ cột trong tab và trang **Kết nối trực tiếp** hiển thị số đơn mới mỗi ngày trong 14 ngày gần nhất, theo múi giờ Việt Nam. Có bảng số liệu thay thế. Các thẻ đếm tổng/chờ/đã xem xét/đã bác bỏ là toàn bộ thời gian; bộ lọc danh sách không thay đổi phạm vi biểu đồ. Một đơn có nhiều lý do vẫn chỉ được đếm một lần. Ngày không có đơn được điền số 0.

Thiết kế form tham khảo [Riot: Player Reporting Guide](https://support.riotgames.com/en-us/league-of-legends/security/player-reporting-guide-and-faq), với lý do và mô tả sự việc ngắn. Luồng hộp thư/chi tiết tham khảo [AccelByte: View and take action on reports](https://docs.accelbyte.io/gaming-services/tutorials/reporting-telemetry/view-take-action-reports/). Biểu đồ là lựa chọn cho dự án này để theo dõi lượng đơn, không coi số đơn là kết luận vi phạm.

## Giao thức và dữ liệu

Không đổi định dạng END hay map hash. Protocol vẫn là 21 với các opcode TCP bổ sung:

| Opcode | Payload sau header |
| --- | --- |
| 81 QUICK_JOIN | `u8 mode`: 255 = bất kỳ, 0 = Thường, 1 = Nhập vai; tùy chọn nối `u16 mapId`: 0/không có = bất kỳ, 2/3/4 = map đã chọn; có thể nối tiếp `u32 roomId`: 0/không có = tìm ngẫu nhiên. ID khác 0 ưu tiên, server bỏ mode/map. Payload cũ 1/3 byte vẫn được chấp nhận. Trả ACTION_RESULT cùng sequence rồi ROOM_STATE khi thành công. |
| 92 REPORT_PLAYER | `u64 match`, `u8 targetSlot`, `u8 reasons` (bitmask sáu lý do), `str details`. |
| 93 REPORT_RESULT | `u16 status`; thành công có `u64 reportId`, `bool duplicate`; lỗi có `str message`. |

`Golden.java` và test Java/C# kiểm tra 51 opcode, gồm tố cáo thành công, đơn trùng và lỗi. Client cũ tiếp tục dùng các opcode cũ; client mới cần server mới để dùng tính năng bổ sung.

Server lấy người gửi từ phiên đăng nhập và ánh xạ slot của người bị tố cáo từ kết quả trận có thẩm quyền; không nhận tên/tài khoản do client tự khai. Cả hai phải đã cùng tham gia trận, không được tố cáo chính mình. Kết quả pending được đăng ký trước khi gửi END để chấp nhận đơn gửi ngay sau trận. Giới hạn 100 đơn mới/tài khoản trong 24 giờ; retry trùng không tăng số đơn.

`player_reports` và `report_reviews` nằm trong database game SQLite, tự tạo khi mở database, kể cả khi web admin tắt. Không cần xóa database cũ. Các thao tác đi qua DB worker, không ghi đĩa trên room tick. Báo thành công chỉ sau khi insert hoàn tất. API `/api/reports`, `/api/report-detail`, `/api/report-summary` yêu cầu phiên admin; POST `/api/report-review` yêu cầu CSRF.

## Kiểm chứng lượt cập nhật ngày 08/11/2026

- Java **287/287** và Unity **350/350** test đạt. Test mới kiểm ID duy nhất khi nhiều yêu cầu cấp ID và sau restart; ID ưu tiên hơn bộ lọc; không vào phòng khác khi ID mất/đầy/đang đấu; không bỏ qua mật khẩu.
- API đơn liên quan được kiểm với 52 đơn qua 3 trang, đúng tài khoản bị tố cáo, không trùng/thiếu và không gồm đơn đang mở. Kiểm 401 khi chưa đăng nhập, 400 cho trang sai và 404 cho đơn không có.
- Hai client từ bản local mới chơi đủ 60 giây (`observedTick=1800`, `invalid=0`). Client thứ hai chọn ID của phòng Thường/map 4 trong khi truyền bộ lọc Nhập vai/map 3; vẫn vào đúng phòng theo ID. Hai client đều nhận mã tố cáo.
- 11 ảnh từ game thực trong `runtime/room-id-qa/`: ảnh map thật, nhập ID làm mờ/khóa bộ lọc, ID trong phòng/danh sách, hai cột tố cáo bằng nhau, chọn mặt trận căn giữa ở 1920×1000 và 960×600. Đã xem ảnh ở các kích thước này.
- Chrome test qua HTTP/database QA thật kiểm lại xử lý tố cáo và mục **Xem toàn bộ** với 52 đơn, 3 trang, quay về 10 đơn gần nhất, mở chi tiết đơn liên quan; không có lỗi JavaScript. Kết quả ở `browser-check.json` và `related-browser-check.json` trong thư mục QA.
- Build server và client online/local thành công; `build/BUILD_INFO.json` lưu hash bản đóng gói. Server QA đã dùng database riêng, không đổi tài khoản/dữ liệu local của người dùng và không triển khai server chính.

## Kiểm chứng lượt cập nhật bộ lọc và nút tố cáo ngày 07/11/2026

- Java **284/284** test đạt; `QuickJoinTest` có 4 bài, kiểm payload cũ, bộ lọc chỉ mode/chỉ map/cả hai, không tự nới điều kiện khi không có phòng, payload lỗi và tình huống room dừng/đổi trạng thái.
- Unity EditMode **348/348** đạt, gồm fixture Java có gói quick join kèm map và kiểm giá trị bộ lọc hợp lệ.
- Build online, local và server thành công. Hai client local chơi 60 giây trên **Thường + Sông Băng (map 4)**; client thứ hai gửi quick join với cả hai bộ lọc, cả hai đạt `observedTick=1800`, `invalid=0`, `mapMatched=true` và nhận mã tố cáo.
- Ảnh từ game thực ở 1600×900 và 960×600 trong `runtime/quick-filter-qa/`: nút tố cáo cam căn phải/căn giữa hàng, thống kê thẳng cột; bảng trận nhanh có lựa chọn Bất kỳ và lựa chọn riêng mode/map.
- Chrome test với database riêng xác minh giải thích khác nhau cho OPEN/IN_REVIEW, 7 mục lịch sử, cảnh cáo, khóa, gỡ khóa và tự đóng sau lưu. `runtime/quick-filter-qa/browser-check.json` không có lỗi JavaScript.
- `build/BUILD_INFO.json` và `LOCAL_BUILD_INFO.json` ghi nhận artifact, endpoint và hash bản hiện tại. Không triển khai server chính.

## Kiểm chứng đợt ban đầu ngày 07/11/2026

- Server: toàn suite 279/279 đạt sau cập nhật golden; lượt cuối 9/9 đạt cho QuickJoinTest (thêm trường hợp room dừng), PlayerReportsTest, GoldenInteropTest và NetworkRejoinTest. Tổng các XML hiện tại 280 test, không fail/error/skip.
- Unity EditMode: 346/346 đạt, gồm 6 bài PlayerReportsTests và fixture từ Java.
- Build online/local thành công. Kiểm ảnh sảnh, bảng kết quả có nút tố cáo và form ở 960×600/1600×900; giữ ảnh/log trong `runtime/report-feature-qa/`.
- Hai client local chơi một trận 60 giây: success=true, observedTick=1800, invalid=0 ở cả hai. Client thứ hai dùng quick join; cả hai nhận reportId từ server sau trận.
- `node scripts/test-admin-reports.cjs`: DOM mô phỏng chạy JavaScript quản trị với HTTP/SQLite server QA thật, kiểm đăng nhập, hai biểu đồ, hộp thư, tìm kiếm, lọc, chi tiết, lưu trạng thái/lịch sử, escape HTML và đối chiếu số đếm.
- Computer Use không xác định được URL trình duyệt Windows nên dừng kiểm tra trang web trực tiếp. DOM test không thay thế kiểm tra hiển thị trực tiếp trên trình duyệt; chưa kiểm LAN/VPN hay triển khai server chính.

Lệnh DOM test sau khi khởi tạo môi trường QA riêng có `server-process.json`/`credentials.json` và hai đơn từ smoke:

```powershell
npm install --prefix runtime/report-dom-tools --no-audit --no-fund jsdom
node scripts/test-admin-reports.cjs runtime/report-feature-qa
```

Chỉ dùng tài khoản/database thử cho script DOM. Helper QA nằm trong runtime, không thuộc bản game phát hành. Không có dữ liệu người chơi thật trong bộ kiểm chứng.
