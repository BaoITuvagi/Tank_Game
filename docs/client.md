# Client Unity, giao diện và tài nguyên

Tài liệu mô tả client trong working tree dựa trên HEAD `e788610bdb0e2cd9fbd3f0cce54d9d29e815947e` và các thay đổi chưa commit. Menu và HUD được chỉnh ngày **2026-10-25–26** theo yêu cầu bổ sung sau [plan_UI](../plan/plan_UI.md) và đợt đồng bộ Multiplayer/Chiến dịch; phạm vi và bằng chứng kiểm tra ở [UI validation](ui-validation.md). Các phần gameplay giữ mốc đối chiếu 2026-10-23.

Mục lục:

- [Mở project và bootstrap](#bootstrap)
- [Giao diện và render arena](#ui)
- [Tài nguyên, importer và giấy phép](#assets)
- [Khôi phục Combat FX](#restore-combat-fx)
- [Preview và chụp ảnh](#previews)

Hướng dẫn [build và kết nối](deployment.md#build-client), [điều khiển](gameplay.md#controls), [prediction và reconciliation](protocol.md#prediction), cùng [kiểm thử](testing.md#matrix) có tài liệu riêng.

<a id="bootstrap"></a>
## Mở project và bootstrap

Dùng Unity **6000.3.24f1**, theo [ProjectVersion.txt](../source/client/ProjectSettings/ProjectVersion.txt). Trong Unity Hub, thêm thư mục `source/client`, mở project rồi mở `Assets/Scenes/Main.unity` và bấm Play. Các điều kiện cài Editor, license và Windows Build Support nằm trong [README](../README.md); hãy đóng Editor trước khi chạy script build/test cho cùng project.

[TankApp.Boot, Awake, Update, OnGUI](../source/client/Assets/Scripts/TankApp.cs) tạo đối tượng `Tank Battle` ở runtime sau khi scene tải, nếu chưa có `TankApp` và không bật smoke harness. Scene không cần chứa sẵn menu hoặc arena. Repository hiện không có prefab; giữ cách bootstrap này khi sửa client.

`TankApp.Awake` tạo thư mục dữ liệu riêng của ứng dụng, đặt chạy nền và mục tiêu 60 FPS, tạo `ClientCore` qua [MapLoader.CreateClient](../source/client/Assets/Scripts/MapLoader.cs), gắn sự kiện snapshot cho lớp hiệu ứng rồi đọc cấu hình kết nối. `MapLoader` đọc map và hash từ StreamingAssets, kiểm tra sprite cần cho Nhập vai, preload Combat FX và mask xe. Luồng vòng đời client/server nằm tại [kiến trúc khởi động](architecture.md#startup).

Client cố định server `13.75.2.41`, TCP `8888`, UDP `8889` bằng các hằng trong [TankApp](../source/client/Assets/Scripts/TankApp.cs). Đăng nhập và đăng ký luôn dùng địa chỉ này. Đã bỏ nút/màn hình Kết nối và file cấu hình StreamingAssets; `connection.json` từng lưu trên máy không còn được đọc. Muốn đổi endpoint phải sửa hằng và build lại client. Smoke/test harness vẫn có endpoint riêng để kiểm thử.

<a id="ui"></a>
## Giao diện và render arena

Menu và arena dùng **IMGUI**. [TankApp.OnGUI, Arena](../source/client/Assets/Scripts/TankApp.cs) chọn màn hình theo phase của client; [TankApp.DrawMenus, DrawDialogs](../source/client/Assets/Scripts/TankMenus.cs) phụ trách đăng nhập/đăng ký, lobby, phòng, lịch sử, kết quả và hộp thoại. Bộ chọn quốc gia của Nhập vai ở [TankApp.NationSelector](../source/client/Assets/Scripts/TankRoleplayMenus.cs).

Form đăng ký có ô **Nhập lại mật khẩu**, được che ký tự như ô mật khẩu. Client kiểm tra hai giá trị trùng khớp trước khi kết nối/gửi yêu cầu đăng ký; ô xác nhận không được gửi lên server. Đăng nhập không yêu cầu xác nhận mật khẩu.

Header sau đăng nhập hiển thị huy hiệu vàng sát bên trái ô tên người chơi, gồm icon đồng xu, số dư và nút **+**, tải sau login và làm mới định kỳ. Hai thông báo đều có tiêu đề **Vàng**: bấm số vàng hiện “Vàng dùng để mua vật phẩm trong cửa hàng ở chế độ nhập vai”; bấm + hiện “Vàng có thể được tìm thêm khi tham gia chế độ chiến trường. Vàng còn lại sau mỗi chuyến đi sẽ được cộng dồn vào tài khoản”. Thông báo có nút Đóng và khóa input khi mở. [ClientCore.Shop](../source/client/Assets/Scripts/ClientCore.Shop.cs) nhận ví/giới hạn mua/túi item từ server; [TankRoleplayShop](../source/client/Assets/Scripts/TankRoleplayShop.cs) mở modal bằng E trong đợt RP, hiển thị 6 loại, giá, trạng thái đã mua và đồng hồ đóng shop. Nút chỉ ghi **Đóng**; E/Esc cũng đóng, hết đợt tự đóng. Modal khóa input chiến đấu và tự bắn, không tạm dừng trận. Mỗi đợt vật phẩm Nhập vai mở cửa hàng sẽ hiện thông báo vàng “Cửa hàng đã mở, bạn có thể mua thêm vật phẩm ở cửa hàng” trong 2 giây, một lần mỗi đợt theo tick server; khung vừa chiều rộng sidebar và chữ xuống dòng, không khóa input. Hướng dẫn E cùng thời gian chờ/còn mở nằm trong sidebar cạnh hướng dẫn WASD/Shift/chuột, không có ô nổi trên map. Icon item đã mua nằm trong ô nền tối ở sidebar, không có nền nút tròn vàng và không vẽ nhãn số lượng trên icon. Bấm icon để dùng một item khi xe sống, kể cả khi shop đóng. Luật chi tiết ở [gameplay/items](gameplay.md#items).

Preview offline: `--arena-preview --preview-roleplay --preview-shop-items` dựng túi/ví mẫu và đợt đang mở; thêm `--preview-open-shop` để xem modal. Thêm `--preview-shop-opening` để chuyển từ chờ sang mở cửa hàng ở giây thứ 4 của preview, kiểm tra thông báo bằng `--capture-delay 3` và trạng thái đã biến mất bằng `--capture-delay 6`. `--preview-wallet-info`/`--preview-wallet-add` dựng hai thông báo vàng, ghép với arena hoặc `--menu-preview lobby`. Có thể dùng `--capture` để kiểm ảnh ở nhiều kích thước; các cờ preview không kết nối hoặc ghi dữ liệu tài khoản.

Chiến dịch dùng [CampaignUI](../source/client/Assets/Scripts/CampaignUI.cs) làm lớp style theo ngữ nghĩa màn hình trên `CartoonUi`: cùng font, màu, nút và khung với Multiplayer; khung nhiệm vụ/kỹ năng giữ màu sand. `TankApp.OnGUI` vẽ Chiến dịch trước khi mở viewport cuộn của menu; các nút Chiến dịch/BXH nằm trong vùng cuộn thao tác của sảnh. Hướng dẫn chơi ở [README](../README.md#chế-độ-chiến-dịch).

[CartoonUi](../source/client/Assets/Scripts/CartoonUi.cs) dùng giao diện cuối của nhánh Bao tại commit `97c6a46`: nền xanh tối `#121C19`, panel xanh quân đội `#344237`, panel tối `#202B26`, chữ tối Ink `#18241D` hoặc chữ kem; nút chính vàng `#FFC34B`, nút phụ kem `#F2E7CA`, ready/lựa chọn xanh `#9AC96C`, rời/kick cam `#E88C65`, nhiệm vụ sand `#D8C191`, trạng thái không khả dụng `#84907E`. Panel, thẻ và ô nhập phẳng, cắt góc theo hình tấm giáp; viền xanh `#62705A` được pha theo màu mặt, viền nút dùng Ink. Nút có bóng đổ nhẹ, giảm khi nhấn; focus/lựa chọn làm ấm màu mặt nút. Hai nút tên người chơi/Đăng xuất dùng chung chiều cao đo theo tên và cùng tọa độ dọc, căn giữa header 68 px trong trận hoặc 76 px ở menu. Texture được tạo/cache theo màu, tham số góc và trạng thái. Nút có trạng thái hover, active, focus và lựa chọn; trạng thái chờ có dấu chấm chuyển động. Icon HP/giáp/sát thương/tầm bắn/thời gian/người chơi/khóa/kỹ năng, badge, huy chương và thanh tiến độ dùng chung; portrait tái sử dụng body/barrel và palette xe hiện có. Toàn UI không hiện ô tooltip đi theo chuột.

[MenuTheme.Init](../source/client/Assets/Scripts/MenuTheme.cs) dùng **Bungee Regular** cho tiêu đề, **Nunito Bold** cho nút/nội dung/nhập liệu; cả hai có tiếng Việt và giấy phép OFL. Đăng nhập, sảnh và tạo/cài đặt phòng dùng ảnh xe tăng `ScaleToFit` để giữ toàn bộ ảnh, với lớp phủ xanh tối chuyển dần từ trái sang phải; logo cũng dùng `ScaleToFit`, giữ alpha/màu gốc. Header đăng nhập có “SẴN SÀNG XUẤT KÍCH”, form có “TRỞ LẠI CHIẾN TRƯỜNG”. Phòng chờ, lịch sử và kết quả dùng nền xanh tối có dấu xích xe nhẹ phía sau dữ liệu.

[MenuLayout.Fit](../source/client/Assets/Scripts/MenuLayout.cs) bố trí logo trái/form phải từ 1100 px, căn giữa form khi hẹp hơn; viewport dưới 720×600 có cuộn ngoài. Form phòng tối đa 920×560, chia hai cột khi panel đủ 840 px; các nút Xác nhận/Hủy ghim ở cuối panel. Sảnh dùng thẻ phòng có portrait, số người, khóa và badge phân biệt chờ/đang đấu/đầy/có thể vào; trạng thái trống có lời dẫn và hành động. Phòng chờ dùng thẻ người chơi với xe, tên, ready, phe và chủ phòng; roster một hoặc hai cột và cuộn riêng. Tên và mô tả được đo theo font/chiều rộng thực để xuống dòng và tăng chiều cao thẻ; các nhãn nút dài cũng được tính chiều cao. Phòng Nhập vai có thẻ chọn phe hai cột cao 108 px, gồm xe, quốc gia, kỹ năng Q và trạng thái đang chọn/đã có người; đặt cạnh roster khi đủ rộng hoặc trong vùng cuộn roster khi hẹp. Mô tả kỹ năng nằm ở cột thông tin, ready/start/cài đặt/rời phòng ghim phía dưới. Chữ không thu nhỏ toàn UI; nút thường cao tối thiểu 52 px. Giữ handler, validation, tên control và quyền chủ phòng.

[Kết quả và lịch sử](../source/client/Assets/Scripts/TankMenus.cs) dùng huy chương, portrait và thẻ thành tích. Podium đặt hạng 1 ở giữa, cao hơn hạng 2/3; danh sách đầy đủ cuộn riêng, hàng của người chơi dùng mint. Nút Về phòng/Về sảnh luôn ở dưới. Tên dài được đo và xuống dòng; danh sách thành tích và nội dung chi tiết dài có vùng cuộn. Trạng thái trống, chờ tải, lỗi kết nối và xác nhận có cùng style.

[Màn Chiến dịch](../source/client/Assets/Scripts/TankCampaignScreens.cs) dùng ba thẻ xe Mỹ/Liên Xô/Ý từ chiều rộng 1000 px; chiều cao thẻ được đo theo nội dung, tối thiểu 416 px, có portrait lớn, HP/giáp/sát thương/tầm bắn, kỹ năng và badge lựa chọn. Hồ sơ nhiệm vụ sand và hướng dẫn nằm dưới thẻ; cửa sổ hẹp chuyển sang một cột cuộn, các nút Bắt đầu/BXH/Tiếp tục vẫn nằm ở chân màn. Shop có icon vật phẩm, giá và lý do chưa mua được. BXH phân biệt các hạng đầu và người chơi hiện tại; kết quả có tiến độ, số địch, vàng, trạng thái lưu và nút thử lưu lại khi cần. Bắt đầu bị khóa khi còn chuyến đi hoặc kết quả chưa lưu, đồng thời hiện hành động Tiếp tục/Xem kết quả.

[ArenaTheme](../source/client/Assets/Scripts/ArenaTheme.cs) dùng cùng font, palette và khung cắt góc với menu cho header/HUD, đồng hồ, bảng hạng, Q, thông báo AFK/hồi sinh và banner battlefield. HP và năng lượng giữ ở thanh trên xe trong map, không lặp lại thành khối trong sidebar. Sidebar dành cho thời gian, phe, item, bảng hạng và kỹ năng; trạng thái chậm tên lửa ghi số lần trúng, mức giảm tốc nhân dồn và thời gian còn lại đến hết AREA; bảng hạng nhấn hàng của bạn, đo chiều cao theo tên xuống dòng và cuộn riêng. Nút Q dùng Nunito, đo chiều cao nội dung; phần trạng thái dài có vùng cuộn riêng. Ping/FPS là dòng chữ nhỏ gọn trong header, không có panel bao quanh; AFK dùng cảnh báo có đếm ngược. Rời trận mở modal xác nhận, khóa input chiến đấu khi modal hiện. HUD không dùng ảnh nền minh họa; vẫn giữ map, màu xe/hiệu ứng/cảnh báo và toàn bộ tọa độ ngắm. Font mặc định được giữ riêng cho ký hiệu nhỏ không có trong font mới. Phạm vi HUD/font này được người dùng yêu cầu bổ sung, thay thế giới hạn giữ style trận/font cũ trong plan ban đầu.

[HUD Chiến dịch](../source/client/Assets/Scripts/TankCampaignHud.cs) giữ vùng header 100 px và footer 96 px, không đổi phép bố trí map. Header tách xe/HP/giáp, tiến độ/trùm và số địch; hotbar I/O/P gồm icon, phím, trạng thái sẵn sàng/chờ/hồi theo lượt và thanh hồi chiêu. Chữ/icon sáng trên ô không khả dụng để giữ tương phản. Kết thúc chuyến đi mở xác nhận và tạm khóa input; Esc hoặc Tiếp tục đóng xác nhận. Notice thường dùng sand, mua/lưu thành công mint, lỗi coral; notice chiến đấu đặt ở lề trái nếu đủ chỗ, thông báo thường hết sau 3,5 giây từ lượt, lỗi không tự hết giờ. Các lệnh, chỉ số và kết quả vẫn do server quyết định.

[ArenaLayout.Fit](../source/client/Assets/Scripts/ArenaLayout.cs) giữ tỷ lệ map trong phần màn hình còn lại, với sidebar rộng **294 px** sát mép phải. [ArenaRenderer.Draw, Panel, NetworkStatus](../source/client/Assets/Scripts/ArenaRenderer.cs) vẽ địa hình, xe, đạn, HUD và trạng thái mạng. Thanh máu trên xe biểu diễn độ đầy, không ghi số HP. Sau khi resize hoặc đổi fullscreen, cần kiểm tra lại ngắm theo chuột, map và sidebar; test có sẵn là [CombatTests.RectangularArenaFitsWithoutOverlappingTheRightAnchoredSidebarAndAimStaysCorrect](../source/client/Assets/Tests/Editor/CombatTests.cs) và [ProtocolTests.AimTracksCursorAfterResizeAndOnEitherMap](../source/client/Assets/Tests/Editor/ProtocolTests.cs).

[TankApp.Update](../source/client/Assets/Scripts/TankApp.cs) chỉ lấy điều khiển gameplay khi cửa sổ đang được focus và không mở hộp thoại. Khi mất focus hoặc rời trận, chế độ tự bắn được reset; [PlayerRecoveryTests.AutoFireTogglesAndResetsOnFocusLossAndMatchExit](../source/client/Assets/Tests/Editor/PlayerRecoveryTests.cs) mô tả hành vi này. Phím và thao tác đầy đủ nằm tại [điều khiển](gameplay.md#controls).

Hiệu ứng chiến đấu được nhận từ snapshot trong [ArenaRenderer.AcceptEffects](../source/client/Assets/Scripts/CombatRendering.cs), quản lý tuổi và chống phát lại trong [CombatPresentation.Accept, Update](../source/client/Assets/Scripts/CombatPresentation.cs). Red Zone/Bombstorm có dải thông báo tên và thời gian ở phía trên; vùng cảnh báo trên map dùng nền gạch chéo đỏ, viền vàng, tâm ngắm và hiệu ứng Cozy Chunky, không đặt chữ hoặc đồng hồ lên từng vùng. Lửa/khói và vụ nổ chịu giới hạn tầm nhìn; HUD không tiết lộ vị trí vùng bị sương mù che. Xem [ArenaRenderer.DrawBattlefieldWarning, DrawBattlefield, DrawBattlefieldNotice](../source/client/Assets/Scripts/BattlefieldPresentation.cs), [luật battlefield](gameplay.md#battlefield) và [kiểm tra hình ảnh thủ công](testing.md#manual).

<a id="assets"></a>
## Tài nguyên, importer và giấy phép

| Tài nguyên | Cách dùng và yêu cầu bảo toàn |
|---|---|
| `Assets/Resources/ArenaArt/` | Texture được nạp bằng tên qua `Resources.Load`, ví dụ trong [ArenaRenderer.Art](../source/client/Assets/Scripts/ArenaRenderer.cs). Giữ tên/đường dẫn, `.meta` và GUID để bảo toàn tham chiếu. |
| `Assets/Resources/MenuArt/` | Bản sao nguyên byte `logo.png`, `anh_nen.png` và bộ thành phần Chequered Ink — Cartoon UI Pack đã import; checkbox/thanh cuộn vẫn dùng pack. Nút/panel dùng texture hình học của CartoonUi. Giữ [giấy phép riêng](../source/client/Assets/Resources/MenuArt/ChequeredInk-LICENSE.txt). Không dùng ảnh tham khảo Goose Goose Duck trong game. |
| `Assets/Resources/MenuArt/Fonts/` | Hai TTF static Bungee Regular và Nunito Bold; giữ `Bungee-OFL.txt`, `Nunito-OFL.txt`, `NOTICE.txt` với nguồn/hash/cách tạo instance Nunito. Không cần cài font trên máy người chơi. |
| `Assets/Resources/CombatFx/CozyChunky/` | Đã kèm **21 clip Cooked FX — Cozy Chunky**, PNG, `catalog.json`, `LICENSE.pdf` và `.meta`; clone đủ repository là Unity có thể import ngay, không cần chọn ZIP trước khi Play, build hoặc test. |
| `Assets/StreamingAssets/` | Chứa dữ liệu map và cấu hình kết nối mặc định. Map dùng chung với server; SHA-256 tính trên **byte gốc**, nên đổi khoảng trắng cũng đổi hash. Chi tiết tại [map](gameplay.md#maps) và [shared contracts](protocol.md#shared-contracts). |

[ArenaArtImporter.OnPreprocessTexture](../source/client/Assets/Editor/ArenaArtImporter.cs) áp dụng texture Default, alpha transparency, Read/Write bật, không mipmap, không nén, Bilinear, kích thước tối đa 256 và không scale NPOT. Sprite cần đọc pixel cho mask; giữ các thiết lập này khi thay hình.

[MenuArtImporter](../source/client/Assets/Editor/MenuArtImporter.cs) chỉ áp dụng cho PNG trong MenuArt: alpha từ ảnh, không mipmap/nén/scale NPOT, Bilinear, Clamp. Hai ảnh cung cấp giữ tối đa 2048 và không Read/Write; các thành phần UI PNG giữ cấu hình 64/128/256 hiện có. `CartoonUi` cache texture 72×72 cắt góc; `MenuTheme.Init`/`ArenaTheme.Init` cache style/font/asset. Không tạo hay nạp texture lại mỗi lần OnGUI. `MenuArtBuildLicense` chép license pack và hai OFL/notice font sang `ThirdParty/` cạnh bản chạy. Importer ArenaArt/Combat FX không đổi.

[CombatFxImporter.OnPreprocessTexture](../source/client/Assets/Editor/CombatFxImporter.cs) áp dụng alpha từ ảnh, không Read/Write, không mipmap, sRGB, không nén, RGBA32, Bilinear, Clamp, không scale NPOT và tối đa 4096. Không áp dụng nhầm thiết lập ArenaArt cho Combat FX. [CombatFxCatalog.Preload, Validate](../source/client/Assets/Scripts/CombatFxCatalog.cs) kiểm tra manifest, số frame, kích thước, định dạng và ngân sách texture 32 MiB. Tài nguyên thiếu/sai chặn build qua `CombatFxBuildValidation.OnPreprocessBuild`, và chặn chuẩn bị vào trận qua [ClientCore.Handle (Op.Prepare)](../source/client/Assets/Scripts/ClientCore.Messages.cs).

Hình ảnh Kenney dùng **CC0**; giữ [Kenney-LICENSE.txt](../source/client/Assets/Resources/ArenaArt/Kenney-LICENSE.txt). Combat FX có [LICENSE.pdf riêng](../source/client/Assets/Resources/CombatFx/CozyChunky/LICENSE.pdf), không gộp giấy phép này thành CC0. [CombatFxBuildValidation.OnPostprocessBuild](../source/client/Assets/Editor/CombatFxImporter.cs) tự chép giấy phép Combat FX thành `ThirdParty/CookedFX-LICENSE.pdf` cạnh bản chạy. Khi phân phối, giữ toàn bộ thư mục client và giấy phép đi kèm; xem [build client](deployment.md#build-client).

Khi cập nhật asset, đưa cả `.meta` đi kèm vào thay đổi; không xóa `.meta` để ép import lại. Test hiện có gồm [CombatAssetTests.ImportedSheetsMeetCatalogAndBuildValidation](../source/client/Assets/Tests/Editor/CombatAssetTests.cs), `EveryMissingSheetAndWrongDimensionsFailWithTheClipName` và `InvalidArchiveIsRejectedBeforeAnyAssetIsWrittenAndIndexedPngAlphaIsAccepted`. Sự hiện diện của test không chứng minh chúng đã pass trên working tree hiện tại.

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
5. Đóng Editor trước khi chạy lại [build/test bằng script](testing.md#commands).

Đối chiếu: [CombatFxImporter.ChooseArchive, ImportArchive, ValidateAssets](../source/client/Assets/Editor/CombatFxImporter.cs). Importer kiểm tra hash và toàn bộ entry được chọn trước khi ghi asset, rồi refresh và validate trong Unity. Có entry point `ImportFromEnvironment` đọc `TANK_COMBAT_FX_ZIP` cho công cụ tự động, nhưng luồng Play/build/test thông thường không cần biến này.

<a id="previews"></a>
## Preview và chụp ảnh

Preview dựng dữ liệu mẫu để kiểm tra hình ảnh offline. Nó không xác nhận kết nối multiplayer, sát thương server hoặc persistence. Dùng bản client đã build theo [triển khai](deployment.md#build-client); chạy từ PowerShell ở gốc repository:

```powershell
./build/client/TankBattle.exe --arena-preview --preview-eight --preview-battlefield fire
```

Để tạo bản review riêng, [BuildClient.BuildUiReview](../source/client/Assets/Editor/BuildClient.cs) build scene `Assets/Scenes/Main.unity` hiện có ra `build/client-ui/TankBattle.exe`. Entry này không tạo lại scene hay gán PlayerSettings như `BuildClient.Build`; vẫn cần đóng Editor, giữ một thao tác Unity trên project tại mỗi thời điểm và soát diff sau build. Có thể thay đường dẫn executable trong các ví dụ bằng bản review này. Kết quả build/test/ảnh đã chạy của từng đợt nằm tại [UI validation](ui-validation.md).

Thay `fire` bằng `airstrike` để xem Bombstorm; thêm `--preview-fog` để kiểm tra phần bị che tầm nhìn. [ArenaPreview.Setup, Animate, AnimateBattlefield](../source/client/Assets/Scripts/ArenaPreview.cs) còn hỗ trợ:

| Cờ | Nội dung mẫu |
|---|---|
| `--preview-eight` | Đội hình tám xe và palette tám người. |
| `--preview-roleplay` | Quốc gia, loadout và hiệu ứng kỹ năng Q; `--preview-nation <slot>` chọn xe có trong mẫu. Ghép `--preview-eight` để kiểm tra đủ slot `0` đến `7`. |
| `--preview-afk` | Cảnh báo AFK. |
| `--preview-respawn` | Xe người chơi bị phá hủy, notice và thời gian hồi sinh. |
| `--preview-leave-confirm` | Modal xác nhận rời trận, ghép với `--arena-preview`. |
| `--preview-final-ten` | Đồng hồ trận ở mười giây cuối. |
| `--preview-battlefield fire` / `airstrike` | Các giai đoạn cảnh báo, tác động và kết thúc của từng sự kiện. |
| `--preview-fog` | Hiệu ứng tầm nhìn. |

Để chụp ảnh sau một khoảng chờ:

```powershell
New-Item -ItemType Directory -Force runtime/previews | Out-Null
./build/client/TankBattle.exe --arena-preview --preview-eight --preview-battlefield fire --capture runtime/previews/fire.png --capture-delay 3
```

[CapturePreview.Start](../source/client/Assets/Scripts/CapturePreview.cs) nhận thời gian chờ từ 0–60 giây, mặc định 3 giây, chụp PNG rồi thoát ứng dụng. Khi ở cửa sổ, helper còn resize để khởi tạo lại swap chain trước khi chụp; thời điểm ảnh thực tế muộn hơn giá trị `--capture-delay`. Chụp riêng cảnh báo, lúc tác động và sau khi hiệu ứng kết thúc, rồi mở ảnh để kiểm tra trực tiếp.

Preview menu dùng `--menu-preview <page>` với `login`, `register`, `settings`, `create`, `profile`, `join-password`, `lobby`, `history`, `history-detail`, `afk-lobby`, `room`, `roleplay-room`, `room-settings` hoặc `results`. Các page `login-loading`, `lobby-empty`, `history-empty` dựng thêm trạng thái chờ/trống; `roleplay-preparing` dựng phòng Nhập vai đang chuẩn bị vào trận. Có thể ghép `--capture` như trên. Nguồn: [TankApp.SetupMenuPreview](../source/client/Assets/Scripts/UxPreview.cs).

[TankApp.SetupCampaignPreview](../source/client/Assets/Scripts/TankCampaignMenus.cs) dùng cùng cờ `--menu-preview` cho Chiến dịch:

| Page | Nội dung mẫu |
|---|---|
| `campaign` | Chọn xe, chỉ số/kỹ năng và hồ sơ nhiệm vụ. |
| `campaign-ranking`, `campaign-ranking-empty` | BXH có dữ liệu hoặc chưa có kết quả. |
| `campaign-play`, `campaign-motion`, `campaign-layout` | HUD và chiến trường mẫu. |
| `campaign-shop`, `campaign-shop-unavailable` | Shop có thể mua hoặc các lý do chưa mua được. |
| `campaign-results`, `campaign-victory`, `campaign-results-unsaved` | Kết thúc, giải cứu thành công hoặc cần thử lưu lại. |
| `campaign-loading`, `campaign-error` | Chờ phản hồi hoặc lỗi trên màn chọn xe. |
| `campaign-end` | Xác nhận kết thúc chuyến đi. |

`--campaign-preview-facing 0..3` chọn hướng xe trong mẫu; `--campaign-map-fixture <packet>` nạp fixture phản hồi Chiến dịch. Các cờ này chỉ dựng dữ liệu preview, không gửi thao tác chiến dịch lên server.

Các cờ chỉ dành cho preview: `--preview-long-text` điền tên dài/tiếng Việt và mật khẩu giả để kiểm tra che ký tự, `--preview-wide-names` dùng tên 24 ký tự M không có dấu cách, `--preview-error` hiện thông báo lỗi dài, `--preview-empty-slots` để lại ba người trong phòng tám chỗ, `--preview-two` để lại hai người trong phòng bốn chỗ, `--preview-scroll-bottom` đưa các vùng cuộn xuống cuối (kể cả viewport ngoài và nội dung Chiến dịch). Với phòng Nhập vai, `--preview-unselected` bỏ lựa chọn phe của bạn, `--preview-ready` đặt mọi người sẵn sàng, `--preview-guest` chuyển quyền chủ phòng sang slot khác; `--preview-nation <0..7>` chọn phe mẫu. Cờ nation cũng chọn xe Chiến dịch trong các mẫu có hỗ trợ (`0`, `1`, `5`). `--qa-field-focus <control-name>` dùng kiểm tra focus/caret có sẵn (ví dụ `account`, `account-password`, `server`, `display-name`, `join-password`). Chúng không thêm thao tác vào UI bình thường.

[WindowQa.Start](../source/client/Assets/Scripts/WindowQa.cs) có chế độ `--window-qa <thu-muc>` để chụp các bước 960×600, 1600×900, fullscreen và khôi phục cửa sổ, ghi `window-qa.json` rồi thoát. Đây là công cụ có sẵn, chưa được chạy trong đợt tài liệu này. Kiểm tra focus, thao tác hộp thoại và mạng thật vẫn cần [quy trình thủ công](testing.md#manual).
