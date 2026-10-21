# Contract gameplay và Nhập vai

Sửa `game-rules.json` hoặc `roleplay-content.json`, chạy `python scripts/sync-contracts.py`, rồi kiểm tra bằng `python scripts/sync-contracts.py --check`. Không sửa tay `GameRules` và `RoleplayContent` sinh ra ở Java/C#. Content hash là SHA-256 của JSON canonical `{rules, content}` (UTF-8, khóa sắp xếp, không khoảng trắng), bao gồm tên, mapping nation/Q và thông số. Map hash vẫn tính trên byte gốc của từng map.

Validator kiểm nation 0–7 và mọi deadline `MaxMatchTicks + duration/cooldown <= 65535`.

## Snapshot v5

HP và MaxHP dùng `u16`, phòng mặc định 100 HP và chấp nhận 1–500 HP. Mỗi xe **64 byte**: giữ thứ tự field nền, ba deadline `QReadyTick/QEndTick/SlowEndTick:u16`, `ProtectionFlags:u8`, rồi `QStartTick/ConfusionEndTick/FireLockEndTick/FogEndTick:u16`, `QStage:u8`, `AimX/AimY:f32`, `Life:u32`. QStage=1 biểu thị đang ngắm Liên Xô hoặc chuẩn bị Nhật/Ý. Deadline là tick tuyệt đối; hiệu ứng hoạt động khi `tick < end`. Hồi chiêu giữ qua chết/hồi sinh. Life thay đổi mỗi lần spawn, kể cả rejoin. SlowEndTick ghi hạn làm chậm còn 50% tốc độ trong 30 tick sau khi trúng bom Mỹ; server và client cùng nhân hệ số này với tốc độ hiện tại, gồm Shift và nước. ProtectionFlags được giữ trong record nhưng các kỹ năng mới không dùng nó.

Nation được lấy từ loadout TCP tin cậy, không chiếm byte snapshot. Client gắn nation vào cả FULL và delta sau khi xác minh loadout.

Mỗi đạn **20 byte**, nối `Damage:u8` sau `SpeedPercent:u8`, `RemainingLifetime:u16`. Đạn thường 20 HP, Đức 15 HP được chốt khi tạo; trần nảy 2, lifetime 150 tick. Giới hạn mới **128 đạn**. Với loadout quốc gia duy nhất, tám xe tạo tối đa 60 đạn trước bước hết hạn ở tick bắn, kể cả Đức bắn bộ ba; còn dư ngân sách so với trần mới và không gặp trần 32 cũ.

Tank delta mask dùng `u32`: bit10 là presence, các field mới dùng bit19–26 theo thứ tự trên. Complete mask `0x7ffffff`. Bullet có tám field, complete mask `0xff`. New entity cần đủ field. Áp dụng vào bản sao của đúng base, xác minh xong mới công bố. Thiếu base yêu cầu RESYNC; FULL định kỳ 30 tick.

FULL và DELTA đều kết thúc bằng danh sách hiệu ứng thay thế toàn bộ: `count:u16`, tối đa 192 record **24 byte**: `id:u32, kind:u8, owner:u8, start/end:u16, x/y/radius:f32, angle:u16`. Kind 1–8: rocket, bom, nổ Nhật, tia Ý, radar Anh, phá hoại Trung Quốc, vùng Katyusha đã chốt, kích hoạt. Lịch rocket do server sinh; vụ nổ tồn tại thêm 24 tick để nhận lại sau mất gói. Client khôi phục tiến độ và không phát lại âm thanh sự kiện đã qua khi rejoin.

FULL tối đa có `1 + 8×64 + 1 + 128×20 + 64 + 2 + 192×24 = 7748 byte` trước metadata. Chỉ chọn delta khi `delta.length + 4 < full.length`, có tính base ID. Snapshot lớn dùng opcode **50 SNAPSHOT_PART**: `match:u64, snapshotId:u32, originalOp:u8, index:u8, count:u8, totalLength:u16, bytes`. Mỗi phần tối đa 1100 byte dữ liệu, datagram tối đa **1129 byte**, dưới trần 1200. Giới hạn payload ghép 32768 byte; tối đa 16 snapshot chưa đủ phần, hết hạn 500 ms. Chỉ ACK/cache sau khi đủ phần và validation thành công; mất phần chờ snapshot kế tiếp hoặc FULL/RESYNC. Test interop bao gồm 128 đạn, 72 hiệu ứng, đổi toàn bộ ID đạn, đảo thứ tự, lặp/mất phần và khôi phục.

## Điều khiển TCP

`RoomMode` tách khỏi network mode: NORMAL=0, ROLEPLAY=1. `RoomState.PREPARING` được giữ nguyên; RP có `PrepStage` LOADING=0, SYNC_LOADOUT=1, COUNTDOWN=2. Pipeline tương ứng tối đa 10s → tối đa 5s → 3s. Các số nguyên wire theo thứ tự byte network hiện có.

| Opcode | Payload |
|---|---|
| 80 SELECT_NATION | `nation:u8` trong phòng chờ |
| 84 USE_SKILL | `match:u64, requestId:u32, slot:u8, aimX/aimY:f32` (Q=0, giá trị khác bị từ chối) |
| 85 MATCH_LOADOUT | `match:u64, revision:u32, contentHash:32, count:u8, sorted(slot:u8,nation:u8), loadoutHash:32` |

Loadout hash là SHA-256 toàn bộ payload trước hash cuối, gồm match, revision, content hash và danh sách đã sắp theo slot. Loadout giữ cả participant rời trận để client vào lại nhận cùng mapping.

INPUT UDP nối `aimX/aimY:f32` sau danh sách input để đồng bộ vòng ngắm. Server/client cùng giới hạn tâm Katyusha trong `[5,1; width−5,1] × [5,1; height−5,1]`; client giữ tâm hợp lệ cuối khi chuột ra ngoài map/HUD/cửa sổ. CREATE_ROOM, ROOM_SETTINGS và ROOM_STATE dùng starting HP `u16`.

PREPARE RP nối vào payload cũ: `rejoining:bool, roomMode:u8, contentVersion:u16, contentHash:32, requestHighWater:u32`. Client kiểm map, content, sprite bắt buộc, handshake và tám mẫu đồng hồ trước ACK tải. PREPARED RP: `match:u64, stage:u8, revision:u32, hash:32`; stage LOADING dùng content version/hash, SYNC_LOADOUT dùng loadout revision/hash. Rejoin cũng đi qua hai ACK này trước khi spawn/START.

ROOM_STATE RP nối sau starting HP: `roomMode:u8, stage:u8, deadlineUs:u64, match:u64, revision:u32, nation:u8 cho mỗi member theo cùng thứ tự`. Nation 255 là chưa chọn. ROOM_LIST_RESULT nối các mảng locked, canRejoin và roomMode theo cùng thứ tự phòng.

ACTION_RESULT cho USE: `status:u16`, thêm `reason:string` nếu lỗi, rồi `match:u64, requestId:u32, action:u8, slot:u8, appliedTick:u32, readyTick:u16`. Slot không thành công không tiêu cooldown. Kết quả chỉ được gửi sau khi áp dụng; skill intents cùng tick giữ thứ tự hàng đợi trước movement/fire/damage.

Mỗi participant giữ 256 kết quả gần nhất, gồm cả từ chối, cùng opcode và payload gốc. Replay đúng trả cùng kết quả trước kiểm rate/cooldown; cùng ID khác lệnh bị từ chối. High-water từ chối ID đã bị loại khỏi cache và được gửi lại khi rejoin. ID tăng trong từng trận, 0 không hợp lệ.

## Dữ liệu và quan sát

SQLite thêm nullable `matches.room_mode/content_version`, `match_players.nation_id`; `matches.mode` vẫn là network mode. Migration chạy lặp được. Các cột dư trong database cũ được giữ để bảo toàn dữ liệu nhưng không được ứng dụng đọc/ghi. Spool cũ thiếu metadata giữ null. History detail giữ optional duration; nếu có metadata nhưng không có duration, duration wire là `u32.MaxValue`, theo sau metadata version1, room mode, content version và danh sách slot/nation (255 cho null).

Server ghi `ROLEPLAY_USE`, `ROLEPLAY_ACTION`, `ROLEPLAY_SUMMARY` và `ROLEPLAY_RATE` vào log server hiện có. Summary có số lần dùng và số tick sống khi kỹ năng sẵn sàng, để phân biệt không dùng với không có cơ hội. Chỉ số tiếp năng lượng/lướt của bộ kỹ năng cũ không còn được mở cho kỹ năng mới.

Các log và test tự động không thay thế playtest về cảm giác chơi. Thiết kế HP, kỹ năng, hiệu ứng và tiêu chí kiểm chứng nằm trong `plan/plan_change.md`; thông số nguồn dùng chung ở hai JSON cùng thư mục.
