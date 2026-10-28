# Context dự án Tank Battle

Đối chiếu: **2026-10-23**, working tree dựa trên HEAD `e788610bdb0e2cd9fbd3f0cce54d9d29e815947e`, có thay đổi chưa commit. Đây là bản định hướng để chọn phần cần đọc; không phải bằng chứng build, test hoặc thực nghiệm đã đạt.

Mục lục: [Tổng quan](#overview) · [Nguyên tắc kiến trúc](#principles) · [Nguồn chính](#sources) · [Hiện trạng](#status) · [Cách đọc](#reading).

<a id="overview"></a>
## Tổng quan

Tank Battle là bài tập lớn môn Lập trình mạng: game xe tăng nhiều người chơi, server Java và client Unity trên Windows. Một server dùng chung quản lý tài khoản, phòng, trận và lịch sử trong SQLite; client kết nối qua LAN/VPN hoặc chạy cùng máy để thử nghiệm.

Implementation có đăng ký/đăng nhập, tạo và tham gia phòng, ready, trận Thường/Nhập vai, kỹ năng Q theo đội, item, Red Zone/Bombstorm, kết quả, lịch sử và vào lại trận khi còn đủ điều kiện. Xem luật và giới hạn tại [gameplay](gameplay.md), không dùng các ý tưởng trong `plan/` làm danh sách tính năng đã có.

Code đã merge thêm Chiến dịch với quốc gia, map, shop và bảng xếp hạng riêng, qua `CampaignSession` trên server và `CampaignClient`/`TankCampaign*` trên client. Hướng dẫn chơi ở [README](../README.md#chế-độ-chiến-dịch). Sau merge/stash, cần build lại cả hai ứng dụng và khởi động lại để dùng code/map mới; xem [triển khai](deployment.md).

Vàng còn lại từ Chiến dịch tích lũy vào ví tài khoản, hiển thị bằng huy hiệu đồng xu/số dư/+ và dùng cho shop Nhập vai (E). Item RP sinh mỗi 40 giây, mỗi đợt 20 giây trong vòng tròn trung tâm map; shop chỉ mua được trong các đợt đó, mỗi loại tối đa một lần/người/đợt. Mua cất vào túi của trận, bấm icon sidebar để dùng sau; giữ túi qua chết/rejoin. `RoleplayShop` kiểm tra trên room thread, `WalletStore` trừ/hoàn cùng ví `campaign_profiles` trên DB worker; xem [gameplay/items](gameplay.md#items), [data/accounts](data.md#accounts) và cặp TCP 86/87 ở [protocol](protocol.md#tcp).

<a id="principles"></a>
## Nguyên tắc kiến trúc

- Server quyết định trạng thái gameplay. `Room` quản lý vòng đời và loop 30 Hz; `World` mô phỏng di chuyển, chiến đấu và các hiệu ứng.
- TCP mang yêu cầu tài khoản/phòng, điều khiển vòng đời và lịch sử. UDP mang input, đồng hồ và snapshot. Hai bên phải tương thích version, content và map hash.
- Client `ClientCore` nhận mạng, dự đoán và reconciliation; `GameState` chứa mô hình, movement và interpolation. Sửa movement/collision hoặc wire phải xem cả Java và C#.
- Unity khởi tạo bằng `TankApp`, scene `Main.unity`, UI dùng IMGUI. Bảo toàn setup hiện có, GUID `.meta`, importer và giấy phép tài nguyên.
- UI Multiplayer và Chiến dịch dùng chung phong cách Cartoon Military Arcade: palette navy/slate, nhiệm vụ sand, nút kem/vàng/mint/coral và font Bungee/Nunito (2026-10-25–26). `CartoonUi` chia sẻ style, icon, badge, portrait, huy chương và thanh tiến độ; `CampaignUI` là lớp style theo ngữ nghĩa của Chiến dịch. Panel/thẻ phẳng, viền steel gray `#7A8899` mỏng 1 px; nút có viền đen mảnh và bóng đổ mềm theo phản hồi cuối. Chữ giữ tương phản, nội dung dài được đo để xuống dòng/cuộn; không có tooltip theo chuột. HUD có trạng thái kỹ năng và xác nhận rời/kết thúc trận; Ping/FPS là chữ gọn, HP/năng lượng Multiplayer không lặp trong sidebar. Bố cục arena/ngắm, map, asset xe và luật server giữ riêng. Chi tiết và bằng chứng kiểm tra ở [client](client.md#ui) và [UI validation](ui-validation.md).
- SQLite chỉ nằm trên server. Kết quả trận đi qua worker database; lỗi ghi có cơ chế spool/retry, không đồng nghĩa mọi thao tác lưu đều thành công.

Luồng và điểm vào cụ thể: [kiến trúc](architecture.md#data-flow), [bản đồ source](architecture.md#source-map).

<a id="sources"></a>
## Nguồn chính

| Cần biết | Nguồn cần đọc |
|---|---|
| Luật, kỹ năng, item, battlefield | [gameplay.md](gameplay.md); thông số gốc: [game-rules.json](../source/shared/game-rules.json), [roleplay-content.json](../source/shared/roleplay-content.json) |
| Wire, version/hash, sinh Java/C# | [protocol.md](protocol.md#shared-contracts); [sync-contracts.py](../scripts/sync-contracts.py) |
| Database và phục hồi kết quả | [data.md](data.md); [schema.sql](../source/server/src/main/resources/schema.sql) |
| Unity, UI, asset và preview | [client.md](client.md) |
| Cài đặt, build, cấu hình và chạy | [README](../README.md), [deployment.md](deployment.md) |
| Checks và thực nghiệm | [testing.md](testing.md), [evaluation.md](evaluation.md) |
| Yêu cầu môn học và viết báo cáo | [report-guide.md](report-guide.md) |

Mỗi chủ đề giữ một mô tả chính; code/JSON/schema là căn cứ đối chiếu implementation. Khi gặp khác biệt, ghi rõ điều đã quan sát và điều chưa xác minh, không tự sửa code hoặc yêu cầu để làm chúng trùng nhau.

<a id="status"></a>
## Hiện trạng và giới hạn

- Dự án đang phát triển; mốc HEAD không bao gồm toàn bộ working tree hiện tại. Kiểm tra trạng thái file liên quan trước khi dựa vào bản tóm tắt.
- Có test Java, Unity EditMode, fixture interop và harness smoke/preview. Việc có test/harness không chứng minh phiên bản hiện tại đã pass; tác vụ biên soạn docs không chạy Maven/Unity.
- Cảm giác chơi, cân bằng kỹ năng và số liệu mạng cần bằng chứng riêng. [Evaluation](evaluation.md#results) ghi rõ những kết quả chưa thực hiện.
- Các tài liệu `plan/` là lịch sử/đề xuất có phần đã lỗi thời. Chi tiết máy, kết quả chạy tạm và việc dở không lưu trong context chung.

<a id="reading"></a>
## Cách đọc và duy trì

Phiên mới đọc bản tóm tắt này rồi theo bảng tác vụ trong `AGENTS.md`; chỉ mở các section, source và tests liên quan. Đối chiếu Git status và yêu cầu hiện tại trước khi tiếp tục.

Cập nhật context khi mục tiêu, kiến trúc hoặc khả năng chính thay đổi. Sửa chi tiết tại tài liệu chủ đề.
