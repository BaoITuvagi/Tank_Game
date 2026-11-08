# Context dự án Tank Battle

Đối chiếu UI/asset và bố cục map multiplayer: **2026-11-07**, working tree dựa trên HEAD `08d6f756c6009b2c730e1babeb3cb138142e74fe` và thay đổi chưa commit. Đây là bản định hướng để chọn phần cần đọc; không phải bằng chứng build, test hoặc thực nghiệm đã đạt. Các chủ đề ngoài đợt cập nhật này giữ mốc đối chiếu tại tài liệu riêng.

Mục lục: [Tổng quan](#overview) · [Nguyên tắc kiến trúc](#principles) · [Nguồn chính](#sources) · [Hiện trạng](#status) · [Cách đọc](#reading).

Bổ sung nghiệp vụ **08/11/2026, protocol 22**: AI thêm thủ công theo slot; rời/AFK chờ 10 giây rồi AI giữ xe, trở lại tiếp quản nguyên trạng; Chiến dịch tự lưu từng thao tác và tạm dừng để chuyển chế độ; shop một lần/loại/trận; hạng kill/death đồng hạng; tố cáo từ lịch sử và bằng chứng 30 ngày. Quy tắc hiện hành tại [business-workflow.md](business-workflow.md), kiểm chứng tại [business-validation.md](business-validation.md#matrix).

<a id="overview"></a>
## Tổng quan

Bổ sung 07/11/2026: sảnh có **Vào trận nhanh** để vào ngẫu nhiên phòng Thường/Nhập vai đang chờ, còn chỗ và không khóa. Bảng kết quả có form tố cáo người chơi khác; server lưu đơn trong SQLite và web quản trị có hộp thư, xử lý trạng thái, biểu đồ số đơn 14 ngày. Xem [quick-join-and-reports.md](quick-join-and-reports.md) để biết wire, giới hạn và bằng chứng kiểm chứng.

Tank Battle là bài tập lớn môn Lập trình mạng: game xe tăng nhiều người chơi, server Java và client Unity trên Windows. Một server dùng chung quản lý tài khoản, phòng, trận và lịch sử trong SQLite; client kết nối qua LAN/VPN hoặc chạy cùng máy để thử nghiệm.

Implementation có đăng ký/đăng nhập, tạo và tham gia phòng, ready, trận Thường/Nhập vai, kỹ năng Q theo đội, item, Red Zone/Bombstorm, kết quả, lịch sử và vào lại trận khi còn đủ điều kiện. Xem luật và giới hạn tại [gameplay](gameplay.md), không dùng các ý tưởng trong `plan/` làm danh sách tính năng đã có.

Tạo phòng multiplayer có ba map dùng chung cho Thường/Nhập vai: **Tiền Đồn Xanh** (ID 2, `map-large.json`, mặc định), **Pháo Đài Cát** (ID 3) và **Sông Băng** (ID 4). `MapCatalog` hai phía nạp cùng danh mục `multiplayer-maps.json`; ảnh xem trước dùng chung renderer với trận. ID 2 giữ nền cỏ/vật liệu tường và cách vẽ nước gốc trước đợt sửa UI, bố trí lại thành sông dọc hai đoạn, hai hồ bên tổng 648 đơn vị², một cầu gỗ giữa sông, 12 đoạn tường trong và 36 gạch; 16 tháp/cây/đá kích thước khác nhau tạo bốn cụm trên đất có collider nhỏ. Map giữ 16 spawn, 21 item (chuyển hai điểm), 47 mục tiêu không kích; bố trí lại 18 mục tiêu lửa. ID 3 giữ bố cục, vật liệu cát/công sự và hai hồ xanh ngọc, tăng lên 18 cây cọ có collider thân nhỏ, chín cây ở hai góc bờ ngoài mỗi hồ. ID 4 có một hồ băng lớn bờ thuôn lệch giữa sân, tổng 648 đơn vị², không cầu; sáu cụm rừng thông quanh hồ và hai cụm nhà băng phía đông. Tường/gạch được bố trí lại thành 12 công sự ngắn và tám cụm gạch; thêm 44 collider cảnh vật (28 thông, tám đá tuyết, bốn tinh thể, bốn nhà băng), tổng 60 tường. Giữ 16 spawn, 18 item (chuyển bảy điểm), 48 mục tiêu không kích; bố trí lại 48 mục tiêu lửa. Map mặc định và sa mạc giữ nguyên byte theo bản đã được người dùng chốt. Presentation và nguồn ảnh nằm ở `ArenaBiomes`/`ArenaEnvironment`/`ArenaTerrain`, tái sử dụng `CampaignCartoon`; đợt bố cục 07/11 giữ PNG băng/cọ hiện có và bổ sung nhà băng riêng ở `ArenaWinter`, có nguồn/prompt/hash/importer; không dùng người tuyết. Cầu gỗ chỉ dùng ở map mặc định, vẽ bằng code trên khe khô có sẵn, không thêm luật hoặc collider.

Luật địa hình được áp dụng ở server và prediction client: nước map mặc định giảm 50% tốc độ; hồ ốc đảo không giảm tốc và hồi năng lượng Shift ×1,5 sau khi thả Shift/chờ 15 tick; băng tăng 50% tốc độ, không có quán tính. Nhịp hồi ốc đảo luân phiên 1/2 theo tick mô phỏng, không thêm trạng thái wire. Map cố định suốt vòng đời phòng, kể cả sửa cài đặt, rejoin và chơi trận tiếp. Protocol hiện là **22** sau đợt chuyển bão về phủ toàn map và thêm giảm tốc độ bắn do bão cát; `PREPARE` kiểm ID/hash byte gốc, còn content hash bao gồm luật địa hình/bão và được kiểm trong Nhập vai. Đợt bố cục 07/11 trước đó đổi hash ba JSON map do collider/bố cục mới; đợt bão hiện tại giữ nguyên các byte map đã chốt, nhưng cần build/phân phối server/client cùng protocol và contract mới. Map cũ ID 1 giữ nguyên JSON và đường vẽ, không nằm trong ba lựa chọn. Chi tiết tại [client](client.md#ui), [gameplay/maps](gameplay.md#maps) và [protocol](protocol.md#tcp).

Chat chung trong trận Thường/Nhập vai dùng TCP v22, hai tab Xếp hạng/Chat chia sẻ vùng thông tin phía trên sidebar; tên theo màu xe, báo tin chưa đọc và giữ tối đa 100 tin trong RAM để replay khi rejoin, có nhật ký bằng chứng trong DB 30 ngày (giữ cùng đơn khi bị tố cáo). Xem [client](client.md) và [protocol/chat](protocol.md#match-chat). Bằng chứng kiểm chứng từng đợt ghi riêng, không suy ra từ việc có tính năng.

Biến cố multiplayer theo map: Tiền Đồn Xanh giữ Red Zone/Bombstorm; Pháo Đài Cát thay Bombstorm bằng bão cát; Sông Băng thay Red Zone bằng bão tuyết. Hai bão cảnh báo 3 giây rồi phủ toàn map trong 12 giây; mọi xe sống chịu 2 HP tại các pulse giây 1/3/5/7/9/11, có xét giáp/bất tử/khiên. Bão cát giảm 50% tốc độ bắn (khoảng cách hai phát cơ bản 30→60 tick), không đổi tốc độ đạn, không ẩn xe hoặc gây chậm di chuyển; Fog kỹ năng giữ luật riêng. Bão tuyết nhân tốc độ thường/Shift ×0,5 toàn map (trên băng thành ×0,75 tốc độ nền đất khô). Thông báo giữa mép trên map ghi phạm vi toàn bản đồ; gió/hạt phủ viewport nhưng giữ xe, HP và thao tác dễ đọc. Khiên xuất hiện/hồi sinh bảo vệ mọi sát thương trong 90 tick; rejoin không gia hạn, các trạng thái vẫn áp dụng. Server sở hữu damage/timing; prediction client xét cùng wave và tick. Không thêm field snapshot, không đổi JSON map/database/IP. Xem gameplay/protocol/client và kiểm chứng v21; ảnh/kết quả v19/v20 chỉ là lịch sử, không chứng minh bản hiện tại.

Chiến dịch có quốc gia, map, shop và bảng xếp hạng riêng, qua `CampaignSession` trên server và `CampaignClient`/`TankCampaign*` trên client. Sau đăng nhập, màn chọn Chiến dịch/Nhiều người chơi là điều hướng local; chế độ chiến dịch giữ luật theo lượt nhưng dùng khung HUD sidebar 294 px và đồ họa cartoon cùng multiplayer. Asset bổ sung nằm ở `CampaignCartoon`; `ItemCartoon`/`ItemIconArt` ghép thùng/đạn Kenney với hình chức năng vẽ bằng code cho map/shop/túi; đồng xu đơn dùng chung cho vàng toàn client. Bản đồ và luật Chiến dịch giữ nguyên. Hướng dẫn chơi ở [README](../README.md#chế-độ-chiến-dịch), kiểm tra từng đợt ở UI validation.

Vàng còn lại từ Chiến dịch tích lũy vào ví tài khoản, hiển thị bằng huy hiệu đồng xu/số dư/+ và dùng cho shop Nhập vai (E). Item RP sinh mỗi 40 giây, mỗi đợt 20 giây trong vòng tròn trung tâm map; shop chỉ mua được trong các đợt đó, mỗi loại tối đa một lần/người/trận. Mua cất vào túi của trận, bấm icon sidebar để dùng sau; giữ túi qua chết/rejoin. `RoleplayShop` kiểm tra trên room thread, `WalletStore` trừ/hoàn cùng ví `campaign_profiles` trên DB worker; xem [gameplay/items](gameplay.md#items), [data/accounts](data.md#accounts) và cặp TCP 86/87 ở [protocol](protocol.md#tcp).

Launcher Windows từ 1.2.0 tự cập nhật EXE qua manifest riêng trước khi cập nhật game: helper đợi tiến trình cũ thoát, thay file có backup, đợi giao diện mới xác nhận và rollback khi lỗi. Game/cấu hình được giữ riêng; launcher cũ cần tải bản hỗ trợ một lần. Build local không tự upload lên GitHub Releases. Quy trình và giới hạn khôi phục ở [launcher](../source/launcher/README.md), kiểm tra ở testing.

<a id="principles"></a>
## Nguyên tắc kiến trúc

- Server quyết định trạng thái gameplay. `Room` quản lý vòng đời và loop 30 Hz; `World` mô phỏng di chuyển, chiến đấu và các hiệu ứng.
- TCP mang yêu cầu tài khoản/phòng, điều khiển vòng đời và lịch sử. UDP mang input, đồng hồ và snapshot. Hai bên phải tương thích version, content và map hash.
- Client `ClientCore` nhận mạng, dự đoán và reconciliation; `GameState` chứa mô hình, movement và interpolation. Sửa movement/collision hoặc wire phải xem cả Java và C#.
- Unity khởi tạo bằng `TankApp`, scene `Main.unity`, UI dùng IMGUI. Bảo toàn setup hiện có, GUID `.meta`, importer và giấy phép tài nguyên.
- UI Multiplayer và Chiến dịch cùng nền xanh quân đội tối, khung cắt góc, nút vàng/kem/xanh/cam và font Bungee/Nunito. `CartoonUi` chia sẻ style/icon; `BattleHudUi` chia sẻ nút hành động với nhãn phím và trạng thái; kỹ năng multiplayer có viền hồi chiêu xanh, kỹ năng chiến dịch chỉ hiện số lượt, đơn vị lượt/giây giữ riêng; `CampaignUI` là adapter ngữ nghĩa. Hai chế độ dùng header, bản đồ bên trái và sidebar 294 px bên phải; giữ phép ngắm multiplayer và camera/13×13 ô chiến dịch. HP người chơi hiển thị trên xe, không lặp ở sidebar; năng lượng multiplayer giữ trên xe. Chat mặc định chọn tab Xếp hạng; cụm kỹ năng/túi vật phẩm ghim phía dưới. Chiến dịch chỉ giữ mục tiêu/boss/vàng, số hiệu lực Khiên/Đòn ×2 trên ô; không hiện thống kê lượt/hạ địch hay khối Chi tiết nhiệm vụ trong trận, tên khu vực chỉ hiện ngắn rồi mất. Mô tả kỹ năng hai chế độ mở bằng chuột phải qua `SkillDetailsUi`, khóa input/tự bắn và đợi nhả phím khi đóng; không có tooltip theo chuột. Cài đặt cho nhập tên phím, nhấn trực tiếp bốn mũi tên với ký hiệu ↑ ↓ ← → và font dự phòng. Nhặt vàng có âm thanh xác nhận dựa trên thay đổi loot/vàng của server, không phát lại khi refresh. Header/modal/input có cùng đường xử lý, luật server vẫn riêng. Chi tiết và bằng chứng kiểm tra ở [client](client.md#ui) và UI validation.
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
| Checks và thực nghiệm | testing.md, [evaluation.md](evaluation.md) |
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
