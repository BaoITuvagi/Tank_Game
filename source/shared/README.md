# Contract gameplay và Nhập vai

Sửa `game-rules.json` hoặc `roleplay-content.json`, chạy `python scripts/sync-contracts.py`, rồi kiểm tra bằng `python scripts/sync-contracts.py --check`. Không sửa tay `GameRules` và `RoleplayContent` sinh ra ở Java/C#. Content hash là SHA-256 của JSON canonical `{rules, content}` (UTF-8, khóa sắp xếp, không khoảng trắng), bao gồm tên, mapping nation/Q và thông số. Map hash vẫn tính trên byte gốc của từng map.

Validator kiểm nation 0–7 và mọi deadline `MaxMatchTicks + duration/cooldown <= 65535`.

## Snapshot v9

HP và MaxHP dùng `u16`, phòng mặc định 100 HP và chấp nhận 1–500 HP. Mỗi xe **64 byte**: giữ thứ tự field nền, ba deadline `QReadyTick/QEndTick/SlowEndTick:u16`, `ProtectionFlags:u8`, rồi `QStartTick/ConfusionEndTick/FireLockEndTick/FogEndTick:u16`, `QStage:u8`, `AimX/AimY:f32`, `Life:u32`. QStage=1 biểu thị đang ngắm Liên Xô hoặc chuẩn bị Nhật/Ý. Deadline là tick tuyệt đối; hiệu ứng hoạt động khi `tick < end`. Hồi chiêu giữ qua chết/hồi sinh. Life thay đổi mỗi lần spawn, kể cả rejoin. SlowEndTick ghi hạn giảm 30% tốc độ (còn 70%) trong 90 tick ngay khi Mỹ kích hoạt kỹ năng; server và client cùng nhân hệ số này với tốc độ hiện tại, gồm Shift và nước. ProtectionFlags được giữ trong record nhưng các kỹ năng mới không dùng nó.

Nation được lấy từ loadout TCP tin cậy, không chiếm byte snapshot. Client gắn nation vào cả FULL và delta sau khi xác minh loadout.

Gạch đỏ dùng `BrickMaxHp=3`: mỗi đạn làm mất 1 HP, các kỹ năng phá ngay gạch vẫn đặt HP về 0. FULL giữ mảng 64 byte và DELTA giữ record `id/hp:u8`, chỉ nhận HP 0–3. Client bỏ thanh máu tường, vẽ ba hình thái nguyên vẹn/2 phần ba/1 phần ba; vùng chặn xe và đạn giữ nguyên đến khi HP bằng 0. Preview dùng cùng mức HP tối đa.

Bom Mỹ đánh dấu từng đối thủ sống và làm chậm ngay khi kích hoạt. Mỗi vùng giữ ID hiệu ứng, truy bám thẳng mục tiêu với tốc độ cố định 4,5 đơn vị/giây (0,15 mỗi tick), bằng 50% tốc độ xe bình thường không Shift, không vượt quá mục tiêu; không bị nước hay địa hình làm chậm. Tọa độ vùng hiện tại được thay thế theo snapshot FULL/DELTA hiện có. Sau 90 tick bom nổ tại tâm mới: bán kính 7,65, gây tối đa 25 HP mỗi xe trong một lượt dùng, không làm chậm thêm và phá ngay gạch trúng vùng. Mục tiêu chết/rời trận/đổi life thì vùng dừng vĩnh viễn tại vị trí cuối, vẫn nổ đúng lịch và không gây sát thương cho đời xe mới. Bom tồn tại qua cái chết của người gọi; thanh tẩy chỉ gỡ làm chậm. Xe đang bị giảm 30% tốc độ chạy 6,3 đơn vị/giây trên nền thường, hoặc 9,45 khi giữ Shift và còn năng lượng; nước vẫn giảm thêm tốc độ theo cơ chế hiện tại.

Mỗi đạn **21 byte**: `id:u32, owner:u8, x/y:f32, heading:u16, bounces:u8, speedPercent:u8, remainingLifetime:u16, damage:u8, kind:u8`. Kind 0 = thường/Q Đức, 1 = HE, 2 = Mirror mẹ, 3 = Mirror con, 4 = HE + Mirror mẹ, 5 = HE + Mirror con. Đạn mẹ/thường gây 20 sát thương, hoặc 15 khi có Q Đức; đạn con gây 10, hoặc 25 khi cộng dồn Q Đức. HE cộng thêm 6 sát thương vùng khi va chạm, kể cả trên đạn con. Hệ số kích thước va chạm/hiển thị theo kind 0–5: 1; 1,5; 1; 0,7; 1,5; 1,05.

HE và Mirror không thay thế nhau. Mirror giữ 3 lượt bắn, mỗi loạt Q Đức tiêu thụ một lượt và có 3 đạn mẹ, mỗi mẹ tách 3 con khi chạm vật cản. HE + Mirror nổ HE trước rồi tách. Thuộc tính chốt lúc khai hỏa; HE/Q hết hạn hoặc Mirror hết lượt chỉ ảnh hưởng phát mới, đạn đang bay và các con được tách sau đó giữ hiệu ứng/sát thương đã chốt. Trần 128 đạn bao gồm hai chỗ dành trước cho mỗi Mirror mẹ (kind 2/4); Q + Mirror chỉ khai hỏa khi đủ chỗ cho 9 con. Tách thay một mẹ bằng đủ ba con, giữ tốc độ và thời gian sống còn lại. Đạn con bắt đầu ở bounce=1, chỉ nảy thêm một lần và không tách tiếp. Hết hạn, trúng xe hoặc chủ đạn rời trận sẽ giải phóng phần dành trước.

Tank delta mask vẫn dùng `u32`, bit10 presence, bit19–26 theo thứ tự field ở trên, complete mask `0x7ffffff`. Bullet mask đổi thành **u16**: bit0–7 giữ thứ tự cũ, bit8 = kind, complete mask `0x1ff`. Entity mới cần đủ field. Delta áp dụng vào bản sao của base đã ACK, xác minh xong mới công bố; thiếu base yêu cầu RESYNC. FULL định kỳ mỗi 30 tick.

Sau bricks, FULL và DELTA chứa toàn bộ danh sách hiệu ứng kỹ năng: `count:u16`, tối đa 192 record **24 byte**: `id:u32, kind:u8, owner:u8, start/end:u16, x/y/radius:f32, angle:u16`. Kind 1–8 giữ nguyên: rocket, bom, nổ Nhật, tia Ý, radar Anh, phá hoại Trung Quốc, vùng Katyusha, kích hoạt. Server lập lịch rocket; hiệu ứng sau va chạm giữ thêm 24 tick.

Phần **extension item** sau đây được thay thế toàn bộ trong cả FULL và DELTA, giữ tank mask trong 32 bit:

- `tankCount:u8`, bằng đúng số xe trong snapshot, mỗi slot một lần: `slot:u8, armorEnd/heEnd/invincibilityEnd:u16, mirrorShots:u8, qBaseReady:u16` (10 byte). QReady trong record xe là hạn hiệu lực sau phá hoại; QBaseReady là hạn hồi chiêu gốc. Deadline là tick tuyệt đối 30 Hz, hiệu lực khi `tick < end`.
- `itemCount:u8`, tối đa 6 record: `id:u32, type:u8, x/y:f32, expires:u16` (15 byte). Type 0–5 = Medical, Armor, HE, Purification, Invincibility, Mirror. ID duy nhất trong trận. `expires=0` là không hết hạn; giá trị khác là tick hết hạn tuyệt đối.
- `eventCount:u16`, tối đa 256 record cùng định dạng hiệu ứng 24 byte: kind 9 = nhặt (angle=type item), 10 = nổ HE, 11 = tách Mirror (angle=hướng phản xạ). ID riêng theo trận, start=end=tick sự kiện, giữ thêm 24 tick. Khi đầy, bỏ sự kiện hình ảnh cũ nhất. Client vẽ tiến độ theo thời gian server và bỏ sự kiện có trước/đúng snapshot đầu khi vào trận/rejoin, nên snapshot lặp không khởi động lại animation.

Server lưu từng lượt sương mù với ID, nguồn, slot/life xe chịu ảnh hưởng, deadline và dấu đã thanh tẩy. Thanh tẩy chỉ gắn với lượt đó; lượt mới vẫn tác động, và hết hạn không thể khôi phục lượt cũ đã thanh tẩy. Các debuff khác cũng giữ nguồn/life. Client nhận FogEnd/ConfusionEnd/FireLockEnd/SlowEnd do server tổng hợp, không suy lại từ Q đang hoạt động của xe khác. Trì hoãn Q lưu riêng với mốc hồi chiêu gốc và giữ hành vi qua chết/hồi sinh; thanh tẩy chỉ gỡ phần trì hoãn bên ngoài.

Rejoin giữ cơ chế spawn/HP hiện có. Người chơi ngắt kết nối khi còn sống giữ deadline item và số phát Mirror còn lại, không được kéo dài; xe chết xóa các buff đó. Mốc hồi Q gốc/hiệu lực và nguồn phá hoại giữ qua cả hồi sinh lẫn rejoin. Đời xe mới không nhận lại record sương mù/debuff của đời trước. Trạng thái xe khác, item trên map và deadline tuyệt đối được lấy từ snapshot hiện tại của server.

Mỗi loại có tối đa một item trên map (30 tick/giây). Chế độ thường sinh đủ 6 loại ở tick 300 (10 giây), không tự hết hạn; loại được nhặt sinh lại sau 450 tick (15 giây), có lịch riêng cho từng loại. Nhập vai sinh ngẫu nhiên 4 loại khác nhau trong 6 loại ở tick 600 (20 giây), tối đa 4 item trên map. Item không tự hết hạn, chỉ biến mất khi được nhặt; mỗi lần nhặt tạo một lượt sinh độc lập sau 600 tick (20 giây). Đến lượt sinh, chọn đều trong các loại chưa có trên map tại thời điểm đó, có thể chọn lại loại vừa nhặt; không giữ cố định bốn loại ban đầu. Không sinh bù trước khi đủ thời gian chờ. Vị trí được chọn ngẫu nhiên trong các điểm hợp lệ và khác vị trí item vừa bị nhặt (chế độ thường: vị trí trước đó của cùng loại). Nếu chưa có điểm hợp lệ, thử lại mỗi tick mà không đặt lại thời gian chờ. Cả hai map có `itemSpawns` cố định, kiểm tra nền khô và vật cản khi tải; khi sinh cũng tránh xe còn sống trong bán kính 6 đơn vị và item khác. SHA-256 vẫn tính trên byte map thực tế. Hết hạn trước nhặt, di chuyển/nhặt trước khai hỏa/sát thương. Xe gần tâm hơn thắng, bằng nhau ưu tiên slot nhỏ; kiểm tra cả đường di chuyển.

FULL tối đa **25204 byte**, gồm 8 xe, 128 đạn, 192 hiệu ứng kỹ năng, 6 item, 256 hiệu ứng item và 256 combat event. Test xác minh FULL và delta thay toàn bộ ID đạn/event nằm dưới trần ghép 32768 byte. Chỉ chọn delta khi `delta.length + 4 < full.length`. Snapshot lớn dùng opcode **50 SNAPSHOT_PART**: `match:u64, snapshotId:u32, originalOp:u8, index:u8, count:u8, totalLength:u16, bytes`. Mỗi phần tối đa 1100 byte; datagram tối đa 1129 byte, dưới MTU 1200. Client giữ tối đa 16 snapshot thiếu phần trong 500 ms, chỉ ACK/cache sau khi ghép và xác minh thành công. Fixture Java/C# bao gồm mất delta, phục hồi item/buff/tầm nhìn đã thanh tẩy/hồi Q, kích thước đạn và phân mảnh cực đại. Peer v5/v6/v7/v8 bị từ chối ngay từ header TCP/UDP; cần cập nhật client/server cùng lúc. Giữ hợp đồng v7 về 6 item và expires=0 nghĩa là không hết hạn; v8 bổ sung extension combat bắt buộc; v9 thêm kind đạn kết hợp HE + Mirror và sát thương cộng dồn Q Đức, giữ nguyên kích thước record.

## Combat event v8

FULL nối sau extension item: `combatCount:u16` rồi record COMBAT theo ID tăng. DELTA nối sau extension item: `removedCount:u16, removedIds:u32[]`, rồi `addedCount:u16, records[]`. Chỉ truyền chênh lệch so với base đã ACK; record bất biến, không xóa rồi thêm lại cùng ID để sửa nội dung. Giải mã transactional trên bản sao; lỗi không thay base/Latest/ACK. Record Q truy bám vẫn cập nhật theo hợp đồng cũ.

Mỗi record **43 byte**, network byte order: `id:u32, tick:u32, kind:u8, sourceSlot:u8, targetSlot:u8, sourceLife:u32, targetLife:u32, bulletId:u32, objectId:u16, surface:u8, x:f32, y:f32, angle:u16, value:u16, causeKind:u8, causeId:u32`. Slot không áp dụng=255 với Life=0; object không áp dụng=65535. ID/life dương trong phạm vi int; surface=0 xe/không áp dụng, 1 thép, 2 gạch; angle trong [0,35999]. Kind: SHOT=1, IMPACT=2, BOUNCE=3, DAMAGE=4, DEATH=5, BRICK_DESTROYED=6. Value là HP thực mất cho DAMAGE, MaxHP cho DEATH, còn lại 0.

Cause chỉ dùng cho DAMAGE/DEATH: 0/0 là không có effect nguyên nhân; kind 1–4 tham chiếu ROCKET/BOMB/KAMIKAZE/BEAM của SKILL, kind 10 tham chiếu EXPLOSION của ITEM. Cause không còn trong snapshot vẫn hợp lệ vì retention khác nhau. Hit trực tiếp giữ bulletId; damage vùng/Q dùng 0. Hit trực tiếp và splash HE tại một contact tham chiếu cùng EXPLOSION; giữ nguyên thứ tự damage trực tiếp rồi splash.

Q/item không được sao chép thành event combat tương đương. Contact HE dùng EXPLOSION, contact tách Mirror dùng SPLIT; HE + Mirror phát EXPLOSION rồi SPLIT tại cùng contact. Contact thông thường có một IMPACT hoặc BOUNCE. DEATH là kết quả chết thống nhất; không có event chết riêng theo quốc gia. ROCKET/BOMB/KAMIKAZE/HE sở hữu explosion/audio, các DEATH liên kết chỉ thêm phản hồi hạ gục/wreck theo từng Life. Beam và nổ xe là hai lớp riêng. Client chống nhận trùng theo match/stream/ID/phase và phân quyền theo cause, không suy nguyên nhân từ khoảng cách.

COMBAT giữ tối đa 90 tick và 256 record: 160 trang trí, 96 DEATH/BRICK_DESTROYED; tràn từng nhóm loại record cũ nhất và tăng counter, không tác động gameplay. Q/item giữ End+24 tick và ngân sách cũ. Không cam kết giao mọi one-shot qua mất mạng vượt retention. Snapshot đầu vào/rejoin đặt watermark; hiệu ứng tức thời cũ bị bỏ qua, state liên tục được dựng lại. Protocol v9 bắt buộc cập nhật client/server cùng đợt.

## Điều khiển TCP

`RoomMode` tách khỏi network mode: NORMAL=0, ROLEPLAY=1. `RoomState.PREPARING` được giữ nguyên; RP có `PrepStage` LOADING=0, SYNC_LOADOUT=1, COUNTDOWN=2. Pipeline tương ứng tối đa 10s → tối đa 5s → 3s. Các số nguyên wire theo thứ tự byte network hiện có.

| Opcode | Payload |
|---|---|
| 80 SELECT_NATION | `nation:u8` trong phòng chờ |
| 84 USE_SKILL | `match:u64, requestId:u32, slot:u8, aimX/aimY:f32` (Q=0, giá trị khác bị từ chối) |
| 85 MATCH_LOADOUT | `match:u64, revision:u32, contentHash:32, count:u8, sorted(slot:u8,nation:u8), loadoutHash:32` |

Loadout hash là SHA-256 toàn bộ payload trước hash cuối, gồm match, revision, content hash và danh sách đã sắp theo slot. Loadout giữ cả participant rời trận để client vào lại nhận cùng mapping.

Vùng AREA của Liên Xô có bán kính 11,475. Trong `[start, end)`, xe sống giao vùng bằng hitbox xoay còn 50% tốc độ hiện tại, kể cả người gọi; nhân thêm với nước, Shift và làm chậm của Mỹ. Nhiều vùng AREA giao nhau chỉ nhân 50% một lần. Ra khỏi vùng, chết hoặc hết thời gian vùng thì hiệu ứng dừng. Vùng ngắm chưa chốt không làm chậm. Server, prediction/reconciliation và nhãn trạng thái client cùng đọc AREA từ FULL/DELTA; không bổ sung field wire hoặc dùng chung `slowEndTick` của bom Mỹ.

INPUT UDP nối `aimX/aimY:f32` sau danh sách input để đồng bộ vòng ngắm. Server/client cùng giới hạn tâm Katyusha trong `[11,475; width−11,475] × [11,475; height−11,475]`; client giữ tâm hợp lệ cuối khi chuột ra ngoài map/HUD/cửa sổ. CREATE_ROOM, ROOM_SETTINGS và ROOM_STATE dùng starting HP `u16`.

PREPARE RP nối vào payload cũ: `rejoining:bool, roomMode:u8, contentVersion:u16, contentHash:32, requestHighWater:u32`. Client kiểm map, content, sprite bắt buộc, handshake và tám mẫu đồng hồ trước ACK tải. PREPARED RP: `match:u64, stage:u8, revision:u32, hash:32`; stage LOADING dùng content version/hash, SYNC_LOADOUT dùng loadout revision/hash. Rejoin cũng đi qua hai ACK này trước khi spawn/START.

ROOM_STATE RP nối sau starting HP: `roomMode:u8, stage:u8, deadlineUs:u64, match:u64, revision:u32, nation:u8 cho mỗi member theo cùng thứ tự`. Nation 255 là chưa chọn. ROOM_LIST_RESULT nối các mảng locked, canRejoin và roomMode theo cùng thứ tự phòng.

ACTION_RESULT cho USE: `status:u16`, thêm `reason:string` nếu lỗi, rồi `match:u64, requestId:u32, action:u8, slot:u8, appliedTick:u32, readyTick:u16`. Slot không thành công không tiêu cooldown. Kết quả chỉ được gửi sau khi áp dụng; skill intents cùng tick giữ thứ tự hàng đợi trước movement/fire/damage.

Mỗi participant giữ 256 kết quả gần nhất, gồm cả từ chối, cùng opcode và payload gốc. Replay đúng trả cùng kết quả trước kiểm rate/cooldown; cùng ID khác lệnh bị từ chối. High-water từ chối ID đã bị loại khỏi cache và được gửi lại khi rejoin. ID tăng trong từng trận, 0 không hợp lệ.

## Dữ liệu và quan sát

SQLite thêm nullable `matches.room_mode/content_version`, `match_players.nation_id`; `matches.mode` vẫn là network mode. Migration chạy lặp được. Các cột dư trong database cũ được giữ để bảo toàn dữ liệu nhưng không được ứng dụng đọc/ghi. Spool cũ thiếu metadata giữ null. History detail giữ optional duration; nếu có metadata nhưng không có duration, duration wire là `u32.MaxValue`, theo sau metadata version1, room mode, content version và danh sách slot/nation (255 cho null).

Server ghi `ROLEPLAY_USE`, `ROLEPLAY_ACTION`, `ROLEPLAY_SUMMARY` và `ROLEPLAY_RATE` vào log server hiện có. Summary có số lần dùng và số tick sống khi kỹ năng sẵn sàng, để phân biệt không dùng với không có cơ hội. Chỉ số tiếp năng lượng/lướt của bộ kỹ năng cũ không còn được mở cho kỹ năng mới.

Các log và test tự động không thay thế playtest về cảm giác chơi. Thiết kế HP, kỹ năng, hiệu ứng và tiêu chí kiểm chứng nằm trong `plan/plan_change.md`; thông số nguồn dùng chung ở hai JSON cùng thư mục.
