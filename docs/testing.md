# Kiểm thử và bằng chứng xác nhận

Đối chiếu mã nguồn/script ngày **2026-10-23**, working tree dựa trên HEAD `e788610bdb0e2cd9fbd3f0cce54d9d29e815947e` và có thay đổi chưa commit. **Không chạy suite, build ứng dụng hoặc manual gameplay/UI trong đợt cập nhật tài liệu này.** Các test được nêu là test hiện có, không phải kết quả pass mới.

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

Nguồn kiến trúc: [components](architecture.md#components), [gameplay](gameplay.md#movement), [shared contracts](protocol.md#shared-contracts), [schema](data.md#schema). Không có formatter/linter được cấu hình; giữ quy ước mã hiện có và xem diff cuối để tránh thay đổi ngoài phạm vi.

<a id="prerequisites"></a>
## Điều kiện và thứ tự chạy

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
| Hợp đồng packet | [GoldenInteropTest.everyOpcodeHasCurrentGoldenPayload](../source/server/src/test/java/tank/GoldenInteropTest.java) sinh `golden.tsv`; [ProtocolTests.PreviousProtocolIsRejectedOnBothTransports, UnsignedAnglesAndFraming](../source/client/Assets/Tests/Editor/ProtocolTests.cs) kiểm tra version/framing phía client. |
| Di chuyển và prediction | [PredictionTest.sharedMovementTrace](../source/server/src/test/java/tank/PredictionTest.java) sinh `movement-trace.tsv`; [PlayerRecoveryTests.InputCatchesUpAfterFrameStallAndKeepsUpAtLowFrameRate, ExtrapolationIsBoundedDuringLostSnapshots](../source/client/Assets/Tests/Editor/PlayerRecoveryTests.cs). |
| Full/delta và atomicity | [DeltaTest.roundTripAndInterop](../source/server/src/test/java/tank/DeltaTest.java) sinh `delta-trace.tsv`; [ProtocolTests.JavaTraceReconstructs, DeltaIsAtomic](../source/client/Assets/Tests/Editor/ProtocolTests.cs). |
| Nhập vai/loadout | [RoleplayRoomTest.nationSelectionIsUniqueAtomicAndLockedWhileReady, loadingPublishesNationOnlyLoadoutAndAckGatesCountdown](../source/server/src/test/java/tank/RoleplayRoomTest.java), cùng [RoleplayClientTests](../source/client/Assets/Tests/Editor/RoleplayClientTests.cs) và [SkillContractTests](../source/client/Assets/Tests/Editor/SkillContractTests.cs). |
| Item, combat và VFX | [ItemCodecTest](../source/server/src/test/java/tank/ItemCodecTest.java), [CombatEventsTest.everyKindAndCauseRoundTripsAcrossFullAndDelta](../source/server/src/test/java/tank/CombatEventsTest.java), [CombatTests.RejoinWatermarkAndHiddenEffectsNeverReplay](../source/client/Assets/Tests/Editor/CombatTests.cs), [ItemTests](../source/client/Assets/Tests/Editor/ItemTests.cs). |
| Battlefield | [BattlefieldTest.fullDeltaLostPacketsAndRejoinRoundTripAndProduceUnityFixture, fiveTargetBombingRunsProduceFullDeltaAndReconnectInteropFixture](../source/server/src/test/java/tank/BattlefieldTest.java); [BattlefieldTests.JavaFullDeltaDroppedUpdateAndRejoinPreserveEveryByte, RenamedEventsAndTheirNoticesExpireWithTheLastEffectEvenWithAnOldSnapshot](../source/client/Assets/Tests/Editor/BattlefieldTests.cs). |
| Mất kết nối/vào lại | [NetworkRejoinTest.voluntaryDisconnectAndUdpTimeoutCanRejoinWithOriginalScoreAndLoadout](../source/server/src/test/java/tank/NetworkRejoinTest.java); [RejoinProtocolTests.UdpHandshakeTimeoutReturnsToLobbyWithoutLoggingOutAndCanRetry, UdpSocketFailureDoesNotCloseHealthyTcpLogin](../source/client/Assets/Tests/Editor/RejoinProtocolTests.cs). |
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

Để thử Nhập vai, thêm `--roleplay` vào **cả hai** lệnh client. Có thể thêm `--battlefield` vào cả hai để yêu cầu mỗi client đã nhận cảnh báo và tác động của **cả Red Zone lẫn Bombstorm**. Thời lượng trận tự nó không bảo đảm đã bao phủ đủ hai loại; thiếu quan sát này sẽ làm điều kiện smoke thất bại ngay cả khi trận kết thúc.

Đối chiếu [SmokePlayer.Awake, Update, Save](../source/client/Assets/Scripts/SmokePlayer.cs) và [SmokeAcceptance.Passed](../source/client/Assets/Scripts/SmokeAcceptance.cs). Kiểm tra `success` cùng các trường như `invalid`, `decoded`, `observedTick`, `expectedTicks`, lịch sử, số người và cấu hình trong **cả hai** file. `SmokeResult` là **JSON xác nhận acceptance**, không phải benchmark CSV: nó không chứa mẫu RTT, jitter, thời gian recovery, băng thông hoặc chuỗi mẫu thô của các chỉ số này. Muốn báo cáo đo mạng, dùng nguồn và quy trình tại [evaluation/metrics](evaluation.md#metrics), không suy ra số đo từ `success=true`.

<a id="manual"></a>
## Kiểm tra thủ công gameplay, UI và mạng

Chuẩn bị [demo cục bộ](deployment.md#local-demo) hoặc server LAN/VPN được phép, ít nhất hai client với tài khoản khác nhau và cùng đợt build. Kiểm tra các mục liên quan tới thay đổi; với thay đổi xuyên client/server, đi hết luồng từ tạo phòng tới kết quả.

| Nhóm | Thao tác và điều cần quan sát |
|---|---|
| Tài khoản/cấu hình | Đăng ký, đăng nhập sai/đúng, thông báo lỗi, lưu host/cổng, mở lại client để kiểm tra lựa chọn kết nối; phân biệt file kết nối local với database trên server. |
| Lobby/phòng | Tạo và tham gia phòng, mật khẩu sai/đúng, số người/capacity, quyền chủ phòng, kick/đổi chủ khi liên quan, ready rồi bắt đầu; thay cài đặt phải phản ánh cho mọi client. |
| Thường/Nhập vai | Thử hai chế độ; quốc gia Nhập vai phải duy nhất, đồng bộ loadout và countdown; Q thành công, hồi chiêu, chết/hồi sinh và vào lại theo [luật kỹ năng](gameplay.md#skills). Hộp Đen trên kết quả là phần thưởng kể chuyện, không tự suy ra có inventory/persistence. |
| Di chuyển/ngắm | WASD/phím mũi tên, ngắm theo chuột, Shift, chạm góc/tường/gạch/nước, respawn; thử khi resize/fullscreen và sau frame stall khi đó là phạm vi thay đổi. |
| Bắn/combat/item | Click trái/Space, bật/tắt tự bắn bằng chuột phải trong arena, sát thương và thanh máu, phản xạ/phá gạch, pickup/buff, âm thanh và hiệu ứng không bị nhân đôi hoặc phát lại sau reconnect; đối chiếu [combat](gameplay.md#combat) và [item](gameplay.md#items). |
| Focus/hộp thoại | Alt-tab, chuyển sang cửa sổ client khác, mở/đóng hộp thoại; xe không tiếp tục nhận input ngoài cửa sổ đang focus, tự bắn reset, chuột trên sidebar không làm sai ngắm. |
| Layout | Kiểm tra ít nhất 960×600 và 1600×900, F11/Alt+Enter/nút fullscreen, khôi phục kích thước; map giữ tỷ lệ, sidebar sát phải, text/field không chồng lấn và không bị cắt. Preview ở [client](client.md#previews). |
| Battlefield/tầm nhìn | Quan sát cảnh báo, tác động, đổi lượt và thời điểm kết thúc của cả Red Zone/Bombstorm, kể cả snapshot chậm; tên/đồng hồ trên HUD hết cùng hiệu ứng, không có chữ trong vùng map, VFX chịu sương mù và reconnect không phát lại tác động cũ. Luật số liệu ở [battlefield](gameplay.md#battlefield). |
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
