# Giao thức và hợp đồng dùng chung

Đối chiếu working tree dựa trên HEAD `e788610bdb0e2cd9fbd3f0cce54d9d29e815947e`, có thay đổi chưa commit. Phiên bản hiện tại là **16**, được khai báo thống nhất trong JSON và Java/C# sinh ra. Các bố cục bên dưới được đối chiếu bằng cách đọc mã và test, không phải tuyên bố test đã chạy. Luật chơi chi tiết nằm ở [gameplay](gameplay.md); lưu kết quả nằm ở [data](data.md).

Mục lục: [JSON, sinh mã và hash](#shared-contracts) · [Transport](#transport) · [Snapshot](#snapshot) · [Delta](#delta) · [Sự kiện combat](#combat-events) · [Wave battlefield](#battlefield-wire) · [TCP](#tcp) · [Vào lại](#rejoin) · [Prediction](#prediction) · [Điểm đọc test](#tests).

<a id="shared-contracts"></a>
## JSON, sinh mã và hash

Nguồn thông số là [source/shared/game-rules.json](../source/shared/game-rules.json); tên, mô tả, mapping nation/Q, cooldown/duration và sprite bắt buộc nằm trong [roleplay-content.json](../source/shared/roleplay-content.json). Sửa JSON rồi chạy từ gốc repo:

```powershell
python scripts/sync-contracts.py
python scripts/sync-contracts.py --check
```

Không sửa tay `GameRules.java/.cs` hoặc `RoleplayContent.java/.cs`. [scripts/sync-contracts.py](../scripts/sync-contracts.py) sinh cả bốn file và kiểm nation đúng thứ tự 0–7, content version vừa `u16`, cooldown dương, duration không âm, tên/mô tả bắt buộc, sprite duy nhất và các deadline nội dung `MaxMatchTicks + cooldown/duration <= 65535`. `--check` chỉ so khớp output mong đợi, không sửa file và không thay thế test gameplay.

Content hash là SHA-256 của UTF-8 JSON canonical `{"rules": rules, "content": content}` với khóa sắp xếp, không khoảng trắng phân cách, `ensure_ascii=False`; bao gồm cả tên, mapping và thông số. Content version hiện **7**, nhưng hash mới là kiểm tra chính xác toàn bộ nội dung, kể cả luật thay đổi mà số version nội dung không đổi.

Map hash độc lập: SHA-256 của **byte gốc** trong JSON map; thay khoảng trắng cũng đổi hash. Server [GameMap.load](../source/server/src/main/java/tank/GameMap.java) và client [MapLoader](../source/client/Assets/Scripts/MapLoader.cs) phải nhận cùng bytes. Đây không phải hash của object sau parse.

Trong RP, loadout hash lại là SHA-256 **payload nhị phân MATCH_LOADOUT trước 32 byte hash cuối**: match, revision, content hash, count và cặp slot/nation đã sắp. Nó giữ mapping participant kể cả người đã rời trận. Client xác minh rồi gắn nation vào cả FULL và delta; nation không chiếm byte trong record xe.

Nguồn: [RoleplayContent.java](../source/server/src/main/java/tank/RoleplayContent.java), [RoleplayContent.cs](../source/client/Assets/Scripts/RoleplayContent.cs), [RoleplayMatch.publishLoadout](../source/server/src/main/java/tank/RoleplayMatch.java), [ClientCore.ReceiveLoadout/AttachLoadout](../source/client/Assets/Scripts/ClientCore.Roleplay.cs).

<a id="transport"></a>
## Transport, frame và input UDP

TCP điều khiển tài khoản/phòng/vòng đời/kỹ năng; UDP vận chuyển handshake, đồng hồ, input và snapshot. Mọi số nhiều byte dùng **network byte order (big endian)**; `f32` là IEEE-754 hữu hạn, góc là 1/100 độ trong `[0,35999]`. `string` là `length:u16` theo byte, sau đó UTF-8; boolean chỉ nhận 0/1. Reader từ chối thiếu/trailing bytes và float không hữu hạn.

Frame TCP và từng datagram UDP có header **12 byte**:

```text
magic:u16=0x5442, version:u8=16, opcode:u8,
sequence:u32, payloadLength:u16, flags:u16=0, payload:bytes
```

Payload frame tối đa 16384 byte; UDP cả header tối đa **1200 byte**. Snapshot lớn không đóng thành một frame khổng lồ mà phân mảnh ở tầng ứng dụng. Phiên bản khác 16 bị từ chối ngay tại header TCP/UDP; test nêu rõ các peer 5–15 bị từ chối. Java và Unity phải triển khai cùng đợt, với map và content hash khớp.

`Protocol.Mode` / `NetworkMode` theo thứ tự wire: `BASELINE=0`, `INPUT_REDUNDANCY=1`, `DELTA=2`, `COMBINED=3`. Hai mode có redundancy gửi tối đa ba input gần nhất; hai mode có delta cho phép snapshot chênh lệch. Đây là tùy chọn truyền mạng của server, không phải `RoomMode.NORMAL/ROLEPLAY`.

Mọi gói client→server UDP có prefix `token:16 bytes, match:u64`. Token gắn session; server chỉ nhận endpoint đã handshake cho match đó. Các payload tiếp sau prefix:

| Opcode | Phần sau prefix |
|---|---|
| 16 HANDSHAKE | `nonce:u64` |
| 18 PING | `clientTimeUs:u64` |
| 32 INPUT | `ackSnapshot:u32, count:u8, records[count], aimX:f32, aimY:f32` |
| 33 ACK | `snapshotId:u32` |
| 34 RESYNC | `missingBaseId:u32` (server yêu cầu FULL tiếp theo) |

Input record **7 byte**: `tick:u32, buttons:u8, angle:u16`. Bit buttons: 0 lên, 1 xuống, 2 trái, 3 phải, 4 bắn, 5 Shift; bit khác không hợp lệ. Client gửi record mới nhất trước, count=1 hoặc tối đa 3 tùy mode, rồi tâm ngắm Q. Server hiện cho phép thiếu cặp aim để mặc định 0/0; client hiện tại luôn ghi đủ. Aim chỉ cập nhật từ tick mới hơn đã nhận, lớn hơn tick world và không vượt `world.tick+30`.

`InputBuffer.put` nhận tick tương lai trong cửa sổ 30 tick, tối đa 64 record; tick đã xử lý bị tính late. Cùng tick giữ record đầu, phân biệt duplicate/conflict. Tick thiếu resolve thành buttons=0, giữ góc nòng hiện tại; không tự phát lại bắn/di chuyển cũ.

Server→client UDP:

| Opcode | Payload |
|---|---|
| 17 HANDSHAKE_ACK | `match:u64, nonce:u64, slot:u8, serverTimeUs:u64` |
| 19 PONG | `match:u64, echoedClientTimeUs:u64, receivedUs:u64, sentUs:u64` |
| 48 FULL / 49 DELTA | Metadata snapshot và world/patch ở phần sau |
| 50 SNAPSHOT_PART | Phân mảnh snapshot ở phần sau |

Nguồn: [Protocol.frame/decode/read, Writer/Reader](../source/server/src/main/java/tank/Protocol.java), [Protocol.Frame/Decode/ReadAsync](../source/client/Assets/Scripts/Protocol.cs), [Server.route](../source/server/src/main/java/tank/Server.java), [InputBuffer](../source/server/src/main/java/tank/InputBuffer.java), [ClientCore.GenerateInput/Poll](../source/client/Assets/Scripts/ClientCore.cs).

<a id="snapshot"></a>
## FULL và các extension của snapshot v16

Metadata chung **30 byte** trước world:

```text
match:u64, snapshotId:u32, tick:u32, serverTimeUs:u64,
resolvedInputTick:u32, remainingMatchTicks:u16
```

FULL nối trực tiếp world. DELTA nối `baseSnapshotId:u32` rồi patch; base là snapshot mà chính client đã ACK, không mặc định là gói gửi ngay trước. Server gửi snapshot mỗi bước mô phỏng, buộc FULL tại tick chia hết 30, khi force/resync hoặc thiếu base. Chỉ chọn DELTA nếu `delta.length + 4 < full.length`; cache tối đa 128 world đầy đủ cho mỗi member.

Thứ tự FULL world bắt buộc:

```text
tankCount:u8, tanks[64 byte mỗi xe], bulletCount:u8, bullets[21 byte mỗi đạn],
brickHp:64 bytes, skillEffects, tankItemState + mapItems + itemEvents,
combatEvents, battlefield
```

### Record xe: 67 byte

| Theo thứ tự ghi | Kiểu |
|---|---|
| slot | `u8` |
| x, y | `f32, f32` |
| body, turret, hp, kills, deaths | năm `u16` |
| status | `u8` (0 chết, 1 sống) |
| respawn, shield | hai `u32` |
| maxHp, energy, energyRecovery, lastKill | `u16, u8, u8, u32` |
| qReadyTick, qEndTick, slowEndTick, protectionFlags | `u16, u16, u16, u8` |
| qStartTick, confusionEndTick, fireLockEndTick, fogEndTick | bốn `u16` |
| qStage, aimX, aimY, life | `u8, f32, f32, u32` |
| rocketSlowEndTick, rocketSlowStacks | `u16, u8` |

Tối đa tám xe, slot 0–7 duy nhất; HP không vượt MaxHP 1–500, energy không vượt 150, energyRecovery không vượt 15, status chỉ 0/1. `lastKill=-1` nội bộ được truyền thành `0xffffffff`. Deadline Q/debuff là tick tuyệt đối, hiệu lực khi `tick < end`; `qStage=1` là đang ngắm Gấu Tuyết hoặc chuẩn bị Tháp Nghiêng/Anh Đào. `Life` đổi mỗi spawn, kể cả rejoin. `rocketSlowStacks` là số lần trúng rocket còn hiệu lực (0–9); mỗi lần nhân tốc độ 0,9 khi tick còn trước `rocketSlowEndTick`, cùng deadline kết thúc AREA của lượt gọi. V16 thêm hai field này để FULL/DELTA, prediction và rejoin dùng cùng trạng thái; v15 bị từ chối tại header. `ProtectionFlags` chỉ cho bit0, vẫn giữ trong record nhưng bộ Q hiện tại không dùng. `qReadyTick` là hồi hiệu lực sau phá hoại; hồi gốc nằm trong extension item.

### Record đạn: 21 byte

```text
id:u32, owner:u8, x:f32, y:f32, heading:u16, bounces:u8,
speedPercent:u8, remainingLifetime:u16, damage:u8, kind:u8
```

Tối đa 128 đạn. Owner 0–7, bounce 0–2, speedPercent 100–150, lifetime 0–150. Kind 0–5: thường/Q Thiết Ưng, HE, Mirror mẹ, Mirror con, HE+Mirror mẹ, HE+Mirror con. Damage hợp lệ: thường/mẹ 20 hoặc 15; con 10 hoặc 25. Kích thước và quy tắc chốt thuộc tính khi khai hỏa ở [gameplay/items](gameplay.md#items).

### Gạch và hiệu ứng kỹ năng

Gạch là mảng đúng **64 byte**, mỗi HP 0–3; ID map xác định vị trí, HP=0 xóa collision. Sau đó `count:u16`, tối đa **192** skill effect **24 byte**:

```text
id:u32, kind:u8, owner:u8, start:u16, end:u16,
x:f32, y:f32, radius:f32, angle:u16
```

Kind 1–8: ROCKET, BOMB, KAMIKAZE, BEAM, RADAR, SABOTAGE, AREA, ACTIVATE. ID dương, owner 0–7, end≥start, radius không âm; ID không trùng trong stream. Hiệu ứng giữ đến sau `end+24` tick; lịch gameplay do server sở hữu, không chạy lại từ afterglow. Bom truy bám cập nhật x/y nhưng giữ ID. Rocket giữ ID/lịch; x/y có thể được chốt lại một lần ở tick bắt đầu cảnh báo để trỏ vị trí xe đối phương, rồi giữ nguyên đến impact. Client vẽ điểm rơi từ effect server, không tự truy bám xe. Danh sách này **thay thế toàn bộ cả trong FULL lẫn DELTA**.

### Extension item: thay thế toàn bộ trong FULL và DELTA

| Thành phần theo thứ tự | Định dạng và giới hạn |
|---|---|
| Trạng thái buff mỗi xe | `tankCount:u8`, phải bằng đúng số xe; mỗi slot một lần: `slot:u8, armorEnd:u16, heEnd:u16, invincibilityEnd:u16, mirrorShots:u8, qBaseReady:u16` — **10 byte/xe** |
| Item trên map | `itemCount:u8`, tối đa 6; `id:u32, type:u8, x:f32, y:f32, expires:u16` — **15 byte/item** |
| Hiệu ứng item | `eventCount:u16`, tối đa 256; record cùng dạng skill effect **24 byte** |

Buff deadline là tick tuyệt đối 30 Hz; mirrorShots 0–3. Type item 0–5 là Medical/Armor/HE/Purification/Invincibility/Mirror. Item ID duy nhất trong trận; `expires=0` nghĩa là không tự hết hạn, khác 0 là tick hết hạn tuyệt đối. Giới hạn gameplay ROLEPLAY bốn item không thay trần codec sáu item.

Kind item event: **9 PICKUP** (`angle=type`), **10 EXPLOSION**, **11 SPLIT** (`angle=hướng phản xạ`). ID riêng theo match/stream, `start=end=tick` và giữ thêm 24 tick; đầy ngân sách bỏ event hình ảnh cũ nhất. Client dùng thời gian server và watermark snapshot đầu để snapshot lặp không khởi động lại animation cũ. Kind=11 ở đây là SPLIT; `causeKind=11` trong combat lại là FIRE, thuộc namespace khác.

Nguồn: [World.writeTank/readTank/writeBullet/encode/decode](../source/server/src/main/java/tank/World.java), [WorldState.Full/Encode](../source/client/Assets/Scripts/GameState.cs), [NationSkills.encode/read](../source/server/src/main/java/tank/NationSkills.java), [WorldItems.encode/read](../source/server/src/main/java/tank/WorldItems.java), [ItemCodec](../source/client/Assets/Scripts/ItemState.cs), [Room.snapshot](../source/server/src/main/java/tank/Room.java).

<a id="delta"></a>
## Delta, tính toàn vẹn và phân mảnh

Patch ghi theo thứ tự:

1. `tankChanges:u8`, mỗi record `slot:u8, mask:u32, các field có bit, [presence:u8]`.
2. `removedBullets:u8, removedIds:u32[]`, rồi `changedBullets:u8`, mỗi record `id:u32, mask:u16, các field có bit`.
3. `brickChanges:u8`, mỗi record `id:u8, hp:u8`.
4. Toàn bộ skill effects, extension item, combat delta, rồi toàn bộ battlefield extension.

Tank mask đầy đủ **`0x1fffffff`**: bit0–9 lần lượt x/y/body/turret/hp/kills/deaths/status/respawn/shield; bit10 là presence; bit11–18 maxHp/energy/energyRecovery/lastKill/qReady/qEnd/slowEnd/protectionFlags; bit19–26 qStart/confusionEnd/fireLockEnd/fogEnd/qStage/aimX/aimY/life; bit27–28 rocketSlowEndTick/rocketSlowStacks. Presence được ghi **sau** mọi field dữ liệu có bit. Chỉ mask `1<<10` và presence=0 xóa xe; entity mới phải đủ mask, presence=1. Bullet mask `u16`, đầy đủ **`0x1ff`**: bit0–8 owner/x/y/heading/bounces/speedPercent/remainingLifetime/damage/kind. Đạn mới phải đầy đủ field. Mask rỗng/bit ngoài hợp đồng, slot/ID lặp, HP gạch ngoài 0–3 hoặc thiếu dữ liệu bị từ chối.

Receiver áp dụng patch lên **bản sao** base, đọc/kiểm tra mọi extension và trailing bytes trước khi công bố. Combat record và wave cùng ID phải bất biến; client kiểm wave ID/geometry/timing từ map và combat không ở tương lai. Lỗi không thay base, Latest hay ACK. Thiếu base không đoán state: client yêu cầu RESYNC, rate giới hạn 500 ms. Snapshot được ghép và xác minh xong mới vào cache/Latest; ACK dùng `LastSnapshot` đã chấp nhận, qua INPUT hoặc ACK định kỳ.

FULL world cực đại hiện tại **25238 byte**: 8 xe, 128 đạn, 64 HP gạch, 192 skill effect, 8 trạng thái buff xe, 6 item, 256 item event, 256 combat event và một wave 15 target. Cộng metadata snapshot thành **25268 byte**. Đây là kích thước xác định bởi layout và assertion hiện có ở `CombatEventsTest.worstCaseFullAndReplacementDeltaIncludingAllExtensionsFitBudgets`, không phải số đo mới được chạy trong đợt tài liệu này. Test còn dựng delta thay toàn bộ ID đạn/combat và kiểm payload ghép ≤32768 byte. Con số 25241 và wave 18 target trong tài liệu v14 cũ đã hết hiệu lực.

Snapshot vượt 1200 byte có opcode **50 SNAPSHOT_PART**:

```text
match:u64, snapshotId:u32, originalOp:u8, index:u8, count:u8,
totalLength:u16, bytes:chunk
```

`originalOp` chỉ FULL/DELTA, chunk tối đa **1100 byte**; cộng 17 byte metadata phần và 12 byte header là datagram tối đa **1129 byte**. Payload sau ghép tối đa **32768 byte**. Client giữ tối đa **16** snapshot chưa đủ phần, hết hạn **500 ms**, kiểm index/count/length/identity và duplicate nội dung; phần thiếu/hỏng không được ACK như một snapshot hoàn chỉnh. Số frame nhận không đồng nghĩa số snapshot decode.

Nguồn: [DeltaCodec.encode/apply](../source/server/src/main/java/tank/DeltaCodec.java), [DeltaCodec.Apply](../source/client/Assets/Scripts/DeltaCodec.cs), [SnapshotFragments.split](../source/server/src/main/java/tank/SnapshotFragments.java), [SnapshotAssembler.Add](../source/client/Assets/Scripts/SkillState.cs), [ClientCore.ReceiveSnapshot](../source/client/Assets/Scripts/ClientCore.cs).

<a id="combat-events"></a>
## Combat event: nhận dạng nguyên nhân và chống phát lại

FULL nối sau extension item: `combatCount:u16, records[]` theo ID tăng. DELTA ghi `removedCount:u16, removedIds:u32[]`, rồi `addedCount:u16, records[]` so với base đã ACK. Record bất biến; không được xóa rồi thêm cùng ID trong một patch để sửa nội dung.

Record COMBAT **43 byte**:

```text
id:u32, tick:u32, kind:u8, sourceSlot:u8, targetSlot:u8,
sourceLife:u32, targetLife:u32, bulletId:u32, objectId:u16, surface:u8,
x:f32, y:f32, angle:u16, value:u16, causeKind:u8, causeId:u32
```

ID và Life hợp lệ dương trong phạm vi int khi có xe; slot không áp dụng=255 đi với Life=0. Object không áp dụng=65535, còn lại ID gạch 0–63. Surface=0 xe/không áp dụng, 1 thép, 2 gạch. Kind: **1 SHOT, 2 IMPACT, 3 BOUNCE, 4 DAMAGE, 5 DEATH, 6 BRICK_DESTROYED**. Value là HP thực mất cho DAMAGE, MaxHP cho DEATH, còn lại 0.

Cause chỉ dành cho DAMAGE/DEATH: `0/0` là không có effect nguyên nhân; kind **1–4** trỏ ROCKET/BOMB/KAMIKAZE/BEAM của skill; **10** trỏ EXPLOSION của item; **11/12** trỏ wave FIRE/AIRSTRIKE. Cause có thể không còn trong snapshot vì retention khác nhau. Hit trực tiếp giữ bulletId; damage vùng/Q/môi trường dùng 0. Môi trường có source slot/life=255/0. Hit trực tiếp và splash HE cùng contact tham chiếu cùng EXPLOSION, giữ thứ tự damage trực tiếp rồi splash.

Q/item không bị sao chép thành một combat event hình ảnh tương đương: contact HE có EXPLOSION, Mirror có SPLIT, kết hợp HE+Mirror phát EXPLOSION rồi SPLIT. Contact thường có IMPACT hoặc BOUNCE. DEATH là kết quả chết thống nhất cho mọi nation. ROCKET/BOMB/KAMIKAZE/HE và AIRSTRIKE sở hữu explosion/audio; DEATH liên kết chỉ thêm phản hồi hạ gục/wreck theo Life. Beam và nổ xe là hai lớp riêng; FIRE không triệt nổ xe thông thường.

Client chống trùng theo match/stream/ID/phase, phân quyền hiệu ứng theo cause, không suy nguyên nhân từ khoảng cách. COMBAT giữ khi tuổi <**90 tick**, tối đa **256** record: **160** nhóm trang trí và **96** DEATH/BRICK_DESTROYED. Tràn mỗi nhóm bỏ record cũ nhất và tăng counter, không đổi gameplay. Q/item giữ đến End+24 với ngân sách riêng. Không bảo đảm giao mọi one-shot nếu mất mạng vượt retention. Snapshot đầu khi vào/rejoin đặt watermark bỏ one-shot cũ; trạng thái liên tục được dựng lại.

Nguồn: [CombatEvents.Event/add/prune/delta/apply](../source/server/src/main/java/tank/CombatEvents.java), [CombatEvent/CombatCodec](../source/client/Assets/Scripts/CombatState.cs), [CombatPresentation](../source/client/Assets/Scripts/CombatPresentation.cs), [CombatRendering](../source/client/Assets/Scripts/CombatRendering.cs). Test: [CombatEventsTest](../source/server/src/test/java/tank/CombatEventsTest.java), [CombatTests](../source/client/Assets/Tests/Editor/CombatTests.cs).

<a id="battlefield-wire"></a>
## Wave battlefield v15

FULL và DELTA đều nối **sau COMBAT** một extension thay thế toàn bộ:

```text
present:u8 (0 hoặc 1)
nếu present=1:
  id:u32, kind:u8, start:u32, impact:u32, end:u32,
  targetCount:u8, targetIds:u8[]
```

Không có wave: 1 byte. Có wave: **34 byte**, gồm đúng **15** target ID theo thứ tự ba round × năm. Kind **2 FIRE**, **3 AIRSTRIKE**; kind1 đã dành cho biến cố cũ bị loại bỏ và luôn bị từ chối. Wave ID dương theo match, record cùng ID bất biến; `impact=start+60`, `end=impact+240`. ID target 0–63 phải tồn tại trong đúng danh sách map. Mỗi round không trùng ID, khoảng cách giữa vùng được client kiểm bằng geometry map; Bombstorm còn không lặp vùng round liền trước. Tọa độ không gửi trong wave.

Codec Java/C# và test v15 yêu cầu năm vùng; v14 với sáu vùng/round không tương thích dù tên field không đổi. Thời điểm round, damage, lịch và trình bày hiện tại nằm ở [gameplay/battlefield](gameplay.md#battlefield).

Nguồn: [WorldBattlefield.Wave/encode/read](../source/server/src/main/java/tank/WorldBattlefield.java), [BattlefieldWave/Codec](../source/client/Assets/Scripts/BattlefieldState.cs). Test: [BattlefieldTest](../source/server/src/test/java/tank/BattlefieldTest.java), [BattlefieldTests](../source/client/Assets/Tests/Editor/BattlefieldTests.cs).

<a id="tcp"></a>
## Điều khiển TCP và chuẩn bị trận

Các nhóm opcode trong [Protocol](../source/server/src/main/java/tank/Protocol.java): tài khoản 1–6; phòng 7–15; trạng thái/vòng đời 64–70; history 71/72 và 78/79; heartbeat 73/74; display name 75/76; room settings 77; nhập vai 80/84/85. Request/response dùng sequence; event do server chủ động gửi dùng cơ chế `Session.event`. Response thường bắt đầu `status:u16`; lỗi có `reason:string`. Payload bên dưới không bao gồm header frame.

Ví vàng/shop dùng cặp TCP bổ sung **86 SHOP / 87 SHOP_RESULT**, payload shop **version 2**; giữ payload shop độc lập với record xe của header v16. Query: `command:u8=0, match:u64` (0 tải ví ngoài trận; khác 0 lấy thêm giới hạn mua và túi item của trận RP hiện tại). Mua: `command:u8=1, match:u64, wave:u8, item:u8`. Dùng item: `command:u8=2, match:u64, inventoryRevision:u32, item:u8`. Wave đánh số từ 1 mỗi 1200 tick; item=0–5 theo ItemState. Server xác thực tài khoản, kiểm tra mode/trận, xe sống; mua còn kiểm tra đợt đang mở, số dư và giới hạn mỗi loại. Mua tăng số lượng trong túi, không áp dụng buff. Dùng item không yêu cầu shop mở, nhưng revision phải khớp; trừ một item và tăng revision để request lặp không dùng hai lần. Thành công: `status:u16=0, version:u8=2, gold:u64, match:u64, wave:u8, purchasedMask:u8, inventoryRevision:u32, inventoryCounts:6*u8`; bit item=1 nghĩa đã mua trong đợt đó. Query ngoài đợt trả wave/mask=0 nhưng vẫn trả túi của trận. Lỗi: `status:u16!=0, reason:string`. Không gửi ví/túi tài khoản khác hoặc trừ vàng theo giá client. Client tải ví sau login và cập nhật định kỳ, chỉ áp dụng mask khi match/wave khớp và túi khi match khớp. Golden Java/C# có query, mua, dùng item, thành công và lỗi; tổng 46 opcode. Client/server phải cập nhật cùng phiên bản payload.

### Phòng và bắt đầu

| Opcode | Payload hiện tại |
|---|---|
| 9 CREATE_ROOM | `name:string, capacity:u8, seconds:u16, password:string, startingHp:u16, roomMode:u8`; server giữ một số hậu tố tùy chọn để dùng mặc định |
| 10 JOIN_ROOM | `roomId:u32, [password:string]`; cũng dùng cho participant vào lại |
| 11 LEAVE_ROOM | rỗng |
| 12 SET_READY | `ready:bool` |
| 13 START_MATCH | rỗng; chỉ host |
| 14 KICK_PLAYER | `slot:u8`; chỉ host |
| 77 ROOM_SETTINGS | `name:string, capacity:u8, seconds:u16, passwordMode:u8, [password:string nếu mode=2], [startingHp:u16]` |
| 80 SELECT_NATION | `nation:u8`; chỉ lobby RP, chưa ready, nation chưa ai chọn |

ROOM_LIST_RESULT (8) bắt đầu `status:u16, count:u16`, sau đó từng `roomId:u32, name:string, players:u8, capacity:u8, state:u8`; nối ba mảng theo cùng thứ tự phòng: `locked:bool[]`, `canRejoin:bool[]`, `roomMode:u8[]`.

ROOM_STATE (64): `roomId:u32, name:string, state:u8, host:u8, count:u8`, rồi từng `slot:u8, nickname:string, ready:bool`; tiếp `capacity:u8, seconds:u16, locked:bool, selfSlot:u8, startingHp:u16`. RP nối `roomMode:u8, stage:u8, deadlineUs:u64, match:u64, revision:u32`, rồi `nation:u8` cho từng member theo cùng thứ tự; nation=255 nghĩa chưa chọn. Host=255 khi chưa có host. `RoomState`: WAITING=0, PREPARING=1, RUNNING=2, RESULTS=3.

PREPARE (65) nền:

```text
match:u64, roomId:u32, slot:u8, mapId:u16, mapHash:32 bytes,
networkMode:u8, tickRate:u8=30, udpPort:u16
```

NORMAL vào lại nối `rejoining:bool=true`; NORMAL lần đầu có thể không có hậu tố. RP luôn nối `rejoining:bool, roomMode:u8, contentVersion:u16, contentHash:32 bytes, requestHighWater:u32`. Client kiểm map/rate, content, sprite bắt buộc và combat asset, hoàn tất handshake UDP và lấy **tám mẫu đồng hồ** trước ACK tải.

RP giữ RoomState.PREPARING với ba `PrepStage`: **LOADING=0** tối đa 10 giây → **SYNC_LOADOUT=1** tối đa 5 giây → **COUNTDOWN=2** ba giây. Tải/loadout quá hạn hoặc thành viên rời khi chuẩn bị hủy về lobby; ACK đến muộn không khởi động lại pipeline.

PREPARED (66) NORMAL: `match:u64`. RP: `match:u64, stage:u8, revision:u32, hash:32 bytes`; LOADING dùng content version/hash, SYNC_LOADOUT dùng loadout revision/hash. MATCH_LOADOUT (85):

```text
match:u64, revision:u32, contentHash:32 bytes, count:u8,
sorted(slot:u8, nation:u8)[count], loadoutHash:32 bytes
```

Client kiểm count1–8, slot tăng 0–7, nation duy nhất 0–7, có self slot, revision dương và cả hai hash. START (67) là `match:u64, startUs:u64`; server chỉ chuyển RUNNING khi đủ điều kiện và tới giờ bắt đầu. Khi rejoin, START mang mốc start cũ của trận.

Nguồn: [Server.Session.request](../source/server/src/main/java/tank/Server.java), [Room.broadcast/sendPrepare/prepare/prepared/tick](../source/server/src/main/java/tank/Room.java), [RoleplayMatch.selectNation/publishLoadout/prepared](../source/server/src/main/java/tank/RoleplayMatch.java), [ClientCore.Handle](../source/client/Assets/Scripts/ClientCore.Messages.cs), [ClientCore.Poll](../source/client/Assets/Scripts/ClientCore.cs).

### USE_SKILL và idempotency

USE_SKILL (84): `match:u64, requestId:u32, slot:u8, aimX:f32, aimY:f32`. Slot ở đây là **ability slot**, chỉ 0=Q, không phải slot người chơi. Server hiện cho phép thiếu aim để mặc định 0/0; client hiện tại ghi đủ. ACTION_RESULT (15) cho USE:

```text
status:u16, [reason:string nếu lỗi], match:u64, requestId:u32,
action:u8, slot:u8, appliedTick:u32, readyTick:u16
```

Kết quả được gửi sau khi intent được áp dụng; intent cùng tick giữ thứ tự hàng đợi trước di chuyển/bắn/damage. Thất bại không tiêu cooldown Q. Mỗi participant giữ **256** kết quả gần nhất, kể cả từ chối, cùng opcode và bytes request gốc. Replay đúng trả kết quả cũ **trước** rate/cooldown check; cùng ID nhưng lệnh khác bị từ chối. Request đang chờ có thể giữ tối đa 16 response sequence. ID unsigned tăng trong từng trận, 0 không hợp lệ; high-water từ chối ID đã bị loại khỏi cache và được gửi khi rejoin để client tiếp tục dãy ID. Mất response không được coi là Q chưa áp dụng.

Nguồn: [RoleplayMatch.dispatch/use/result](../source/server/src/main/java/tank/RoleplayMatch.java), [ClientCore.UseSkill/RoleplayResult](../source/client/Assets/Scripts/ClientCore.Roleplay.cs). Test: [RoleplayRoomTest.duplicateSkillRequestsApplyOnceAndReturnTheSameAppliedTick/evictedRequestIdsAreRejected](../source/server/src/test/java/tank/RoleplayRoomTest.java).

### Kết quả và lịch sử

END (68): `match:u64, reason:u8, count:u8`, rồi `slot:u8, nickname:string, kills:u16, deaths:u16, lastKill:u32, rank:u8, disconnected:bool`. Reason hiện tại: 0 đủ thời gian, 1 không còn đủ người chơi, 2 server dừng. REMOVED (69): `roomId:u32, slot:u8, reason:u8` (0 tự rời, 1 TCP đóng, 2 UDP timeout, 3 AFK/quá hạn rejoin, 4 bị kick).

HISTORY (71): `offset:u16, limit:u8`, offset≤1000, limit1–20. HISTORY_RESULT (72) thành công: `status:u16=0, count:u8`, rồi từng `match:u64, endedUtcMs:u64, reason:u8, kills:u16, deaths:u16, rank:u8`.

HISTORY_DETAIL (78): `match:u64`. HISTORY_DETAIL_RESULT (79) thành công:

```text
status:u16=0, match:u64, mapId:u16, startedUtcMs:u64, endedUtcMs:u64,
reason:u8, count:u8,
players[count] = slot:u8, nickname:string, kills:u16, deaths:u16,
                 rank:u8, disconnected:bool, isSelf:bool
[durationTicks:u32]
[metadataVersion:u8=1, roomMode:string, contentVersion:string,
 nationCount:u8, (slot:u8, nation:u8)[nationCount]]
```

Legacy có thể kết thúc ngay sau players, hoặc chỉ có duration. Khi có metadata mà thiếu duration, trường duration vẫn có mặt với sentinel **`0xffffffff`**, rồi metadata version1; nation=255 biểu diễn null, string metadata null truyền thành rỗng. Wire không gửi account ID, map hash, seed hoặc network mode của detail dù database lưu chúng. Cách chọn duration và dữ liệu cũ nằm ở [data/history](data.md#history).

<a id="rejoin"></a>
## Mất kết nối và vào lại

Participant đã rời với reason0–3 có thể JOIN lại **cùng trận đang RUNNING**, nếu còn chỗ và slot chưa được dùng; người ngoài trận hoặc bị kick không vào giữa trận. Trận vẫn phải còn đủ hai người đang chơi để tiếp tục. UDP không hợp lệ/không đến quá **10 giây** gây leave; rejoin cũng có deadline chuẩn bị **10 giây**. AFK cảnh báo sau **15 giây** sống không hoạt động, cho thêm 15 giây; hoạt động là di chuyển/bắn hoặc đổi aim quá ngưỡng, Shift đứng yên không đủ.

Rejoin đặt endpoint mới, input/cache/ACK mới và force FULL; RP phải qua cả ACK content và loadout trước spawn/START. Server giữ participant, nation, kết quả request/high-water và điểm số. Spawn tạo `Life` mới, vị trí/HP/năng lượng theo cơ chế spawn, không khôi phục vị trí/HP cũ.

Nếu ngắt khi còn sống, deadline Armor/HE/Invincibility và số lượt Mirror được khôi phục **nguyên giá trị**, không kéo dài theo thời gian ngắt; nếu đã chết thì buff đã bị xóa. Hồi Q gốc/hiệu lực, phần trì hoãn và nguồn phá hoại giữ qua cả respawn lẫn rejoin. Life mới không nhận lại fog/debuff của đời trước. Item trên map, trạng thái xe khác, wave/cảnh báo/lửa, bom đang theo lịch và HP gạch được lấy từ snapshot hiện tại. Bom gắn Life cũ không chuyển sang Life mới.

Client xóa lịch input/snapshot/fragment cũ khi PREPARE; snapshot đầu dựng lại continuous state và đặt watermark bỏ one-shot trước/đúng tick đầu. Round Bombstorm đã qua bị bỏ, round tương lai vẫn hiện; không dùng replay toàn bộ hiệu ứng để bù khoảng ngắt.

Nguồn: [Room.Member.canRejoin/rejoin/prepared/leave/tick](../source/server/src/main/java/tank/Room.java), [ClientCore PREPARE handler](../source/client/Assets/Scripts/ClientCore.Messages.cs), [NationSkills.trackBomb/expireDebuffs](../source/server/src/main/java/tank/NationSkills.java). Test: [NetworkRejoinTest](../source/server/src/test/java/tank/NetworkRejoinTest.java), [PlayerRecoveryTest](../source/server/src/test/java/tank/PlayerRecoveryTest.java), [RejoinProtocolTests](../source/client/Assets/Tests/Editor/RejoinProtocolTests.cs), [PlayerRecoveryTests](../source/client/Assets/Tests/Editor/PlayerRecoveryTests.cs).

<a id="prediction"></a>
## Đồng hồ, prediction và reconciliation

PONG cho bốn mốc tính RTT `now−clientSent−(serverSent−serverReceived)` và offset `((serverReceived−clientSent)+(serverSent−now))/2`. Tám mẫu đầu dùng offset của RTT thấp nhất; sau đó offset làm mượt 0,9/0,1. Client tính input lead theo RTT/jitter, giới hạn 2–12 tick; `DesiredInputTick` neo vào tick snapshot và tuổi mạng, không vượt cửa sổ tương lai 30 tick của server. Input tick thiếu hoặc quá trễ không được diễn giải thành thời gian mô phỏng mới.

Client prediction chỉ tiến trạng thái xe cục bộ bằng `Movement.Step` với gạch và AREA đã nhận. Snapshot có `resolvedInputTick` cho biết các input đã xử lý; `Reconcile` copy xe authoritative, bỏ input≤resolved, replay phần còn lại. Sai lệch ≤1 đơn vị với xe còn sống được làm mượt render trong 0,1 giây; thay đổi lớn/chết không dùng offset đó. Lịch input ≥256 hoặc tick lệch ngoài cửa sổ gây reset prediction. Client không tự quyết damage/nhặt/kill.

Xe khác dùng `Rendering.Interpolate`: vị trí và góc ngắn nhất giữa snapshot; chỉ nội suy xe sống có cùng respawn/Life, không nội suy xuyên chỗ bị map chặn. Đạn chỉ nội suy khi ID tồn tại và bounce không đổi. Thiếu snapshot sau điểm render thì copy state gần nhất, không tự kéo dài đạn xuyên tường. `TankApp` render trễ 100 ms so với đồng hồ server, phối hợp state đó với xe predicted; khi snapshot cũ quá 500 ms, ẩn xe từ xa. Trình bày combat/wave dùng tick server và dedup riêng.

Nguồn: [ClientCore.ReceiveSnapshot/Reconcile/DesiredInputTick/UpdateInputLead/GenerateInput](../source/client/Assets/Scripts/ClientCore.cs), [ClientCore PONG handler](../source/client/Assets/Scripts/ClientCore.Messages.cs), [Movement/Rendering](../source/client/Assets/Scripts/GameState.cs), [TankApp](../source/client/Assets/Scripts/TankApp.cs). Test: [PredictionTest](../source/server/src/test/java/tank/PredictionTest.java), [ProtocolTests](../source/client/Assets/Tests/Editor/ProtocolTests.cs), [RoleplayClientTests](../source/client/Assets/Tests/Editor/RoleplayClientTests.cs).

<a id="tests"></a>
## Điều hướng kiểm chứng

Các test sau là điểm đọc để hiểu assertion/fixture, chưa được chạy trong đợt viết tài liệu này. Hướng dẫn thực thi và điều kiện môi trường nằm ở [testing](testing.md).

| Hợp đồng | Test/fixture liên quan |
|---|---|
| Header, byte order, frame lỗi và phiên bản | [ProtocolTest](../source/server/src/test/java/tank/ProtocolTest.java), [ProtocolTests](../source/client/Assets/Tests/Editor/ProtocolTests.cs), [ItemCodecTest](../source/server/src/test/java/tank/ItemCodecTest.java) |
| FULL/delta/base ACK và đối chiếu Java/C# | [DeltaTest](../source/server/src/test/java/tank/DeltaTest.java), [GoldenInteropTest](../source/server/src/test/java/tank/GoldenInteropTest.java), [RoleplayCodecTest](../source/server/src/test/java/tank/RoleplayCodecTest.java), [RoleplayCodecTests](../source/client/Assets/Tests/Editor/RoleplayCodecTests.cs) |
| Item/buff/purification/hồi Q và mất delta/rejoin | [ItemCodecTest](../source/server/src/test/java/tank/ItemCodecTest.java), [ItemTests](../source/client/Assets/Tests/Editor/ItemTests.cs) |
| Record bất biến, cause, retention, FULL/delta cực đại và fragments | [CombatEventsTest](../source/server/src/test/java/tank/CombatEventsTest.java), [CombatTests](../source/client/Assets/Tests/Editor/CombatTests.cs) |
| Năm vùng × ba round, timing, v15 và snapshot lỗi không đổi ACK | [BattlefieldTest](../source/server/src/test/java/tank/BattlefieldTest.java), [BattlefieldTests](../source/client/Assets/Tests/Editor/BattlefieldTests.cs) |
| Chuẩn bị loadout/request replay | [RoleplayRoomTest](../source/server/src/test/java/tank/RoleplayRoomTest.java), [RoleplayClientTests](../source/client/Assets/Tests/Editor/RoleplayClientTests.cs) |

Fixture liên ngôn ngữ được Java ghi trong `source/server/target/interop`, rồi client tests đọc; build server trước client tests. Có fixture trace di chuyển, roleplay, bom di chuyển, item/combat, phân mảnh và Bombstorm/rejoin. Mất gói có thể bỏ một-shot quá retention; test phục hồi state không chứng minh mọi animation được giao hoặc cảm giác chơi đã đạt yêu cầu.
