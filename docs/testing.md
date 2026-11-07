# Kiểm thử và bằng chứng xác nhận

Tài liệu nền đối chiếu mã nguồn/script ngày **2026-10-23**; danh mục UI, chọn map và diện mạo sa mạc/băng tuyết multiplayer được cập nhật theo working tree ngày **2026-11-06**, dựa trên HEAD `08d6f756c6009b2c730e1babeb3cb138142e74fe` và có thay đổi chưa commit. Các test được nêu là test hiện có trong source, không phải kết quả pass mới; kết quả chạy và phạm vi đã xác nhận được ghi riêng theo [bằng chứng](#evidence).

Mục lục:

- [Ma trận thay đổi → kiểm tra](#matrix)
- [Điều kiện và thứ tự chạy](#prerequisites)
- [Lệnh, output và test hiện có](#commands)
- [Smoke hai client với server demo](#smoke)
- [Kiểm tra thủ công gameplay, UI và mạng](#manual)
- [Cách ghi bằng chứng và giới hạn](#evidence)

<a id="matrix"></a>
## Ma trận thay đổi → kiểm tra

Chọn kiểm tra theo hành vi bị ảnh hưởng và các caller/đối tác client/server. Không sửa test fixture, generated contract hay môi trường chỉ để làm test xanh; giữ code ổn định trong lúc chạy kiểm tra.

Đồng bộ UI/cartoon ngày 05/11/2026: `ModeNavigationTests` kiểm reset tài khoản, quay về sảnh và ràng buộc run đang dở/chưa lưu; `CampaignHudLayoutTests` kiểm map 13×13, sidebar 294 px, hành động ghim, phím dài và đóng UI chiến dịch khi phản hồi phòng đến muộn. `CampaignArtTests` kiểm atlas RGBA/Texture2D, crop/alpha, mapping cảnh vật/địch/boss và importer riêng. `MatchChatTests` xác nhận chat thu gọn mặc định ở mọi kích thước. Test nguyên vẹn ảnh menu dùng SHA-256 của bản gốc tracked, không phụ thuộc các bản sao `logo.png`/`anh_nen.png` ngoài Git ở thư mục gốc. Kết quả từng đợt ở [UI validation](ui-validation.md).

Đợt tinh gọn HUD tiếp theo: `MatchChatTests` kiểm chọn tab Xếp hạng/Chat, unread và giữ lựa chọn qua resize; `ClientPreferencesTests` kiểm ký hiệu bốn mũi tên có glyph trên cả hai font dự phòng, parser cũ/mới và lưu/mở lại. `CampaignHudPresentationTests` kiểm tên vùng chỉ hiện khi đổi vùng rồi hết hạn, HP trong biên bản đồ, vị trí buff và phản hồi kỹ năng gọn. `SkillDetailsUiTests` kiểm khóa input đến khi nhả phím, đóng khi đổi context/mất focus, geometry popup và độ dài viền hồi chiêu; regression chiến dịch kiểm đóng bằng Đóng/Esc trước Update kế tiếp vẫn hiện chờ nhả phím, xóa hành động đang đợi và giữ khóa điều khiển. `ItemIconArtTests` kiểm mapping sáu hình Kenney và một xu, source/GUID/alpha/giấy phép gốc, texture hình chức năng và giới hạn kích thước nhỏ. `CampaignAudioTests` kiểm nhặt vàng một lần, loại thưởng giải cứu/mua hàng, refresh, tắt tiếng, đổi run và clip thực tồn tại. Manual cần xác nhận chuột phải qua IMGUI ở trạng thái sẵn sàng/hồi chiêu, không kích hoạt kỹ năng/tự bắn, phím Shop khi đang xem mô tả và gán ↑ trực tiếp trong ô Cài đặt.

Chọn map multiplayer ngày 06/11/2026: `MapCatalogTest`/`MapCatalogTests` kiểm ba tên/ID, manifest, SHA-256 byte gốc và map mặc định không đổi, kết nối các điểm spawn/item với gạch nguyên vẹn và xe xoay, lối vượt khô cùng metadata battlefield. Java còn kiểm hai loại biến cố trên 100 seed cho mỗi map và sinh fixture `map-catalog.tsv` để C# đối chiếu. `RoomMapSelectionTest` kiểm chọn map qua TCP thật, mặc định khi thiếu lựa chọn, từ chối ID lạ trước khi cấp phòng, giữ map qua settings/prepare/rejoin/kết thúc/trận tiếp ở cả hai chế độ. `RoomMapProtocolTests` kiểm CREATE_ROOM, ROOM_LIST_RESULT, ROOM_STATE, PREPARE/hash, payload sai/cũ và golden Java theo protocol 18. `RoomMapUiTests` kiểm ba thẻ map, ảnh giữ tỷ lệ, form cuộn/nút ghim tại 960×600, 1280×800, 1600×900, 1920×1080 và cách xếp dọc khi hẹp. Đây là danh mục coverage trong source; kết quả chạy phải ghi riêng theo [bằng chứng](#evidence).

Diện mạo map sa mạc/băng tuyết: `ArenaBiomeTests` kiểm chọn bối cảnh theo ID, palette tuyết ổn định, dữ liệu map không bị sửa khi dựng trang trí, tuyến nằm trong biên và tránh nước/tường, ba lối vượt khô có dấu tuyết nén, hình trang trí cao nằm trong vật cản và hình phẳng tránh tuyến/spawn/item. `ArenaBiomeArtTests` kiểm tám hình trên một atlas cache chung, alpha/texture budget/importer, độ phủ silhouette ở 12/24/32/48 px và từ chối texture thiếu/sai kích thước/format hoặc mapping/crop sai; validator kiểm provenance và hash nguồn. Chụp cả ba map với cùng trạng thái xe/item tại bốn độ phân giải, đối chiếu thumbnail với arena; kiểm độ đọc gạch từng mức HP, nước/đất khô, xe/đạn, item, Red Zone/Bombstorm và sương mù. Kiểm hash cả bốn JSON map trước/sau để tách thay đổi presentation khỏi gameplay. Kết quả chạy diện mạo mới phải ghi riêng, không dùng các pass của đợt chọn map bên dưới.

Đợt làm đẹp tường/nước: `ArenaEnvironmentGeometryTests` kiểm hợp các hình chữ nhật, góc nối L/T/chồng lấn/tọa độ nửa ô, loại mép nội bộ, hướng và thứ tự mép ổn định, dữ liệu map gốc không đổi và tối đa bốn cụm cảnh vật nằm trong vật cản thật. `ArenaEnvironmentArtTests` kiểm năm vật liệu trên một texture cache, importer/budget/hash, mẫu UV không lẫn viền/nền atlas, độ đục ở 12/24/48/96 px và từ chối mapping sai. Ảnh phải xác nhận tường hai map mới, nước cả ba map không có đường nối giả, các khe đất khô còn đọc rõ và trang trí không che xe/item hoặc tạo cảm giác vật cản mới. Đây là presentation phía client; không dùng preview để khẳng định gameplay hoặc mạng đã được kiểm.

Địa hình theo map: `TerrainGameplayTest`/`TerrainGameplayTests` kiểm nước thường ×0,5, ốc đảo ×1 và hồi Shift ×1,5, băng ×1,5, Shift/chậm/deadline/va chạm, 15 tick chờ hồi, trần năng lượng và parity sau FULL/DELTA/rejoin/reconciliation. Java sinh 1.152 ca `terrain-movement.tsv` và `terrain-recovery-state.tsv` cho C#. `ArenaEnvironmentGeometryTests`/`MapCatalogTest` kiểm sáu hồ băng tổng 648 đơn vị², ba cặp đối xứng bố trí lệch, clearance xe xoay với điểm spawn/item, các lối khô và collider cảnh vật khớp vị trí render. `ArenaTerrainArtTests` kiểm hai nguồn băng/cọ, hash/importer/cache/alpha/budget và kích thước nhỏ. Kiểm hash map cuối trong cả client/server và DB lịch sử; các pass trước khi sửa hồ không thay thế lượt kiểm map cuối.

| Loại thay đổi | Kiểm tra cần thực hiện |
|---|---|
| Chỉ tài liệu | Đọc diff, kiểm tra link/anchor, lệnh và phát biểu với mã nguồn. Không cần chạy lại game suite chỉ vì sửa văn bản. |
| Server: tài khoản, phòng, persistence hoặc lịch sử | Chạy test Java liên quan; chạy `build-server.ps1` trước khi giao bản server. Kiểm tra đăng nhập/phòng/lịch sử bị ảnh hưởng trên client thật. Nếu thay wire/data contract, dùng hàng protocol bên dưới. |
| Client: menu, layout, focus, fullscreen hoặc input UI | Chạy các EditMode check bị ảnh hưởng, dùng lại fixture interop còn hợp lệ; build server nếu fixture thiếu/cũ. Compile và kiểm tra màn hình/tương tác trong Editor, hoặc build client khi cần xác nhận bản chạy; thử kích thước, focus và hộp thoại liên quan. Nếu build, đọc diff scene/PlayerSettings tạo ra. |
| Gameplay chung: di chuyển, va chạm, đạn, kỹ năng, item hoặc battlefield | Đọc cả `World`/các lớp server và `GameState`/presentation/prediction phía client; đồng bộ contract nếu đổi nguồn chung; chạy **cả suite Java và Unity**, build cả hai và manual multiplayer cho hành vi thay đổi. |
| Protocol, full/delta snapshot, reconnect, hash/version | Đọc cả hai `Protocol`/`DeltaCodec`, caller TCP/UDP và fixture; chạy `sync-contracts.py --check`, **cả hai suite**, build cả hai, kiểm tra khớp phiên bản, mất/lặp/đảo packet và vào lại trận khi liên quan. |
| Map/metadata | Kiểm tra cả hai phía, validation và hash byte gốc; chạy cả hai suite, build cả hai để phân phối cùng map; manual spawn, đường đi, va chạm, sự kiện và reconnect. |
| Texture/Combat FX/importer | Giữ `.meta`, GUID và license; chạy test Unity gồm `CombatAssetTests`, build client để chạy validator/đóng gói giấy phép; mở preview/ảnh và manual VFX bị ảnh hưởng. Nếu đổi gameplay/wire thì thêm cả hai suite. |
| Script/config vận hành | Đọc caller, giá trị mặc định và tác dụng phụ; kiểm tra đúng lệnh build/run/test bị thay, đường dẫn output, cấu hình local/demo và dữ liệu riêng. Chỉ chạy thử các môi trường được phép. |
| Launcher/đóng gói cập nhật | Chạy `./scripts/test-launcher.ps1` (hai fixture Windows single-file, không thêm package), build standalone bằng `./scripts/build-launcher.ps1`, đối chiếu version/hash/size/URL trong manifest và ZIP. Kiểm tra UI thật cho tiến độ, cancel, tự mở lại, lỗi/offline và hai cửa sổ. Xem [launcher](../source/launcher/README.md#kiểm-tra); fixture dùng core production nhưng không chứng minh UI WinForms. |

Nguồn kiến trúc: [components](architecture.md#components), [gameplay](gameplay.md#movement), [shared contracts](protocol.md#shared-contracts), [schema](data.md#schema). Không có formatter/linter được cấu hình; giữ quy ước mã hiện có và xem diff cuối để tránh thay đổi ngoài phạm vi.

Bão/khiên v19: `WeatherTest` kiểm cặp biến cố theo map, lịch không chồng lấn, warning/impact/end, bốn pulse toàn map, giáp/bất tử, death/respawn và mọi loại sát thương trong khiên; sinh `weather-movement.tsv` (64 ca băng/Shift/slow/deadline) và `weather-state.tsv` (FULL/DELTA/drop/rejoin/cause/zero targets). `WeatherTests` đối chiếu fixture, generate/reconcile tại biên, radius cát/Fog/người chết, tia từ ngoài vùng nhìn, FX/audio không phát lại và validation map/wire. `NetworkRejoinTest` giữ deadline Shield qua các lần vào lại. Fixture combat cũ được giữ nguyên như phép đối chiếu khi chỉ vô hiệu khiên mới bằng subclass test; mô phỏng có khiên thật được kiểm độc lập rằng bật/tắt recording không đổi gameplay. Không sinh lại baseline cũ để che thay đổi không liên quan.

Manual cần xem cảnh báo/đang bão/kết thúc và khiên ở bốn độ phân giải, hai mode/map, tám xe/đạn/FX; tắt âm thanh, rejoin, modal/chat, resize/fullscreen và ngắm. Kiểm rằng địa hình vẫn rõ, băng vẫn tăng tốc tương đối, bão cát không lộ xe qua HP/FX/âm thanh và không đổi hash map. Kết quả thực chạy ghi riêng trong evidence.

<a id="prerequisites"></a>
## Điều kiện và thứ tự chạy

Chat TCP (đưa vào từ v17, hiện dùng v19): `MatchChatTest` kiểm normalize Unicode/800 byte/200 code point, tám màu NORMAL/RP, người chết, chống spam, trạng thái/sai trận, replay 100 tin chia batch sau hai luồng rejoin, xóa khi kết thúc và route TCP thật. `MatchChatTests` kiểm batch atomic, dedup, sai trận, lỗi riêng, reset phiên, golden 88/89 và mặc định thu gọn. Manual: hai client ở hai chế độ; chuột mở/nhập, Enter/Esc và Enter đã gán chiến đấu, IME tiếng Việt, tự bắn, item/shop/modal/mất focus, cuộn tin cũ và tin mới. Preview theo [client](client.md), kiểm ba kích thước với tám người và kỹ năng RP.

Kiểm tra ví/shop nằm trong [RoleplayShopTest](../source/server/src/test/java/tank/RoleplayShopTest.java) và [RoleplayShopTests](../source/client/Assets/Tests/Editor/RoleplayShopTests.cs): vàng ròng Chiến dịch, debit/refund/restart, giới hạn và túi qua chết/rejoin, đóng cửa sổ khi chờ DB, lịch 40 giây/20 giây và payload golden 86/87 v2. Kiểm tra mua không tự kích hoạt, dùng ngoài cửa sổ shop và retry revision cũ không trừ hai item. `ItemGameplayTest`/`ItemCodecTest` và `ItemTests` kiểm tra vị trí trung tâm, deadline và FULL/DELTA khi item sinh/hết hạn. Trace combat vẫn kiểm từng tick; khi cập nhật baseline phải đối chiếu gameplay NORMAL không đổi và RP chỉ đổi từ hành vi được sửa. Nếu layout snapshot đổi, cần so cả bản chuẩn hóa theo layout cũ để phân biệt thay đổi byte với gameplay. Đợt khóa mục tiêu Katyusha ngày 2026-10-26 giữ nguyên NORMAL và kịch bản map lớn; RP map nhỏ đổi từ tick 519 khi rocket bắt đầu khóa mục tiêu. `RoleplayGameplayTest` cùng `SkillContractTests.JavaTargetedRocketsRepairTheirLockedPointsAfterLossAndRejoinWithoutFollowingTheTank` kiểm tọa độ khóa, điểm rơi ngẫu nhiên khi vùng trống, chọn xe sống khác chủ chiêu và phục hồi FULL/DELTA/rejoin qua fixture `katyusha-targeting.tsv`. Phần chậm cộng dồn do rocket dùng protocol v16, thêm 3 byte/xe (deadline và số lần trúng); trace byte thô được sinh lại sau khi kiểm bản chuẩn hóa: NORMAL và RP map lớn giữ nguyên gameplay, RP map nhỏ đổi từ tick 535 (bước di chuyển sau lần trúng đầu tiên). `rocket-slow-movement.tsv` gồm 128 ca đối chiếu Java/C# trên nước/khô, Shift, chậm bom, trong/ngoài AREA và đúng deadline; `rocket-slow-state.tsv` kiểm FULL/DELTA, mất gói và rejoin.

Chạy PowerShell từ gốc repository. Server cần **JDK 21, Maven 3.9.x, Python 3**. Client dùng **Unity 6000.3.24f1**, license đã kích hoạt; build Windows cần Windows Build Support. Đường dẫn công cụ và cách cài ở [deployment](deployment.md#configuration) và [README](../README.md).

**Đóng Unity Editor đang mở cùng project trước script build hoặc test.** Không chạy nhiều thao tác Unity trên `source/client` đồng thời, hoặc nhiều build cùng ghi một output. Kiểm tra điều kiện thực tế tại thời điểm chạy; log cũ không chứng minh license, Editor lock hay công cụ hiện còn sẵn sàng.

Thứ tự thông thường khi sửa gameplay/protocol chung:

1. Sửa nguồn contract JSON khi cần và chạy `python scripts/sync-contracts.py`; không sửa tay generated Java/C#.
2. Chạy `./scripts/build-server.ps1` để check contract, test Java, đóng gói và tạo fixture interop.
3. Đóng Editor rồi chạy `./scripts/test-client.ps1`.
4. Chạy `./scripts/build-client.ps1` khi cần bản chạy/manual hoặc giao bản client; kiểm tra diff scene và project settings.
5. Chạy manual với server/client cùng đợt build, rồi ghi rõ phạm vi và kết quả.

Unity test đọc fixture Java trong **`source/server/target/interop/`**; ví dụ [ProtocolTests.JavaTraceReconstructs](../source/client/Assets/Tests/Editor/ProtocolTests.cs), [CombatTests.JavaCombatDeltaRepairsDroppedSnapshotAndExpiresWithoutChangingBase](../source/client/Assets/Tests/Editor/CombatTests.cs) và [BattlefieldTests.JavaFullDeltaDroppedUpdateAndRejoinPreserveEveryByte](../source/client/Assets/Tests/Editor/BattlefieldTests.cs). Nếu fixture thiếu hoặc đã cũ, chạy build server/test Java đầy đủ trước khi test Unity. Có thể dùng lại fixture còn hợp lệ cho thay đổi UI độc lập; sau khi đổi protocol, map hoặc generated rules phải tạo lại fixture, không chỉ kiểm tra file có tồn tại.

Các test Java sinh fixture bằng đường dẫn `target/interop` riêng trong code, còn client cũng đọc vị trí mặc định. Vì vậy đổi Maven build directory không tự chuyển toàn bộ fixture sang output tùy chọn. Quy trình thông thường dùng build mặc định; đối chiếu các đường dẫn trước khi chạy kiểm tra có output riêng.

Assets Combat FX đã có sẵn trong repository: không cần tải ZIP mới cho mỗi lần test. Nếu clone thiếu/sai asset, dùng [quy trình khôi phục](client.md#restore-combat-fx), để Unity import xong, đóng Editor rồi chạy lại. Không dùng kết quả thiếu tài nguyên như bằng chứng lỗi gameplay.

<a id="commands"></a>
## Lệnh, output và test hiện có

| Lệnh tại gốc repository | Kiểm tra/output |
|---|---|
| `python scripts/sync-contracts.py --check` | Kiểm tra generated contract còn khớp nguồn chung; không sửa file. |
| `./scripts/build-server.ps1` | Check contract, Maven `clean package`, JUnit rồi xuất `build/server/tank-server-1.0.0.jar`. Log test ở `source/server/target/surefire-reports/`, fixture ở `source/server/target/interop/`. |
| `mvn -f source/server/pom.xml test` | Chạy JUnit 5 bằng Maven, không xuất bản JAR vào `build/server`. Dùng khi chỉ cần test; tự chạy contract check riêng nếu thay nội dung chung. |
| `./scripts/test-client.ps1` | Chạy NUnit/UnityTest **EditMode**, ghi `runtime/unity-tests.log` và `runtime/unity-tests.xml`. |
| `./scripts/build-client.ps1` | Build Windows client, ghi `runtime/unity-build.log`, chạy asset build validator; **tạo lại Main.unity và ghi PlayerSettings** như [deployment](deployment.md#build-client). |

[test-client.ps1](../scripts/test-client.ps1) kiểm tra exit code và XML: tổng số test phải lớn hơn 0, không failed, số passed phải bằng total. Do đó Unity process thoát không lỗi nhưng không chạy đủ test vẫn bị script coi là thất bại. Đọc cả log lẫn XML khi báo lỗi; không chỉ tìm một dòng “passed”.

Một số test/fixture đại diện để tìm đúng lớp khi sửa, không phải danh sách toàn bộ suite:

| Phạm vi | Test/lớp và điều đã được mã hóa thành kiểm tra |
|---|---|
| Tài khoản → lobby → trận → lịch sử | [BaselineTest.accountLobbyMatchHistory](../source/server/src/test/java/tank/BaselineTest.java); test tích hợp server loopback và database tạm. |
| Phòng khóa/quyền chủ phòng | [RoomRefinementTest.lockedRoomRejectsWrongPasswordAndAcceptsCorrectPassword, onlyHostCanEditAndSettingsResetReadiness](../source/server/src/test/java/tank/RoomRefinementTest.java). |
| Danh mục và hình học map | [MapCatalogTest](../source/server/src/test/java/tank/MapCatalogTest.java) sinh `map-catalog.tsv`; [MapCatalogTests](../source/client/Assets/Tests/Editor/MapCatalogTests.cs) đối chiếu tên/ID/hash Java/C#, map mặc định nguyên byte, manifest lỗi, đăng ký map, đường đi và metadata. |
| Chọn map/phòng và UI | [RoomMapSelectionTest](../source/server/src/test/java/tank/RoomMapSelectionTest.java), [RoomMapProtocolTests](../source/client/Assets/Tests/Editor/RoomMapProtocolTests.cs) kiểm wire/lifecycle/hash ở Thường/Nhập vai; [RoomMapUiTests](../source/client/Assets/Tests/Editor/RoomMapUiTests.cs) kiểm geometry thẻ/ảnh/form cuộn và nút ghim. |
| Hợp đồng packet | [GoldenInteropTest.everyOpcodeHasCurrentGoldenPayload](../source/server/src/test/java/tank/GoldenInteropTest.java) sinh `golden.tsv`; [ProtocolTests.PreviousProtocolIsRejectedOnBothTransports, UnsignedAnglesAndFraming](../source/client/Assets/Tests/Editor/ProtocolTests.cs) kiểm tra version/framing phía client. |
| Di chuyển và prediction | [PredictionTest.sharedMovementTrace](../source/server/src/test/java/tank/PredictionTest.java) sinh `movement-trace.tsv`; [PlayerRecoveryTests.InputCatchesUpAfterFrameStallAndKeepsUpAtLowFrameRate, ExtrapolationIsBoundedDuringLostSnapshots](../source/client/Assets/Tests/Editor/PlayerRecoveryTests.cs). |
| Full/delta và atomicity | [DeltaTest.roundTripAndInterop](../source/server/src/test/java/tank/DeltaTest.java) sinh `delta-trace.tsv`; [ProtocolTests.JavaTraceReconstructs, DeltaIsAtomic](../source/client/Assets/Tests/Editor/ProtocolTests.cs). |
| Nhập vai/loadout | [RoleplayRoomTest.nationSelectionIsUniqueAtomicAndLockedWhileReady, loadingPublishesNationOnlyLoadoutAndAckGatesCountdown](../source/server/src/test/java/tank/RoleplayRoomTest.java), cùng [RoleplayClientTests](../source/client/Assets/Tests/Editor/RoleplayClientTests.cs) và [SkillContractTests](../source/client/Assets/Tests/Editor/SkillContractTests.cs). |
| Item, combat và VFX | [ItemCodecTest](../source/server/src/test/java/tank/ItemCodecTest.java), [CombatEventsTest.everyKindAndCauseRoundTripsAcrossFullAndDelta](../source/server/src/test/java/tank/CombatEventsTest.java), [CombatTests.RejoinWatermarkAndHiddenEffectsNeverReplay](../source/client/Assets/Tests/Editor/CombatTests.cs), [ItemTests](../source/client/Assets/Tests/Editor/ItemTests.cs). |
| Battlefield | [BattlefieldTest.fullDeltaLostPacketsAndRejoinRoundTripAndProduceUnityFixture, fiveTargetBombingRunsProduceFullDeltaAndReconnectInteropFixture](../source/server/src/test/java/tank/BattlefieldTest.java); [BattlefieldTests.JavaFullDeltaDroppedUpdateAndRejoinPreserveEveryByte, RenamedEventsAndTheirNoticesExpireWithTheLastEffectEvenWithAnOldSnapshot](../source/client/Assets/Tests/Editor/BattlefieldTests.cs). |
| Mất kết nối/vào lại | [NetworkRejoinTest.voluntaryDisconnectAndUdpTimeoutCanRejoinWithOriginalScoreAndLoadout](../source/server/src/test/java/tank/NetworkRejoinTest.java); [RejoinProtocolTests.UdpHandshakeTimeoutReturnsToLobbyWithoutLoggingOutAndCanRetry, UdpSocketFailureDoesNotCloseHealthyTcpLogin](../source/client/Assets/Tests/Editor/RejoinProtocolTests.cs). |
| Bối cảnh map | [ArenaBiomeTests](../source/client/Assets/Tests/Editor/ArenaBiomeTests.cs) kiểm lựa chọn bối cảnh, tuyến/trang trí không che nước, đúng vật cản thật và dữ liệu map không đổi; [ArenaBiomeArtTests](../source/client/Assets/Tests/Editor/ArenaBiomeArtTests.cs) kiểm atlas/crop/alpha/mapping/importer, cache và độ đọc ở kích thước nhỏ. Build validator và ảnh preview là kiểm tra bổ sung. |
| Tường/nước/cảnh vật | [ArenaEnvironmentGeometryTests](../source/client/Assets/Tests/Editor/ArenaEnvironmentGeometryTests.cs) kiểm hợp địa hình và cụm cảnh vật; [ArenaEnvironmentArtTests](../source/client/Assets/Tests/Editor/ArenaEnvironmentArtTests.cs) kiểm năm mẫu vật liệu, UV/importer/cache/provenance. Chụp ba map và thumbnail cùng bản build, kiểm mép nối, đường khô và độ rõ của combat. |
| Asset và layout | [CombatAssetTests.ImportedSheetsMeetCatalogAndBuildValidation, EveryMissingSheetAndWrongDimensionsFailWithTheClipName](../source/client/Assets/Tests/Editor/CombatAssetTests.cs), [CombatTests.RectangularArenaFitsWithoutOverlappingTheRightAnchoredSidebarAndAimStaysCorrect](../source/client/Assets/Tests/Editor/CombatTests.cs). |
| Kiểm tra điều kiện smoke | [ReviewRegressionTests.SmokeRejectsEarlyEndInvalidDataAndMissingDuration](../source/client/Assets/Tests/Editor/ReviewRegressionTests.cs) kiểm tra bộ điều kiện acceptance, không thay thế việc chạy multiplayer thật. |

Thư mục đầy đủ: [test Java](../source/server/src/test/java/tank) và [test Unity](../source/client/Assets/Tests/Editor). Khi chạy lọc test, ghi rõ filter và chạy đủ các test sinh fixture mà test client liên quan cần; không gọi một lần chạy có filter là toàn suite.

<a id="smoke"></a>
### Smoke hai client với server demo

Ví dụ này dùng server và client **đã build cùng đợt**, không chạy build lại. Mở ba cửa sổ PowerShell ở gốc repository: một cho server, một cho client chủ phòng và một cho client tham gia. Khởi động server trước theo [cấu hình demo](deployment.md#local-demo):

```powershell
./scripts/start-server.ps1 -Config config/demo.properties
```

Khi server đã lắng nghe trên `127.0.0.1`, TCP **23888**, UDP **23889**, chạy client chủ phòng:

```powershell
./build/client/TankBattle.exe --smoke --create --host 127.0.0.1 --tcp 23888 --udp 23889 --account docsmokehost --room docs-smoke-180 --players 2 --seconds 180 --expected-ticks 5400 --hp 100 --output runtime/smoke-host.json
```

Ngay sau đó chạy client tham gia ở cửa sổ còn lại để cả hai client hoạt động đồng thời:

```powershell
./build/client/TankBattle.exe --smoke --host 127.0.0.1 --tcp 23888 --udp 23889 --account docsmokepeer --room docs-smoke-180 --players 2 --seconds 180 --expected-ticks 5400 --hp 100 --output runtime/smoke-peer.json
```

Chỉ chủ phòng có `--create`. Hai lệnh dùng cùng tên phòng, hai tài khoản và hai đường dẫn output khác nhau; nếu đổi tên phòng cho lần thử khác thì đổi ở cả hai lệnh. Harness tự đăng ký/đăng nhập, ready, chủ phòng bắt đầu khi đủ hai người, điều khiển trong trận **180 giây/5400 tick**, chờ kết quả và lịch sử rồi ghi JSON và thoát. Server tiếp tục chạy cho tới khi Ctrl+C. Đây là các lệnh hướng dẫn, chưa được thực thi trong đợt tài liệu này.

Để thử Nhập vai, thêm `--roleplay` vào **cả hai** lệnh client. Có thể thêm `--battlefield` vào cả hai để yêu cầu mỗi client đã nhận cảnh báo và tác động của **cả hai loại theo map**: map 2 Red Zone/Bombstorm, map 3 Red Zone/Bão cát, map 4 Bão tuyết/Bombstorm. Thời lượng trận tự nó không bảo đảm đã bao phủ đủ hai loại; thiếu quan sát này sẽ làm điều kiện smoke thất bại ngay cả khi trận kết thúc.

Thêm `--chat` vào mọi client để mỗi người gửi một tin tiếng Việt có markup nguyên văn, xác nhận đã nhận tin của mọi slot và palette khớp loadout. Kiểm `chatSent=true`, `chatColorsValid=true`, `chatSlots=(1<<players)-1` và `success=true` trong từng JSON. Có thể dùng `--seconds 60 --expected-ticks 1800` để kiểm chat nhanh. Đây là kiểm qua ClientCore, không thay cho thao tác UI. Harness `--ui-test --ui-output <file>` bổ sung quan sát chat/focus, tự bắn và inputButtons để đối chiếu khi kiểm trực tiếp.

Thêm `--map 2`, `--map 3` hoặc `--map 4` vào **cả hai** lệnh để chọn và xác nhận Tiền Đồn Xanh, Pháo Đài Cát hoặc Sông Băng; bỏ cờ dùng map 2. Chủ phòng gửi map khi tạo, client tham gia tìm đúng phòng cùng map và cả hai kiểm map trong phòng/snapshot. Mỗi JSON phải có `expectedMap=roomMap=map`, `mapMatched=true` và `success=true`. Với thay đổi chọn map, chạy đủ ba map × hai chế độ (sáu trận), dùng tên phòng và output riêng từng trận. Đối chiếu thêm `matches.map_id`/`map_hash` của database demo với ID/SHA-256 byte gốc khi xác nhận lưu kết quả; smoke chỉ có hàng lịch sử chưa chứng minh chính xác hash đã lưu. Có thể ghép `--ui-test --ui-auto --map <id>` để tạo phòng tự động phục vụ quan sát UI; `selectedRoomMap`, `roomMap`, `map` trong output phân biệt lựa chọn trên form, metadata phòng và map đang dùng.

Đối chiếu [SmokePlayer.Awake, Update, Save](../source/client/Assets/Scripts/SmokePlayer.cs) và [SmokeAcceptance.Passed](../source/client/Assets/Scripts/SmokeAcceptance.cs). Kiểm tra `success` cùng các trường như `invalid`, `decoded`, `observedTick`, `expectedTicks`, lịch sử, số người và cấu hình trong **cả hai** file. `SmokeResult` là **JSON xác nhận acceptance**, không phải benchmark CSV: nó không chứa mẫu RTT, jitter, thời gian recovery, băng thông hoặc chuỗi mẫu thô của các chỉ số này. Muốn báo cáo đo mạng, dùng nguồn và quy trình tại [evaluation/metrics](evaluation.md#metrics), không suy ra số đo từ `success=true`.

<a id="manual"></a>
## Kiểm tra thủ công gameplay, UI và mạng

Chuẩn bị [demo cục bộ](deployment.md#local-demo) hoặc server LAN/VPN được phép, ít nhất hai client với tài khoản khác nhau và cùng đợt build. Kiểm tra các mục liên quan tới thay đổi; với thay đổi xuyên client/server, đi hết luồng từ tạo phòng tới kết quả.

| Nhóm | Thao tác và điều cần quan sát |
|---|---|
| Tài khoản/cấu hình | Đăng ký, đăng nhập sai/đúng, thông báo lỗi, lưu host/cổng, mở lại client để kiểm tra lựa chọn kết nối; phân biệt file kết nối local với database trên server. |
| Lobby/phòng | Tạo và tham gia phòng, mật khẩu sai/đúng, số người/capacity, quyền chủ phòng, kick/đổi chủ khi liên quan, ready rồi bắt đầu; thay cài đặt phải phản ánh cho mọi client. |
| Chọn map | Mở Tạo phòng phải chọn sẵn Tiền Đồn Xanh; bấm từng thẻ và kiểm tên/ảnh, tạo đủ ba map ở Thường/Nhập vai rồi so giữa hai client, sảnh và phòng chờ. Cài đặt phòng chỉ xem map đã chọn; sửa settings, rejoin, kết thúc/trận tiếp giữ nguyên map. Kiểm spawn, đường khô/vượt sông, gạch/nước/vật cản, item/battlefield và hash lưu trong database demo. Chụp form tại 960×600, 1280×800, 1600×900, 1920×1080; nút Xác nhận/Hủy luôn thấy khi cuộn. Preview `--menu-preview create --preview-map <2|3|4>` và `--arena-preview --preview-map <2|3|4>` chỉ chứng minh hình ảnh. |
| Diện mạo map | Tiền Đồn Xanh giữ nền cỏ, tường thép và cách vẽ nước xanh gốc; cầu gỗ nối khe khô giữa sông làm chậm. Pháo Đài Cát có hồ xanh ngọc và cọ dày ở hai góc ngoài mỗi hồ. Sông Băng có hồ băng lớn lệch giữa, rừng thông/nhà băng trên đất và không có cầu. So thumbnail với arena và ảnh cùng tỷ lệ ở 960×600, 1280×800, 1600×900, 1920×1080. Kiểm mép tường/nước liên tục, hành lang cầu đủ xe xoay, các cụm cảnh vật map xanh nằm trên đất với collider chân tương ứng; gạch/HP, xe, đạn, item và biến cố đọc rõ. Kiểm đường khô tới mọi spawn/item khi gạch còn nguyên và băng không có hiệu ứng trượt. |
| Thường/Nhập vai | Thử hai chế độ; quốc gia Nhập vai phải duy nhất, đồng bộ loadout và countdown; Q thành công, hồi chiêu, chết/hồi sinh và vào lại theo [luật kỹ năng](gameplay.md#skills). Hộp Đen trên kết quả là phần thưởng kể chuyện, không tự suy ra có inventory/persistence. |
| Di chuyển/ngắm | WASD/phím mũi tên, ngắm theo chuột, Shift, chạm góc/tường/gạch/nước, respawn; thử khi resize/fullscreen và sau frame stall khi đó là phạm vi thay đổi. |
| Bắn/combat/item | Click trái/Space, bật/tắt tự bắn bằng chuột phải trong arena, sát thương và thanh máu, phản xạ/phá gạch, pickup/buff, âm thanh và hiệu ứng không bị nhân đôi hoặc phát lại sau reconnect; đối chiếu [combat](gameplay.md#combat) và [item](gameplay.md#items). |
| Focus/hộp thoại | Alt-tab, chuyển sang cửa sổ client khác, mở/đóng hộp thoại; xe không tiếp tục nhận input ngoài cửa sổ đang focus, tự bắn reset, chuột trên sidebar không làm sai ngắm. |
| Layout | Kiểm tra ít nhất 960×600 và 1600×900, F11/Alt+Enter/nút fullscreen, khôi phục kích thước; map giữ tỷ lệ, sidebar sát phải, text/field không chồng lấn và không bị cắt. Preview ở [client](client.md#previews). |
| Battlefield/tầm nhìn | Quan sát cảnh báo, tác động, đổi lượt và thời điểm kết thúc của hai biến cố theo map, gồm bão cát/tuyết, kể cả snapshot chậm; tên/đồng hồ ở mép trên map hết cùng hiệu ứng, không lặp thông tin bão ở sidebar, VFX chịu sương mù và reconnect không phát lại tác động cũ. Luật số liệu ở [battlefield](gameplay.md#battlefield). |
| Mạng | Thử server cùng máy rồi LAN/VPN khi cần; phân biệt TCP đăng nhập và UDP vào trận. Với thay đổi recovery, kiểm tra rớt UDP/TCP, vào lại khi được phép, giữ điểm/loadout, map/gạch và không nhân đôi hiệu ứng; mô hình ở [rejoin](protocol.md#rejoin). |
| Kết quả/lịch sử | Đợi kết thúc đúng hạn, so bảng hạng/kết quả/lịch sử giữa client, trở về cùng phòng và chơi trận tiếp; nếu sửa persistence, khởi động lại server rồi kiểm tra dữ liệu theo [history](data.md#history). |
| Đăng ký/vàng/shop | Đăng ký với ô xác nhận trống/sai/đúng; đăng nhập lại kiểm số dư. Kết thúc Chiến dịch sau khi mua đồ, so vàng ròng cộng một lần. Trong RP thử E trước/trong/sau đợt, đủ/thiếu vàng, mua lặp từng loại, mua sau nhặt, chết/rejoin, sang đợt mới. Đóng shop bằng E/Esc, chờ tự đóng; xác nhận xe không bắn/di chuyển trong modal. Thử 1/3/5/10 phút và các kích thước viewport liên quan. |

Offline preview giúp kiểm tra hình ảnh và chụp PNG, nhưng không kiểm tra mô phỏng server. [WindowQa.Start](../source/client/Assets/Scripts/WindowQa.cs) ghi các bước resize/fullscreen; [UxTestDriver.Update](../source/client/Assets/Scripts/UxPreview.cs) có chế độ kết nối UI tự động và ghi quan sát. Các chế độ UI/smoke có thể tạo tài khoản, phòng, trận và lịch sử trên server: dùng dữ liệu demo riêng, ghi rõ cấu hình.

Lệnh chạy harness tích hợp và giới hạn output được mô tả tại [smoke hai client](#smoke); sự hiện diện của harness không phải bằng chứng đã chạy thành công. Kịch bản đánh giá rộng hơn nằm ở [evaluation](evaluation.md#scenarios).

<a id="evidence"></a>
## Cách ghi bằng chứng và giới hạn

Mỗi lần xác nhận thay đổi cần ghi: commit/working tree, ngày chạy, cấu hình và công cụ thực dùng, lệnh/filter, exit code, tổng pass/fail/skip, đường dẫn log/XML/ảnh/JSON liên quan, manual scenario đã thử và các bước chưa chạy kèm lý do. Kiểm tra diff cuối để phát hiện thay đổi ngoài phạm vi, nhất là scene và PlayerSettings sau build client. Không đưa database, credentials, log hay output build vào commit.

Phân biệt rõ ba mức:

- **Có trong mã nguồn:** test/harness và điều kiện kiểm tra tồn tại.
- **Đã chạy:** có lệnh, thời điểm, cấu hình và output của lần chạy tương ứng.
- **Đã xác nhận hành vi:** kết quả tự động hoặc manual đủ để chứng minh phạm vi đang báo cáo; preview offline không chứng minh multiplayer, benchmark không tự chứng minh cân bằng gameplay.

Bằng chứng của đợt tài liệu ngày 2026-10-23 chỉ là đọc script, config, lớp runtime/importer và các test được liên kết ở trên, cùng kiểm tra tài liệu. Không có kết quả pass mới cho Java, Unity, Windows build, LAN/VPN, gameplay hoặc UI. Các kết quả đo/thử nghiệm khác chỉ có giá trị theo phạm vi và thời điểm được ghi tại [evaluation/results](evaluation.md#results); không suy diễn từ log cũ sang working tree hiện tại.

### Kiểm chứng chọn map multiplayer — 2026-11-06

Các kết quả dưới đây thuộc đợt thêm chọn map và hai bố cục mới, trước đợt tách diện mạo sa mạc/băng tuyết và đổi tên thành Pháo Đài Cát/Sông Băng; không chứng minh renderer/asset mới đã qua các kiểm tra tương ứng.

Working tree dựa trên HEAD `08d6f756`, giữ các sửa UI/asset chưa commit trước đó. Dùng Unity **6000.3.24f1**, protocol **18**, ba map ID **2/3/4** từ manifest hiện tại.

| Lệnh/phạm vi đã chạy | Kết quả |
|---|---|
| `python scripts/sync-contracts.py --check` | Exit 0; contract Java/C# khớp JSON nguồn. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-server.ps1` | Exit 0; **252 passed, 0 failed/error/skipped**; xuất JAR mới trong `build/server/`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-client.ps1` | Exit 0; **264 passed, 0 failed/skipped**; XML/log chuẩn tại `runtime/unity-tests.*`. |
| Unity batch `-executeMethod BuildClient.BuildCurrentScene` | Exit 0; xuất `build/client/TankBattle.exe` từ scene hiện tại. Runtime assembly SHA256 bắt đầu `8E1DA3EBE5CE`; JAR bắt đầu `F2A8996C907A`. |
| Sáu trận loopback, mỗi map × Thường/Nhập vai, hai player build mới/trận | **12/12 client success**, đủ **1.800 tick**, `invalid=0`, `expectedMap=roomMap=map`, `mapMatched=true`; cả hai nhận kết quả/lịch sử và chat đúng slot/palette. |
| SQLite chỉ đọc sau sáu trận | Đủ sáu bản ghi `matches`, mỗi trận hai `match_players`; `map_id`, `room_mode`, `duration_ticks=1800` và SHA256 `map_hash` khớp byte gốc của map đã chọn. |
| Giới hạn thay đổi | Tám hash bảo vệ không đổi: hai map gốc, scene, PlayerSettings, EditorBuildSettings, Unity version và hai file package. Loại đúng block Android mặc định do Unity tự thêm vào 21 importer cũ sau đối chiếu byte; không sửa importer còn lại. |

Smoke dùng server kiểm thử riêng trên **127.0.0.1:28188/28189**, `benchmark=true`, `match.ticks=1800`, DB/spool riêng và admin tắt. Mỗi cặp chạy `--smoke --map <2|3|4> --players 2 --seconds 60 --expected-ticks 1800 --hp 100 --chat`, thêm `--create` cho host và `--roleplay` cho cả hai khi kiểm Nhập vai. Đây là client headless điều khiển qua harness; không phải sáu vòng chơi thủ công hoặc đánh giá cân bằng.

Database, PNG, JSON, helper và log kiểm thử riêng được dọn sau nghiệm thu theo hướng dẫn repository; giữ test nguồn, fixture trong `target/interop`, báo cáo Unity chuẩn và build. Chưa thử LAN/VPN hoặc phát hành lên launcher trong đợt chọn map này.

### Kiểm chứng diện mạo sa mạc/băng tuyết — 2026-11-06

Cùng working tree chưa commit, Unity **6000.3.24f1**, protocol **18**. ID 3 đổi tên thành **Pháo Đài Cát**, ID 4 thành **Sông Băng**. Chỉ thay presentation/tên hiển thị; cả bốn JSON map ID 1/2/3/4 giữ nguyên byte/hash.

| Lệnh/phạm vi đã chạy | Kết quả |
|---|---|
| `python scripts/sync-contracts.py --check` | Exit 0; không thay generated rules/content. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-server.ps1` | Exit 0; **252 passed, 0 failed/error/skipped**; JAR mới SHA256 bắt đầu `0589DE416108`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-client.ps1` | Full suite cuối sau sửa seam/nhãn tuyết: **283 passed, 0 failed/skipped**, exit 0; gồm 19 case biome mới. Giữ báo cáo chuẩn `runtime/unity-tests.xml`/`.log`. |
| Unity batch `-executeMethod BuildClient.BuildCurrentScene` | Exit 0, validator atlas/alpha/crop/provenance đạt; bản Windows mới ở `build/client/`, runtime assembly SHA256 bắt đầu `5D5B7A560D16`. |
| Preview Windows, 30 lần capture cho 18 scenario | Đúng kích thước, không đen, exit 0; không exception/IMGUI error. Arena ba map tại **960×600, 1280×800, 1600×900**; form tạo phòng 960/1280, phòng Thường/Nhập vai, tám xe/item, Red Zone sa mạc và Bombstorm tuyết. |
| Hai lỗi phát hiện qua ảnh và kiểm lại | Khe giữa tile tuyết ở 1280/1600 hết sau căn Floor/Ceil; nhãn trạng thái/BẠN có viền tối nên đọc rõ trên tuyết tại 960/1280. Các ảnh bị ảnh hưởng được chụp lại. Green giữ đường vẽ cũ; so ảnh chỉ thấy khác tại vùng xe động, địa hình còn lại khớp. |
| Bốn client giao diện thật trên server loopback riêng | Hai client map 3 Thường và hai client map 4 Nhập vai; cả bốn `PLAYING`, `map=roomMap`, `invalid=0`. Vòng build cuối có tick/decoded **707, 707, 722, 723**. Client tự vào phòng/ready/start, không tiêm chuột/phím. |
| Giữ phạm vi và asset | Mười hash bảo vệ không đổi: bốn map, scene, PlayerSettings, EditorBuildSettings, Unity version, manifest/package-lock. Atlas PNG nguyên byte SHA256 `06270564ac3505c4c18a79453d2fc637ad9f0d82368bddd6c7e4af187189e8cb`. Chỉ loại block Android mặc định Unity tự sinh trên 21 importer cũ sau đối chiếu đúng byte. |

Preview dùng `--arena-preview --preview-map <2|3|4> --capture <file> --capture-delay 2`, các cờ state `--preview-eight --preview-world-items --preview-roleplay` và `--preview-battlefield fire|airstrike`; menu dùng `--menu-preview create|room|roleplay-room`. Vòng giao diện mạng dùng server/DB thử riêng tại **127.0.0.1:28188/28189**, `benchmark=true`, admin tắt; bốn player có `--ui-test --ui-auto --map <3|4> --players 2 --seconds 60 --hp 100 --ui-output <file>`, thêm `--ui-peer` cho client thứ hai và `--roleplay` cho map 4. Capture sau 25 giây rồi tự thoát; đây không phải trận chơi đủ thời lượng hay kiểm thao tác thủ công. Server kiểm thử đã dừng.

Giữ ba [ảnh đối chiếu](client.md#ui) nguyên byte trong `docs/images/map-biomes/` để xem diện mạo; DB/ảnh thử khác/log/helper riêng được dọn. **1920×1080 chưa xác nhận bằng ảnh:** capture chạy ẩn trả ảnh đen; geometry ở kích thước đó có test nhưng không thay thế visual QA. Không dùng native input, chưa kiểm LAN/VPN, đạn riêng trên hai biome (preview chủ động bỏ đạn khi đổi map) hoặc đánh giá cân bằng. Stress tám xe còn có nhãn trạng thái bị cắt ở mép map theo bố trí có sẵn; sửa viền chữ không thay vị trí các nhãn đó. Không phát hành launcher hoặc commit trong đợt này.

### Kiểm chứng tăng gấp đôi kích thước đạn — 2026-11-06

Tăng cả hai chiều hình đạn/vùng va chạm xe qua JSON chung; vùng chạm tường riêng của đạn thường/Mirror mẹ tăng từ nửa cạnh 0,08 lên 0,16. `GameplayTest.doubledProjectilesHitAlongTheirNewLengthAndWidthForEveryKind` kiểm cả sáu loại đạn trúng ở phần dài/rộng mới và trượt ngoài mép mới; `doubledOrdinaryWallContactReflectsAtNewEdgeButPreservesNearMiss` kiểm phản xạ/tách Mirror và trường hợp trượt tường.

- `python scripts/sync-contracts.py --check`: đạt; Java/C# cùng content hash mới. Các hệ số HE/Mirror và sát thương không đổi.
- Maven `-Dtest=GameplayTest,GameplayUpgradeTest,AfkRejoinAndHitTest,ItemGameplayTest test`: **73 passed**. Sinh lại trace bằng `-Dtest=CombatGameplayTraceTest -Dtank.recordTrace=true test`, đối chiếu trước/sau: kịch bản NORMAL, RP map nhỏ và RP map lớn bắt đầu khác lần lượt tại tick **4/5/41**, phù hợp thay đổi va chạm; không dùng baseline cũ để khẳng định gameplay không đổi.
- `build-server.ps1` đã chạy Maven `clean package`; đủ **34 báo cáo JUnit, 254 passed, 0 failed/error/skipped** và JAR mới. PowerShell 5 báo lỗi native stderr tại thông báo lỗi DB/spool được test cố ý tạo, nên bước chép JAR được hoàn tất riêng bằng `Copy-Item`; SHA-256 JAR ở `target` và `build/server` khớp, bắt đầu `CCD48FF3974D`.
- Unity **6000.3.24f1** batch EditMode, chạy sau khi Java xuất đủ fixture: **283 passed, 0 failed/skipped**, exit 0. Build `-executeMethod BuildClient.Build`: exit 0, `Build Finished, Result: Success`; khôi phục đúng byte scene trước build và 21 `.meta` CampaignArt có block Android do Unity tự sinh, PlayerSettings không đổi. Lần đầu bị lỗi IPC/license; các lần cuối chạy được mà không sửa package/settings/license.
- Smoke server loopback riêng **29888/29889**, hai client Thường và hai client Nhập vai, map 2, **60 giây/1800 tick** mỗi trận: cả bốn `success=true`, `invalid=0`, `decoded=observedTick=1800`, `historyRows=1`, `mapMatched=true`. Đây là harness tự động, không phải thao tác giao diện thủ công.
- Đã mở ảnh arena preview từ bản Windows mới, thấy hai hình đạn trên map xanh. Chưa kiểm trực tiếp hiệu ứng đạn Chiến dịch, đạn trên map sa mạc/băng tuyết, thao tác thủ công hoặc LAN/VPN. Không phát hành launcher; log/ảnh/DB/helper của lượt kiểm tra này được dọn sau khi đọc kết quả.

<a id="environment-polish"></a>
### Kiểm chứng tường, nước và cảnh vật — 2026-11-06

Đợt này chỉ sửa presentation phía client: tường không phá hủy map 3/4, nước map 2/3/4 và tối đa bốn cụm cảnh vật bổ sung trên mỗi map. Các thay đổi gameplay/contract đã có trước đợt này được giữ riêng; không dùng kết quả Java của các đợt trên làm kết quả mới.

| Kiểm tra thực hiện | Kết quả |
|---|---|
| `python scripts/sync-contracts.py --check` | Đạt; không sửa JSON luật hoặc contract sinh ra trong đợt làm đẹp. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-client.ps1` | Full suite cuối: **305 passed, 0 failed/skipped**, exit 0; gồm 16 case geometry và 6 case asset môi trường mới. Báo cáo chuẩn ở `runtime/unity-tests.xml`/`.log`. |
| Unity `-batchmode -nographics -quit -projectPath <source/client> -executeMethod BuildClient.BuildCurrentScene -logFile <log>` | Build Windows thành công, exit 0, `Build Finished, Result: Success`. Không tạo lại scene hoặc gán PlayerSettings. Vòng đầu gặp lỗi IPC/license; vòng cuối dùng Package Manager bình thường. `Tank.Runtime.dll` SHA-256 `F8F0C7CA2F8B576499D9936B6D357C3779BAF8E36724F4F79D3DC4394EFD0A3D`. |
| Arena ba map × bốn kích thước | **12 PNG** từ build mới đúng **960×600, 1280×800, 1600×900, 1920×1080**, không đen. So mép nước/tường, chỗ nối L/T, đường khô, xe và gạch; giữ sidebar và nút rời trận. |
| Menu và trạng thái bổ sung | **10 PNG**: tạo phòng 960/1280, phòng Thường map 3 và Nhập vai map 4 tại 1280; tám xe/sáu item/túi Nhập vai trên ba map; Red Zone map 3, Bombstorm và sương mù map 4. Thumbnail dùng cùng vật liệu/cảnh vật như arena. |
| Bốn client giao diện thật, server loopback riêng | Hai client Thường map 3 và hai client Nhập vai map 4 đều `PLAYING`, `map=roomMap`, `invalid=0`; quan sát mới khi cả bốn còn kết nối có tick/decoded **113, 91, 84, 84**. Có bốn ảnh 1280×800 từ luồng giao diện thật; không tiêm chuột/phím. |
| Nguồn asset và phạm vi | Atlas nguyên byte SHA-256 `d495a06c21772afd464f97d07c5bd82cda636631b4c3e1ae0e97e6117d1d5250`, RGBA32 6 MiB, có provenance/prompt và bản chép `ThirdParty`. **14 hash bảo vệ không đổi**: bốn map, scene/settings/version/packages và `World.java`/JSON luật/hai `GameRules` có sửa sẵn trước đợt này. Loại đúng block Android Unity tự sinh trên 21 `.meta` cũ khi phần còn lại khớp HEAD, giữ GUID/importer gốc. |

Lệnh ảnh dùng `--arena-preview --preview-map <2|3|4> --capture <file> --capture-delay 2`, `-screen-fullscreen 0 -screen-width <w> -screen-height <h> -popupwindow`; các state/menu dùng cờ đã liệt kê trong [client/preview](client.md#previews). Lần đầu cửa sổ phóng to đã nhớ làm ảnh sai kích thước; `CapturePreview` nay áp lại kích thước CLI lúc tái tạo swap chain và ghi kích thước cuối vào log. Đã bỏ các ảnh sai khỏi bằng chứng. Giới hạn ảnh đen 1920×1080 ghi ở đợt biome trước không còn xảy ra trong vòng này.

Lượt mạng cuối dùng server/JAR có sẵn, DB thử mới và địa chỉ **127.0.0.1:28588/28589**, `benchmark=true`, admin tắt. Bốn client có `--ui-test --ui-auto --map <3|4> --players 2 --seconds 60 --hp 100 --ui-output <file> --capture <file> --capture-delay 20`, thêm `--ui-peer` cho client thứ hai và `--roleplay` cho map 4. Harness chờ đủ bốn client nhận ít nhất 60 snapshot rồi giữ quan sát mới trước khi helper chụp/thoát; không đọc output cũ hoặc lấy trạng thái peer sau khi chủ phòng đã thoát làm kết quả. Server đã dừng. Đây là kiểm kết nối/hiển thị ngắn qua UI, không phải chơi đủ trận 60 giây hay kiểm AFK/rejoin.

Ba [ảnh đối chiếu 1280×800](client.md#ui) tại `docs/images/map-biomes/` được thay bằng ảnh nguyên byte của đợt làm đẹp; helper/ảnh thử/log/DB riêng được dọn sau khi đọc kết quả. Chưa kiểm thao tác tay, LAN/VPN, FPS/GPU hoặc cân bằng. Preview trên map 3/4 chủ động bỏ đạn, nên không khẳng định đã kiểm riêng độ tương phản đạn ở hai map đó. Nhãn trạng thái của stress tám xe vẫn có thể bị cắt ở mép map theo bố trí sẵn có; không đổi vị trí nhãn trong đợt này. Không stage/commit hoặc phát hành launcher.

<a id="biome-terrain-rules"></a>
### Kiểm chứng hồ băng, ốc đảo và cảnh vật trên đất — 2026-11-06

Lượt này thay cả gameplay địa hình và dữ liệu map: nước map 2 vẫn ×0,5; hồ map 3 không chậm và hồi năng lượng Shift ×1,5 sau 15 tick thả phím; hồ đóng băng map 4 ×1,5 tốc độ. Thêm bốn collider chân cảnh vật ở map 2 và sáu collider thân cọ map 3. Hồ băng giảm từ 1.362 xuống **648 đơn vị²**, sáu hồ bằng số lượng/ngân sách diện tích map mặc định, bố trí lệch thành ba cặp đối xứng 180°. Luật và đường va chạm đạn đã chỉnh ở lượt trước được giữ lại.

| Kiểm tra đã chạy | Kết quả |
|---|---|
| `python scripts/sync-contracts.py --check` | Đạt; sinh Java/C# từ JSON nguồn, combined content hash mới; wire/protocol vẫn 18. |
| Maven `-f source/server/pom.xml clean package` | Vòng cuối với bố cục hồ lệch: **262/262 tests**, 35 suites, không fail/error/skip. Toàn bộ fixture interop đã sinh lại trước Unity. Các kiểm tập trung ban đầu 54/54 và MapCatalog 7/7 cũng đạt sau sửa clearance. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-client.ps1` | Vòng cuối: **318/318 tests**, không fail/skip, exit 0. Có 7 test terrain Java, 5 test terrain C#, 7 test asset mới cùng các test geometry cập nhật. Báo cáo chuẩn giữ ở `runtime/unity-tests.xml`/`.log`. |
| Build server và Unity `BuildClient.BuildCurrentScene` | Cả hai thành công. JAR target/build cùng SHA-256 `2BB5442A31E500D9502EE1B61D524A14BA0D35CCC49D7E8B4ABBFDF97956F468`; `Tank.Runtime.dll` cuối `E0E9837A636471CE2B4F49ED0432731844ADA9A7EE1E6DA96CD0E433D23C36E4`. Giữ scene/settings/packages. |
| 3 map × Thường/Nhập vai, 2 client/trận | Sáu trận headless qua TCP/UDP thật, 60 giây/1.800 tick, HP 100: **12/12 client success=true**, mapMatched=true, map=roomMap=expected, invalid=0, historyRows=1. SQLite riêng có sáu trận/12 người, duration_ticks=1800, reason=0. |
| Map băng bố cục cuối chạy lại | Sau khi đổi hồ từ một hàng sang bố cục lệch, chạy riêng Thường/Nhập vai song song: **4/4 client đạt**, mỗi trận 1.800 tick, invalid=0, lịch sử thành công. SQLite xác nhận đúng hash `44F7318D…` của map cuối; dùng runtime/JAR cuối ở hàng build. |
| Kiểm tra ảnh và giữ phạm vi | **12 PNG** arena đúng 960×600, 1280×800, 1600×900, 1920×1080 cho cả ba map, không đen; thêm Tạo phòng 960 và tám xe/item/túi Nhập vai ở sa mạc/băng. Ba ảnh 1280 cuối thay vào `docs/images/map-biomes/`. Chín hash bảo vệ (map legacy, scene/settings/version/packages, hai atlas cũ) không đổi; chỉ loại đúng block Android Unity tự sinh trên 21 `.meta` cũ sau đối chiếu phần còn lại khớp HEAD. |

Sáu trận đầu dùng DB/cấu hình riêng, loopback TCP **4943** / UDP **60232**, hai batch sáu client; đợt map băng cuối dùng TCP **18026** / UDP **60353** và DB mới. Server có `benchmark=true`, `admin.enabled=false`, stop file riêng. Client dùng `-batchmode -nographics --smoke --host 127.0.0.1 --tcp <tcp> --udp <udp> --account <qa> --room <room> --map <2|3|4> --players 2 --seconds 60 --expected-ticks 1800 --hp 100 --output <file>`; chủ phòng thêm `--create`, cả hai thêm `--roleplay` khi thử Nhập vai. Bốn case map 2/3 không đổi sau lần chạy đầu; map 4 được chạy lại với đúng dữ liệu/runtime cuối. Tất cả server/client QA đã dừng.

Đã giữ baseline trace có thay đổi projectile từ trước tác vụ để đối chiếu, không thay theo HEAD. Thêm collider cảnh vật map 2 làm đạn 47 bật tại collider mới 19 ở tick **393**: **684 hash tick của scenario 2** đổi; scenario 0/1 và cả ba dòng RNG giữ nguyên byte. Chạy hai world bằng cùng code đạn, chỉ đổi map cũ/mới, xác nhận khác biệt đầu là phản xạ đó trước khi ghi lại trace. Test tuyến khô ban đầu phát hiện item `(72,48)` quá sát hồ; đã sửa hình học, tăng assertion clearance lên `TankHalfSize×√2` và kiểm lại mọi spawn/item, không nới assertion cho qua.

Hash map cuối: map 2 `a60c5f8e15253964579b818da663b3f6cde5cf2ab9e29d55e35cbb8b3a21067d`, map 3 `6003bc2a38aaedb3623b7d891ac6915ba22a0fb29d5d2d119475a8f534bd0682`, map 4 `44f7318dd3d0303fa979000c0f355b0cbdea49669444f22594d310a114915605`. Vì cả ba map và content hash đổi, phải dùng server/client cùng đợt. Map legacy ID 1, toàn bộ tường/gạch/spawn/item/fire target cũ và asset nguồn cũ giữ nguyên; thay đổi map chỉ là hồ băng/collider bổ sung đã nêu. Hai nguồn mới giữ byte/prompt/provenance riêng ở `ArenaTerrain`, build kèm provenance và prompt trong `ThirdParty`.

Smoke tự động không bảo đảm đi qua từng hồ; tỷ lệ tốc độ/hồi Shift và prediction được xác nhận bằng test/fixture riêng. Chưa kiểm thao tác tay, LAN/VPN, FPS/GPU hoặc cân bằng. Không phát hành launcher hoặc stage/commit. Helper/log/ảnh thử/DB riêng của tác vụ được dọn sau khi lưu bằng chứng; giữ báo cáo Unity chuẩn, fixture và ảnh đối chiếu.

Capture dùng các cờ preview ở [client](client.md#previews), `-screen-fullscreen 0 -screen-width <w> -screen-height <h> -popupwindow --capture-delay 2`. Helper nay thu hẹp 1 px rồi trở về kích thước yêu cầu; cách mở rộng 1 px cũ có thể bị kẹp ở chiều rộng desktop và không tái tạo swap chain, gây ảnh đen 1920. Ảnh đen đã được phát hiện bằng kiểm kích thước/pixel, bỏ và chụp lại; ảnh cuối đúng kích thước và có nội dung.

<a id="map-composition"></a>
### Kiểm chứng bố cục map và hồ băng trong rừng — 2026-11-07

Working tree chưa commit dựa trên `08d6f756`, Unity **6000.3.24f1**, protocol **18**. Bản cuối: Tiền Đồn Xanh có sông dọc, hai hồ phụ và một cầu gỗ trên khe khô; Pháo Đài Cát có 18 cọ quanh hai hồ. Hai map này được người dùng chốt và giữ nguyên byte trong lượt sửa mùa đông cuối. Sông Băng đổi hẳn hồ/tường: hồ băng lớn bờ thuôn lệch giữa, sáu cụm rừng và hai cụm nhà băng phía đông; không có cầu hoặc người tuyết. Giữ nguyên luật địa hình và combat đã có trước tác vụ.

| Lệnh/phạm vi đã chạy | Kết quả cuối |
|---|---|
| `python scripts/sync-contracts.py --check` | Đạt; file luật, `World.java`, prediction `GameState.cs`, generated contracts và protocol không đổi trong tác vụ bố cục. |
| Maven `-B -f source/server/pom.xml clean package` qua helper PowerShell riêng | **262/262 tests**, 35 suites, không fail/error/skip. Đã xuất lại fixture Java trước Unity và chép JAR đúng byte sang `build/server/`. JAR SHA-256 `6A8D548BD68ACF1755597A74CDEDC70D642D779F377C4F5733FE7DC9D3E00585`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-client.ps1` | **326/326 tests**, không fail/skip, exit 0; gồm sáu test nhà băng mới, mapping/collider cảnh vật, hồ nối liền, lối đi khô cho xe xoay 45° và lịch hazard. Giữ báo cáo chuẩn `runtime/unity-tests.xml`/`.log`. |
| Unity batch `-executeMethod BuildClient.BuildCurrentScene` | Thành công, exit 0; không dựng lại scene/settings. `Tank.Runtime.dll` SHA-256 `1A9E6F8E5DEC76FDB9D731DADD06C8638D7C77C850BC541C20BE996B1D3358CF`. |
| Geometry ngoài engine | SAT 45° và BFS lưới 0,5: **23.896 điểm khô nối thông**, đủ mọi spawn/item khi gạch còn nguyên. Hồ nối rộng theo hai trục, 648 đơn vị²; tán cây/nhà/băng không chồng hồ, tường/gạch hoặc thân xe tại spawn/item. |
| Arena ba map × bốn kích thước | **12 PNG mới** đúng 960×600, 1280×800, 1600×900, 1920×1080; kiểm kích thước/pixel chống ảnh đen và xem hình. Cộng chín PNG: tám xe/item/túi Nhập vai trên ba map, ba màn tạo/phòng và ba ảnh đủ gạch/tám xe không bật kỹ năng. |
| Mạng Thường/Nhập vai | Đủ **sáu cặp map/chế độ**, hai client/trận, 60 giây/1.800 tick: **12/12 client thành công**, decoded=observedTick=1800, invalid=0, historyRows=1, map=roomMap=expectedMap. Hai trận mùa đông cuối chạy trên đúng build/hash mới. |
| SQLite | Sáu bản ghi đã đối chiếu: map ID, hash byte gốc, NORMAL/ROLEPLAY, duration_ticks=1800, reason=0 và hai người/trận đều đúng. |
| Giữ phạm vi | **20 hash bảo vệ không đổi**: 17 source/rules/scene/settings/packages/ảnh nguồn đã chốt trước tác vụ và ba guard map mặc định, map sa mạc, combat trace sau khi hai map được chốt. Loại đúng block Android 13 dòng Unity tự sinh trên 21 importer cũ, chỉ khi phần còn lại khớp HEAD; giữ GUID và mọi importer khác. `git diff --check` đạt, index rỗng. |

Bằng chứng mạng được ghép đúng phiên bản: bốn trận map 2/3 trên loopback **12510/51583** (`network-dc839624`) dùng DLL `71DD5A3E…`, JAR `26C93855…`, với đúng hai map đã được chốt. Sau khi chỉ sửa mùa đông/presentation, hai trận map 4 được chạy lại riêng trên **37380/51959** (`network-881d76af`) bằng DLL/JAR cuối nêu trên. Core mô phỏng, luật, map 2/3 và combat trace được bảo vệ bằng hash suốt lượt mùa đông; không lấy hai trận winter bố cục cũ làm kết quả mới. Các server có DB/spool riêng, `benchmark=true`, admin tắt và stop file riêng. Client dùng `-batchmode -nographics --smoke --map <id> --players 2 --seconds 60 --expected-ticks 1800 --hp 100`, host thêm `--create`, hai phía thêm `--roleplay` khi thử Nhập vai. Tất cả tiến trình QA đã dừng.

Khi đổi bố cục map mặc định, trace được chẩn đoán bằng hai world cùng code trước khi cập nhật fixture: tick **14** khác vị trí xe vì đi vào sông mới làm chậm. RNG scenario 2 chỉ bắt đầu khác ở tick **1200**, khi điểm item `(56,099537;20,875002)` trước đây khô nay nằm trong sông, bị từ chối nên tiêu thêm lần lấy số ngẫu nhiên. Chỉ thay **1.186 hash tick và dòng RNG scenario 2** so với bản ngay trước bố cục sông dọc; scenario 0/1 và RNG của chúng giữ nguyên. Trace sau đó giữ SHA-256 `a839e8c9117396d3a30e6c8a7817bd65f9345ec02ebebe773c510342ce840d7d` trong mọi sửa mùa đông. Test cũ đếm sáu hồ được đổi theo bốn vùng nước mới; test debris dùng bán kính 10 vô tình bắt thêm gạch mới ID 12, nên thu fixture về 4,5 và vẫn kiểm đúng ba ID 0/1/2, mỗi ID một sự kiện sau hai bước, không nới kiểm tra chống lặp.

Hash map cuối: map 2 `1afb24e8b0681e24637d8ee7927fb0a1f630b660c1aff09dd09463f1c1a0c8de`; map 3 `246cb0f29ff737b8b4ec41a2b90044f2e01684fe401fba968f6317fa1951d8a4`; map 4 `5693ccbb553ed4c3d6dc9e12b0126ade230a23241dc9f9ad06d3dfbadbc5edd2`. Map mùa đông giữ 16 spawn và 48 mục tiêu không kích; chuyển bảy trong 18 điểm item tránh địa hình mới, bố trí lại 48 mục tiêu lửa. Các JSON source và StreamingAssets của build khớp byte. Phải dùng server/client cùng bộ map mới; không cần đổi IP nếu vẫn dùng địa chỉ local hiện có.

Asset bổ sung duy nhất là [nhà băng](../source/client/Assets/Resources/ArenaWinter/igloo.png), tạo bằng built-in `image_gen`, PNG RGBA 1254², alpha nguyên bản, SHA-256 `baa56efa921ac0c0d5108330f7ee8f39aa9bf056a199f40c1c8f3a418785cc16`. Nguồn, prompt, importer, kiểm hash/alpha và notice trong `ThirdParty` đã được kiểm; texture 6.290.064 byte dưới ngân sách riêng 8 MiB. Thông/đá/tinh thể dùng lại `ArenaBiomes`; không đổi ảnh băng hoặc asset sa mạc. Ba ảnh [đối chiếu 1280×800](client.md#ui) lưu nguyên byte trong `docs/images/map-biomes/`.

Đây là kiểm ảnh tĩnh và smoke tự động, không phải chơi bằng chuột/phím. Chưa kiểm LAN/VPN, FPS/GPU hoặc cân bằng; smoke không chứng minh xe đi qua từng mép hồ. Test địa hình/prediction giữ trách nhiệm kiểm tỷ lệ tốc độ và hồi Shift. Preview chủ động bỏ đạn khi đặt xe vào spawn map, nên không khẳng định đã kiểm độ tương phản đạn trong ảnh này; một số nhãn kỹ năng ở stress tám xe vẫn sát mép map theo bố trí có sẵn. Không phát hành launcher, stage hoặc commit. Helper/log/DB/ảnh thử riêng được dọn sau khi ghi bằng chứng; giữ báo cáo Unity chuẩn, fixture, build và ảnh đối chiếu.


<a id="weather-v19"></a>
### Kiểm chứng bão theo map và khiên hồi sinh — 2026-11-07

Working tree chưa commit trên `08d6f756`, Unity **6000.3.24f1**, protocol **19**. Thêm bão cát map 3, bão tuyết map 4 và khiên xuất hiện/hồi sinh thật 90 tick. Toàn bộ JSON map giữ byte của bản người dùng đã duyệt; không đổi database schema, scene, ProjectSettings hoặc packages.

| Lệnh/phạm vi thực chạy | Kết quả |
|---|---|
| `python scripts/sync-contracts.py` rồi `python scripts/sync-contracts.py --check` | Sinh Java/C# từ JSON nguồn; check đạt. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-server.ps1` | Build thành công, **271/271**, 36 suites, không fail/error/skip; sinh lại fixture interop gồm 64 ca movement tuyết và 22 record state bão. |
| Maven `-f source/server/pom.xml -Dtest=ProtocolTest,ItemCodecTest test` | **12/12** sau khi bổ sung xác nhận v18 bị từ chối trên TCP/UDP; production server không đổi sau build đầy đủ. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-client.ps1` | **336/336**, không fail/skip, gồm 10 WeatherTests, fixture Java, generate/reconcile ở biên, cát/Fog/beam và cause chết. Báo cáo chuẩn `runtime/unity-tests.xml`/`.log`. |
| Unity batch `-executeMethod BuildClient.BuildCurrentScene` | Exit 0, client Windows trong `build/client/`; giữ nguyên scene/settings. Log chuẩn `runtime/unity-build.log`. |
| Hai client × ba map × hai mode, mỗi trận 120 giây | **12/12 client exit 0**, success=true, invalid=0, observedTick=3600, historyRows=1. Cả types/warnings/impacts đúng mask **12/20/40** tương ứng map 2/3/4; có chat, hai quốc gia khác nhau và Q ở RP. |
| SQLite demo riêng | Sáu match đúng map ID/hash byte gốc, NORMAL/ROLEPLAY, duration_ticks=3600, reason=0. Server loopback **32388/32389**, admin tắt, DB/spool/stop file riêng; đã dừng tiến trình QA. |
| PNG từ build, 16 scenario | Tám ảnh đang bão tại **960×600, 1280×800, 1600×900, 1920×1080**, bốn ảnh cảnh báo/kết thúc, cát sát ranh giới/cát+Fog và hai ảnh RP cửa sổ thấp có chat chưa đọc. Đúng kích thước, không đen, không shader error/exception. |
| Âm thanh Windows | Native lifecycle: **opened=7, closed=7, matchStops=10**, buffer được thả khi Stop/Dispose; bật bão dùng voice thứ bảy, tắt SoundEnabled dừng voice bão. PCM được làm im lặng trong test, không coi đây là kiểm chất lượng âm thanh bằng tai. |
| Phạm vi/diff | Map 2/3/4 và map cũ ID 1 không đổi hash; combat trace `a839e8c9…` giữ nguyên. Loại đúng block Android 13 dòng Unity tự thêm trên 21 importer cũ, chỉ sau khi phần còn lại khớp HEAD; giữ GUID. Index rỗng, không stage/commit. |

Lệnh smoke dùng client build với `-batchmode -nographics --smoke --host 127.0.0.1 --tcp 32388 --udp 32389 --map <2|3|4> --players 2 --seconds 120 --expected-ticks 3600 --battlefield --chat`; host thêm `--create`, cả hai thêm `--roleplay` khi thử RP, output/tài khoản/phòng riêng. JAR SHA-256 **8a2de1bfbee822462ac5a4d4c8ec94ac50f3f83aafb5d1f0c825453fed8b223e**. Lượt smoke cuối dùng runtime DLL **2fb2b558ac761d3fd577b48a8317273a8bcb8ee2df11a045ea541ee88f9aa52f**. Bản giao cuối **a6d3bdc916c00c995cf956e6855c858f0a4962dc0fbf710f4077cb02325a6284** chỉ bổ sung cố định tick và dữ liệu khiên của `ArenaPreview` sau lượt mạng; đường chơi thật không đổi. Unity suite được chạy lại trên source cuối.

Ảnh cuối dùng `--arena-preview --preview-eight --preview-world-items --preview-map <3|4> --preview-battlefield <sandstorm|snowstorm> --preview-battlefield-tick <20|180|330> --capture <png>` và kích thước tương ứng. Cố định tick tránh thời gian khởi tạo cửa sổ/resize làm ảnh cảnh báo vô tình chụp sau impact. Cảnh `--preview-visibility --preview-roleplay` đặt xe/đạn sát và ngoài radius, có beam từ nguồn ngoài radius; xác nhận phần giao vẫn hiện và phần ngoài bị cắt. `--preview-fog` thu quan sát về 5,1 và che địa hình theo Fog cũ. Lưu sáu ảnh gốc ở [docs/images/weather](images/weather/sandstorm.png): [cát](images/weather/sandstorm.png), [tuyết](images/weather/snowstorm.png), [ranh giới](images/weather/sandstorm-visibility.png), [Fog](images/weather/sandstorm-fog.png), [cảnh báo cát](images/weather/sandstorm-warning.png), [cảnh báo tuyết](images/weather/snowstorm-warning.png).

Đây là smoke qua ClientCore và kiểm ảnh tĩnh, **chưa phải chơi bằng chuột/phím**. Chưa kiểm thủ công giữ phím qua modal/chat, focus, fullscreen và ngắm trong bão; chưa nghe trực tiếp âm thanh, đo FPS/GPU, kiểm LAN/VPN hoặc cân bằng gameplay. Các test input/modal cũ vẫn đạt nhưng không thay thế các thao tác này. Không suy thời gian mô phỏng từ đồng hồ/HP cố định trong preview. Helper/log/DB/ảnh thử riêng được dọn sau khi ghi bằng chứng; giữ fixture, báo cáo Unity chuẩn, build và sáu ảnh đối chiếu.

<a id="weather-v20"></a>
### Kiểm chứng dải bão di chuyển — 2026-11-07

Working tree chưa commit, Unity **6000.3.24f1**, protocol **20**. Bão cát/tuyết đổi sang dải rộng 33% quét trái → phải trong 12 giây; cảnh báo 3 giây, chỉ tâm xe trong dải chịu sát thương/giảm tốc. Bỏ cắt xe theo bán kính của bão cát, chuyển thông báo lên mép trên bản đồ; tuyết ×0,5. Bộ kiểm chứng v19 ở trên là lịch sử, không thay thế kết quả đợt này.

| Lệnh/phạm vi thực chạy | Kết quả |
|---|---|
| `python scripts/sync-contracts.py` và `python scripts/sync-contracts.py --check` | Sinh contract từ JSON nguồn; check đạt. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-server.ps1` | **273/273 test**, 36 suites, không fail/error/skip, gồm 10 WeatherTest; đóng gói JAR. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-client.ps1` | **340/340**, không fail/skip, gồm 14 WeatherTests. Giữ báo cáo chuẩn `runtime/unity-tests.xml`/`.log`. |
| Java/C# interop | **640 ca movement**, **1.728 ca geometry**, **26 FULL/DELTA/drop/rejoin records**; kiểm bit IEEE754 của hai mép và membership, băng/Shift/slow, pre-move position, khiên/giáp/chết môi trường và prediction/reconcile. |
| Unity batch `-executeMethod BuildClient.BuildUiRevision` | Build thành công vào `build/client-ui-revision`; sau khi xác nhận không còn tiến trình dùng `build/client`, copy nguyên bộ sang `build/client`. Runtime DLL hai thư mục khớp hash. Log chuẩn `runtime/unity-build.log`. |
| Hai client × ba map × Thường/Nhập vai, mỗi trận 120 giây | **12/12 client exit 0**, success=true, decoded=3600, invalid=0, observedTick=3600, historyRows=1, endReason=0, mapMatched=true. Types/warnings/impacts đúng **12/20/40** theo map 2/3/4; chat hợp lệ, RP có Q. |
| SQLite demo riêng | Sáu match smoke đúng map ID/hash byte gốc, mode, duration_ticks=3600, reason=0. Server loopback **32418/32419**, admin tắt, DB/spool riêng; dừng sạch bằng stop file. Match UI thử bổ sung bị AFK được loại khỏi sáu match acceptance. |
| 19 ảnh PNG từ cùng build | Đủ **960×600, 1280×800, 1600×900, 1920×1080** ở cả hai map; thêm warning/early/mid/late/ended, RP cửa sổ thấp và Fog. Đã xem tám ảnh bắt buộc và các trạng thái bổ sung; chữ/bố cục, xe bốn hướng, đạn/HP, vùng trong/ngoài đọc được. PNG đúng kích thước, không đen, log không exception/shader error. |
| Âm thanh Windows | Test native dùng voice thứ bảy, đổi gain khi vào/ra vùng, mute/Stop/Dispose thả buffer. PCM test làm im lặng; chưa nghe trực tiếp chất lượng âm thanh. |
| Diff/phạm vi | Hash map 1/2/3/4, scene, ProjectSettings, packages và baseline combat giữ nguyên. Xóa đúng 21 block Android Unity tự thêm sau khi xác minh phần còn lại khớp HEAD; giữ GUID/importer. `git diff --check` đạt; index rỗng. |

Lượt Unity đầu có hai failure: geometry phát hiện **một ULP lệch thật giữa Java/Mono** ở tick 749, đã sửa bằng double trung gian và ép float rõ ràng; regression kiểm đúng bit/biên nửa mở, không nới tolerance. Test Fog trước đó coi âm hết Fog là replay, đã sửa để chỉ cho phép đúng cue hết Fog. Suite cuối 340/340 được chạy sau toàn bộ sửa code.

Smoke dùng `-batchmode -nographics --smoke --host 127.0.0.1 --tcp 32418 --udp 32419 --map <2|3|4> --players 2 --seconds 120 --expected-ticks 3600 --battlefield --chat`; host thêm `--create`, cả hai thêm `--roleplay` cho RP. JAR SHA-256 **5250c333dee904537a039549ae09c2b896cde4b4a3e8100c1ab3aefe10efad7a**, runtime `Tank.Runtime.dll` **8fecbb213365d9043abb77b3b482980cd94f0f66d20a4c476b13d4ad8b051296**. Build được kiểm mạng, chụp ảnh và bản giao có cùng runtime DLL, không có sửa source sau đó.

Ảnh dùng `--arena-preview --preview-eight --preview-world-items --preview-visibility --preview-map <3|4> --preview-battlefield <sandstorm|snowstorm> --preview-battlefield-tick <20|150|270|390|480> --capture <png> --capture-delay 0` và `-screen-width/-screen-height` tương ứng. Thêm `--preview-roleplay --preview-chat` cho cửa sổ thấp; `--preview-roleplay --preview-fog` cho Fog. Lưu 19 ảnh gốc ở [images/weather-band](images/weather-band/sand-mid-1280.png), gồm [cát](images/weather-band/sand-mid-1280.png), [tuyết](images/weather-band/snow-mid-1280.png), [cảnh báo](images/weather-band/sand-warning.png), [giai đoạn cuối](images/weather-band/snow-late.png), [RP 960×600](images/weather-band/sand-roleplay-low.png) và [Fog](images/weather-band/sand-fog.png).

**Giới hạn:** đây là smoke qua ClientCore và ảnh preview, chưa phải chơi trực tiếp bằng chuột/phím. Computer Use không trả về cửa sổ Tank Battle dù hai client UI đã vào trận; không gửi được input, hai client đó bị AFK và không được tính acceptance. Chưa kiểm thực tế giữ phím qua chat/modal, focus/fullscreen/ngắm trong bão, chưa nghe âm thanh, đo FPS/GPU, kiểm LAN/VPN hoặc cân bằng gameplay. Ảnh early/mid/late chứng minh vị trí dải theo pha; không coi ảnh tĩnh hoặc FPS 60 trên preview là benchmark chuyển động. Không đổi IP, database schema, deploy cloud, stage hoặc commit. Helper/log/DB/ảnh thử trong thư mục QA được dọn sau khi giữ bằng chứng ở đây và ảnh trong docs; giữ báo cáo Unity chuẩn, fixture và build.

<a id="weather-v21"></a>
### Kiểm chứng bão toàn map và giảm tốc độ bắn — 2026-11-07

Working tree chưa commit, Unity **6000.3.24f1**, protocol **21**. Hai bão phủ toàn map trong 12 giây sau cảnh báo 3 giây. Cát giảm 50% tốc độ bắn, tuyết giữ giảm 50% di chuyển; không còn dải quét. Kết quả v19/v20 ở trên là lịch sử của những phiên bản khác.

| Lệnh/phạm vi thực chạy | Kết quả |
|---|---|
| `python scripts/sync-contracts.py` và `python scripts/sync-contracts.py --check` | Sinh từ JSON; check đạt, bỏ luật độ rộng dải và thêm `SandstormFireRatePercent=50`. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-server.ps1` | **275/275 test**, 36 suites, không fail/error/skip; đóng gói JAR mới. |
| Maven `-f source/server/pom.xml -Dtest=WeatherTest,NetworkRejoinTest,ProtocolTest,ItemCodecTest test` | **27/27** sau bổ sung assertion cuối về nhịp bắn/rejoin/đạn; production JAR đã chứa thay đổi cuối. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-client.ps1` | **340/340**, không fail/skip, gồm 14 WeatherTests; kiểm full-map/biên thời gian, Fog, prediction/reconciliation và fixture Java. Lượt đầu chỉ fail assertion ItemTests còn ghi version20; đã sửa thành21, thêm từ chối20 và chạy lại toàn suite. |
| Java/C# fixture | **640 ca movement**, **1.728 ca phạm vi map**, **26 FULL/DELTA/drop/rejoin records**. Tuyết giảm tốc ở mọi vị trí trong map; bắt đầu/kết thúc đúng tick. |
| Unity batch `-executeMethod BuildClient.BuildUiRevision` | Exit0, giữ scene/settings. Build vào `build/client-ui-revision`; khi hai client cũ đã đóng, copy nguyên bộ vào `build/client`. Hai runtime DLL khớp hash, không dừng game của người dùng. |
| Sáu trận × hai client: ba map × Thường/Nhập vai, 120 giây/trận | **12/12 client exit0**, success=true, decoded=3600, observedTick=3600, invalid=0, historyRows=1, endReason=0, mapMatched=true. Types/warnings/impacts đúng mask **12/20/40** theo map2/3/4, chat hợp lệ và có Q trong RP. |
| Nhịp bắn trong trận thật qua ClientCore | Hai wave cát × hai slot: **24 khoảng cách từ phát trước đều60tick**, gồm các phát ngay trước lúc bão bắt đầu; 20 khoảng giữa hai phát cùng nằm trong bão cũng đều60. Ví dụ `304→334→364` là30tick; `364→424` và `424→484→…→724` là60tick; hết bão tại750 thì `724→754→784` lại30tick. Không giữ chậm bắn sau bão hoặc bắn bù. |
| SQLite/đóng server QA | Sáu match đúng map ID/SHA-256 byte gốc, NORMAL/RP, duration_ticks=3600, reason=0. Loopback **32438/32439**, admin tắt, DB/spool riêng. Stop file đã được xử lý, cả13 PID QA không còn, TCP/UDP được giải phóng. ExitCode server qua PowerShell không lấy được (`null`), không báo thành0. |
| 17 PNG từ build mới | Đủ **960×600, 1280×800, 1600×900, 1920×1080** cho cả hai bão; thêm cảnh báo/cuối bão/hết bão, RP thấp và Fog. Đã xem tám ảnh bắt buộc cùng trạng thái phụ; xe bốn hướng, đạn, HP và chữ còn rõ. Pixel ở cả bốn góc map đều đổi so với khi hết bão, không chỉ một dải. Kích thước đúng, không đen, log không exception/shader error. |
| Âm thanh Windows/diff | Test native `opened=7, prepared=60, closed=7, matchStops=10`; mute/Stop/Dispose thả buffer. Hash bốn map, Main.unity, ProjectSettings, packages và baseline combat giữ nguyên. Loại đúng21 block Android Unity tự thêm sau khi phần còn lại khớp HEAD; giữ importer/GUID. `git diff --check` đạt; index rỗng. |

Wrapper smoke ban đầu dừng ở assertion đòi ExitCode server bằng0 sau `WaitForExit`; handle trả về `null`. Không chạy lại trận hoặc sửa kết quả thành0: giữ `serverExitCode=null`, ghi riêng xác nhận dừng bằng PID/cổng/stop file. Verifier độc lập sau đó exit0, xác nhận đủ12 JSON, sáu hàng DB và cadence từ telemetry. Đây là giới hạn lấy exit code của harness, không phải bằng chứng server exit0.

Lệnh mạng: `-batchmode -nographics --smoke --host 127.0.0.1 --tcp 32438 --udp 32439 --map <2|3|4> --players 2 --seconds 120 --expected-ticks 3600 --battlefield --chat`; host thêm `--create`, RP thêm `--roleplay` trên cả hai. JAR SHA-256 **0f73d53374b86a1becad91bd6fe6a4c292d41933f7129a3799a2b3befe091daa**; `Tank.Runtime.dll` **8c605523c2b8052879cc9af0a3b7f6cf11a522baa66aae3a6b06fb4a76ebe577**. Bản smoke, ảnh và bản giao cùng runtime, không sửa source sau build.

Ảnh dùng `--arena-preview --preview-eight --preview-world-items --preview-visibility --preview-map <3|4> --preview-battlefield <sandstorm|snowstorm> --preview-battlefield-tick <20|270|390|480> --capture <png> --capture-delay 0` và kích thước tương ứng; RP thấp thêm `--preview-roleplay --preview-chat`, Fog thêm `--preview-roleplay --preview-fog`. Giữ17 PNG gốc tại [images/weather-global](images/weather-global/sand-mid-1280.png): [cát](images/weather-global/sand-mid-1280.png), [tuyết](images/weather-global/snow-mid-1280.png), [cảnh báo](images/weather-global/sand-warning.png), [RP960×600](images/weather-global/snow-roleplay-low.png), [Fog](images/weather-global/sand-fog.png).

**Chưa kiểm chứng:** chơi trực tiếp bằng chuột/phím, giữ phím qua chat/modal, focus/fullscreen/ngắm, nghe chất lượng âm thanh bằng tai, FPS/GPU, LAN/VPN và cân bằng gameplay. Smoke có input qua ClientCore và đo tick/shot của trận server; ảnh preview cố định không thay thế kiểm tương tác hoặc benchmark. Không đổi IP, database schema, deploy cloud, stage hoặc commit. Đã dọn thư mục helper/log/DB thử của tác vụ sau khi giữ kết quả ở đây và17 ảnh trong docs; giữ fixture, báo cáo Unity chuẩn và build.
