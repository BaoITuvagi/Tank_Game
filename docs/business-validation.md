# Kiểm tra và bằng chứng

Các reference tên test/preview cũ trong tài liệu là lịch sử; bộ test đó không còn trong source được theo dõi. Không suy ra suite đạt từ Maven khi hiện thông báo No tests to run. Kiểm tra chức năng bổ sung phải dùng database/tài khoản QA riêng, không dùng dữ liệu người chơi.

<a id="matrix"></a>
## Matrix nghiệp vụ v22

| Nhóm | Kiểm tra cần thực hiện |
|---|---|
| Phòng | Chủ phòng là người thật; thêm bot đúng ô; quyền host; retry; người vào chiếm ô đang cấp; phòng khóa; không tự lấp. |
| Sức chứa/phe | Người thật thay bot slot lớn nhất; giảm sức chứa giữ người thật; bot đợi chọn phe; kick giải phóng phe. |
| Tiếp quản | Rời/TCP/UDP/AFK; chờ 10 giây; xe vẫn chịu mô phỏng; giữ HP/Life/effect/Q/điểm/túi; không vượt khóa. |
| Vòng đời | Phòng đầy vẫn trở lại được; tất cả rời vẫn chạy; hết giờ không còn người thật đóng phòng. |
| Chiến dịch | Snapshot và RNG cho cùng diễn biến; pause/resume; restart với run chưa xong; lỗi DB không ACK trạng thái mới; run hỏng được giữ; cộng vàng đúng một lần. |
| Shop | Giới hạn toàn trận; dùng không reset quyền; lỗi/hủy hoàn vàng cho thử lại; startup hoàn reservation một lần. |
| Xếp hạng | Kill/death, đồng hạng 1,1,3, podium và lịch sử; không sửa rank trận cũ. |
| Tố cáo | Người gốc HUMAN được report kể cả AI thay; bot gốc bị chặn; từ lịch sử; chống trùng; admin timeline; retention 30 ngày giữ trận có đơn. |
| Wire/build | v22 từ chối v21; metadata phòng/kết quả/lịch sử; NORMAL/RP handshake; FULL/DELTA; source contract đồng bộ; build Java/Unity. |

## Lệnh kiểm tra có sẵn

Chạy PowerShell tại repo root:

```powershell
python scripts/sync-contracts.py --check
python scripts/check-admin-integration.py --server
python scripts/check-admin-integration.py --client
node --check source/server/src/main/resources/admin/app.js
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-server.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-client.ps1
git diff --check
```

ExecutionPolicy Bypass chỉ áp dụng tiến trình PowerShell trên, không đổi chính sách máy. Nếu sandbox chặn IPC Package Manager/Licensing, dùng cơ chế nâng quyền của môi trường cho đúng lệnh build; không đổi Unity/packages hoặc giả kết quả build thành công.

## Kiểm chứng ngày 08/11/2026

- Java: harness cục bộ **2.456 assertion đạt**. Trong đó 2.400 đối chiếu diễn biến snapshot/RNG, cùng kiểm phòng/bot/handover, hạng, giao dịch, rollback gây lỗi bằng trigger trong DB QA, migration/restart, giữ run chưa xong, bản lưu hỏng và retention.
- Hai instance **ClientCore C# thực** kết nối server Java qua loopback, COMBINED: trận NORMAL và ROLEPLAY đều lưu **1.800 tick**. NORMAL quan sát 117 FULL / 3.179 DELTA; ROLEPLAY 123 FULL / 3.475 DELTA; server invalid=0 ở cả hai trận. Đây là kiểm chức năng, không phải benchmark hiệu năng.
- NORMAL đã kiểm bot thủ công, quick join thay bot, rời/chờ 10 giây/AI/tiếp quản phòng đầy, metadata lịch sử và gửi tố cáo. Kết quả kill/death/rank đối chiếu trực tiếp DB.
- ROLEPLAY kết thúc toàn bộ kịch bản client với **22 check đạt**, client invalid=0/0: loadout, shop mở/mua/dùng/giữ giới hạn, pause/resume Chiến dịch và kết quả được lưu.
- Build server và Unity **6000.3.24f1** thành công; source C# còn được biên dịch độc lập với reference Unity, không lỗi. Build server không có suite Maven hiện hữu; số assertion ở trên là harness cục bộ riêng.
- Hai EXE Unity mới chạy `--ui-test` trên server/database QA, đều ghi `LOCAL_UI_LOGIN ... phase=LOBBY`, không có exception trong log smoke. Chạy batchmode/nographics nên không dùng kết quả này để khẳng định bố cục trực quan.
- Contract, hook quản trị, cú pháp JavaScript và diff whitespace đạt.
- Harness, database, log và file QA của lượt này được dọn sau kiểm chứng theo hướng dẫn repository; không thêm test/harness vào Git. Giữ bản build bàn giao.

Chưa kiểm tra trực quan các nút/màn mới trên các kích thước màn hình, chưa chạy browser thao tác admin cho timeline mới và chưa playtest cân bằng. Không triển khai server chính hoặc publish release trong lượt này.
