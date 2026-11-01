# Kiểm tra giao diện

## Con trỏ form đăng nhập/đăng ký — 31/10/2026

Đối chiếu `TankMenus.Field` với `MenuTheme`: form vừa bật con trỏ Unity màu Ink, vừa vẽ thêm vạch thủ công, gây hiện hai con trỏ. Theo yêu cầu cuối, đã bỏ vạch tự vẽ và phần ẩn con trỏ Unity của phương án trước; dùng con trỏ mặc định trong TextField/PasswordField, giữ cấu hình màu/nhịp nháy của MenuTheme. Các form dùng chung Field (đổi tên/phòng) nhận cùng bản sửa; khung chat giữ nguyên. Chỉ soát source/diff; chưa chạy Unity, tests hay build theo yêu cầu không tự build. Cần kiểm tra trực tiếp tài khoản, mật khẩu, xác nhận mật khẩu, chọn văn bản, cuộn ngang và chuyển focus.

## Chat trong trận — 31/10/2026

**Chỉnh style theo phản hồi sau kiểm thử:** bỏ dấu cộng/trừ khỏi tiêu đề, căn CHAT góc trên trái; tiêu đề/Gửi dùng nền và viền giống ô nhập ở mọi trạng thái. Bấm Chat để đóng/mở, kể cả tab ở cửa sổ thấp. Đợt chỉnh này chỉ soát source/diff; chưa chạy lại Unity hoặc kiểm hình ảnh, không build theo yêu cầu người dùng. Bằng chứng test/build/ảnh phía dưới thuộc phiên bản trước đợt chỉnh style này.

Người dùng chạy `scripts/build-client.cmd` sau đợt chỉnh style: build Player thất bại với CS1061 ở `MatchChatHud.cs` vì `GUIStyleState.scaledBackgrounds` chỉ có trong Editor. Đã bỏ truy cập thuộc tính này, giữ `background` dùng chung cho các trạng thái nút; người dùng xác nhận bản sửa đã ổn. Không có lượt Unity tests mới từ agent sau bản sửa; lỗi được xác định từ `runtime/unity-build.log`.

**Con trỏ ô nhập chat:** bổ sung vạch kem sáng 2×18 px, nhịp hiện 0,6 giây/chu kỳ 1 giây như ô đổi tên trong `TankMenus.Field`. Dùng `TextEditor.cursorIndex`, `scrollOffset` và style của ô nhập để định vị; chỉ vẽ khi ô có focus, UI cho phép nhập, không chọn văn bản và đang Repaint. Chỉ soát source/diff trong lượt này, chưa chạy Unity tests, build hoặc kiểm tra trực tiếp. Cần kiểm tra ô rỗng, click/di chuyển con trỏ giữa chữ, tin dài cuộn ngang, chọn văn bản và mất focus.

Áp dụng `game-studio:game-ui-frontend` cho sidebar IMGUI hiện có: chat có thể thu gọn, tên theo màu xe, xuống dòng theo từ, tab chung vùng xếp hạng khi cửa sổ thấp. Không đổi vùng bản đồ, dependencies, Unity version, scene hoặc PlayerSettings. Dữ liệu và bản chụp thử riêng tại `runtime/chat-qa/run-c96a1553/`, ngoài Git.

| Kiểm tra | Kết quả và bằng chứng |
| --- | --- |
| Contract/server | `python scripts/sync-contracts.py --check` đạt; `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-server.ps1` build thành công, **243/243 Java tests**, gồm `MatchChatTest` (4 test). Log `runtime/chat-server-build.log`, fixture Java được sinh lại ở `source/server/target/interop/`. Lượt đầu còn kỳ vọng v16 trong `ItemCodecTest`; đã cập nhật sang v17 và chạy lại toàn bộ. |
| Client | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-client.ps1`: **178/178 EditMode tests**, gồm 5 test chat. Sau chỉnh xuống dòng đã chạy lại cùng suite và build `BuildClient.BuildCurrentScene`, exit 0, output `build/client/`. Log `runtime/unity-tests.log`, XML và `runtime/chat-client-build.log`. Các lần thử trước bị chặn bởi sandbox IPC rồi license; người dùng đã kích hoạt license trước lần chạy đạt. |
| Layout | **6 PNG**, cả NORMAL/RP × 960×600 / 1280×800 / 1920×1080; đã xác minh kích thước, nội dung không đen và hash trong `verified-captures.json`. Đã mở cả sáu ảnh kiểm tra; không che bản đồ, skill/Leave còn đủ chỗ. Ảnh đầu trong desktop sandbox có kích thước sai do cửa sổ bị maximize; đã chụp đè ngoài sandbox sau khi restore. Hai ảnh fullscreen ẩn bị đen đã chụp lại ở foreground và xác minh lại. Log ảnh cuối không có exception/IMGUI error. |
| Hai client qua mạng thật | Hai trận độc lập 60 giây, NORMAL và ROLEPLAY, mỗi trận hai Unity player `--smoke --chat`, server local **52861/54255**. Cả bốn JSON `normal-host/peer`, `roleplay-host/peer` có `success=true`, tick 1800, `invalid=0`, `chatSent=true`, `chatColorsValid=true`, `chatSlots=3`. RP dùng nation offset 3 để màu khác slot. Đây là smoke tự động qua ClientCore. |
| Thao tác trực tiếp | Computer Use: mở tab chat tại 960×600, nhập tiếng Việt/markup, Esc hủy và trả focus. Trận RP thật với một client UI và hai peer tự động: tự bắn trước nhập là true/input 16; trong nhập là false/input 0, xe giữ tọa độ, Q không tăng skillPress. Enter gửi tin với tên màu cam (palette 5); sau AFK → Vào lại trận, nhận đủ ba tin, tên/nội dung/palette giữ nguyên, invalid 0. Quan sát lưu trong `live-before-typing.json`, `live-during-typing.json`, `live-q-during-typing.json`, `live-after-rejoin.json`. |

Luồng ba client dùng để kiểm thủ công không được tính là smoke tổng hợp đạt: chủ phòng chủ động đóng và nội dung gõ tay khác marker cố định của `--chat`, nên hai `live-peer*.json` báo success/chatColorsValid false dù nhận đủ ba slot và invalid=0. `chatColorsValid` của harness kiểm cả palette lẫn nội dung mẫu; bằng chứng màu/gửi/rejoin trong lượt này là quan sát UI/JSON riêng. Bốn smoke hai người phía trên là các lượt đồng nhất đã đạt.

Hash Main.unity, ProjectSettings và EditorBuildSettings trước/sau build giống nhau. Unity tự thêm block Android vào 21 `.meta` CampaignArt; chỉ các block phát sinh này được loại sau khi so sánh từng file với HEAD, giữ toàn bộ GUID/importer cũ. Không stage/commit/push hoặc cập nhật Launcher/server remote.

Chưa kiểm trực tiếp: IME composition riêng (đã nhập tiếng Việt bằng công cụ), Enter đã được gán cho chiến đấu, mọi tổ hợp modal/mất focus/item và LAN/VPN. Không coi ảnh offline là bằng chứng các tương tác này đã đạt. Hướng dẫn demo trong deployment còn mô tả ô host/cổng đã bỏ khỏi client; lượt này dùng endpoint riêng của `--smoke`/`--ui-test`, không đổi endpoint production.

## Đợt UI — 25/10/2026

Báo cáo cho lần chỉnh UI theo yêu cầu bổ sung sau [plan_UI](../plan/plan_UI.md): font, nút bo góc viền tối, màu và background của menu/phòng/HUD. Working tree dựa trên HEAD `e788610bdb0e2cd9fbd3f0cce54d9d29e815947e`, có sẵn thay đổi ngoài UI. Đợt này không stage/commit/push, không sửa server, contract, dependency, scene hoặc PlayerSettings. Build, log, ảnh và database thử nằm ngoài Git tại `runtime/ui-revision/` và `build/client/`.

## Thay đổi đã kiểm tra

- Font Bungee Regular cho tiêu đề/nút và Nunito Bold cho nội dung/nhập liệu; có tiếng Việt, giữ OFL và notice. Đây là font miễn phí gần phong cách tham khảo Goose Goose Duck.
- `CartoonUi` dùng chung palette navy/slate, chữ kem, primary vàng, ready mint, danger san hô. Nút bo góc 14 px, viền tối 3 px, cạnh dưới 5 px; có hover/active/focus/selected. Texture và style được cache.
- Đăng nhập/sảnh/tạo phòng có ảnh xe tăng; phòng chờ/lịch sử/kết quả dùng nền navy nhẹ để dễ đọc. Panel slate tách màu nền.
- Form phòng chia hai cột khi đủ rộng, ghim Xác nhận/Hủy. Phòng Nhập vai đặt chọn phe ở vùng chính, phần mô tả cuộn riêng; ready/start/cài đặt/rời phòng luôn có chỗ. Viewport dưới 720×600 có cuộn ngoài.
- Header, HUD, đồng hồ, bảng hạng, Q, AFK/hồi sinh và banner battlefield dùng cùng hệ màu/khung/font. Q dùng Nunito và gộp hiệu lực/hồi chiêu trên cùng dòng để không cắt chữ. Giữ map, màu xe, hiệu ứng và tọa độ ngắm; giữ handler và quyền thao tác phòng. Chi tiết tại [client](client.md#ui).

## Lệnh và kết quả

Chạy từ PowerShell ở gốc repo, Unity **6000.3.24f1**, Windows Build Support, license đã kích hoạt; Editor đóng trong lúc test/build. Dùng lại fixture Java còn hợp lệ tại `source/server/target/interop/` cho thay đổi UI độc lập. Server thử dùng JDK **21.0.12.1** và JAR có sẵn từ 23/10; không phải bản server build mới.

| Kiểm tra | Lệnh/điểm vào | Kết quả và bằng chứng local |
|---|---|---|
| Contract | `python scripts/sync-contracts.py --check` | `Gameplay contracts match.` |
| Unity EditMode | `powershell -ExecutionPolicy Bypass -File scripts/test-client.ps1` | **138 passed, 0 failed, 0 skipped**; `runtime/unity-tests.xml`, `runtime/unity-tests.log`. Bao gồm tiếng Việt/giấy phép font, layout hẹp, asset/importer và các regression input/aim/protocol hiện có. |
| Build Windows | Unity `-batchmode -nographics -quit -projectPath source/client -executeMethod BuildClient.BuildCurrentScene` | Exit **0**, `runtime/ui-revision/build.log`; `build/client/TankBattle.exe`. Dùng scene hiện có, không ghi lại scene/settings. License Chequered Ink, Cooked FX và hai OFL/notice font có trong `build/client/ThirdParty/`. |
| Menu tiêu chuẩn | `TankBattle.exe --menu-preview <page> -screen-width <w> -screen-height <h> -screen-fullscreen 0 --capture <png> --capture-delay 0.5` | **42 PNG**: 14 màn × 960×600, 1280×800, 1600×900 tại `runtime/ui-revision/previews/`. Kiểm tra kích thước PNG và log, không exception/IMGUI error. |
| HUD/nội dung khó | `--arena-preview` và các cờ [preview](client.md#previews) | **14 PNG** tại `runtime/ui-revision/extras/`: HUD Thường/Nhập vai, 8 người, AFK, 10 giây cuối, battlefield/fog; tên dài, cuối vùng cuộn, focus profile/mật khẩu, lỗi form và cửa sổ 640×400. Đã mở ảnh kiểm tra chữ/nút, tương phản và vùng cuộn. |
| Resize/fullscreen menu | `TankBattle.exe --menu-preview roleplay-room --window-qa runtime/ui-revision/window-menu` | `window-qa.json`: **passed=true**, 960×600 → 1600×900 → fullscreen 1920×1080 → khôi phục 1600×900; có PNG từng bước. |
| Resize/fullscreen HUD | `TankBattle.exe --arena-preview --preview-eight --preview-roleplay --window-qa runtime/ui-revision/window-arena` | `window-qa.json`: **passed=true**, cùng bốn bước; đã xem ảnh fullscreen. Test tọa độ ngắm sau resize pass trong EditMode. |
| Hai client có UI kết nối thật | Host `--ui-test --ui-auto --roleplay --players 2 --seconds 60 --hp 100 --room ui-style-review --tcp 25888 --udp 25889`; peer thêm `--ui-peer`; mỗi client có `--ui-output`, `--capture` riêng | Cả hai exit **0**. Host chụp ở `PLAYING`, 2 người, loadout=true, `decoded=297`, `invalid=0`; peer chụp ở `RESULTS`, `decoded=329`, `invalid=0` sau khi host thoát. JSON/PNG/log tại `runtime/ui-revision/live/`. Đây là kiểm tra vào trận/hiển thị và chuyển kết quả, không phải smoke đủ 60 giây. |

14 màn menu: `login`, `register`, `settings`, `lobby`, `create`, `room`, `roleplay-room`, `room-settings`, `history`, `history-detail`, `results`, `profile`, `join-password`, `afk-lobby`. Ảnh tham khảo chỉ dùng đối chiếu, không đưa vào game và không lưu trong repository.

Server thử bind loopback, dùng `runtime/ui-revision/server.properties`, database/spool/log và tài khoản thử riêng. Server/client do harness khởi chạy đã dừng sau kiểm tra. Không ghi cấu hình kết nối người dùng.

## Giới hạn

Preview/focus là fixture offline; harness live thao tác qua `ClientCore`, không bấm nút menu. Chưa kiểm chứng đầy đủ chuột/Tab/gõ tiếng Việt, caret/chọn chữ, hover/press, cuộn chuột, focus modal, mật khẩu sai/đúng và chọn phe/ready/start bằng thao tác trực tiếp. Resize/fullscreen dùng harness, không xác nhận phím F11 thực tế. Hồi sinh đã đổi style trong source nhưng chưa chụp trạng thái hồi sinh. Không chạy lại LAN/VPN, smoke đủ thời lượng hoặc suite Java vì đợt này không đổi gameplay/wire/server.

Ảnh chính: `runtime/ui-revision/previews/create-1280.png`, `roleplay-room-1280.png`, `runtime/ui-revision/extras/arena-roleplay-1280.png`. Bằng chứng phiên UI trước ở `runtime/menu-ui/` là phiên cũ, không đại diện cho màu/font/HUD hiện tại.

## Đợt đồng bộ Multiplayer và Chiến dịch — 25–26/10/2026

Đợt này thực hiện yêu cầu thiết kế lại toàn bộ UI theo Cartoon Military Arcade, tiếp nối phần style Multiplayer ở trên. Các kết quả ở phần trước thuộc bản UI trước; bằng chứng của đợt đồng bộ nằm riêng tại `runtime/ui-unified/`, bản chạy tại `build/client-ui/TankBattle.exe`. Hai client và server người dùng đang chạy ở cổng 8888/8889 được giữ nguyên.

### Thiết kế và phạm vi

- `CartoonUi` là nguồn palette/font/surface chung: navy `#19273B`, vàng `#FFD65E`, mint `#69D2AC`, coral `#EA7C6B`, cream/sand. Bổ sung icon hình học cùng nét, badge, thanh tiến trình, portrait thân/nòng xe, huy chương và tooltip cho nội dung dài. Texture/style được cache; không thêm dependency hoặc tài nguyên ngoài.
- `CampaignUI` chuyển thành adapter ngữ nghĩa của bộ style chung. Giữ Sprout/các asset và giấy phép gốc trong project; bản đồ, tile, hình xe chiến đấu và importer không đổi.
- Sảnh dùng thẻ phòng có số chỗ, khóa, chế độ và trạng thái chờ/đang đấu/đầy/vào lại. Lịch sử có thẻ thành tích, kết quả có bục Top 1–3, tóm tắt người chơi và bảng điểm cuộn riêng. Chiều cao panel theo nội dung để giảm khoảng trống.
- Phòng Nhập vai desktop tách đội hình, thẻ phe và thông tin/kỹ năng. Đủ 8 người và 8 phe ở 1366×768. Thẻ thể hiện xe, chủ phòng, trạng thái ready; tên dài có rút gọn và tooltip. Phe đã lấy/đang khóa có trạng thái riêng; thẻ vẫn hiển thị khi đang chuẩn bị. Giữ nguyên điều kiện chọn phe, ready, start và kick. Cốt truyện Hộp Đen nằm trong vùng mô tả cuộn.
- Form/profile/mật khẩu có phần nội dung cuộn và footer cố định. Đăng nhập/đăng ký/kết nối tiếp tục dùng logo/background hiện có; loading, lỗi và lựa chọn dùng cùng style.
- HUD Multiplayer có HP/năng lượng của xe hiện tại, đồng hồ, vật phẩm, bảng hạng tô sáng người chơi, Q và trạng thái. Layout nhỏ 960×600 có phần tổng quan gọn để bảng hạng không chồng nút Q. AFK/hồi sinh dùng thông báo hẹp hơn; rời trận có xác nhận và khóa điều khiển nền trong lúc mở modal. Polling/input tick/network tiếp tục chạy như trước.
- Chọn xe Chiến dịch có ba thẻ 416 px, portrait lớn, icon/chỉ số, mô tả kỹ năng và hồ sơ nhiệm vụ màu sand. Ở 1366×768, thẻ/nhiệm vụ/nút Bắt đầu vừa màn; màn hẹp cuộn phần nội dung, giữ hành động ở chân.
- HUD Chiến dịch giữ vùng map trên/dưới 100/96 px như trước. Hotbar I/O/P có icon, phím, trạng thái sẵn sàng/hồi theo **lượt**, tiến trình và tooltip. Kỹ năng chưa dùng được có chữ sáng trên nền tối. Shop, BXH, chi tiết thành tích, chiến thắng/thất bại, lưu lại kết quả và xác nhận Kết thúc dùng chung thiết kế. Notice thường màu sand, thành công mint, lỗi coral.

Không sửa server, protocol, JSON nguồn/generated contract, map, gameplay, dependency, Unity version hoặc cấu hình project. `BuildClient.BuildUiReview` build scene Main hiện có sang thư mục riêng; không tạo lại scene hoặc gán PlayerSettings. Không stage/commit/push.

### Cách tái hiện kiểm tra

Từ PowerShell tại repo root, dùng Unity 6000.3.24f1 và đóng Editor của project:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-client.ps1
```

Build bằng Unity `-batchmode -nographics -quit -projectPath source/client -executeMethod BuildClient.BuildUiReview -logFile <log>`. Preview dùng bản `build/client-ui/TankBattle.exe` với `--menu-preview <page>` hoặc `--arena-preview`, cờ `-screen-width/-screen-height/-screen-fullscreen` và `--capture <png> --capture-delay 1.5`.

Các page/cờ bổ sung được mô tả tại [client/previews](client.md#previews). Script matrix/extras là công cụ local trong `runtime/ui-unified/`, không nằm trong Git. Mỗi ảnh được kiểm tra kích thước PNG, exit code, log exception/IMGUI và nội dung không đen trước khi mở ảnh để đánh giá. 1366×768 và 1600×900 dùng cửa sổ; 1920×1080 dùng cửa sổ fullscreen hiển thị. Ảnh chụp ẩn ở 1920 bị đen đã bị loại, không tính là bằng chứng đạt. Sandbox cũng từng ngăn Unity dùng Package Manager/cache và điều chỉnh kích thước cửa sổ; các lượt thành công chạy ngoài sandbox, không đổi cấu hình bảo mật hoặc packages.

### Bằng chứng tự động và trực quan — 26/10/2026

| Kiểm tra | Kết quả thực chạy | Bằng chứng local |
|---|---|---|
| Unity EditMode | **140 passed, 0 failed, 0 skipped**, gồm layout 1366/1920, tiếng Việt/font/license và regression gameplay/input/aim/protocol hiện có. Lượt sửa cuối chỉ giới hạn câu hướng dẫn đổi phe ở WAITING; build tăng dần sau đó thành công. | `runtime/ui-unified/unity-tests.xml`, `unity-tests.log`. |
| Windows client | `BuildClient.BuildUiReview`, exit **0**; bản chạy riêng chứa assembly mới. Hash scene Main, PlayerSettings và EditorBuildSettings trước/sau giống nhau. Block Android do Unity tự thêm vào metadata CampaignArt đã được loại chính xác sau khi đối chiếu với index; GUID, PNG, importer và các thiết lập có sẵn được giữ nguyên. | `runtime/ui-unified/build.log`, `build/client-ui/TankBattle.exe`, hash assembly trong `verified-captures.json`. |
| Màn chính | **57 PNG**, 19 màn × 1366×768 / 1600×900 / 1920×1080; kích thước đúng, không đen, mọi process exit 0, không exception/IMGUI error. Đã mở ảnh menu và Chiến dịch để kiểm tra bố cục/chữ/nút. | `runtime/ui-unified/previews/`, `capture-results.json`. |
| Trạng thái khó và HUD | **39 PNG**: HUD Thường/Nhập vai ở ba kích thước, AFK, hồi sinh, xác nhận rời, 10 giây cuối, battlefield/fog, 960×600, tên dài, phòng/lịch sử trống, chốt phe/ready/guest, loading/error, shop không mua được, thắng/thua/chưa lưu và xác nhận kết thúc. Retake các màn bị ảnh hưởng sau sửa contrast/hint; đã mở kiểm tra các trường hợp này. | `runtime/ui-unified/extras/`, `extras-results.json`, `verified-captures.json`. |
| Hai client tích hợp Thường | Server thử riêng TCP **29498**, UDP **29499**; hai client `-batchmode -nographics --smoke`, 2 người, 60 giây, 100 HP, 1.800 tick. Cả hai exit **0**, `success=true`, `observedTick=1800`, `decoded=1800`, `invalid=0`, có lịch sử và kết quả 2 người. Đây là smoke qua ClientCore, không bấm UI. | `runtime/ui-unified/smoke/host.json`, `peer.json`, log client/server. |

19 màn chính: 14 màn menu ở đợt trước và `campaign`, `campaign-ranking`, `campaign-play`, `campaign-shop`, `campaign-results`. Retake đã xác nhận chữ cooldown Q trắng trên navy ở 960/1366/1600/1920, hotbar Chiến dịch disabled có chữ sáng, loading Busy và lỗi coral; không đổi kích thước/handler kỹ năng.

### Kiểm tra tương tác trực tiếp đang thực hiện

Computer Use chính và dự phòng đều không kết nối được native pipe, sau khi thử lại và khởi tạo lại runtime. Người dùng chọn tự thử theo `runtime/ui-unified/manual-checklist.md`. Hai cửa sổ `ui26host`/`ui26peer` đã đăng nhập vào server thử riêng **29488/29489**, có observation JSON/log trong `runtime/ui-unified/live/`. Cấu hình kết nối đã lưu của người dùng không đổi.

Người dùng đã báo tooltip đi theo chuột, viền/bóng quá đậm, chữ bị cắt ở nhiều ô, Ping/FPS quá dài và khối HP/năng lượng trong sidebar bị lặp. **Chưa coi kiểm tra thao tác trực tiếp là đạt**. Chưa kết luận đầy đủ về click/Tab/nhập tiếng Việt, mật khẩu, ready/start, modal, Q và chuyển fullscreen từ các ảnh offline hoặc smoke tự động. LAN/VPN và suite Java không chạy lại trong đợt UI độc lập này; JAR server dùng bản có sẵn cùng protocol/content, không phải bản server build mới.

### Chỉnh theo phản hồi trực tiếp — 26/10/2026

Phần thiết kế/bằng chứng phía trên ghi nhận phiên trước phản hồi, không xác nhận các sửa đổi dưới đây. Người dùng yêu cầu giữ giao diện chiến trường hiện tại và tự sửa phần đó sau; đợt chỉnh này không khôi phục hoặc thiết kế lại map/xe/hiệu ứng.

- Bỏ renderer tooltip toàn cục theo chuột. Tên dài hiện đầy đủ bằng xuống dòng; đo chiều cao theo font, cả tên không có dấu cách. Viền dùng `Outline #7A8899`, mỏng 1 px; panel/card/field phẳng, nút chỉ có bóng tiếp xúc rất nhẹ, không còn lớp đáy nổi.
- Bỏ khối HP/năng lượng trong sidebar Multiplayer; giữ thanh trên xe. Ping/FPS là dòng chữ nhỏ, không tạo panel. Bảng hạng đo cả tên và độ rộng số hạ/tử, cuộn riêng; kỹ năng/trạng thái dài có vùng cuộn khi cần.
- Rà soát thẻ người chơi/phe/phòng, form/modal, bục kết quả, lịch sử và bảng điểm. Chiều cao theo nội dung, có padding đáy; hành động nằm ngoài vùng cuộn.
- Rà soát thẻ xe/kỹ năng/nhiệm vụ Chiến dịch, BXH, thông báo, shop và kết quả. Đo nội dung thay chiều cao chữ cố định; giữ map/header/footer và các handler hiện có.

Bản chạy để kiểm tra vòng này dùng `BuildClient.BuildUiRevision`, output riêng `build/client-ui-revision/`; ảnh/log/JSON lưu tại `runtime/ui-feedback/`. Bổ sung fixture `--preview-two`, `--preview-wide-names` (24 ký tự M), `--preview-nation` cho Chiến dịch và cuộn cuối nội dung. Kiểm tra thêm 958×981 (kích thước observation của hai cửa sổ người dùng) và 960×600 bên cạnh ba độ phân giải mục tiêu. Kết quả chỉ được ghi sau khi chạy trên bản mới.

Vòng phản hồi này tạo **138 ảnh** (57 màn chính, 42 trường hợp cửa sổ hẹp, 39 trạng thái phụ). Đã kiểm tra kích thước, nội dung không đen, exit 0 và không lỗi IMGUI; mở ảnh để rà các thẻ, form, viewport và HUD. Phát hiện thêm thanh cuộn ngang trong form hẹp và sửa phần chừa chỗ cho scrollbar/margin; các form bị ảnh hưởng được chụp lại trên assembly `9B7E07…`. Bằng chứng: `results-main.json`, `results-narrow.json`, `results-states.json`. Đây là phiên trước hai chỉnh sửa kết thúc dưới đây, không phải toàn bộ ảnh của build cuối.

### Hai chỉnh sửa kết thúc

Người dùng yêu cầu căn ô tên người chơi với Đăng xuất và đổi nút sang viền đen có bóng, rồi kết thúc goal. `DrawHeader` dùng chung Y/chiều cao cho hai nút, căn giữa header 68/76 px; chiều cao được đo theo tên xuống dòng. Nút dùng viền đen 1 px, bóng đổ mềm giảm khi nhấn; panel/thẻ/ô nhập vẫn phẳng với viền nhạt. Không thêm lớp đáy nổi. `ArenaPreview` lấy nickname từ người chơi mẫu để kiểm tra tên ngắn và tên 24 ký tự M trong header thật.

| Kiểm tra bản cuối | Kết quả và bằng chứng local |
|---|---|
| Unity EditMode | **140 passed, 0 failed, 0 skipped**; `runtime/ui-feedback/unity-tests-finish.xml`, `unity-tests-finish.log`. |
| Windows build | `BuildClient.BuildUiRevision`, exit **0**; `build-finish.log`, bản `build/client-ui-revision/TankBattle.exe`, assembly `5FAA3CBC…`. |
| Kiểm tra trực quan tập trung | **11 PNG mới**, tất cả đúng kích thước, không đen, exit 0, không exception/IMGUI error. Đã mở đủ 11 ảnh: header tên ngắn/dài ở 960/1366/1600/1920, nút đăng nhập/phòng/form và Chiến dịch/HUD/shop. Hai ô tên/Đăng xuất cùng mép trên/dưới; tên dài đủ hai dòng; viền đen/bóng mềm hiển thị rõ. `finish/`, `results-finish.json`, `verified-finish.json`. |
| Giữ phạm vi | Hash Main.unity/PlayerSettings/EditorBuildSettings không đổi; khối vẽ world của ArenaRenderer giống index có sẵn. Các block Android do Unity sinh đã được loại sau đối chiếu chính xác, metadata còn lại không đổi. Không stage/commit/push. |

Computer Use vẫn không khả dụng trong phiên này; các ảnh cuối là preview offline, không tuyên bố có một vòng thao tác chuột/Tab/nhập liệu trực tiếp mới sau hai chỉnh sửa kết thúc. Gameplay/wire/server không đổi trong lượt UI; không chạy lại suite Java hoặc LAN/VPN. Kết thúc công việc theo yêu cầu người dùng sau khi build và kiểm tra hai mục cuối.
