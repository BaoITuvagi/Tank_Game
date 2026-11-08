# Client Unity, giao diện và tài nguyên

Kiểm chứng hiện hành: [business-validation.md](business-validation.md#matrix). Các tên test/harness cũ bên dưới là reference lịch sử; bộ đó không còn trong source được theo dõi.

Phần UI/asset, chọn map và bố cục sa mạc/băng tuyết multiplayer đối chiếu working tree ngày **2026-11-07**, dựa trên HEAD `08d6f756c6009b2c730e1babeb3cb138142e74fe` và thay đổi chưa commit. Menu, HUD và đồ họa chiến dịch được đồng bộ theo `game-ui-frontend`, tiếp nối các đợt UI trước; phạm vi và bằng chứng kiểm tra ở UI validation và kiểm chứng map. Các phần gameplay khác giữ mốc đối chiếu 2026-10-23.

Mục lục:

- [Mở project và bootstrap](#bootstrap)
- [Giao diện và render arena](#ui)
- [Tài nguyên, importer và giấy phép](#assets)
- [Khôi phục Combat FX](#restore-combat-fx)
- [Preview và chụp ảnh](#previews)

Hướng dẫn [build và kết nối](deployment.md#build-client), [điều khiển](gameplay.md#controls), [prediction và reconciliation](protocol.md#prediction), cùng kiểm thử có tài liệu riêng.

<a id="bootstrap"></a>
## Mở project và bootstrap

Dùng Unity **6000.3.24f1**, theo [ProjectVersion.txt](../source/client/ProjectSettings/ProjectVersion.txt). Trong Unity Hub, thêm thư mục `source/client`, mở project rồi mở `Assets/Scenes/Main.unity` và bấm Play. Các điều kiện cài Editor, license và Windows Build Support nằm trong [README](../README.md); hãy đóng Editor trước khi chạy script build/test cho cùng project.

[TankApp.Boot, Awake, Update, OnGUI](../source/client/Assets/Scripts/TankApp.cs) tạo đối tượng `Tank Battle` ở runtime sau khi scene tải, nếu chưa có `TankApp` và không bật smoke harness. Scene không cần chứa sẵn menu hoặc arena. Repository hiện không có prefab; giữ cách bootstrap này khi sửa client.

`TankApp.Awake` tạo thư mục dữ liệu riêng của ứng dụng, đặt chạy nền và mục tiêu 60 FPS, tạo `ClientCore` qua [MapLoader.CreateClient](../source/client/Assets/Scripts/MapLoader.cs), gắn sự kiện snapshot cho lớp hiệu ứng rồi đọc cấu hình kết nối. `MapLoader` nạp ba map và hash byte gốc qua [MapCatalog](../source/client/Assets/Scripts/MapCatalog.cs), bắt đầu với map mặc định ID 2, kiểm tra sprite cần cho Nhập vai, preload Combat FX và mask xe. Luồng vòng đời client/server nằm tại [kiến trúc khởi động](architecture.md#startup).

Client cố định server `13.75.2.41`, TCP `8888`, UDP `8889` bằng các hằng trong [TankApp](../source/client/Assets/Scripts/TankApp.cs). Đăng nhập và đăng ký luôn dùng địa chỉ này. Đã bỏ nút/màn hình Kết nối và file cấu hình StreamingAssets; `connection.json` từng lưu trên máy không còn được đọc. Muốn đổi endpoint phải sửa hằng và build lại client. Smoke/test harness vẫn có endpoint riêng để kiểm thử.

Các ô đăng nhập/đăng ký dùng `TankMenus.Field` chỉ dùng con trỏ mặc định của Unity, với màu Ink và nhịp nháy do `MenuTheme` cấu hình; đã bỏ vạch tự vẽ để tránh hiện hai con trỏ. Ô đổi tên và các form phòng dùng chung `Field` cũng dùng con trỏ mặc định.

### Chat trong trận Thường / Nhập vai

Sidebar luôn có hai tab **Xếp hạng / Chat** phía trên vùng thông tin, mặc định chọn Xếp hạng ở mọi kích thước. Hai tab thay nội dung trong cùng một vùng; Chat không còn là khung riêng chen giữa bảng hạng và kỹ năng. Tab đang chọn có điểm nhấn vàng, Chat hiện số tin chưa đọc; bấm lại tab Chat vẫn giữ Chat. Đổi tab hoặc resize không làm dịch chuyển cụm kỹ năng/túi vật phẩm và nút Rời trận ghim phía dưới. Thiết kế theo `game-studio:game-ui-frontend`, áp dụng bằng IMGUI với màu/font/khung cắt góc hiện có; vùng bản đồ/ngắm không đổi.

Bấm tab Chat rồi bấm ô nhập để gõ; ô có con trỏ kem sáng nhấp nháy tại vị trí chèn chữ, theo vị trí cuộn ngang và ẩn khi chọn văn bản hoặc mất focus. Enter/Enter bàn phím số gửi, Esc hủy; không có phím mở chat và không thay gán phím chiến đấu. Trong lúc nhập, khóa di chuyển/ngắm/bắn/kỹ năng/shop/item, tắt tự bắn; gửi/hủy hoặc đổi về Xếp hạng trả điều khiển sau khi nhả phím gửi hoặc chuột. Mở modal, bấm ra ngoài hoặc mất focus kết thúc nhập; trận và AFK tiếp tục. Không tự khôi phục bản nháp khi mất kết nối.

Tên dùng `TankPalette.Colors` theo màu xe (NORMAL: slot; ROLEPLAY: nation), nội dung màu kem và rich text tắt. Tên/nội dung dài được đo xuống dòng, danh sách cuộn; đang xem tin cũ thì tin mới không kéo xuống cuối, có nút **Tin mới ↓**. Giới hạn, lỗi và lịch sử 100 tin/rejoin ở [protocol/chat](protocol.md#match-chat).

Preview: `--arena-preview --preview-eight --preview-chat`; ghép `--preview-roleplay --preview-shop-items` để kiểm HUD RP. Tin mẫu gồm tiếng Việt, tám màu, tên dài và markup nguyên văn. Thử 960×600, 1280×800, 1600×900 và 1920×1080; kiểm cả hai tab. Mã: `MatchChatHud.cs`, `ClientCore.Chat.cs`, phần sidebar trong `ArenaRenderer.cs`.

## Cài đặt trên máy người chơi

Nút **Cài đặt** có ở màn đăng nhập, lobby, trận multiplayer và chiến dịch. Sau đăng nhập, nút **Đăng xuất** nằm trong Cài đặt, dưới các liên kết GitHub/Facebook; đăng xuất bỏ bản cài đặt đang sửa chưa lưu. Người chơi có thể gán hai phím bàn phím/nút chuột cho mỗi thao tác: di chuyển chung; bắn, tăng tốc, tự bắn, kỹ năng và shop multiplayer; bắn, kỹ năng và tiếp tế chiến dịch. Các ô gán phím vẫn cho sửa/chọn/dán/xóa văn bản; không có chế độ bắt toàn bộ bàn phím hay làm tối/khóa các ô khác. Nhập tên như `W`, `Space`, `Shift trái`, `Chuột trái`, `Mouse3`, `Enter` hoặc tên `KeyCode`, không phân biệt hoa/thường. Riêng bốn phím mũi tên có thể nhấn trực tiếp khi ô đang được chọn, không kèm Alt/Ctrl/Shift/Command. Ô và HUD hiển thị **↑ ↓ ← →**. `CartoonUi` giữ font Nunito/Bungee và thêm fallback Arial/Segoe UI Symbol lúc chạy để có đủ glyph; parser vẫn nhận nhãn tiếng Việt cũ và `UpArrow/DownArrow/LeftArrow/RightArrow`. Để trống một ô để bỏ gán. Mỗi thao tác trong cả ba nhóm đều phải còn ít nhất một phím chính hoặc phụ. Bấm **Lưu** mới kiểm tra toàn bộ: tên phím không hợp lệ, Esc/F11 dành cho giao diện, phím trùng trong cùng chế độ, thao tác thiếu phím, hoặc lỗi ghi file đều giữ Cài đặt mở và hiện lỗi cụ thể. Có thể đổi chỗ hai phím trong cùng lần sửa. Lưu thành công áp dụng và đóng Cài đặt; Mặc định điền lại thiết lập ban đầu, Hủy bỏ thay đổi, Esc đóng và bỏ thay đổi. Cấu hình cũ thiếu phím cho bất kỳ thao tác nào cũng dùng lại mặc định kèm cảnh báo khi đọc. Nút chuột dùng để điều khiển chỉ có tác dụng trên vùng bản đồ, tránh kích hoạt khi bấm giao diện.

Bật/tắt âm thanh áp dụng cho cả PCM WinMM và Unity AudioSource. Màn này có nút mở repository GitHub và Facebook `https://www.facebook.com/huy.huu.nguyen.266934` bằng trình duyệt. Thiết lập lưu tại `Application.persistentDataPath/settings.json`, độc lập tài khoản, server và file kết nối cũ; file thiếu/hỏng dùng lại mặc định. Nhãn phím trong HUD cập nhật theo thiết lập đã lưu.

Nhặt vàng Chiến dịch có tiếng xác nhận ngắn sau chuyển động xe: `CampaignAudioTimeline` kiểm một lượt mới, vàng tăng và xu đã biết tại ô người chơi biến mất. Phản hồi cùng lượt, mua hàng, cập nhật ví, thưởng giải cứu, đổi run hoặc khôi phục sau mất khoảng lượt không phát lại tiếng nhặt. Cue `gold` tái sử dụng `CampaignAudio/purchase.ogg` của Kenney ở pitch 1,35; file nguồn/giấy phép giữ nguyên, bật/tắt theo cài đặt âm thanh chung. Preview chuyển động có bước nhặt xu để kiểm đường phát thật.

Mã chính: `ClientPreferences.cs` (mapping, kiểm tra và lưu file), `TankSettings.cs` (giao diện). `ClientPreferencesTests` kiểm tra gán phím, xung đột giữa chế độ, lọc chuột ngoài bản đồ, ghi/đọc và thay thế file, cấu hình hỏng và master audio. Preview offline: `--menu-preview settings`.

Giao diện nghiệp vụ v22: chủ phòng bấm ô trống thêm AI, có nhãn AI và số người thật/bot; phòng chờ đầy AI vẫn cho người thật vào. Khi AFK, nút Tiếp quản xe mở lại đồng bộ trước khi điều khiển. Chiến dịch có Lưu và thoát, tiếp tục theo tài khoản. Shop ghi một lần/loại/trận; kết quả đồng hạng và lịch sử có nút tố cáo người gốc HUMAN. Chi tiết tại [nghiệp vụ v22](business-workflow.md).

<a id="ui"></a>
## Giao diện và render arena

Menu và arena dùng **IMGUI**. [TankApp.OnGUI, Arena](../source/client/Assets/Scripts/TankApp.cs) chọn màn hình theo phase của client; [TankApp.DrawMenus, DrawDialogs](../source/client/Assets/Scripts/TankMenus.cs) phụ trách đăng nhập/đăng ký, lobby, phòng, lịch sử, kết quả và hộp thoại. Bộ chọn quốc gia của Nhập vai ở [TankApp.NationSelector](../source/client/Assets/Scripts/TankRoleplayMenus.cs).

Form tạo/sửa phòng đóng sau xác nhận server trong `UpdateRoomForm`, trước cặp IMGUI Layout/Repaint; không đổi màn giữa hai lượt vẽ. Cảnh báo AFK ghi thời gian còn lại trước khi dừng điều khiển, chỉ hiện khi người chơi vẫn điều khiển xe; khi chờ trở lại hoặc AI tiếp quản, chỉ hiện nút **Tiếp quản xe**.

Form đăng ký có ô **Nhập lại mật khẩu**, được che ký tự như ô mật khẩu. Client kiểm tra hai giá trị trùng khớp trước khi kết nối/gửi yêu cầu đăng ký; ô xác nhận không được gửi lên server. Đăng nhập không yêu cầu xác nhận mật khẩu.

### Chọn bản đồ khi tạo phòng multiplayer

Trong sảnh **Nhiều người chơi**, bấm **Tạo phòng** rồi chọn một trong ba thẻ bản đồ ở đầu form. Mỗi thẻ có tên, mô tả ngắn và ảnh xem trước được vẽ từ chính dữ liệu địa hình bằng `ArenaRenderer`; thẻ đang chọn có vạch vàng và chữ **ĐÃ CHỌN**. Lựa chọn áp dụng cho cả Đấu thường và Nhập vai.

| ID | Tên | Dữ liệu và đặc điểm |
|---|---|---|
| 2 | **Tiền Đồn Xanh — mặc định** | `map-large.json`; nền cỏ và vật liệu tường cũ, nước vẽ bằng `DrawWater` gốc. Bố cục mới có sông dọc chia hai đoạn, hai hồ bên và một cầu gỗ trên khe khô giữa sông; tổng diện tích nước 648 đơn vị². Mười sáu tháp/cây/đá có kích thước khác nhau tạo bốn cụm trên đất, tránh đặt asset trên tường. Mỗi form tạo phòng mới chọn sẵn map này. |
| 3 | **Pháo Đài Cát** | `map-fortress.json`; giữ nền cát, pháo đài bốn cổng, công sự và hai hồ xanh ngọc; tăng lên chín cây cọ thành cụm ở hai góc bờ ngoài mỗi hồ để tạo cảm giác ốc đảo. Trong hồ không giảm tốc, hồi năng lượng Shift ×1,5 sau khi thả Shift và chờ 15 tick. |
| 4 | **Sông Băng** | `map-river.json`; một hồ băng lớn bờ thuôn, lệch giữa sân, tổng 648 đơn vị² và không có cầu. Rừng thông ôm các bờ hồ, hai cụm nhà băng phía đông; tường và gạch chia thành các tuyến công sự ngắn, không dùng lại bốn trại chữ L. Trên băng tốc độ ×1,5. |

Ảnh đối chiếu 1280×800 từ cùng bản Windows ngày 07/11/2026: [Tiền Đồn Xanh](images/map-biomes/green.png), [Pháo Đài Cát](images/map-biomes/desert.png), [Sông Băng](images/map-biomes/snow.png). Đây là ảnh renderer thật ở chế độ preview tám xe, hiển thị mọi vị trí gạch (hai viên có sát thương mẫu) và không bật kỹ năng; kết quả kiểm tra mạng được ghi riêng trong testing.

Cả ba map có kích thước 120×80 và 16 điểm xuất hiện. Danh mục dùng chung nằm ở [multiplayer-maps.json](../source/client/Assets/StreamingAssets/multiplayer-maps.json); map cũ ID 1 (`map.json`) không nằm trong lựa chọn tạo phòng. Nội dung form cuộn khi thiếu chiều cao, còn nút Xác nhận/Hủy ghim phía dưới; ở vùng hẹp, các thẻ map xếp một cột.

Map đã tạo được giữ cố định suốt vòng đời phòng, kể cả sửa cài đặt, vào lại trận, kết thúc và bắt đầu trận tiếp. Màn Cài đặt phòng chỉ hiện tên/ảnh map đã chọn. Tên map cũng có trên thẻ phòng ở sảnh, phần thông tin phòng chờ và chi tiết lịch sử. Muốn dùng map khác, tạo phòng mới. Mã UI: [TankRoomMaps](../source/client/Assets/Scripts/TankRoomMaps.cs), [TankMenus](../source/client/Assets/Scripts/TankMenus.cs).

Diện mạo chọn theo ID map tại client qua `ArenaBiomeGeometry`/`ArenaBiomePresentation` và `ArenaEnvironmentGeometry`/`ArenaEnvironmentPresentation`, dùng chung trong trận, thumbnail tạo phòng và phòng chờ. Bố cục cập nhật ngày **07/11/2026**: ID 2 giữ nền cỏ và vật liệu tường thép, khôi phục nguyên đường vẽ nước `DrawWater` trước đợt sửa UI; ID 3 giữ mặt nước ốc đảo hiện có; ID 4 giữ mặt băng opaque, vết nứt và phản sáng tĩnh. Tường không phá hủy ở map 3/4 vẫn dùng vật liệu sa thạch/đá phủ tuyết cartoon. Mép bờ và viền tường theo chu vi hợp các hình chữ nhật địa hình, không tạo đường nối giả ở chỗ giao L/T. Vật liệu neo theo tọa độ thế giới và đảo chiều ở ô kề để giảm đường nối; bóng tường chỉ lệch tối đa 2 px. Chỉ map mặc định có cầu gỗ: hình vẽ bằng code đặt trên khe đất khô giữa hai đoạn sông, không thêm collider hoặc đổi luật địa hình. Map mùa đông không có cầu, giữ dải băng liền mạch cho tuyến tăng tốc.

Tiền Đồn Xanh dùng 16 asset `CampaignCartoon` gồm tháp, đá, cây và cụm cây thành bốn nhóm có kích thước tán khác nhau, tránh đường vòng, tường, hồ, spawn, item và vùng lửa. Collider nhỏ ID 16–31 nằm gọn trong hình asset; renderer bỏ đế thép ở các collider này. Pháo Đài Cát dùng 18 cây cọ `ArenaTerrain/oasis_palm`, chín cây mỗi hồ dồn thành cụm ở hai góc bờ ngoài; collider thân cây ID 18–35 nhỏ hơn tán và cũng không có đế tường. Chân cây/công trình chặn xe và phản xạ đạn theo nhóm `walls`. Tường/gạch, nước, spawn, item và battlefield gốc của map sa mạc giữ nguyên. Bốn landmark sa mạc vẫn nằm trên vật cản; toàn bộ thông/đá/nhà băng mùa đông nằm trên đất, không trang trí chồng lên tường. Vệt cát/tuyết phẳng tránh spawn, item và tuyến chính.

Tiền Đồn Xanh bố trí lại 12 đoạn tường thép bên trong, giữ bốn tường biên và 16 collider cảnh vật; 36 viên gạch tạo các nhóm chắn rõ ràng. Hai đoạn sông dọc rộng 9, dài 24 cùng hai hồ bên 12×9 chiếm tổng 648 đơn vị². Cầu gỗ ở `(54,5; 35,5)`, kích thước 11×9, đánh dấu khe đất khô giữa hai đoạn sông. Giữ nguyên 16 spawn và 47 mục tiêu không kích; giữ 21 điểm item nhưng chuyển hai điểm để tránh sông, bố trí lại 18 mục tiêu lửa cho địa hình mới.

Sông Băng có một hồ liền mạch ghép từ năm dải bờ thuôn, nằm trong vùng x=38–74, y=30–54, tổng 648 đơn vị². Băng giữ vật liệu hiện có, có tuyến tăng tốc xuyên hồ theo cả hai chiều, không có cầu. Bốn tường biên và 12 công sự ngắn được bố trí mới cùng 24 viên gạch thành tám cụm. Rừng/cảnh quan có 28 thông phủ tuyết, tám đá tuyết, bốn cụm tinh thể băng và bốn nhà băng, với collider chân ID 16–59; tổng 60 tường kể cả công sự và cảnh vật. Sáu cụm rừng bao quanh hồ, hai cụm nhà băng nằm phía đông; các hình không phủ hồ, gạch hoặc tường. Giữ nguyên 16 spawn và 48 mục tiêu không kích; giữ 18 điểm item, chuyển bảy điểm tránh hồ/công sự, bố trí lại 48 mục tiêu lửa. Map mặc định/sa mạc giữ nguyên byte sau khi người dùng chốt.

Nhà băng mới ở `Resources/ArenaWinter/igloo.png`, dùng `ArenaWinterArt`/`ArenaWinterImporter`, riêng ngân sách 8 MiB. Nguồn được tạo bằng `image_gen` dựa trên hai atlas cartoon hiện có; giữ nguyên PNG và alpha, có nguồn và prompt, được chép vào `ThirdParty` khi build. Thông/đá/tinh thể tái sử dụng `ArenaBiomes`; không có người tuyết.

Luật địa hình nằm ở server và prediction client: nước map mặc định ×0,5 tốc độ, hồ ốc đảo giữ tốc độ và hồi năng lượng ×1,5, băng ×1,5 tốc độ mà không có quán tính. Chi tiết nhịp hồi sau khi thả Shift ở [gameplay/movement](gameplay.md#movement). Đợt bố cục 07/11 không thay file luật hoặc protocol/wire; cả ba JSON map được chọn đổi hash byte gốc vì tăng collider hoặc đổi bố cục, nên cần build/phân phối server và client cùng dữ liệu map mới. Đợt bão/khiên hồi sinh nâng protocol lên 19, sau đó dải bão dùng protocol 20; bản bão phủ toàn map hiện tại dùng **protocol 21** và thêm giảm tốc độ bắn trong bão cát, không sửa tiếp byte map. Map ID 1 giữ nguyên JSON và renderer cũ.

Trên map tuyết, nhãn **BẠN** và trạng thái kỹ năng dưới xe có viền tối 1 px để đọc được trên tuyết/mặt băng sáng và vật cản tối. Nền tuyết căn mép tile theo pixel để không lộ khe khi map được thu/phóng.

Metadata map trong tạo phòng, danh sách phòng và trạng thái phòng được bổ sung từ **protocol 18**, giữ layout trong **protocol 21** hiện tại; client và server phải được cập nhật cùng đợt. Khi chuẩn bị trận, client vẫn đối chiếu ID và SHA-256 byte gốc từ `PREPARE`; chi tiết wire tại [protocol/TCP](protocol.md#tcp).

### Menu và thành phần UI dùng chung

Enter hoặc Enter bàn phím số chuyển sang ô nhập tiếp theo; chỉ xác nhận ở ô cuối. Đăng nhập: tên → mật khẩu → Đăng nhập. Đăng ký: tên → mật khẩu → nhập lại mật khẩu → Đăng ký. Tạo/cài đặt phòng: tên → mật khẩu (nếu có) → máu → Xác nhận. Form một ô (tên hiển thị, mật khẩu vào phòng) xác nhận ngay bằng Enter. Trong Cài đặt, Enter đi qua từng ô phím chính/phụ, ô cuối thực hiện Lưu; muốn gán Enter, nhập chữ `Enter`. Dùng cùng validation và trạng thái khóa/chờ như nút chuột; form phía sau modal không nhận Enter. Tổ hợp có Alt/Ctrl/Shift/Command không gửi form; Alt+Enter vẫn dành cho toàn màn hình. Khung đăng nhập/đăng ký đo chiều cao theo nội dung, kể cả thông báo và nút phát sinh; chỉ cuộn khi vượt viewport, không dành khoảng trống cố định bên dưới nút xác nhận.

Header sau đăng nhập hiển thị huy hiệu vàng sát bên trái ô tên người chơi, gồm icon đồng xu, số dư và nút **+**, tải sau login và làm mới định kỳ. Hai thông báo đều có tiêu đề **Vàng**: bấm số vàng hiện “Vàng dùng để mua vật phẩm trong cửa hàng ở chế độ nhập vai”; bấm + hiện “Vàng có thể được tìm thêm khi tham gia chế độ chiến trường. Vàng còn lại sau mỗi chuyến đi sẽ được cộng dồn vào tài khoản”. Thông báo có nút Đóng và khóa input khi mở. [ClientCore.Shop](../source/client/Assets/Scripts/ClientCore.Shop.cs) nhận ví/giới hạn mua/túi item từ server; [TankRoleplayShop](../source/client/Assets/Scripts/TankRoleplayShop.cs) mở modal bằng E trong đợt RP, hiển thị 6 loại, giá, trạng thái đã mua và đồng hồ đóng shop. Nút chỉ ghi **Đóng**; E/Esc cũng đóng, hết đợt tự đóng. Modal khóa input chiến đấu và tự bắn, không tạm dừng trận. Mỗi đợt vật phẩm Nhập vai mở cửa hàng sẽ hiện thông báo vàng “Cửa hàng đã mở, bạn có thể mua thêm vật phẩm ở cửa hàng” trong 2 giây, một lần mỗi đợt theo tick server; khung vừa chiều rộng sidebar và chữ xuống dòng, không khóa input. Sidebar không còn khối nhắc phím di chuyển/tăng tốc/tự bắn, nhắc click vào game hoặc hướng dẫn E mở shop kèm thời gian chờ. Cảnh báo mạng chậm vẫn hiển thị; thời gian đóng shop vẫn có trong modal cửa hàng. Icon item đã mua nằm trong ô nền tối ở sidebar, không có nền nút tròn vàng và không vẽ nhãn số lượng trên icon. Bấm icon để dùng một item khi xe sống, kể cả khi shop đóng. Luật chi tiết ở [gameplay/items](gameplay.md#items).

Preview offline: `--arena-preview --preview-roleplay --preview-shop-items` dựng túi/ví mẫu và đợt đang mở; thêm `--preview-open-shop` để xem modal. Thêm `--preview-shop-opening` để chuyển từ chờ sang mở cửa hàng ở giây thứ 4 của preview, kiểm tra thông báo bằng `--capture-delay 3` và trạng thái đã biến mất bằng `--capture-delay 6`. `--preview-wallet-info`/`--preview-wallet-add` dựng hai thông báo vàng, ghép với arena hoặc `--menu-preview lobby`. Có thể dùng `--capture` để kiểm ảnh ở nhiều kích thước; các cờ preview không kết nối hoặc ghi dữ liệu tài khoản.

Chiến dịch dùng [CampaignUI](../source/client/Assets/Scripts/CampaignUI.cs) làm lớp style theo ngữ nghĩa màn hình trên `CartoonUi`: cùng font, màu, nút và khung với Multiplayer. Màn chọn xe/nhận nhiệm vụ Chiến dịch dùng lại nền xanh tối cũ của CampaignUI. `TankApp.OnGUI` dùng chung header và đường vẽ modal cho cả hai chế độ. Màn chọn chế độ sau đăng nhập có hai thẻ Chiến dịch/Nhiều người chơi; sảnh multiplayer giữ lựa chọn Thường/Nhập vai trong form phòng. Có thể xem sảnh khi còn chuyến đi chiến dịch nhưng UI khóa tạo/tham gia phòng và hướng dẫn kết thúc/lưu chuyến đi theo ràng buộc server hiện có. Hướng dẫn chơi ở [README](../README.md#chế-độ-chiến-dịch).

[CartoonUi](../source/client/Assets/Scripts/CartoonUi.cs) dùng giao diện cuối của nhánh Bao tại commit `97c6a46`: nền xanh tối `#121C19`, panel xanh quân đội `#344237`, panel tối `#202B26`, chữ tối Ink `#18241D` hoặc chữ kem; nút chính vàng `#FFC34B`, nút phụ kem `#F2E7CA`, ready/lựa chọn xanh `#9AC96C`, rời/kick cam `#E88C65`, nhiệm vụ sand `#D8C191`, trạng thái không khả dụng `#84907E`. Panel, thẻ và ô nhập phẳng, cắt góc theo hình tấm giáp; viền xanh `#62705A` được pha theo màu mặt, viền nút dùng Ink. Nút có bóng đổ nhẹ, giảm khi nhấn; focus/lựa chọn làm ấm màu mặt nút. Cụm vàng/tên người chơi/Cài đặt dùng chung chiều cao đo theo tên, cùng tọa độ trên/dưới, căn giữa header 68 px trong trận hoặc 76 px ở menu. Ô tên mở rộng khi cần ở cửa sổ nhỏ để tên dài xuống dòng mà không đẩy nút lệch hàng. Chiến dịch dùng cùng cụm ví/tên/Cài đặt; nút Chọn chế độ nằm trong nội dung màn chuẩn bị và BXH. Modal chiến dịch được vẽ sau header, khóa toàn bộ HUD phía sau kể cả cuộn chuột. Texture được tạo/cache theo màu, tham số góc và trạng thái. Nút có trạng thái hover, active, focus và lựa chọn; trạng thái chờ có dấu chấm chuyển động. Icon HP/giáp/sát thương/tầm bắn/thời gian/người chơi/khóa/kỹ năng, badge, huy chương và thanh tiến độ dùng chung; portrait tái sử dụng body/barrel và palette xe hiện có. Toàn UI không hiện ô tooltip đi theo chuột.

[MenuTheme.Init](../source/client/Assets/Scripts/MenuTheme.cs) dùng **Bungee Regular** cho tiêu đề, **Nunito Bold** cho nút/nội dung/nhập liệu; cả hai có tiếng Việt và giấy phép OFL. Đăng nhập, sảnh và tạo/cài đặt phòng dùng ảnh xe tăng `ScaleToFit` để giữ toàn bộ ảnh, với lớp phủ xanh tối chuyển dần từ trái sang phải; logo cũng dùng `ScaleToFit`, giữ alpha/màu gốc. Header đăng nhập có “SẴN SÀNG XUẤT KÍCH”, form có “TRỞ LẠI CHIẾN TRƯỜNG”. Lịch sử và chi tiết lịch sử dùng cùng nền ảnh xe tăng với màn tạo phòng và sảnh. Phòng chờ và kết quả dùng nền xanh tối có dấu xích xe nhẹ phía sau dữ liệu.

[MenuLayout.Fit](../source/client/Assets/Scripts/MenuLayout.cs) bố trí logo trái/form phải từ 1100 px, căn giữa form khi hẹp hơn; logo header đăng nhập lúc này đặt ở góc trái như header sảnh, không nằm giữa màn hình. Viewport dưới 720×600 có cuộn ngoài. Form phòng tối đa 920×560, chia hai cột khi panel đủ 840 px; các nút Xác nhận/Hủy ghim ở cuối panel. Nút đổi Lịch sử/Danh sách phòng dùng một dòng và đo chiều rộng theo nhãn dài nhất để không cắt chữ; các nút cạnh nó được căn lại theo chiều rộng thực. Sảnh dùng thẻ phòng có portrait, số người, khóa và badge phân biệt chờ/đang đấu/đầy/có thể vào; trạng thái trống có lời dẫn và hành động. Phòng chờ dùng thẻ người chơi với xe, tên, ready, phe và chủ phòng; roster một hoặc hai cột và cuộn riêng. Tên và mô tả được đo theo font/chiều rộng thực để xuống dòng và tăng chiều cao thẻ; các nhãn nút dài cũng được tính chiều cao. Phòng Nhập vai có thẻ chọn phe hai cột cao 108 px, gồm xe, quốc gia, kỹ năng Q và trạng thái đang chọn/đã có người; đặt cạnh roster khi đủ rộng hoặc trong vùng cuộn roster khi hẹp. Mô tả kỹ năng nằm ở cột thông tin, ready/start/cài đặt/rời phòng ghim phía dưới. Chữ không thu nhỏ toàn UI; nút thường cao tối thiểu 52 px. Giữ handler, validation, tên control và quyền chủ phòng.

[Kết quả và lịch sử](../source/client/Assets/Scripts/TankMenus.cs) dùng huy chương, portrait và thẻ thành tích. Podium đặt hạng 1 ở giữa, cao hơn hạng 2/3; danh sách đầy đủ cuộn riêng, hàng của người chơi dùng mint. Nút Về phòng/Về sảnh luôn ở dưới. Tên dài được đo và xuống dòng; danh sách thành tích và nội dung chi tiết dài có vùng cuộn. Trạng thái trống, chờ tải, lỗi kết nối và xác nhận có cùng style.

[Màn Chiến dịch](../source/client/Assets/Scripts/TankCampaignScreens.cs) dùng ba thẻ xe Mỹ/Liên Xô/Ý ở cột trái và hồ sơ xe lớn bên phải khi đủ rộng; khi hẹp, ba thẻ nằm ngang phía trên hồ sơ. Portrait dùng cùng xe cartoon trong trận, có HP/giáp/sát thương/tầm bắn và kỹ năng. Hồ sơ nhiệm vụ và mô tả kỹ năng mặc định thu gọn, bấm Chi tiết để mở. Header tài khoản/ví/cài đặt dùng chung với multiplayer; các nút Xuất kích/BXH/Tiếp tục ghim ngoài vùng cuộn nội dung. Shop ghi Mua và dùng ngay cùng lý do chưa mua được. BXH phân biệt hạng đầu và người chơi hiện tại; kết quả có quãng đường, số địch, vàng chuyến đi/tài khoản, trạng thái lưu và Lưu lại khi cần. Xuất kích bị khóa khi còn chuyến đi hoặc kết quả chưa lưu, đồng thời hiện Tiếp tục/Xem kết quả.

[ArenaTheme](../source/client/Assets/Scripts/ArenaTheme.cs) dùng cùng font, palette và khung cắt góc với menu cho header/HUD, đồng hồ, bảng hạng, Q, thông báo AFK/hồi sinh và banner battlefield. HP và năng lượng giữ ở thanh trên xe trong map, không lặp lại thành khối trong sidebar. Sidebar dành cho thời gian, phe, item, bảng hạng và kỹ năng; kỹ năng và túi vật phẩm Nhập vai ghim phía dưới, cùng `BattleHudUi` với chiến dịch; trạng thái chậm tên lửa ghi số lần trúng, mức giảm tốc nhân dồn và thời gian còn lại đến hết AREA; bảng hạng nhấn hàng của bạn, đo chiều cao theo tên xuống dòng và cuộn riêng. Nút Q dùng Nunito, đo chiều cao nội dung; phần trạng thái dài có vùng cuộn riêng. Ping/FPS là dòng chữ nhỏ gọn trong header, không có panel bao quanh; AFK dùng cảnh báo có đếm ngược. Rời trận mở modal xác nhận, khóa input chiến đấu khi modal hiện. HUD không dùng ảnh nền minh họa; vẫn giữ map, màu xe/hiệu ứng/cảnh báo và toàn bộ tọa độ ngắm. Font mặc định được giữ riêng cho ký hiệu nhỏ không có trong font mới. Phạm vi HUD/font này được người dùng yêu cầu bổ sung, thay thế giới hạn giữ style trận/font cũ trong plan ban đầu.

[HUD Chiến dịch](../source/client/Assets/Scripts/TankCampaignHud.cs) dùng `ArenaLayout.Fit(...,13,13)` và sidebar 294 px từ y=88 giống multiplayer; giữ camera và 13×13 ô, bỏ footer riêng. HP hiển thị một thanh trên xe người chơi, được giữ trong viewport khi xe sát mép; sidebar không lặp HP/giáp. Phần đầu chỉ còn quốc gia, quãng đường/thanh tiến độ, số trùm và vàng chuyến đi. Đã bỏ khối Chi tiết nhiệm vụ/Thông tin chuyến đi, tên khu vực thường trực, số lượt và số địch đã hạ trong trận; thống kê kết quả cuối chuyến đi vẫn còn. Thông báo hành động/lỗi chỉ hiện khi có nội dung và cuộn trong phần trống.

Khi đến khu vực mới, `CampaignPresentation` ghi nhận định danh khu vực và hiện tên ở đầu viewport trong **2,8 giây**, có xuất hiện/mờ dần; không lặp lại theo từng lượt hoặc phản hồi cùng khu vực, không chặn điều khiển. Ô hiệu ứng ngay trên cụm hành động chỉ hiện khi còn hiệu lực: Khiên ghi số lần chặn, Đòn ×2 ghi số đòn cường hóa còn lại. Số lấy từ server và chỉ giảm khi hiệu ứng được sử dụng, không tự trừ theo thời gian. Ba nút Bắn/Kỹ năng/Tiếp tế và Kết thúc luôn ghim phía dưới; Bắn là nút vàng chính. Kết thúc mở xác nhận; Esc/Tiếp tục đóng, chờ nhả phím/chuột trước khi trả điều khiển. Các lệnh, chỉ số, nhịp lượt và kết quả vẫn do server quyết định.

`BattleHudUi` chia sẻ thẻ hành động, nhãn phím tùy chỉnh, trạng thái chờ/lý do khóa giữa hai chế độ. Kỹ năng multiplayer dùng **viền xanh bao quanh ô**, độ dài viền rút dần theo phần hồi chiêu còn lại; hồi xong hết viền đếm ngược. Ô kỹ năng Chiến dịch tắt viền bằng `showCooldownBorder:false`, giữ trạng thái/số lượt hồi chiêu; nút Bắn vẫn có chỉ báo riêng. Chiến dịch dùng lượt, multiplayer dùng giây; số hiệu lực buff là thông tin riêng, không đồng nhất với hồi chiêu.

Trong trận, mô tả kỹ năng chỉ mở bằng **chuột phải vào ô kỹ năng**; không còn đoạn mô tả thường trực trên HUD. `SkillDetailsUi` dùng chung một khung gắn với ô được bấm và giới hạn trong màn hình; Đóng, Esc, bấm ra ngoài hoặc chuột phải lại cùng ô để đóng. Khi khung đang mở, input chiến đấu bị khóa, tự bắn tắt; đóng khung vẫn đợi nhả phím/chuột trước khi trả điều khiển. Trận multiplayer vẫn chạy trên server. Chuột phải trong UI không bật tự bắn. Ô Khiên/Đòn ×2 cũng hỗ trợ xem giải thích theo cách này.

[ArenaLayout.Fit](../source/client/Assets/Scripts/ArenaLayout.cs) giữ tỷ lệ map trong phần màn hình còn lại, với sidebar rộng **294 px** sát mép phải. [ArenaRenderer.Draw, Panel, NetworkStatus](../source/client/Assets/Scripts/ArenaRenderer.cs) vẽ địa hình, xe, đạn, HUD và trạng thái mạng. Thanh máu trên xe biểu diễn độ đầy, không ghi số HP. Sau khi resize hoặc đổi fullscreen, cần kiểm tra lại ngắm theo chuột, map và sidebar; test có sẵn là CombatTests.RectangularArenaFitsWithoutOverlappingTheRightAnchoredSidebarAndAimStaysCorrect và ProtocolTests.AimTracksCursorAfterResizeAndOnEitherMap.

Đạn multiplayer lấy nửa chiều dài/rộng **0,6/0,32** từ contract chung, tăng gấp đôi cả hình ảnh và vùng va chạm theo [luật chiến đấu](gameplay.md#combat); các hệ số HE/Mirror giữ nguyên. Hình đạn bay trong hiệu ứng Bắn của Chiến dịch cũng tăng gấp đôi chiều dài/rộng; luật trúng đích theo ô và thời điểm tác động của Chiến dịch giữ nguyên.

[TankApp.Update](../source/client/Assets/Scripts/TankApp.cs) chỉ lấy điều khiển gameplay khi cửa sổ đang được focus và không mở hộp thoại. Khi mất focus hoặc rời trận, chế độ tự bắn được reset; PlayerRecoveryTests.AutoFireTogglesAndResetsOnFocusLossAndMatchExit mô tả hành vi này. Phím và thao tác đầy đủ nằm tại [điều khiển](gameplay.md#controls).

Hiệu ứng chiến đấu được nhận từ snapshot trong [ArenaRenderer.AcceptEffects](../source/client/Assets/Scripts/CombatRendering.cs), quản lý tuổi và chống phát lại trong [CombatPresentation.Accept, Update](../source/client/Assets/Scripts/CombatPresentation.cs). Red Zone/Bombstorm có dải thông báo tên và thời gian ở phía trên; vùng cảnh báo trên map dùng nền gạch chéo đỏ, viền vàng, tâm ngắm và hiệu ứng Cozy Chunky, không đặt chữ hoặc đồng hồ lên từng vùng. Lửa/khói và vụ nổ chịu giới hạn tầm nhìn; HUD không tiết lộ vị trí vùng bị sương mù che. Xem [ArenaRenderer.DrawBattlefieldWarning, DrawBattlefield, DrawBattlefieldNotice](../source/client/Assets/Scripts/BattlefieldPresentation.cs), [luật battlefield](gameplay.md#battlefield) và kiểm tra hình ảnh thủ công.

Bão theo map dùng [WeatherPresentation](../source/client/Assets/Scripts/WeatherPresentation.cs), chia sẻ vị trí thông báo **giữa mép trên bản đồ** với Red Zone/Bombstorm, không có khối thông tin bão trong sidebar. Cảnh báo 3 giây có tên, đếm ngược và phạm vi **toàn bản đồ**. Trong 12 giây hoạt động, thông báo ghi thời gian, giảm 50% tốc độ bắn hoặc di chuyển và sát thương 2 HP/2 giây; không có trạng thái trong/ngoài vùng hoặc hướng tránh bão.

Bão phủ toàn viewport bản đồ cùng lúc, không có dải quét hoặc vùng an toàn theo vị trí. Bão cát dùng mây bụi vàng nâu phía sau xe, cụm bụi và vệt cát chuyển động; bão tuyết dùng mây xanh xám, hạt nhiều kích thước tương phản với nền tuyết và sắc lạnh nhẹ trên mọi xe. Hạt phía trước thưa để đọc được xe, đạn và HP; mọi lớp thời tiết nằm trong viewport, không phủ header/sidebar. **Không có bán kính quan sát 18 đơn vị hoặc cắt thực thể do bão cát**; Fog kỹ năng vẫn giữ luật riêng cho địa hình, xe, đạn, item, FX và âm thanh định vị.

Âm thanh gió/cảnh báo dùng voice WinMM thứ bảy, không lấy voice bắn; âm gió theo giai đoạn bão, không phụ thuộc vị trí xe. Trạng thái âm thanh theo snapshot/tick, tuân cài đặt âm thanh và không phát lại cảnh báo cũ khi rejoin. Khiên hồi sinh 90 tick có vòng xanh lam nhạt quanh xe, không thêm thông tin sidebar. Luật giảm tốc độ bắn cát 50%, giảm tốc tuyết ×0,5 và sát thương nằm ở [gameplay/battlefield](gameplay.md#battlefield).

Ảnh lịch sử từ build **dải bão v20**: [bão cát cũ](images/weather-band/sand-mid-1280.png), [bão tuyết cũ](images/weather-band/snow-mid-1280.png), [cảnh báo cũ](images/weather-band/sand-warning.png), [dải bão đã quét sang phải](images/weather-band/snow-late.png). Thư mục `images/weather-band/` giữ 19 ảnh gốc và testing/weather-v20 ghi kết quả của bản cũ; chúng không chứng minh bão phủ toàn map hiện tại. Bản **v21 toàn map** giữ riêng 17 ảnh: [cát](images/weather-global/sand-mid-1280.png), [tuyết](images/weather-global/snow-mid-1280.png), [cảnh báo](images/weather-global/sand-warning.png), [RP cửa sổ thấp](images/weather-global/snow-roleplay-low.png). Lệnh, kết quả và giới hạn bản hiện tại ở testing/weather-v21.

<a id="assets"></a>
## Tài nguyên, importer và giấy phép

| Tài nguyên | Cách dùng và yêu cầu bảo toàn |
|---|---|
| `Assets/Resources/ArenaArt/` | Texture được nạp bằng tên qua `Resources.Load`, ví dụ trong [ArenaRenderer.Art](../source/client/Assets/Scripts/ArenaRenderer.cs). Giữ tên/đường dẫn, `.meta` và GUID để bảo toàn tham chiếu. Tile cát Kenney tiếp tục dùng cho Pháo Đài Cát theo giấy phép CC0 gốc. |
| `Assets/Resources/ArenaBiomes/` | Atlas `props.png` bổ sung tám hình cartoon nhìn từ trên xuống: xương rồng, bụi khô, đá sa mạc, đụn cát, thông tuyết, đá tuyết, ụ tuyết và băng. Ảnh sinh bằng image_gen ngày 2026-11-06; giữ PROVENANCE.md, catalog, `.meta` và importer/validator riêng. Đây không phải asset Kenney và không gắn giấy phép CC0 của Kenney cho ảnh sinh. |
| `Assets/Resources/ArenaEnvironment/` | Atlas nguyên bản `materials.png` giữ đủ năm mẫu vật liệu của lượt trước; renderer hiện dùng hai loại tường và nước sa mạc, còn nước map xanh/băng tuyết dùng đường vẽ riêng. Sinh bằng image_gen ngày 2026-11-06 theo phong cách CampaignCartoon; catalog lấy phần trong mỗi mẫu để tránh nền mờ. Giữ PROVENANCE.md/prompt/hash nguồn và `.meta`, không gắn giấy phép Kenney cho ảnh sinh. |
| `Assets/Resources/ArenaTerrain/` | Hai PNG độc lập `ice.png` và `oasis_palm.png`, mỗi ảnh 1254×1254, nguồn RGB8 opaque cho băng và RGBA8 cutout cho cây cọ. Ảnh sinh bằng image_gen ngày 2026-11-06, giữ nguyên byte nguồn, hash, prompt, provenance và `.meta`; importer nạp RGBA32. Đây là ảnh sinh riêng, không kế thừa giấy phép Kenney của asset tham chiếu. |
| `Assets/Resources/MenuArt/` | Bản sao nguyên byte `logo.png`, `anh_nen.png` và bộ thành phần Chequered Ink — Cartoon UI Pack đã import; checkbox/thanh cuộn vẫn dùng pack. Nút/panel dùng texture hình học của CartoonUi. Giữ [giấy phép riêng](../source/client/Assets/Resources/MenuArt/ChequeredInk-LICENSE.txt). Không dùng ảnh tham khảo Goose Goose Duck trong game. |
| `Assets/Resources/MenuArt/Fonts/` | Hai TTF static Bungee Regular và Nunito Bold; giữ `Bungee-OFL.txt`, `Nunito-OFL.txt`, `NOTICE.txt` với nguồn/hash/cách tạo instance Nunito. Không cần cài font trên máy người chơi. |
| `Assets/Resources/CombatFx/CozyChunky/` | Đã kèm **21 clip Cooked FX — Cozy Chunky**, PNG, `catalog.json`, `LICENSE.pdf` và `.meta`; clone đủ repository là Unity có thể import ngay, không cần chọn ZIP trước khi Play, build hoặc test. |
| `Assets/Resources/CampaignCartoon/` | Hai atlas RGBA scenery/địch/boss bổ sung và catalog UV, nguồn/prompt tại PROVENANCE.md; importer riêng dùng Bilinear/uncompressed/NPOT None. Xe dùng TankPalette, nền/đạn dùng ArenaArt, FX dùng CombatFx; giữ CampaignArt/CampaignUI gốc và giấy phép. Build validator kiểm crop/alpha/mapping. |
| `Assets/Resources/ItemCartoon/` | Catalog hoạt động của `ItemIconArt` tham chiếu thùng/đạn Kenney trong ArenaArt và các hình chức năng được vẽ bằng code, dùng chung trên map/shop/túi. Đồng xu đơn cùng một hàm vẽ cho pickup, ví, HUD, giá shop, BXH và kết quả. Bảy PNG AI của lượt trước giữ nguyên GUID/nguồn nhưng không còn được renderer dùng; PROVENANCE.md phân biệt hai đợt. Giữ importer và giấy phép nguồn Kenney. |
| `Assets/StreamingAssets/` | Chứa dữ liệu map và danh mục `multiplayer-maps.json`: ba map multiplayer ID 2/3/4; `map.json` ID 1 giữ cho dữ liệu/kiểm tra cũ. Map dùng chung với server; SHA-256 tính trên **byte gốc**, nên đổi khoảng trắng cũng đổi hash. Chi tiết tại [map](gameplay.md#maps) và [shared contracts](protocol.md#shared-contracts). |

[ArenaArtImporter.OnPreprocessTexture](../source/client/Assets/Editor/ArenaArtImporter.cs) áp dụng texture Default, alpha transparency, Read/Write bật, không mipmap, không nén, Bilinear, kích thước tối đa 256 và không scale NPOT. Sprite cần đọc pixel cho mask; giữ các thiết lập này khi thay hình.

[ArenaBiomeArt](../source/client/Assets/Scripts/ArenaBiomeArt.cs) nạp/cache atlas và mapping; renderer cache tuyến/trang trí từ `ArenaBiomeGeometry` theo map; nền tuyết và các trạng thái gạch được tạo một lần rồi tái sử dụng. Không nạp asset hoặc dựng texture lại mỗi frame. [ArenaBiomeImporter](../source/client/Assets/Editor/ArenaBiomeImporter.cs) dùng RGBA32, Bilinear/Clamp, không Read/Write, mipmap, nén hay scale NPOT, tối đa 2048. Validator kiểm atlas/alpha/crop/mapping/hash nguồn/provenance và chặn build khi tài nguyên sai; `ArenaBiomeBuildValidation` chép PROVENANCE.md vào `ThirdParty/ArenaBiomes-PROVENANCE.md` cạnh bản chạy; không đổi importer ArenaArt/Combat FX.

[ArenaEnvironmentArt](../source/client/Assets/Scripts/ArenaEnvironmentArt.cs) cache chung atlas 1536×1024 RGBA32 (6 MiB, ngân sách 16 MiB). [ArenaEnvironmentImporter](../source/client/Assets/Editor/ArenaEnvironmentImporter.cs) dùng Bilinear/Clamp, không Read/Write, mipmap, nén hay scale NPOT, tối đa 2048; validator kiểm hash nguồn, vùng mẫu bên trong có alpha ≥250/255, mapping và provenance, chặn build khi sai và chép nguồn sang `ThirdParty/ArenaEnvironment-PROVENANCE.md`. Renderer cache hợp địa hình, mép ngoài, mảnh UV và vị trí cảnh vật theo map; chỉ tạo một texture gợn nước 64×16 khi cần rồi hủy cùng renderer. Không dựng atlas hoặc tính lại hợp địa hình mỗi frame.

[ArenaTerrainArt](../source/client/Assets/Scripts/ArenaTerrainArt.cs) cache nguyên hai texture 1254×1254 RGBA32, tổng **12.580.128 byte** (xấp xỉ 12 MiB), trong ngân sách riêng 16 MiB. [ArenaTerrainImporter](../source/client/Assets/Editor/ArenaTerrainImporter.cs) dùng Bilinear/Clamp, không Read/Write, mipmap, nén hoặc scale NPOT, tối đa 2048; validator kiểm hash byte nguồn, kích thước/định dạng, alpha opaque/cutout và provenance trước build. Không crop/nhân bản texture trong runtime hoặc đổi byte PNG nguồn; nguồn/prompt tại PROVENANCE.md, được chép vào `ThirdParty/ArenaTerrain-PROVENANCE.md` khi build.

[MenuArtImporter](../source/client/Assets/Editor/MenuArtImporter.cs) chỉ áp dụng cho PNG trong MenuArt: alpha từ ảnh, không mipmap/nén/scale NPOT, Bilinear, Clamp. Hai ảnh cung cấp giữ tối đa 2048 và không Read/Write; các thành phần UI PNG giữ cấu hình 64/128/256 hiện có. `CartoonUi` cache texture 72×72 cắt góc; `MenuTheme.Init`/`ArenaTheme.Init` cache style/font/asset. Không tạo hay nạp texture lại mỗi lần OnGUI. `MenuArtBuildLicense` chép license pack và hai OFL/notice font sang `ThirdParty/` cạnh bản chạy. Importer ArenaArt/Combat FX không đổi.

[CombatFxImporter.OnPreprocessTexture](../source/client/Assets/Editor/CombatFxImporter.cs) áp dụng alpha từ ảnh, không Read/Write, không mipmap, sRGB, không nén, RGBA32, Bilinear, Clamp, không scale NPOT và tối đa 4096. Không áp dụng nhầm thiết lập ArenaArt cho Combat FX. [CombatFxCatalog.Preload, Validate](../source/client/Assets/Scripts/CombatFxCatalog.cs) kiểm tra manifest, số frame, kích thước, định dạng và ngân sách texture 32 MiB. Tài nguyên thiếu/sai chặn build qua `CombatFxBuildValidation.OnPreprocessBuild`, và chặn chuẩn bị vào trận qua [ClientCore.Handle (Op.Prepare)](../source/client/Assets/Scripts/ClientCore.Messages.cs).

Hình ảnh Kenney dùng **CC0**; giữ [Kenney-LICENSE.txt](../source/client/Assets/Resources/ArenaArt/Kenney-LICENSE.txt). Combat FX có [LICENSE.pdf riêng](../source/client/Assets/Resources/CombatFx/CozyChunky/LICENSE.pdf), không gộp giấy phép này thành CC0. [CombatFxBuildValidation.OnPostprocessBuild](../source/client/Assets/Editor/CombatFxImporter.cs) tự chép giấy phép Combat FX thành `ThirdParty/CookedFX-LICENSE.pdf` cạnh bản chạy. Khi phân phối, giữ toàn bộ thư mục client và giấy phép đi kèm; xem [build client](deployment.md#build-client).

Khi cập nhật asset, đưa cả `.meta` đi kèm vào thay đổi; không xóa `.meta` để ép import lại. Test hiện có gồm CombatAssetTests.ImportedSheetsMeetCatalogAndBuildValidation, `EveryMissingSheetAndWrongDimensionsFailWithTheClipName` và `InvalidArchiveIsRejectedBeforeAnyAssetIsWrittenAndIndexedPngAlphaIsAccepted`. Sự hiện diện của test không chứng minh chúng đã pass trên working tree hiện tại.

<a id="restore-combat-fx"></a>
## Khôi phục Combat FX

Chỉ dùng quy trình này khi bộ asset có sẵn bị thiếu/hỏng:

1. Mở [trang Cooked FX của tác giả](https://the-cooked-works.itch.io/cooked-fx), chọn **Download Now**, tải **`Cooked_FX_FREE_Cozy_Chunky.zip`** và giữ nguyên ZIP.
2. Kiểm tra SHA-256 từ PowerShell:

   ```powershell
   Get-FileHash -Algorithm SHA256 -LiteralPath '<DUONG_DAN_DEN_Cooked_FX_FREE_Cozy_Chunky.zip>'
   ```

   Importer hiện chỉ chấp nhận hash **`0ebb14888c970e6d4e08f3eeb9c469712c81bd44932ea2d7d151717e940af3af`**. File khác sẽ bị từ chối; nếu tác giả thay bản tải, phải đối chiếu phiên bản trước khi sửa importer.
3. Mở `source/client` bằng Unity **6000.3.24f1**, đợi compile xong, chọn **Tools → Tank Battle → Import Combat FX** rồi chọn ZIP trên.
4. Thành công khi Console ghi **`Imported 21 Cozy Chunky Combat FX clips.`**. Asset được khôi phục dưới `Assets/Resources/CombatFx/CozyChunky/`; importer tự đặt texture settings, validate và giữ `.meta` hiện có. Kiểm tra thay đổi PNG, catalog, license và `.meta` trước khi commit.
5. Đóng Editor trước khi chạy lại build/test bằng script.

Đối chiếu: [CombatFxImporter.ChooseArchive, ImportArchive, ValidateAssets](../source/client/Assets/Editor/CombatFxImporter.cs). Importer kiểm tra hash và toàn bộ entry được chọn trước khi ghi asset, rồi refresh và validate trong Unity. Có entry point `ImportFromEnvironment` đọc `TANK_COMBAT_FX_ZIP` cho công cụ tự động, nhưng luồng Play/build/test thông thường không cần biến này.

<a id="previews"></a>
## Preview và chụp ảnh

Preview dựng dữ liệu mẫu để kiểm tra hình ảnh offline. Nó không xác nhận kết nối multiplayer, sát thương server hoặc persistence. Dùng bản client đã build theo [triển khai](deployment.md#build-client); chạy từ PowerShell ở gốc repository:

```powershell
./build/client/TankBattle.exe --arena-preview --preview-eight --preview-battlefield fire
```

Để tạo bản review riêng, [BuildClient.BuildUiReview](../source/client/Assets/Editor/BuildClient.cs) build scene `Assets/Scenes/Main.unity` hiện có ra `build/client-ui/TankBattle.exe`. Entry này không tạo lại scene hay gán PlayerSettings như `BuildClient.Build`; vẫn cần đóng Editor, giữ một thao tác Unity trên project tại mỗi thời điểm và soát diff sau build. Có thể thay đường dẫn executable trong các ví dụ bằng bản review này. Kết quả build/test/ảnh đã chạy của từng đợt nằm tại UI validation.

Thay `fire` bằng `airstrike` để xem Bombstorm; dùng `--preview-map 3 --preview-battlefield sandstorm` hoặc `--preview-map 4 --preview-battlefield snowstorm` để xem bão phủ toàn map. Thêm `--preview-fog` để kiểm tra Fog kỹ năng độc lập với bão. Cờ fire trên map 4/airstrike trên map 3 được chuyển sang bão tương ứng. [ArenaPreview.Setup, Animate, AnimateBattlefield](../source/client/Assets/Scripts/ArenaPreview.cs) còn hỗ trợ:

| Cờ | Nội dung mẫu |
|---|---|
| `--preview-eight` | Đội hình tám xe và palette tám người. |
| `--preview-map 2` / `3` / `4` | Chọn Tiền Đồn Xanh / Pháo Đài Cát / Sông Băng cho arena preview; xe và item mẫu trên map mới dùng điểm xuất hiện hợp lệ. Bỏ cờ hoặc ID preview không hợp lệ dùng map 2. |
| `--preview-world-items` | Đặt đủ sáu item lên map để kiểm độ rõ của hình ở kích thước nhặt; ghép `--preview-roleplay --preview-shop-items` để đối chiếu với túi. |
| `--preview-roleplay` | Quốc gia, loadout và hiệu ứng kỹ năng Q; `--preview-nation <slot>` chọn xe có trong mẫu. Ghép `--preview-eight` để kiểm tra đủ slot `0` đến `7`. |
| `--preview-afk` | Cảnh báo AFK. |
| `--preview-respawn` | Xe người chơi bị phá hủy, notice và thời gian hồi sinh. |
| `--preview-leave-confirm` | Modal xác nhận rời trận, ghép với `--arena-preview`. |
| `--preview-final-ten` | Đồng hồ trận ở mười giây cuối. |
| `--preview-battlefield fire` / `airstrike` / `sandstorm` / `snowstorm` | Các giai đoạn cảnh báo, tác động và kết thúc của từng sự kiện. |
| `--preview-fog` | Hiệu ứng tầm nhìn. |
| `--preview-battlefield-tick 20` / `270` / `480` | Cố định tick cảnh báo/giữa bão/đã kết thúc để chụp ảnh không phụ thuộc thời gian khởi động Windows. |
| `--preview-visibility` | Xe/đạn ở nhiều vị trí trên map để kiểm độ rõ qua bão; ghép tám xe/Nhập vai để kiểm hiệu ứng, thêm `--preview-fog` để kiểm Fog riêng. |

Để chụp ảnh sau một khoảng chờ:

```powershell
New-Item -ItemType Directory -Force runtime/previews | Out-Null
./build/client/TankBattle.exe --arena-preview --preview-eight --preview-battlefield fire --capture runtime/previews/fire.png --capture-delay 3
```

[CapturePreview.Start](../source/client/Assets/Scripts/CapturePreview.cs) nhận thời gian chờ từ 0–60 giây, mặc định 3 giây, chụp PNG rồi thoát ứng dụng. Khi ở cửa sổ, helper còn resize để khởi tạo lại swap chain trước khi chụp, áp lại kích thước `-screen-width`/`-screen-height` nếu có để tránh trạng thái phóng to đã nhớ, rồi ghi kích thước ảnh vào log; thời điểm ảnh thực tế muộn hơn giá trị `--capture-delay`. Chụp riêng cảnh báo, lúc tác động và sau khi hiệu ứng kết thúc, rồi mở ảnh để kiểm tra trực tiếp.

Preview menu dùng `--menu-preview <page>` với `mode-select`, `mode-select-resume`, `login`, `register`, `settings`, `create`, `profile`, `join-password`, `lobby`, `history`, `history-detail`, `afk-lobby`, `room`, `roleplay-room`, `room-settings` hoặc `results`. Các page `login-loading`, `lobby-empty`, `history-empty` dựng thêm trạng thái chờ/trống; `roleplay-preparing` dựng phòng Nhập vai đang chuẩn bị vào trận. Có thể ghép `--capture` như trên. Nguồn: [TankApp.SetupMenuPreview](../source/client/Assets/Scripts/UxPreview.cs).

Ghép `--preview-map 2|3|4` để dựng map được chọn ở `create`, `room-settings`, `room`, `roleplay-room`, danh sách phòng và `history-detail`; ví dụ `--menu-preview create --preview-map 3` hoặc `--arena-preview --preview-map 4`. Đây là lựa chọn offline, không đổi map của kết nối đang hoạt động. Các công cụ preview/QA nói trên đã bị loại khỏi source được theo dõi; hướng dẫn này chỉ còn dành cho bản QA cũ. Bản build thường hiện có `--ui-test --account <tên> --tcp 23888 --udp 23889` để tự đăng ký/đăng nhập server local rồi thao tác giao diện, xem [demo nhiều client](deployment.md#local-demo). Luồng mới không hỗ trợ `--ui-output` hoặc `--ui-auto`.

[TankApp.SetupCampaignPreview](../source/client/Assets/Scripts/TankCampaignMenus.cs) dùng cùng cờ `--menu-preview` cho Chiến dịch:

| Page | Nội dung mẫu |
|---|---|
| `campaign` | Chọn xe, chỉ số/kỹ năng và hồ sơ nhiệm vụ. |
| `campaign-play-ready` / `campaign-play-pending` | HUD hành động sẵn sàng / chờ phản hồi server. |
| `campaign-ranking`, `campaign-ranking-empty` | BXH có dữ liệu hoặc chưa có kết quả. |
| `campaign-play`, `campaign-motion`, `campaign-layout` | HUD và chiến trường mẫu. |
| `campaign-shop`, `campaign-shop-unavailable` | Shop có thể mua hoặc các lý do chưa mua được. |
| `campaign-results`, `campaign-victory`, `campaign-results-unsaved` | Kết thúc, giải cứu thành công hoặc cần thử lưu lại. |
| `campaign-loading`, `campaign-error` | Chờ phản hồi hoặc lỗi trên màn chọn xe. |
| `campaign-end` | Xác nhận kết thúc chuyến đi. |

`--campaign-preview-facing 0..3` chọn hướng xe trong mẫu; `--campaign-map-fixture <packet>` nạp fixture phản hồi Chiến dịch. Các cờ này chỉ dựng dữ liệu preview, không gửi thao tác chiến dịch lên server.

Các cờ chỉ dành cho preview: `--preview-long-text` điền tên dài/tiếng Việt và mật khẩu giả để kiểm tra che ký tự, `--preview-wide-names` dùng tên 24 ký tự M không có dấu cách, `--preview-error` hiện thông báo lỗi dài, `--preview-empty-slots` để lại ba người trong phòng tám chỗ, `--preview-two` để lại hai người trong phòng bốn chỗ, `--preview-scroll-bottom` đưa các vùng cuộn xuống cuối (kể cả viewport ngoài và nội dung Chiến dịch). Với phòng Nhập vai, `--preview-unselected` bỏ lựa chọn phe của bạn, `--preview-ready` đặt mọi người sẵn sàng, `--preview-guest` chuyển quyền chủ phòng sang slot khác; `--preview-nation <0..7>` chọn phe mẫu. Cờ nation cũng chọn xe Chiến dịch trong các mẫu có hỗ trợ (`0`, `1`, `5`). `--qa-field-focus <control-name>` dùng kiểm tra focus/caret có sẵn (ví dụ `account`, `account-password`, `server`, `display-name`, `join-password`). Chúng không thêm thao tác vào UI bình thường.

[WindowQa.Start](../source/client/Assets/Scripts/WindowQa.cs) có chế độ `--window-qa <thu-muc>` để chụp các bước 960×600, 1600×900, fullscreen và khôi phục cửa sổ, ghi `window-qa.json` rồi thoát. Đây là công cụ có sẵn, chưa được chạy trong đợt tài liệu này. Kiểm tra focus, thao tác hộp thoại và mạng thật vẫn cần quy trình thủ công.


Ảnh lịch sử của **bão toàn map v19** từng giới hạn tầm nhìn cát và chỉ kéo dài 8 giây: [Bão cát cũ](images/weather/sandstorm.png), [Bão tuyết cũ](images/weather/snowstorm.png), [cắt ranh giới tầm nhìn cũ](images/weather/sandstorm-visibility.png), [kết hợp Fog cũ](images/weather/sandstorm-fog.png). Lệnh, hash build, kết quả và giới hạn của bản v19 nằm ở testing/weather-v19; các ảnh/kết quả này không phải bằng chứng nghiệm thu bão toàn map v21 hiện tại.
