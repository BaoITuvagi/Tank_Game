# Xử lý tố cáo và biện pháp đối với tài khoản

Cập nhật 07/11/2026. Hướng dẫn này thay phần xử lý đơn ban đầu trong `quick-join-and-reports.md`.

## Người chơi gửi đơn

Form tự đóng khi nhận xác nhận **đã lưu** từ server, kể cả khi server xác nhận đơn đã tồn tại. Bảng kết quả hiện nút **Đã gửi** và thông báo ngắn trong 6 giây. Lỗi hoặc quá hạn vẫn giữ form để người chơi sửa/gửi lại.

## Lưu kết quả trên web làm gì?

| Lựa chọn | Dữ liệu và tác động sau khi lưu thành công |
| --- | --- |
| Chờ xem xét / Đang xem xét | Lưu tiến trình và ghi chú nội bộ. |
| Bác bỏ — chưa đủ cơ sở | Lưu lý do bác bỏ và gửi phản hồi chung cho người tố cáo. |
| Xác nhận vi phạm → Cảnh cáo | Lưu căn cứ và biện pháp; gửi lý do cảnh cáo cho tài khoản bị tố cáo, phản hồi chung cho người gửi. Người bị cảnh cáo vẫn được chơi. |
| Xác nhận vi phạm → Khóa có thời hạn | Lưu quyết định, chặn thao tác của phiên hiện tại, ngắt phiên sau khoảng 0,5 giây để có thời gian chuyển thông báo, chặn đăng nhập đến khi hết hạn. Khi đăng nhập bị từ chối, client hiện lý do và thời điểm hết hạn theo giờ Việt Nam. |

**Chờ xem xét**: đơn còn trong hàng chờ, chưa được đánh dấu bắt đầu kiểm tra. **Đang xem xét**: admin đã lưu trạng thái bắt đầu kiểm tra/đối chiếu bằng chứng. Chỉ mở chi tiết không tự đổi trạng thái. Hai trạng thái này đều chưa áp dụng hình phạt; khác nhau ở tiến trình làm việc của admin.

Thời hạn admin chọn: **1 giờ, 1 ngày, 3 ngày hoặc 7 ngày**. Khóa theo tài khoản. Khi hết hạn, server tự cho đăng nhập trở lại qua kiểm tra thời gian, kể cả sau khi restart. Mỗi biện pháp đang hiệu lực có nút **Gỡ biện pháp khóa này**, yêu cầu lý do và ghi lịch sử; các biện pháp khóa khác của cùng tài khoản vẫn có hiệu lực nếu chưa hết hạn/chưa gỡ.

Sau khi lưu thành công, cửa sổ quản trị tự đóng, hiện thông báo tác động cụ thể, tải lại danh sách và thống kê. Biểu đồ vẫn đếm đơn nhận vào theo ngày; trạng thái và gộp đơn không làm giảm số đơn đã nhận.

## Quy trình xem xét

1. Lọc **Chờ xem xét**, mở đơn và đọc trận, người chơi, lý do, mô tả. Chuyển **Đang xem xét** khi cần đối chiếu.
2. Xem các phản ánh liên quan và tiền sử biện pháp đã áp dụng. Ghi dữ liệu thực sự đã kiểm tra vào **Căn cứ xem xét / ghi chú nội bộ**. Số đơn không tự chứng minh vi phạm.
3. Chưa đủ cơ sở: bác bỏ và ghi lý do. Đã xác minh vi phạm: chọn cảnh cáo hoặc thời hạn khóa phù hợp với mức độ, tiền sử đã xác minh; ghi **Lý do gửi cho người chơi** riêng với ghi chú nội bộ.
4. Lưu. Với kết luận vi phạm, server yêu cầu biện pháp, lý do công khai ít nhất 5 ký tự và căn cứ nội bộ ít nhất 10 ký tự. Các trường đều có giới hạn độ dài.
5. Khi cần sửa quyết định khóa, mở lại đơn và gỡ đúng biện pháp với lý do kiểm tra lại. Lịch sử được giữ nguyên để truy vết.

Các đơn đang chờ/đang xem xét về **cùng tài khoản, cùng trận và cùng nhóm lý do** được gộp theo quyết định đầu tiên, không cộng thêm hình phạt cho cùng vụ việc. Đơn đến sau có cùng các tiêu chí cũng được liên kết với quyết định đó. Phản ánh có nhóm lý do khác vẫn chờ xem xét riêng để tránh bỏ sót hành vi khác. UI hiển thị mã đơn gốc. Đơn đã có biện pháp được khóa phần ra quyết định để tránh xử phạt lại.

Lịch sử dùng các khối riêng cho admin, thời điểm, trạng thái/biện pháp và ghi chú; có giới hạn vùng cuộn. Tiêu đề và nút đóng/lưu giữ ngoài vùng nội dung cuộn để không đè ghi chú. Phần đơn khác mặc định là **10 đơn gần nhất**; nút **Xem toàn bộ** duyệt tất cả đơn khác về đúng tài khoản bị tố cáo, phân trang 25 đơn, không gồm đơn đang mở. Có thể quay lại 10 đơn gần nhất.

## Thông báo và tính nhất quán

- Cảnh cáo và phản hồi được lưu trong `account_notifications`. Nếu người chơi online, server đưa thông báo vào hàng đợi TCP ngay; nếu offline, gửi sau đăng nhập thành công. Đưa vào hàng đợi không có nghĩa người chơi đã đọc.
- Phản hồi cho người tố cáo chỉ thông báo kết quả chung. Lý do xử phạt gửi cho tài khoản bị xử lý; ghi chú nội bộ chỉ nằm trong web quản trị.
- Thông báo khóa đã hết hạn hoặc đã gỡ được bỏ qua khi đăng nhập để không báo sai rằng tài khoản còn bị khóa.
- Quyết định, biện pháp, gộp đơn và thông báo được ghi trong transaction SQLite. Từ chối lưu thì không có hình phạt mới.
- Mỗi lần lưu dùng mã yêu cầu chống trùng và phiên bản đơn. Retry cùng yêu cầu trả lại xác nhận đã lưu; không thêm lịch sử, không kéo dài khóa và không gửi cảnh cáo lần hai. Nếu admin khác đã cập nhật đơn, UI yêu cầu mở lại dữ liệu mới.
- `account_sanctions` giữ biện pháp, thời hạn, admin, lý do và thông tin gỡ khóa. Schema tự bổ sung cột/bảng khi mở database cũ; dữ liệu tố cáo/lịch sử cũ được giữ lại. Bản ghi cũ chỉ có trạng thái **Đã xem xét** không được coi là đã áp dụng hình phạt.

## Căn cứ tham khảo công khai

- [Riot — Instant Feedback System](https://support.riotgames.com/en-us/league-of-legends/penalties/instant-feedback-system-faq): xem xét bằng chứng hành vi trước hình phạt; có thông báo cho người bị xử lý. Dự án áp dụng nguyên tắc xác minh và phản hồi, với quyết định thủ công của admin.
- [Riot — Suspension & Ban FAQ](https://support.riotgames.com/ms-my/league-of-legends/penalties/league-of-legends-suspension-ban-faq): phân biệt hạn chế và khóa có thời hạn, thông báo lý do, cân nhắc mức độ vi phạm và các bước xử lý trước đó.
- [Activision — Voice Chat Moderation](https://support.activision.com/content/atvi/support/web/en/articles/call-of-duty-voice-chat-moderation/.html): thông tin vi phạm/hình phạt và thời hạn cần rõ cho người chơi. Dự án tách lý do công khai khỏi căn cứ nội bộ.

Các mốc 1 giờ/1/3/7 ngày là lựa chọn triển khai cho Tank Battle, không được trình bày như quy định chung của các game tham khảo.

## Kiểm chứng bản này

- Java: **283/283** test đạt, gồm quy trình cảnh cáo, khóa phiên online/đăng nhập, lưu qua restart, hết hạn, gỡ khóa, idempotency, dữ liệu thay đổi giữa hai admin và gộp đơn cùng vụ.
- Unity: **347/347** đạt, gồm kiểm form giữ mở trước xác nhận và tự đóng sau receipt, giữ thông báo thành công.
- Build client online/local và server thành công. Hai client local chơi 60 giây, observedTick=1800, invalid=0, nhận hai mã đơn hợp lệ.
- Chrome headless dùng profile và database QA riêng: **7 mục lịch sử**, viewport **1440×1000** và **960×600**; kiểm tọa độ khối ghi chú không đè metadata/các hàng khác, footer luôn trong viewport. Đã xem ảnh `runtime/moderation-qa/history-960.png` và `suspension-revoked.png`.
- Trình duyệt test thực hiện cảnh cáo, khóa 1 giờ và gỡ khóa chỉ với tài khoản fixture, xác minh dialog tự đóng sau lưu, nội dung HTML được escape, không có lỗi JavaScript.

Script có thể chạy sau một trận smoke sinh hai đơn trong một môi trường QA mới:

```powershell
npm install --prefix runtime/moderation-browser-tools --no-audit --no-fund playwright-core
node scripts/test-moderation-browser.cjs runtime/moderation-qa
```

Script sử dụng Chrome cài tại đường dẫn Windows mặc định và chỉ cho request tới origin loopback của QA. Không dùng profile/tài khoản trình duyệt của người dùng. Chưa triển khai lên server chính.
