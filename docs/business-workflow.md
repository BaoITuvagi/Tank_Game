# Nghiệp vụ Tank Battle — protocol 22

Cập nhật 08/11/2026. Mô tả source hiện tại; bản phân phối cần build server/client cùng đợt. Các bằng chứng build và kiểm tra ghi ở [testing](business-validation.md#matrix); chưa kết luận cân bằng từ kiểm tra chức năng.

## Người tham gia và phòng chờ

- Chủ phòng là người thật. Nguồn gốc participant là HUMAN/BOT, không đổi khi quyền điều khiển chuyển HUMAN → WAITING_RETURN → AI → HUMAN.
- Chỉ chủ phòng bấm chuột trái ô trống để thêm AI đúng slot. Không tự thêm bot theo thời gian, không checkbox hay đếm ngược. Phòng có mật khẩu vẫn thêm AI thủ công được.
- Cấp danh tính bot qua DB worker; ô chờ được đánh dấu. Retry cùng mã không cấp thêm bot; người thật chiếm ô hoặc thay đổi cấu hình trong lúc chờ làm yêu cầu bị hủy.
- Vào trực tiếp, ID và tìm nhanh đều thay bot có slot lớn nhất khi phòng chờ đầy. Toàn người thật thì báo đầy. Người ngoài không vào trận đã chạy.
- Nhập vai mỗi phe duy nhất. Bot đợi người thật chọn trước; nếu muốn đổi sang phe bot đang giữ, chủ phòng kick bot đó. Không tự đổi phe bot.
- Giảm sức chứa phải đủ người thật, loại bot dư và sắp lại slot ở phòng chờ. Giữ chủ phòng/phe, hủy sẵn sàng. Map/chế độ cố định suốt vòng đời phòng.

## Rời, AFK và trở lại

- Chủ động rời/mất TCP: chờ 10 giây từ lúc server nhận biết. Mất UDP: ngưỡng phát hiện 10 giây hiện tại, sau đó mới chờ thêm 10 giây.
- AFK giữ cảnh báo 15 giây sau 15 giây không hoạt động; hết cảnh báo chuyển chờ 10 giây. Phiên AFK còn kết nối được xem trận và bấm Tiếp quản xe.
- Khi chờ, input xe trung lập. Sát thương, đạn đã bắn, kỹ năng đã gọi, hiệu ứng, hồi chiêu, chết/hồi sinh vẫn được xử lý bình thường.
- Hết 10 giây AI dùng cùng xe. Trở lại cần đăng nhập và đồng bộ UDP/loadout; trong lúc tải không dừng AI hoặc gia hạn chờ.
- Handover không gọi spawn: giữ vị trí, Life, HP/năng lượng, khiên, buff/debuff, Q, điểm, túi và quyền mua. Một participant chỉ có một dòng kết quả.
- AI thay người không tự mua hoặc dùng túi; vẫn nhặt item trên map. Người quay lại chỉ điều khiển slot của chính tài khoản đó.
- Tất cả người thật rời thì trận vẫn chạy đến hết giờ. Sau trận giữ người còn kết nối, loại người vắng; không còn người thật kết nối thì đóng phòng.
- Kick ở phòng chờ không tạo quyền vào lại. Khóa tài khoản chặn điều khiển/đăng nhập; không phục hồi trận Multiplayer sau server restart.

## Chiến dịch và vàng

Mỗi tài khoản có tối đa một chuyến đi chưa kết thúc. Tự lưu sau thao tác, kể cả đổi hướng không tiến lượt và mua đồ. Snapshot có phiên bản chứa seed/RNG, xe, địch, loot, danger, buff, cooldown và các bộ đếm sinh nội dung; không replay FX cũ.

Lệnh có run ID/revision/mã yêu cầu. Server chỉ xác nhận sau DB commit; retry không lặp hành động hoặc phần thưởng, lưu lỗi giữ bản đã xác nhận trước đó. Bản không đọc được báo lỗi và giữ dữ liệu.

Lưu và thoát hoặc ngắt kết nối tạm dừng, không kết thúc run và không cộng vàng. Có thể chơi Multiplayer sau tạm lưu rồi tiếp tục chuyến đi. Thời gian offline/tạm dừng không cộng vào thời gian xếp hạng. Thắng/chết/chủ động kết thúc mới ghi kết quả và cộng vàng còn lại đúng một lần, cùng transaction với snapshot cuối.

## Shop và xếp hạng

Shop RP vẫn mở mỗi 40 giây trong 20 giây, giá 50 vàng/loại. Mỗi loại chỉ mua một lần/người/trận; chết, đổi đợt, AI tiếp quản hoặc trở lại không reset. Giao dịch hủy/hoàn tiền không tiêu quyền mua. Nhặt trên map độc lập; túi chỉ thuộc trận.

Trận mới xếp hạng kill giảm dần, death tăng dần; bằng cả hai đồng hạng kiểu 1,1,3. Slot chỉ sắp thứ tự vẽ. Podium hiển thị các participant cùng hạng với đúng huy hiệu. Trận cũ giữ hạng đã lưu, không tính lại theo luật mới.

## Tố cáo và bằng chứng

Tố cáo từ kết quả hoặc chi tiết lịch sử, chỉ với người đã cùng tham gia. Bot gốc không được tố cáo ở cả UI/server. Participant gốc người thật được tố cáo kể cả từng do AI điều khiển.

DB giữ chat, rời/AFK/trở lại, chuyển quyền và thay đổi kill/death; không phải replay hoặc hệ thống tự phát hiện gian lận. Admin xem tick, slot, driver và nội dung; phải phân biệt hành vi người/AI. Đơn cũ về bot được giữ nhưng không tạo thêm hình phạt cho bot.

Bằng chứng mặc định 30 ngày. Khi trận có đơn, bằng chứng của trận được giữ cùng đơn. Dọn bằng chứng không xóa lịch sử trận hay quyết định. Dữ liệu cũ/đã hết hạn hiển thị không có bằng chứng, không suy diễn từ số đơn.
