# Luật chơi và mô phỏng

Phần di chuyển/địa hình và map cập nhật ngày **2026-11-06**; các phần còn lại giữ mốc đối chiếu **2026-10-23** với working tree dựa trên HEAD `e788610bdb0e2cd9fbd3f0cce54d9d29e815947e`, có thay đổi chưa commit. Tài liệu mô tả mã và test hiện có; không khẳng định đã chạy test hoặc đã chứng minh độ cân bằng bằng playtest. [JSON luật](../source/shared/game-rules.json) và [nội dung nhập vai](../source/shared/roleplay-content.json) là nguồn thông số; quy trình sinh, hash và wire nằm trong [giao thức](protocol.md#shared-contracts).

Mục lục: [Trận đấu](#match) · [Người chơi máy](#bots) · [Điều khiển](#controls) · [Di chuyển](#movement) · [Chiến đấu](#combat) · [Kỹ năng](#skills) · [Vật phẩm](#items) · [Biến cố chiến trường](#battlefield) · [Bản đồ](#maps).

<a id="match"></a>
## Trận đấu

`RoomMode.NORMAL=0` và `ROLEPLAY=1` là hai chế độ chơi, độc lập với bốn chế độ truyền mạng. Phòng có 2–8 người, HP khởi đầu mặc định 100, cho phép 1–500. Client cho chọn thời lượng 1/3/5/10 phút; server chấp nhận 1–10 phút theo bước một phút và còn có thời lượng mặc định trong cấu hình. Chủ phòng chọn một trong ba bản đồ khi tạo, áp dụng cho cả hai chế độ. Map được giữ cố định trong suốt vòng đời phòng; mặc định là ID 2 — Tiền Đồn Xanh.

Server quản lý trận và mô phỏng ở **30 tick/giây**. Thành viên phải sẵn sàng trước khi chủ phòng bắt đầu; nhập vai còn yêu cầu mỗi người chọn một nation riêng, không đổi lựa chọn khi đang ready. Trình tự tải/đồng bộ và vào lại được mô tả ở [TCP](protocol.md#tcp) và [rejoin](protocol.md#rejoin).

Trận kết thúc khi đủ số tick, khi còn dưới hai thành viên đang chơi, hoặc khi server dừng trận. Xếp hạng theo số kill giảm dần, sau đó thời điểm kill cuối tăng dần (giá trị không có kill được xem như `u32.MaxValue`), cuối cùng slot tăng dần. Death không được dùng làm tiêu chí phụ. Kết quả vẫn gồm participant đã rời trận; [dữ liệu](data.md#persistence) giải thích cách lưu.

Nguồn: [Room.prepare/tick/finish](../source/server/src/main/java/tank/Room.java), [Server.Session.request](../source/server/src/main/java/tank/Server.java), [TankApp.durations](../source/client/Assets/Scripts/TankMenus.cs). Test liên quan: RoomFeaturesTest, RoleplayRoomTest, MatchDurationTest.

<a id="bots"></a>
## Người chơi máy trong phòng công khai

Cập nhật **2026-11-08**. Khi phòng đang chờ, không có mật khẩu và còn người thật, server đợi **15 giây** rồi thêm bot vào chỗ trống. Các bot tiếp theo vào cách nhau ngẫu nhiên **1–3 giây**, dừng khi đủ chỗ hoặc chủ phòng bắt đầu trận. Tên được sinh từ nickname và các thành phần tên Việt, chỉ dùng chữ, không gắn số ở cuối hay nhãn bot trên client. Tên được kiểm tra trùng với username và tên hiển thị đã có; bot cũ có hậu tố số được đổi tên khi nạp pool, giữ nguyên ID và lịch sử trận. Bot tự sẵn sàng sau một khoảng trễ ngắn; trong Nhập vai, bot đợi người thật chọn quốc gia rồi chọn một quốc gia còn trống. Chủ phòng vẫn quyết định bắt đầu trận.

Chủ phòng có thể kick bot bằng thao tác kick thành viên hiện có. Sau khi kick, server chờ lại 15 giây trước khi lấp chỗ; tài khoản vừa bị kick không trở lại phòng đó. Phòng có mật khẩu không thêm bot. Nếu chủ phòng đặt mật khẩu cho phòng đang chờ, các bot hiện có được đưa ra. Khi người thật cuối cùng rời phòng, trận được kết thúc và phòng đóng, giải phóng bot.

AI dùng các ưu tiên đơn giản: tìm vật phẩm gần và hồi máu khi cần, giao chiến khi thấy đối thủ, tìm vị trí di chuyển hoặc đi tuần khi mất mục tiêu. Đường đi dùng A* trên lưới 1 đơn vị, tối đa 3.200 ô mỗi lần tìm, cập nhật khi gạch bị phá và làm thẳng các đoạn đủ trống. Khoảng trống tính cả thân xe xoay; hướng di chuyển được thử bằng cùng mô phỏng va chạm/địa hình của `World`. Khi không tiến được, bot lùi hoặc đổi sang hướng thoáng rồi tìm lại đường.

Trong giao chiến bot giữ khoảng cách, đổi hướng, bắn theo nhịp và dùng Q trong Nhập vai. Shift được dùng theo từng đợt để di chuyển, đổi vị trí và né, có khoảng nghỉ hồi năng lượng; khi di chuyển thường giữ dự trữ để né khẩn cấp. Bot xét quỹ đạo đạn đang nhìn thấy và chỉ né khi có nguy cơ giao nhau, chọn phía còn khoảng trống và giữ hướng né trong một khoảng ngắn. Việc phát hiện đạn vẫn theo nhịp nhận biết, không phản ứng hoàn hảo từng tick. Bot nhận vật phẩm bằng di chuyển qua chúng theo cùng cơ chế với người thật; chưa tự mua đồ trong shop.

Mức trung bình là cấu hình khởi đầu: nhận biết mỗi 0,2–0,4 giây, phản ứng với mục tiêu mới sau khoảng 0,3–0,67 giây và ngắm có sai số. Bot không tìm đối thủ qua tường, không đọc input người thật; máu, tốc độ, sát thương và hồi chiêu dùng nguyên luật `World`. Độ khó vẫn cần playtest với người thật để điều chỉnh; kiểm thử chức năng không chứng minh bot tương đương một mức xếp hạng cụ thể.

Bot có tài khoản nội bộ `accounts.is_bot=1`, không thể đăng nhập bằng mật khẩu và được tái sử dụng giữa các phòng. Kết quả trận và đơn tố cáo vẫn lưu đủ thành viên. Bot không tạo socket TCP/UDP giả và không làm tăng số kết nối thật.

Giới hạn hiện tại: admin có thể xem đơn tố cáo AI và lưu quyết định xử lý, nhưng cảnh cáo không thay đổi hành vi AI; pool AI chưa kiểm tra lệnh khóa có thời hạn. Vì vậy khóa chưa tự đưa AI ra khỏi trận hay ngăn tài khoản AI đó vào phòng khác. Chủ phòng vẫn có thể kick AI trong phòng chờ.

Cấu hình tại [server.properties](../config/server.properties):

```properties
bots.enabled=true
bots.fill.delay.seconds=15
bots.join.min.seconds=1
bots.join.max.seconds=3
```

Giữ 15 giây cho demo; khi phát hành có thể tăng `bots.fill.delay.seconds`, ví dụ 90. Có thể tắt hoàn toàn bằng `bots.enabled=false`. Cấu hình được đọc khi server khởi động. Chế độ benchmark mặc định tắt bot, trừ khi đặt `bots.enabled=true` rõ ràng.

Tham khảo cách tổ chức quyết định theo ưu tiên trong [tài liệu AI của Epic](https://dev.epicgames.com/documentation/unreal-engine/behavior-tree-in-unreal-engine---overview?lang=en-US). Dự án triển khai trực tiếp bằng Java và A*, không cần mô hình học máy hay dịch vụ ngoài. Mã chính: [BotLobby](../source/server/src/main/java/tank/BotLobby.java), [BotBrain](../source/server/src/main/java/tank/BotBrain.java), [BotNavigation](../source/server/src/main/java/tank/BotNavigation.java).

<a id="controls"></a>
## Điều khiển

| Thao tác | Điều khiển hiện tại |
|---|---|
| Di chuyển | WASD hoặc các phím mũi tên |
| Ngắm | Chuột trong vùng đấu trường |
| Bắn thường | Giữ chuột trái trong đấu trường hoặc Space |
| Bật/tắt tự bắn | Chuột phải trong đấu trường |
| Chạy nhanh | Giữ Shift trái/phải, còn năng lượng và đang di chuyển |
| Kỹ năng nhập vai | Q; Gấu Tuyết nhấn Q lần nữa để chốt vùng ngắm |
| Shop Nhập vai | E mở/đóng trong đợt item; Esc đóng shop, khóa input chiến đấu khi đang mở |
| Toàn màn hình | F11 hoặc Alt trái + Enter |

Khi mất focus hoặc mở hộp thoại, client gửi nút trung lập và giữ hướng nòng đã chốt; tự bắn bị tắt. Chuột ra ngoài đấu trường không cập nhật hướng ngắm thường. Vòng ngắm Gấu Tuyết giữ tâm hợp lệ cuối khi chuột ra ngoài bản đồ/HUD/cửa sổ, rồi được giới hạn để cả vùng nằm trong map. Client chỉ gửi ý định; server quyết định va chạm, HP, bắn, nhặt và kỹ năng.

Nguồn: [TankApp.Update](../source/client/Assets/Scripts/TankApp.cs), [GameplayInput.Resolve/MovementButtons, AutoFireControl](../source/client/Assets/Scripts/GameplayInput.cs), [GameplayAim.Angle](../source/client/Assets/Scripts/GameplayAim.cs), [SkillGeometry.TryAim](../source/client/Assets/Scripts/SkillState.cs).

<a id="movement"></a>
## Di chuyển và va chạm

Xe là hình vuông xoay theo thân với nửa cạnh `TankHalfSize=1,02`. Di chuyển chéo chuẩn hóa bằng `1/√2`, nên không nhanh hơn đi thẳng. Vận tốc nền khô là **9 đơn vị/giây**; Shift nhân 1,5 thành 13,5. Hệ số địa hình lấy theo map và vị trí tâm xe đầu bước, áp dụng cho cả Thường/Nhập vai:

| Địa hình | Hệ số tốc độ | Tốc độ không Shift / có Shift |
|---|---|---|
| Nền khô trên mọi map | ×1 | 9 / 13,5 đơn vị/giây |
| Nước Tiền Đồn Xanh (ID 2), map cũ ID 1 và fallback | ×0,5, giảm 50% | 4,5 / 6,75 đơn vị/giây |
| Hồ ốc đảo Pháo Đài Cát (ID 3) | ×1, giữ tốc độ | 9 / 13,5 đơn vị/giây |
| Hồ đóng băng Sông Băng (ID 4) | ×1,5, tăng 50% | 13,5 / 20,25 đơn vị/giây |

Băng chỉ đổi tốc độ khi tâm xe nằm trong vùng hồ; không thêm quán tính hoặc trượt khi thả phím. Hệ số chậm do bom nhân 0,7; vùng Gấu Tuyết đã chốt nhân thêm 0,5. Địa hình, Shift và các hệ số chậm nhân với nhau; nhiều vùng Gấu Tuyết giao nhau chỉ áp dụng 0,5 một lần.

Mỗi bước kiểm tra hình chữ nhật xoay với biên map, thép và gạch còn HP; cập nhật trục X rồi Y, cho phép trượt dọc tường. Thân quay theo hướng di chuyển nếu không gây chồng lấn. Nòng quay theo đường góc ngắn nhất, giới hạn `BaseTurretTurn=400` đơn vị góc/tick, tức 4°/tick. Hỗn loạn đảo hướng di chuyển và khóa cập nhật nòng, không khóa thân xe.

Năng lượng tối đa **150**; mỗi tick thực sự chạy nhanh tiêu thụ 1. Giữ Shift đặt thời gian chờ hồi về **15 tick**, kể cả khi đứng yên/hết năng lượng. Sau khi thả Shift, chờ đủ 15 tick rồi hồi 1/tick tới trần. Khi tâm xe nằm trong hồ ốc đảo ID 3, hồi nhanh **×1,5** sau cùng thời gian chờ: luân phiên cộng 1/2 theo tick mô phỏng, trung bình 45 năng lượng/giây thay vì 30. Ra khỏi hồ trở lại 1/tick; giữ Shift vẫn không hồi. Vì vậy mô tả “giữ Shift là luôn chạy nhanh” không đúng khi năng lượng bằng 0.

Server và prediction client dùng cùng công thức, hình học và deadline; xem [prediction/reconciliation](protocol.md#prediction). Code còn giữ một số hằng số/cơ chế cũ như dash, nhưng chúng không tạo thêm phím kỹ năng đang được cấp cho bộ Q hiện tại.

Nguồn: [World.movement/turn](../source/server/src/main/java/tank/World.java), [GameMap.blocked/bodyOverlap/water](../source/server/src/main/java/tank/GameMap.java), [Movement.Step/Turn](../source/client/Assets/Scripts/GameState.cs), [GameMap.Blocked](../source/client/Assets/Scripts/MapLoader.cs). Test: PredictionTest.sharedMovementTrace, RoleplayGameplayTest.roleplayMovementTraceMatchesPredictionAcrossDeadlineAndTerrain, GameplayUpgradeTests.

TerrainGameplayTest và TerrainGameplayTests đối chiếu hệ số theo map, hồi năng lượng ốc đảo và prediction; MapCatalogTest/MapCatalogTests kiểm đường khô tới spawn/item khi xe xoay và gạch nguyên vẹn. Đây là phạm vi test trong source; kết quả chạy từng đợt ghi ở testing.

<a id="combat"></a>
## Chiến đấu, chết và hồi sinh

Đạn thường gây **20 HP**, di chuyển `BulletStep=0,72` đơn vị/tick (21,6 đơn vị/giây), sống tối đa **150 tick** và được nảy tối đa **hai lần**; va chạm tiếp theo loại đạn. Nhịp bắn nền là **30 tick/phát**. Đạn phát sinh cách tâm xe 0,72 đơn vị; vị trí hiệu ứng đầu nòng dùng offset 1,26. Hình đạn có nửa chiều dài/rộng **0,6/0,32**, gấp đôi kích thước trước đó ở cả hình ảnh và vùng va chạm xe; đường va chạm xe dùng SAT liên tục giữa hình chữ nhật xoay và chuyển động tương đối để bắt mục tiêu cắt qua đường đạn giữa hai tick. Đạn thường/Mirror mẹ chạm tường vẫn dùng hình vuông riêng, nửa cạnh `BulletWallHalfSize=0,16` (trước là 0,08); HE và Mirror con dùng biên hình chữ nhật theo hướng đạn và hệ số loại đạn. Các vùng này đều tăng gấp đôi nhưng hình chạm tường của đạn thường không trùng hình đạn vẽ. Đạn bỏ qua chủ trong sáu tick đầu, sau đó có thể tự trúng.

Thép chặn xe và phản xạ đạn. Gạch đỏ có **3 HP**, mỗi contact đạn giảm 1 HP; hình va chạm giữ nguyên đến HP=0. Client vẽ ba trạng thái nguyên vẹn/2 phần ba/1 phần ba, không có thanh máu tường; preview dùng cùng HP tối đa. Kỹ năng gây phá gạch đặt thẳng HP về 0. Cơ chế giới hạn bốn contact trong một bước tránh vòng lặp va chạm; vượt giới hạn loại đạn và tăng counter.

Sát thương kẹp ở 0 HP. Kill chỉ ghi cho xe của người khác còn có mặt trong world; tự sát và môi trường không cộng kill. Chết tăng death, xóa buff item và trạng thái Q/debuff tạm thời; **không xóa deadline hồi Q gốc/hiệu lực**. Hồi sinh sau **90 tick (3 giây)**, đầy HP khởi đầu và năng lượng, có `Life` mới; `SpawnShieldTicks=90` cấp **3 giây bảo vệ HP** lúc xuất hiện đầu trận và sau hồi sinh. Khiên chặn mọi loại sát thương, kể cả tức tử/tự hi sinh, nhưng không chặn hiệu ứng trạng thái, giảm tầm nhìn hoặc chậm địa hình/bão. Xe vẫn được bắn/Q; đạn trúng xe vẫn bị tiêu thụ. Rejoin giữ deadline khiên cũ, không cấp thêm 3 giây. Xe vừa hồi sinh chưa bắn trong chính tick đó.

Spawn đầu trận chọn nhóm điểm trải quanh chu vi và xáo trộn chủ sở hữu, tránh lặp phân công trận trước khi có thể. Respawn chọn điểm hợp lệ xa xe sống gần nhất nhất. Rejoin dùng spawn mới/HP đầy theo cùng cơ chế, giữ điểm số; chi tiết phần trạng thái được giữ nằm ở [rejoin](protocol.md#rejoin).

Thứ tự đáng chú ý trong `World.step`: tăng tick/prune → xử lý hết hạn item/debuff → áp dụng hàng đợi Q → di chuyển/hồi sinh → nhặt item → sát thương battlefield → bắn thường → xử lý kỹ năng đến hạn → tiến đạn → ghi sự kiện gạch vừa bị phá. Vì vậy item nhặt trong tick có thể bảo vệ hoặc tăng sức mạnh ngay trong lần bắn/sát thương sau đó.

Nguồn: [World.step/advance/tankHit/damage/spawn/arrangeInitialSpawns](../source/server/src/main/java/tank/World.java), [ArenaRenderer](../source/client/Assets/Scripts/ArenaRenderer.cs). Test: GameplayTest, GameplayUpgradeTest, AfkRejoinAndHitTest, CombatGameplayTraceTest. Hợp đồng sự kiện hình ảnh/âm thanh: [combat event](protocol.md#combat-events).

<a id="skills"></a>
## Tám kỹ năng nhập vai

Nation là ID 0–7; tên hiển thị hiện tại lấy từ JSON, khác tên quốc gia trong tên test và một số identifier cũ. Mỗi người chỉ có Q (`ability slot=0`). Thời gian dưới đây dùng 30 tick/giây; cooldown giữ qua chết, respawn và rejoin.

| ID / tên hiện tại | Q | Hiệu lực chính | Hồi Q |
|---|---|---|---|
| 0 Đại Bàng | Oanh tạc chiến lược | Chậm địch 30% ngay trong 90 tick; bom truy bám nổ sau 90 tick | 900 tick = 30 giây |
| 1 Gấu Tuyết | Bão hỏa tiễn | Q ngắm tối đa 150 tick, Q nữa hoặc hết hạn để chốt; 9 rocket và vùng chậm | 977 tick ≈ 32,57 giây, tính từ lúc chốt |
| 2 Sư Tử Vàng | Chiến dịch hỗn loạn | Đảo di chuyển và khóa hướng nòng đối thủ sống trong 300 tick | 900 tick = 30 giây |
| 3 Tháp Sắt | Màn sương chiến tranh | Thu hẹp tầm nhìn đối thủ sống về bán kính 5,1 trong 300 tick; không ảnh hưởng mình | 900 tick = 30 giây |
| 4 Thiết Ưng | Thiết giáp chớp nhoáng | Trong 150 tick, mỗi phát có 3 viên ở 0°/±12°, mỗi viên 15 HP; giữ nhịp bắn nền | 900 tick = 30 giây |
| 5 Tháp Nghiêng | Siêu pháo tụ lực | Tụ 60 tick rồi bắn tia rộng 0,85 tới biên map theo hướng nòng lúc bắn | 1029 tick = 34,3 giây |
| 6 Anh Đào | Tấn công cảm tử | Chuẩn bị 150 tick rồi nổ bán kính 9,5625, hạ địch và bắt buộc tự hủy; chết sớm hủy nổ | 1029 tick = 34,3 giây |
| 7 Long Thành | Du kích phá hoại | Khóa bắn thường địch sống 300 tick; cộng 300 tick vào hồi Q còn lại | 1029 tick = 34,3 giây |

**Bom Đại Bàng.** Mỗi đối thủ sống tại lúc gọi có một vùng mang ID riêng và gắn slot/Life mục tiêu. Vùng đi thẳng tới vị trí hiện tại mục tiêu ở **4,5 đơn vị/giây (0,15/tick)**, không vượt qua mục tiêu, không bị nước/địa hình cản hoặc làm chậm. Khi mục tiêu chết, rời world hoặc đổi Life, vùng dừng vĩnh viễn tại vị trí cuối; vẫn nổ đúng lịch, không chuyển sang đời xe mới. Bom tồn tại qua cái chết người gọi. Nổ bán kính **7,65**, phá ngay gạch giao vùng, tối đa **25 HP mỗi xe cho toàn lượt gọi** dù nhiều vùng chồng nhau; chỉ các Life đối thủ đã được đánh dấu có thể nhận damage, không gây thêm slow ở impact. Thanh tẩy chỉ gỡ slow. Trên nền khô, xe đang slow chạy 6,3 đơn vị/giây, hoặc 9,45 khi Shift còn năng lượng; nhân thêm hệ số địa hình tương ứng của map.

**Bão hỏa tiễn Gấu Tuyết.** Tâm vùng bị kẹp trong `[11,475; width−11,475] × [11,475; height−11,475]`. Khi chốt, AREA bán kính **11,475** có hiệu lực ngay tới cuối mưa rocket. Rocket bắt đầu impact sau 24 tick, rải 9 lần trong 90 tick. Khi bắt đầu cảnh báo rơi (15 tick trước impact), mỗi quả chọn ngẫu nhiên một xe đối phương còn sống có hitbox giao AREA và khóa tọa độ xe tại tick đó. Điểm rơi giữ nguyên dù xe di chuyển, chết hoặc rời vùng; quả sau chọn lại theo các xe đang trong vùng. Nếu không còn xe đối phương hợp lệ, quả đó giữ điểm rơi ngẫu nhiên trong AREA, không đổi sang bám xe đi vào sau khi đã cảnh báo. Người gọi không được chọn làm mục tiêu nhưng vẫn có thể chịu sát thương nổ. Mỗi rocket bán kính **3,4**, damage **12**, trần **36 HP/xe/lượt gọi**, gồm cả chính người gọi. Rocket phá gạch giao vùng lúc impact. Mỗi lần nổ trúng một xe sống nhân thêm tốc độ hiện tại với **0,9**, cộng dồn theo phép nhân (2 lần còn 81%, 3 lần còn 72,9% trước khi trúng), kể cả sau khi đã đủ trần damage. Phần chậm do trúng còn tới deadline kết thúc AREA, không mất khi ra khỏi vùng; hết vùng, chết/hồi sinh/rejoin sẽ xóa. Thanh tẩy xóa các lần trúng từ nguồn khác, lần trúng mới có thể cộng lại; tự trúng vẫn chậm như tự chịu damage. Bất tử chỉ chặn damage, không chặn phần chậm này. Xe sống có hitbox xoay giao AREA còn 50% tốc độ hiện tại, nhân tiếp địa hình/Shift/slow bom; ra ngoài, chết hoặc hết AREA thì dừng. Vùng chỉ đang ngắm chưa gây slow. AREA được đọc từ snapshot, không dùng `SlowEndTick` của bom. Nếu chết khi đang ngắm thì phần chuẩn bị bị xóa; nếu đã chốt, lượt rocket thuộc server tiếp tục.

**Hỗn loạn, sương mù và phá hoại.** Mỗi lượt debuff lưu ID, nguồn, slot/Life, deadline và dấu đã thanh tẩy. Thanh tẩy chỉ vô hiệu lượt của nguồn khác đang gắn với đời xe đó; lượt mới vẫn có thể tác động, việc lượt mới hết hạn không khôi phục lượt cũ đã thanh tẩy. Client dùng deadline tổng hợp từ server, không suy lại từ Q của đối thủ. Phá hoại không hủy Q đã bắt đầu; mỗi lượt cộng vào `max(tick, qReady)+300`, giữ riêng hồi gốc và phần trì hoãn qua chết/respawn/rejoin. Thanh tẩy gỡ phần trì hoãn bên ngoài, không đặt Q về sẵn sàng nếu cooldown gốc chưa hết.

**Tia và cảm tử.** Tia xuyên địa hình, phá gạch giao dải và hạ mọi đối thủ giao hitbox, không tự hạ người bắn. Cảm tử xuyên vật cản trong bán kính và phá gạch giao vùng. Đây là damage loại instant kill: giáp không giảm, nhưng bất tử chặn được ở mục tiêu. Riêng tự hủy của người cảm tử là bắt buộc, bất tử không chặn được. Xe chết sớm hủy chuẩn bị tia/cảm tử; bản thân hiệu ứng nổ/tia sau khi phát không phát lại gameplay từ animation.

Nguồn: [NationSkills.use/lock/tick/trackBomb/katyushaSlowEnd/purify/sabotage](../source/server/src/main/java/tank/NationSkills.java), [World.clearEffects/useSkill/damage](../source/server/src/main/java/tank/World.java), [SkillGeometry](../source/client/Assets/Scripts/SkillState.cs), [SkillPresentation](../source/client/Assets/Scripts/SkillPresentation.cs). Test: RoleplayGameplayTest, RoleplayRoomTest, SkillContractTests, RoleplayClientTests.

<a id="items"></a>
## Vật phẩm và đạn kết hợp

| Type | Vật phẩm | Tác dụng |
|---|---|---|
| 0 | Medical | Hồi `ceil(MaxHP × 30%)`, kẹp ở MaxHP; đầy máu vẫn nhặt, không hồi sinh xe chết |
| 1 | Armor | 180 tick (6 giây), giảm 35% damage thường rồi làm tròn; không giảm instant kill |
| 2 | HE | 150 tick (5 giây), đạn lớn 1,5 lần; contact nổ bán kính 1,7 gây thêm 6 HP |
| 3 | Purification | Gỡ debuff có nguồn bên ngoài và phần trì hoãn hồi Q bên ngoài; giữ Q/buff của mình |
| 4 | Invincibility | 90 tick (3 giây), chặn damage trừ tự hủy bắt buộc; không chặn khống chế |
| 5 | Mirror | Đặt lại còn 3 lượt bắn; đạn mẹ chạm vật cản tách 3 con ở hướng phản xạ và ±25° |

Nhặt lại item có deadline đặt deadline mới từ tick nhặt, không cộng dồn thời lượng. HE và Mirror tồn tại độc lập; có thể kết hợp với Q Thiết Ưng theo mọi thứ tự nhặt. Một loạt ba viên của Q chỉ tiêu thụ **một** lượt Mirror, tạo ba mẹ, mỗi mẹ có thể tách ba con. Đạn thường/mẹ gây 20 HP, hoặc 15 với Q; con gây 10 HP, hoặc **25** khi mẹ có damage Q. HE luôn cộng splash 6 HP, kể cả con kết hợp, có thể trúng người bắn; splash kiểm đường nhìn và bị thép/gạch còn nguyên chặn. Hit trực tiếp xảy ra trước splash, cùng một nguyên nhân EXPLOSION. HE + Mirror tại vật cản nổ HE trước rồi tách.

Kind đạn 0–5 lần lượt là thường, HE, Mirror mẹ, Mirror con, HE+Mirror mẹ, HE+Mirror con. Hệ số kích thước tương ứng **1; 1,5; 1; 0,7; 1,5; 1,05**. Thuộc tính được chốt lúc khai hỏa: buff/Q hết hạn hoặc Mirror hết lượt chỉ ảnh hưởng phát mới. Con giữ HE/damage/tốc độ và thời gian sống còn lại của mẹ, bắt đầu `bounce=1`, chỉ nảy thêm một lần và không tách tiếp. Trúng xe hoặc hết hạn không tách.

Trần **128 đạn** tính cả hai chỗ dự trữ cho mỗi Mirror mẹ (kind 2/4), bảo đảm thay mẹ bằng đủ ba con. Loạt Q+Mirror chỉ được tạo khi đủ chỗ cho chín con, rồi mới tiêu lượt Mirror; đạn hết hạn, trúng xe hoặc chủ rời world giải phóng phần dự trữ.

Mỗi loại có tối đa một item trên map. NORMAL sinh đủ sáu loại từ **tick 300 (10 giây)**; mỗi loại được nhặt có lịch riêng, sinh lại sau **450 tick (15 giây)**.

ROLEPLAY bắt đầu một đợt item mỗi **40 giây (1200 tick)**, mỗi đợt mở **20 giây (600 tick)**, giữ cùng nhịp ở mọi thời lượng trận. Trận 3 phút bắt đầu các đợt ở giây **40/80/120/160**; đợt cuối đóng khi trận kết thúc. Không sinh đợt ở lúc bắt đầu hoặc đúng deadline trận. Mỗi đợt chọn ngẫu nhiên 4 loại khác nhau trong 6 loại. Tâm item chọn ngẫu nhiên đều theo diện tích trong vòng tròn tâm map, bán kính **25% cạnh ngắn map**, tránh nước/vật cản, xe sống gần hơn 6 đơn vị và item khác. Nếu vùng trung tâm không đủ chỗ hợp lệ sau số lần thử hữu hạn, có thể sinh ít hơn 4 item; không chuyển ra ngoài vùng hoặc sinh bù giữa đợt. Nhặt không tạo lịch respawn riêng. Item chưa nhặt hết hạn đúng lúc đóng đợt; hết hạn được xử lý trước nhặt.

NORMAL giữ `expires=0`, chọn vị trí trong `itemSpawns`, tránh vị trí trước của cùng loại; không có vị trí thì thử lại mỗi tick. Nhặt kiểm cả hitbox và đường di chuyển hai trục; xe gần tâm item hơn thắng, bằng nhau ưu tiên slot nhỏ. Chết xóa buff nhưng không đổi item trên map hoặc thuộc tính đạn đang bay.

**Shop Nhập vai và vàng tài khoản.** Mỗi chuyến Chiến dịch bắt đầu với **0 vàng**; phần vàng còn lại sau mua đồ được cộng vào tài khoản khi kết quả được lưu, không cộng lại khi retry. Sảnh sau đăng nhập hiển thị số dư do server tải. Thường/Nhập vai không tự sinh vàng thưởng. Trong đúng cửa sổ mỗi đợt item, xe còn sống có thể bấm **E** mua bất kỳ trong 6 loại với giá **50 vàng/loại**. Item mua được **cất vào túi**, không kích hoạt ngay. Bấm icon trong sidebar khi xe còn sống để dùng một item; có thể dùng cả ngoài thời gian mở shop. Túi giữ qua chết/hồi sinh/rejoin, chỉ dùng cho trận hiện tại; các lần mua ở đợt khác có thể cộng số lượng. Nhặt item trên bản đồ vẫn áp dụng ngay. Mỗi người được mua tối đa **1 item mỗi loại mỗi đợt**; nhặt hoặc dùng item không đặt lại quyền mua, chết/vào lại trận không đặt lại giới hạn. Sang đợt tiếp theo đặt lại giới hạn mua. Shop đóng tự động khi hết đợt; input chiến đấu trung lập trong lúc mở, trận vẫn tiếp tục. Server kiểm tra lại thời gian/trạng thái sau thao tác ví bất đồng bộ; nếu không còn hợp lệ, không thêm item và hoàn vàng. Chi tiết transaction/recovery ở [data](data.md#accounts).

Nguồn: [WorldItems.beginTick/spawn/pickup/apply/explode/split/reservedBullets](../source/server/src/main/java/tank/WorldItems.java), [World.step/advance/damage](../source/server/src/main/java/tank/World.java), [ItemState](../source/client/Assets/Scripts/ItemState.cs), [ItemPresentation](../source/client/Assets/Scripts/ItemPresentation.cs). Test: ItemGameplayTest, ItemCodecTest, ItemTests.

<a id="battlefield"></a>
## Biến cố chiến trường: Red Zone, Bombstorm và bão theo map

Cả NORMAL và ROLEPLAY bật bộ lập lịch do server sở hữu nếu map có metadata `battlefield`. Lượt đầu bắt đầu **tick 300 (10 giây)**; khoảng cách giữa các lần bắt đầu tiếp theo được chọn đều trong **800–1200 tick (26,67–40 giây)**. RNG riêng trộn túi hai loại, tránh lặp loại ở ranh giới túi, không tiêu ngẫu nhiên của spawn/item. Red Zone/Bombstorm có **60 tick cảnh báo đầu + 240 tick hoạt động = 300 tick (10 giây)**, kết thúc trước lượt tiếp theo. Wave chỉ bắt đầu nếu hoàn tất trước deadline trận; kết thúc trận xóa scheduler. Map thiếu metadata tùy chọn không có biến cố.

Mỗi wave Red Zone/Bombstorm có **ba round, mỗi round năm vùng khác nhau**; tổng 15 ID xếp theo round. Bán kính mỗi vùng **5**. Tâm trong cùng round cách nhau ít nhất **13 đơn vị**. Bombstorm còn giữ khoảng cách đó với mọi vùng của round trước, nên không tái dùng vùng liền kề; round không liền nhau có thể lặp. Server chọn trước hai bộ Bombstorm đầy đủ cách nhau, sau đó chọn bộ tiếp theo loại trừ bộ trước. Nếu custom map không đủ vùng tách biệt hoặc tìm kiếm giới hạn không tìm được bộ đầy đủ, bỏ lượt; không giảm số vùng.

| Biến cố | Impact tính từ đầu wave | Hiệu lực mỗi round |
|---|---|---|
| Red Zone (`FIRE=2`) | 60, 140, 220 tick | Năm vùng cháy đồng thời trong 80 tick; gây 5 HP tại impact và mỗi 15 tick tiếp theo: offset 0/15/30/45/60/75, sáu pulse/vùng |
| Bombstorm (`AIRSTRIKE=3`) | 60, 172, 284 tick | Năm vùng trúng cùng lúc, mỗi xe giao vùng nhận 35 HP một lần/round; animation nổ 16 tick |

Mỗi round cảnh báo trước 60 tick. Khoảng round Bombstorm là `(240−16)/(3−1)=112 tick`; Red Zone là 80. Vùng cháy cuối và animation nổ cuối kết thúc đúng tick 300 của wave, không có round thứ tư. Xe đi theo một vùng Red Zone qua cả ba round có thể chịu **18 pulse (90 HP trước giáp)**. Đây là quy tắc đếm của code/test, không phải kết quả playtest.

Sát thương dùng giao hình tròn với hitbox xe xoay, chạy sau di chuyển/nhặt và trước bắn; tôn trọng giáp/bất tử, không cộng kill. **Cả hai biến cố chỉ gây damage xe; không thay đổi gạch, vật cản, collision, tầm nhìn hay cooldown Q.** Điểm này đã khác mô tả v14 cũ về Bombstorm phá gạch. Gạch vẫn do HP gạch quyết định. Mọi vùng tránh tâm spawn ít nhất `5 + 4 + 1,02×√2`; vùng lửa phải khô và không chồng vật cản ban đầu.

HUD chỉ báo tên sự kiện và tổng thời gian còn lại. Cảnh báo trong map dùng gạch chéo đỏ tương phản cao, viền vàng, tâm ngắm và tia lửa CozyChunky; Red Zone có lửa/khói, Bombstorm có `explosion_top`. Hazard không có chữ/đếm ngược riêng. HUD, cảnh báo và lửa dùng cùng presentation tick, biến mất đúng deadline dù snapshot cũ còn wave. Hiển thị tuân theo sương mù. Vào lại dựng trạng thái cảnh báo/lửa hiện tại; watermark snapshot đầu bỏ explosion cũ, vẫn hiển thị round tương lai. Dedup theo `(match, wave, target, phase)`; Bombstorm sở hữu nổ của nó, Red Zone vẫn cho phép nổ xe chết thông thường. Hợp đồng wave/cause nằm ở [protocol](protocol.md#battlefield-wire).

Theo map, túi biến cố là **Tiền Đồn Xanh: Red Zone/Bombstorm; Pháo Đài Cát: Red Zone/Bão cát; Sông Băng: Bão tuyết/Bombstorm**. Cả hai mode dùng cùng luật, không đổi JSON map. Bão cảnh báo **90 tick (3 giây)**, sau đó **phủ toàn map trong 360 tick (12 giây)**. Hiệu lực trong `[impact,end)`, áp dụng mọi xe sống và không phụ thuộc vị trí, kể cả spawn, hồ hoặc nơi có công sự. Không có dải di chuyển, vùng tránh bão, ba round hoặc danh sách target. Server/client suy ra giai đoạn từ tick và wave sẵn có; chi tiết tại [wave wire](protocol.md#battlefield-wire).

Bão gây **2 HP ở offset 30/90/150/210/270/330 tick sau impact** (giây 1/3/5/7/9/11), xét mọi xe đang sống tại đúng pulse. Tổng tối đa trước giảm trừ là 12 HP nếu sống suốt sáu pulse. Không gây hit riêng khi hồi sinh/rejoin, không trừ bù pulse đã bỏ lỡ và không gây sát thương cảnh vật. Bão có thể kết liễu xe, tăng death và không cộng kill; giáp, bất tử và khiên hồi sinh tiếp tục được áp dụng.

Bão cát (`SANDSTORM=4`) **giảm 50% tốc độ bắn**: `SandstormFireRatePercent=50` làm khoảng cách hai phát đạn thường tăng gấp đôi, **30→60 tick (1→2 giây)** với mọi quốc gia, kể cả loạt ba đạn của Đức. Server xét phát gần nhất: vừa bắt đầu bão phải đủ 60 tick, hết bão quay về deadline cơ bản 30 tick; không trì hoãn phát đầu khi chưa bắn, không bắn bù. Rejoin xe sống giữ lịch bắn cũ. Đây là nhịp khai hỏa, không phải tốc độ bay của đạn; không làm chậm di chuyển hoặc thay nhịp kỹ năng Q. Bụi vàng nâu và luồng cát phủ toàn map; mây ở sau xe, hạt phía trước thưa để đọc được xe, đạn và HP. **Bão không ẩn xe, item, đạn hoặc FX ngoài bán kính**. Fog từ kỹ năng vẫn giữ giới hạn tầm nhìn riêng và deadline cũ; bỏ giới hạn 18 đơn vị của bão không vô hiệu hóa Fog.

Bão tuyết (`SNOWSTORM=5`) nhân **×0,5** tốc độ di chuyển thường/Shift đúng một lần **trên toàn map**, kết hợp nhân với địa hình và các slow cũ. Băng ×1,5 kết hợp bão thành **×0,75** tốc độ nền đất khô trước các hệ số khác. Hết bão thì hết giảm tốc do bão; không trượt, không đóng băng bất động, không đổi khoảng lướt/Q/năng lượng. Server và hai đường generate/reconcile client xét wave và từng input tick, hiệu lực trong `impact,end)`. Thanh tẩy không xóa hiệu ứng môi trường; khiên hồi sinh/bất tử chặn HP nhưng không miễn chậm hoặc giảm tốc độ bắn do bão.

Bão dùng chung vị trí thông báo **giữa mép trên bản đồ** với Red Zone/Bombstorm, không chiếm sidebar. Cảnh báo có tên, đếm ngược và nêu **toàn bản đồ**; khi hoạt động ghi tác dụng giảm tốc độ bắn hoặc di chuyển và nhịp sát thương. Không có thông báo trong/ngoài vùng hoặc mũi tên yêu cầu tránh dải. Gió/cát/tuyết phủ viewport, không phủ header/sidebar; bão tuyết có mây xanh xám, nhiều cỡ hạt và sắc lạnh nhẹ trên xe. Âm gió dùng voice WinMM riêng với mức âm theo giai đoạn, tuân cài đặt và không phát lại cảnh báo cũ khi rejoin. Bão không tạo animation tấn công lửa/bom; xe chết do bão vẫn có hiệu ứng chết thông thường. Test chuyên biệt: [WeatherTest, WeatherTests; kết quả chạy hiện tại ở testing/weather-v21, kết quả v20 là lịch sử của dải quét.

Nguồn: [WorldBattlefield.configure/beginTick/schedule/pickTargets/airstrikeTargets/damage](../source/server/src/main/java/tank/WorldBattlefield.java), [BattlefieldMap.validate](../source/server/src/main/java/tank/BattlefieldMap.java), [BattlefieldWave](../source/client/Assets/Scripts/BattlefieldState.cs), [BattlefieldPresentation](../source/client/Assets/Scripts/BattlefieldPresentation.cs), [CombatPresentation](../source/client/Assets/Scripts/CombatPresentation.cs). Test: BattlefieldTest, BattlefieldRoomTest, BattlefieldTests.

<a id="maps"></a>
## Bản đồ và tài nguyên

Danh mục chọn phòng dùng chung [multiplayer-maps.json](../source/client/Assets/StreamingAssets/multiplayer-maps.json): **2 — Tiền Đồn Xanh** (`map-large.json`, mặc định), **3 — Pháo Đài Cát** (`map-fortress.json`) và **4 — Sông Băng** (`map-river.json`). Cả ba có kích thước 120×80. Bố cục ngày **07/11/2026**: Tiền Đồn Xanh có hai đoạn sông dọc 9×24, hai hồ bên 12×9 và một cầu gỗ trên khe đất khô giữa sông, tổng nước **648 đơn vị²**; 12 đoạn tường thép bên trong, bốn tường biên, 16 collider cảnh vật và 36 viên gạch. Map giữ 16 spawn, 21 item (chuyển hai điểm), 47 mục tiêu không kích; bố trí lại 18 mục tiêu lửa. Pháo Đài Cát giữ pháo đài trung tâm bốn cổng và hai hồ ốc đảo. Sông Băng có một hồ băng bờ thuôn x=38–74, y=30–54, diện tích **648 đơn vị²**, lệch giữa sân và không có cầu. Hồ có đường tăng tốc xuyên theo hai chiều; sáu cụm rừng thông quanh hồ và hai cụm nhà băng phía đông. Bốn tường biên, 12 công sự ngắn, 24 gạch được bố trí lại; 44 collider cảnh vật gồm 28 thông, tám đá tuyết, bốn tinh thể băng và bốn nhà băng, tạo tổng 60 tường. Map tuyết giữ 16 spawn, 18 item (chuyển bảy điểm) và 48 mục tiêu không kích, bố trí lại 48 mục tiêu lửa tránh địa hình mới. Lối khô nối mọi spawn/item khi gạch còn nguyên và xe xoay. `map.json` ID 1 giữ cho fixture/dữ liệu cũ, không nằm trong ba lựa chọn. Server/client đọc cùng manifest và JSON map. Map có `mapId`, `width/height`, các mảng `walls/bricks/water`, `spawns`, `itemSpawns` và metadata `battlefield` tùy chọn (`fireZones/airstrikeZones`, mỗi zone có `id/x/y`). Tọa độ hazard do server chọn bằng ID map; client không tự chọn hình học.

Hình ảnh từng map được chọn theo ID tại client, dùng chung cho trận và thumbnail: Tiền Đồn Xanh giữ nền cỏ và vật liệu tường thép, khôi phục đường vẽ nước `DrawWater` ban đầu; Pháo Đài Cát giữ cát vàng, công sự sa thạch/gạch đất nung và mặt nước xanh ngọc; Sông Băng giữ tuyết trắng xanh, công sự/cây/đá phủ tuyết và hình mặt băng sáng hiện có. Map sa mạc/tuyết không dùng dải cát giữa sân hoặc đường vòng của map xanh. Map mặc định dùng 16 tháp/cây/đá thành bốn cụm kích thước khác nhau trên đất, có vùng va chạm nhỏ tương ứng (wall ID 16–31). Map sa mạc tăng lên 18 cây cọ (wall ID 18–35), chín cây mỗi hồ dồn thành cụm ở hai góc bờ ngoài để tạo ốc đảo; tường/gạch, hồ, spawn, item và battlefield gốc của map này giữ nguyên. Các khối va chạm cảnh vật chặn xe/phản xạ đạn theo nhóm `walls`, được vẽ bằng asset cảnh vật, không có đế tường thép/đá bên dưới. Cầu gỗ chỉ xuất hiện ở map mặc định, đánh dấu khe khô có sẵn; không thêm collider hoặc sửa luật địa hình. Map tuyết dùng hồ băng liền cho tăng tốc. Thông/đá/tinh thể từ `ArenaBiomes`, băng/cọ `ArenaTerrain` giữ nguồn hiện có; nhà băng mới dùng `ArenaWinter`, có PNG/prompt/provenance riêng và collider tương ứng. Không có người tuyết; thông/nhà băng nằm trên đất, không phủ lên tường. Map mặc định/sa mạc giữ nguyên byte sau khi người dùng chốt. Luật tốc độ/hồi năng lượng giữ như [di chuyển](#movement): nước mặc định ×0,5, hồ sa mạc ×1 tốc độ/hồi năng lượng ×1,5, băng ×1,5 tốc độ. Xem [render/asset](client.md#ui).

`GameMap.validate` giới hạn kích thước mỗi chiều 40–128, tối đa 64 gạch với ID liên tiếp từ 0, ít nhất hai spawn; rectangle phải hữu hạn, kích thước dương và nằm trong map. `itemSpawns` có 3–64 điểm duy nhất, khô và không bị chặn với gạch ban đầu còn nguyên. Mỗi danh sách hazard có 1–64 zone, ID duy nhất 0–63 trong chính danh sách đó, nằm trong biên và giữ khoảng bảo vệ spawn. Fire zone kiểm không chồng thép/gạch/nước. Điều kiện đủ năm vùng mỗi round còn được kiểm khi lập lịch, không chỉ khi tải map.

Map hash là SHA-256 của **byte gốc**, nên chỉ đổi khoảng trắng cũng đổi tính tương thích. Cả ba map được chọn đổi hash trong đợt bố cục/cảnh vật 07/11; file luật và protocol/wire giữ nguyên trong đợt này. Cần build và phân phối server/client cùng bộ JSON map mới, protocol vẫn là 18; xem [hash và hợp đồng](protocol.md#shared-contracts). Asset tải theo tên trong `Assets/Resources/ArenaArt` cùng các nhóm cartoon bổ sung; giữ tên, `.meta` GUID, cấu hình importer và giấy phép. Cách chạy Unity/kiểm tài nguyên nằm ở [client](client.md).

Nguồn: [GameMap.load/validate](../source/server/src/main/java/tank/GameMap.java), [MapLoader](../source/client/Assets/Scripts/MapLoader.cs), [BattlefieldMap.Validate](../source/client/Assets/Scripts/BattlefieldState.cs). Test: ItemGameplayTest.mapItemPointsValidateOnBothMapsAndRejectMalformedOrWetCoordinates, BattlefieldTest.
