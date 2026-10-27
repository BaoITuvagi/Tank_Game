# Dữ liệu, kết quả và log

Đối chiếu ngày **2026-10-23** với working tree dựa trên HEAD `e788610bdb0e2cd9fbd3f0cce54d9d29e815947e`, có thay đổi chưa commit. Nội dung được xác minh bằng đọc schema, implementation và test hiện có; không tuyên bố đã chạy test trong đợt tài liệu này. Luật tính kết quả ở [gameplay](gameplay.md#match); wire lịch sử ở [protocol](protocol.md#tcp).

Mục lục: [Schema](#schema) · [Lưu và phục hồi](#persistence) · [Lịch sử](#history) · [Tài khoản](#accounts) · [Log và số liệu](#observability) · [Điểm đọc test](#tests).

<a id="schema"></a>
## SQLite và migration

[schema.sql](../source/server/src/main/resources/schema.sql) được đóng gói vào server, thực thi khi `Database` mở kết nối. SQLite bật `foreign_keys=ON`, `journal_mode=WAL`, `busy_timeout=5000`. Cấu hình mặc định trỏ đến `runtime/tank.db`; đường dẫn có thể đổi trong cấu hình local, xem [deployment](deployment.md).

| Bảng | Cột và ý nghĩa |
|---|---|
| `accounts` | `id` khóa chính; `username` duy nhất, không null; `salt`, `hash`, `iterations`, `created` không null |
| `player_profiles` | `account_id` khóa chính/khóa ngoại tới accounts; `display_name` không null |
| `matches` | `id` khóa chính; `map_id`, `map_hash`, `mode`, `started`, `ended`, `reason`, `seed`, `duration_ticks`, `room_mode`, `content_version` |
| `match_players` | Khóa chính ghép `(match_id,account_id)`; hai khóa ngoại tới matches/accounts; `nickname`, `slot`, `kills`, `deaths`, `rank`, `disconnected`, `nation_id` |

`matches.mode` luôn là **network mode** (`BASELINE`, `INPUT_REDUNDANCY`, `DELTA`, `COMBINED`); `room_mode` mới là `NORMAL`/`ROLEPLAY`. `content_version` là TEXT dạng `version@hash` với trận RP hiện tại, không chỉ một số nguyên; trận NORMAL giữ null. `nation_id` nullable, 0 là nation Đại Bàng hợp lệ, không phải giá trị mặc định cho dữ liệu thiếu.

Sau schema, constructor gọi migration thêm `matches.duration_ticks`, `matches.room_mode`, `matches.content_version`, `match_players.nation_id` nếu chưa có. Mỗi migration đọc `PRAGMA table_info` trước `ALTER TABLE`, nên chạy lặp được. Các dòng lịch sử cũ giữ null; không suy nation hoặc content từ bản hiện tại. Cột dư trong database cũ, chẳng hạn `card_skill_id`, được giữ nguyên để không mất dữ liệu nhưng application hiện tại không đọc/ghi cột đó; JSON spool có field cũ cũng không biến nó thành tính năng đang dùng.

Nguồn: [Database constructor/migrateDurationColumn/migrateColumn](../source/server/src/main/java/tank/Database.java), [Room.finish](../source/server/src/main/java/tank/Room.java). Test mô tả yêu cầu tương thích: [RoleplayDatabaseTest](../source/server/src/test/java/tank/RoleplayDatabaseTest.java), [MatchDurationTest.legacyDatabaseMigratesIdempotentlyWithoutChangingHistory](../source/server/src/test/java/tank/MatchDurationTest.java).

<a id="persistence"></a>
## Lưu kết quả, spool và phục hồi

`Room.finish` chụp kết quả mọi participant, kể cả người đã ngắt kết nối: account, nickname tại trận, slot, kills/deaths/rank/disconnected và nation nếu RP. Bản ghi trận gồm ID, map ID/hash, network mode, timestamp bắt đầu/kết thúc UTC mili giây, reason, seed, **số tick đã chơi thực tế**, room mode và content version/hash. Kết thúc sớm lưu tick thực tế, không lưu thời lượng phòng đã cấu hình hoặc lấy hiệu timestamp làm duration chính.

`Database` dùng một worker sở hữu các thao tác DB, hàng đợi giới hạn **1024**. `save` đưa kết quả vào `pending` trước khi submit. `persist` mở transaction, `INSERT OR IGNORE` vào matches rồi match_players, commit khi đủ cả hai; lỗi rollback. Lưu lại cùng match/player không nhân bản dòng, nhưng cũng không ghi đè một dòng đã tồn tại bằng kết quả sửa đổi. Đây là cơ chế retry kết quả bất biến, không phải API cập nhật kết quả trận.

Nếu DB lỗi, `persistOrSpool` giữ pending và ghi JSON vào `spool/<match>.tmp`, sau đó đổi thành `<match>.json` bằng atomic move nếu được hỗ trợ, có phương án replace thường khi filesystem không hỗ trợ atomic move. Worker đầy cũng trả thất bại và nhờ executor retry ghi spool; nếu cả DB lẫn spool hỏng thì pending còn trong RAM và server ghi lỗi. **Pending trong RAM không bảo đảm tồn tại qua process crash**; cần phân biệt lỗi đã spool thành công với lỗi chưa lưu ở đâu.

Constructor đọc lại các file `.json` trong spool vào pending; file không parse được được ghi log và giữ để kiểm tra. Retry scheduler chạy ngay rồi mỗi **5 giây**, submit lại pending qua worker. Khi persist thành công, xóa pending và file spool tương ứng. Spool cũ thiếu duration/metadata giữ null trong `Database.Result/ResultPlayer`, không tự gán phiên bản/nation hiện tại. Khi đóng server, `Database.close` ngừng retry, cố ghi pending ra spool, submit retry cuối rồi chờ worker tối đa 15 giây trước đóng kết nối.

`Room.finish` gửi kết quả qua END trước; lưu bất đồng bộ sau đó. Nếu `save` chưa thành công, client nhận NOTICE `DATABASE_UNAVAILABLE` với thông báo kết quả đang chờ lưu và cần kiểm log/spool. Màn hình kết quả không phải bằng chứng SQLite đã commit. ID trận mới lấy từ maximum DB và mốc thời gian server trong `Server`, tránh dùng lại ID nhỏ sau restart.

Database, WAL/SHM, spool, log và kết quả build/test là dữ liệu local/runtime, phải giữ ngoài Git. Vận hành/cấu hình nằm ở [deployment](deployment.md); tài liệu này không yêu cầu thay schema hoặc thao tác dữ liệu đang có.

Nguồn: [Database.Result/ResultPlayer/save/persistOrSpool/persist/retryPending/spoolOnly/close/nextMatch](../source/server/src/main/java/tank/Database.java), [Room.finish](../source/server/src/main/java/tank/Room.java), [Server constructor](../source/server/src/main/java/tank/Server.java). Test: [DatabaseTest.spoolAndRetry/queueOverflowSpoolsAndStartupRecovers/databaseAndSpoolFailureKeepsResultUntilRecovery](../source/server/src/test/java/tank/DatabaseTest.java), [RoleplayDatabaseTest.oldSpoolWithoutRoleplayMetadataStillPersists](../source/server/src/test/java/tank/RoleplayDatabaseTest.java).

<a id="history"></a>
## Lịch sử và thời lượng

`Database.history` chỉ trả các trận có match_players của account đã xác thực, sắp `ended DESC, id DESC`, với offset/limit. `historyDetail` cũng JOIN theo account và match; người không tham dự không đọc được chi tiết trận qua API này. Danh sách người chơi chi tiết sắp rank rồi slot, có nickname đã lưu và cờ disconnected. Nickname lịch sử không tự đổi theo display name hiện tại.

`duration_ticks` nullable lưu thời gian mô phỏng ở 30 Hz; ví dụ 1800 tick là một phút đầy đủ. Timestamp vẫn cần cho thời gian lịch nhưng có thể khác duration mô phỏng do tải, lịch thread hoặc kết thúc sớm. Client hiển thị giây bằng `(durationTicks+15)/30`; legacy thiếu duration làm tròn hiệu timestamp mili giây sang giây, kẹp 0 nếu kết thúc không sau bắt đầu. Không điền duration cũ bằng cách sửa hàng database khi khởi động.

Wire detail giữ phần duration tùy chọn để đọc được bản ghi cũ. Nếu có metadata mà không có duration, server gửi sentinel `u32.MaxValue` trước metadata version1; nation null thành 255. Bố cục byte đầy đủ được duy trì tại [protocol/TCP](protocol.md#tcp). Metadata RP là dấu vết phiên bản của trận đã diễn ra; giữ null khi spool cũ không có metadata, không suy từ loadout đang chạy.

Nguồn: [Database.history/historyDetail](../source/server/src/main/java/tank/Database.java), [Server.Session.request — HISTORY/HISTORY_DETAIL](../source/server/src/main/java/tank/Server.java), [ClientCore.Handle — HistoryResult/HistoryDetailResult](../source/client/Assets/Scripts/ClientCore.Messages.cs), [ClientModels](../source/client/Assets/Scripts/ClientModels.cs). Test: [MatchDurationTest](../source/server/src/test/java/tank/MatchDurationTest.java), [MatchDurationTests](../source/client/Assets/Tests/Editor/MatchDurationTests.cs), [RoleplayDatabaseTest](../source/server/src/test/java/tank/RoleplayDatabaseTest.java).

<a id="accounts"></a>
## Tài khoản và tên hiển thị

Username hợp lệ `[A-Za-z0-9_]{3,24}`, được hạ chữ thường theo `Locale.ROOT` trước lưu/tìm. Password có 8–64 ký tự Java và tối đa 256 byte UTF-8. Mỗi account có salt ngẫu nhiên **16 byte**; PBKDF2-HMAC-SHA256 **600000 vòng**, output **256 bit**; login dùng số vòng đã lưu và so sánh hash bằng `MessageDigest.isEqual`. Database không lưu password nguyên văn.

Display name riêng trong player_profiles: trim biên bằng `strip`, chuẩn hóa NFC, không rỗng, tối đa **24 code point / 96 byte UTF-8**, không ký tự điều khiển. Login dùng profile khi có, fallback về username. Đổi display name ghi upsert và cập nhật member đang trong phòng; lịch sử giữ nickname đã chụp ở kết quả trận.

Vàng tài khoản dùng bảng `campaign_profiles(account_id,gold,best)` hiện có, không tạo ví song song. `CampaignStore.save` ghi `campaign_runs` và cộng **vàng còn lại của chuyến đi** trong cùng transaction; `INSERT OR IGNORE` theo ID run giữ retry không cộng hai lần. Shop Nhập vai đọc/trừ cùng số dư đó qua `WalletStore` trên database worker. `roleplay_purchases` có khóa `(match_id,account_id,wave,item)`, `cost`, `state` (0 giữ tiền, 1 đã cấp, 2 đã hoàn) và mã reservation riêng từng lần thử. Reserve chỉ trừ nếu số dư đủ; transaction rollback khi không mua được. Callback ở room kiểm tra lại điều kiện rồi cấp item hoặc hoàn vàng; settlement idempotent, callback hoàn cũ không ảnh hưởng reservation mới. Lỗi settlement giữ trong RAM để retry mỗi 5 giây; reservation chưa xác nhận được hoàn một lần khi server khởi động lại. Nếu crash sau khi đã cấp item nhưng trước khi xác nhận DB, người chơi có thể được hoàn vàng; không khôi phục trận cũ để cấp lại item. Giới hạn mua của participant giữ qua chết và rejoin.

Các bảng `campaign_runs`, `campaign_profiles`, `roleplay_purchases` được tạo bổ sung, giữ dữ liệu tài khoản/vàng đã có. Query ví không yêu cầu vào Chiến dịch; số dư ở sảnh và shop được lấy từ server.

Túi item đã mua thuộc `RoleplayMatch.Player` trong RAM của trận: số lượng theo 6 loại và revision tăng khi mua/dùng. Chết, respawn và rejoin giữ túi; trận mới đặt lại túi. DB chỉ ghi giao dịch vàng, không tạo kho item vĩnh viễn cho tài khoản. Dùng item không trừ vàng lần nữa; server kiểm tra revision, số lượng và trạng thái sống trước khi giảm số lượng/áp dụng hiệu lực.

Nguồn: [Database.credentials/hash/register/login/validDisplayName/displayName/setDisplayName](../source/server/src/main/java/tank/Database.java), [Server.Session.authenticate/request](../source/server/src/main/java/tank/Server.java).

<a id="observability"></a>
## Log và số liệu

`Main` mở `log.path` (mặc định `runtime/server.log`) ở chế độ append UTF-8 và tee stdout/stderr ra console cùng file. RP dùng các log hiện có, không có database telemetry riêng:

| Dòng log | Nội dung chính |
|---|---|
| `ROLEPLAY_USE` | Nation, HP/map, ability slot, kết quả/tick; giá trị trước/sau thao tác như energy, recovery, Shift/moving, aim error, water, distance và đạn bị loại |
| `ROLEPLAY_ACTION` | Match/account/request/slot, kết quả đã áp dụng và tick; không in từng lần rate limit tại đây |
| `ROLEPLAY_RATE` | Tổng số request bị giới hạn, gộp thông báo tối đa khoảng một lần/giây mỗi participant |
| `ROLEPLAY_SUMMARY` | `qUses`, `qAvailableTicks`, `aliveTicks`, `rateRejected` khi kết thúc |
| `ROLEPLAY_EFFECT` | Telemetry gameplay do World gửi khi RP, gồm shot và wave battlefield |
| `ROLEPLAY_CANCEL` | Lý do hủy chuẩn bị trận RP |

`qAvailableTicks` đếm tick **xe sống và hồi Q đã sẵn sàng**, giúp phân biệt không dùng với ít cơ hội; `qUses` tăng khi USE thành công, nên Q ngắm và Q chốt đều có thể góp vào bộ đếm theo cách implementation gọi `use`. Đây không phải thống kê “số đợt sát thương hoàn tất”. Code còn nhánh `ROLEPLAY_UTILITY` cho chỉ số Mỹ cũ, nhưng bộ Q hiện tại không mở `usMetricsOpen`, nên không coi chỉ số năng lượng/lướt cũ là phép đo cho bom mới.

`Metrics` thu số từ room thread vào queue **8192**; một writer riêng định dạng/ghi CSV. Cột gồm tick/encode/send time, overrun, full/delta/fallback/resync, input ontime/missing/late/duplicate/conflict/drop, command depth, byte/packet TX/RX, invalid/UDP drop, heap/GC/process CPU, schedule lag và metrics drop. Byte/packet/các counter transport có phạm vi server; dòng vẫn gắn room/match. `tick_us`/`encode_us`/`send_us`/`schedule_lag_us` là microsecond, timestamp lịch sử là millisecond; không trộn hai đơn vị.

COMBAT retention/overflow có counter riêng; việc loại event trang trí/critical không làm đổi damage hoặc state gameplay. Giới hạn và dedup của event được mô tả ở [protocol/combat](protocol.md#combat-events). CSV/log hỗ trợ chẩn đoán và đánh giá mạng; số liệu và test tự động không thay thế playtest về cảm giác chơi/cân bằng. Phương pháp đánh giá và cách chạy test nằm ở [evaluation](evaluation.md) và [testing](testing.md).

Nguồn: [Main.main/Tee](../source/server/src/main/java/tank/Main.java), [Metrics.row/writeLoop](../source/server/src/main/java/tank/Metrics.java), [RoleplayMatch.use/dispatch/observe/summary](../source/server/src/main/java/tank/RoleplayMatch.java), [Room.prepare](../source/server/src/main/java/tank/Room.java), [CombatEvents.add/prune](../source/server/src/main/java/tank/CombatEvents.java).

<a id="tests"></a>
## Điều hướng kiểm chứng

| Phạm vi | Test hiện có để đọc/chạy theo testing guide |
|---|---|
| DB lock, spool/retry, worker đầy, startup recovery | [DatabaseTest](../source/server/src/test/java/tank/DatabaseTest.java) |
| Migration lặp, null legacy, field/cột cũ và RP metadata | [RoleplayDatabaseTest](../source/server/src/test/java/tank/RoleplayDatabaseTest.java) |
| Đủ phút, kết thúc sớm, duration nullable | [MatchDurationTest](../source/server/src/test/java/tank/MatchDurationTest.java), [MatchDurationTests](../source/client/Assets/Tests/Editor/MatchDurationTests.cs) |
| Kết quả phòng, tên và lịch sử | [RoomFeaturesTest](../source/server/src/test/java/tank/RoomFeaturesTest.java), [RoomRefinementTest](../source/server/src/test/java/tank/RoomRefinementTest.java) |

Không có kết quả chạy mới được tạo cho thay đổi tài liệu này; các test được liên kết làm bằng chứng về yêu cầu mà mã hiện tại cố gắng đáp ứng.
