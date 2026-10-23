# Yêu cầu
* Bổ sung thêm chế độ chơi cốt truyện (chiến dịch) vào trong game theo các nội dung được mô tả bên dưới
* Nội dung được lấy cảm hứng từ game OneBit Adventure
* Chế độ chơi này cần thiết kế độc lập với các chế độ chơi trước đó (coi như là 1 module riêng, lắp vào là chạy, tránh xung đột). Chỉ sử dụng dung cơ sở dữ liệu để lưu dữ liệu

# 1. Cốt truyện
* Tổng thống Đô-na-tri và gia đình đã bị bọn khủng bố vu-a-lo bắt cóc khi đang trên đường đi nghỉ mát
* Là 1 chỉ huy xe tăng, tiến hàng oanh tạc căn cứ kẻ thù để giải cứu ngài tổng thống

# 2. Lối chơi/thể loại
* Game 2D topdown, 1 người, sử dụng đồ họa xe tăng gốc
* Game vượt ải, vô hạn, giải cứu mục tiêu
* Lối chơi làm theo game OneBit Adventure nhưng đổi đồ họa thành xe tăng và kẻ thù

# 3. Story Board
* Tham khảo game OneBit Adventure

# 4. Quá trình vào chế độ game
* 1. Ở giao diện khi mới vào game có thêm nút chọn "chiến dịch"
* 2. Bấm vào nút chiến dịch
* 3. Giao diện bảng chiến dịch hiện ra gồm 3 ô chọn xe tăng xếp theo hàng ngang (tận dụng lại 3 xe tăng của 3 quốc gia có kỹ năng tấn công chủ động gây sát thương), mỗi ô bao gồm ảnh xe tăng, thông tin cơ bản và kỹ năng. Bên dưới là nút vào game
* 4. Bấm chọn xe tăng 1
* 5. Ô xe tăng được highlight
* 6. Bấm nút vào game
* 7. Hiển thị giao diện game chế độ chiến dịch

# Cơ chế điều khiển
* Tương tự game OneBit Adventure, mỗi khi người chơi bấm phím di chuyển theo 4 hướng (WASD hoặc mũi tên điều khiển) thì sẽ di chuyển 1 ô (1bit) và kẻ thù cũng di chuyển theo (hướng về phía người chơi)
* Di chuyển để tấn công cơ bản (giống game Onebit Adventure)
* Người chơi bấm I để bắn đạn (gây sát thương gấp đôi tấn công cơ bản lên kẻ địch đứng trước hướng di chuyển hiện tại 2 ô) (10 lượt di chuyển để hồi chiêu)
* Người chơi bấm J để dùng kỹ năng độc nhất (kỹ năng tự động dùng lên mục tiêu - người chơi không cần thao tác chọn mục tiêu, 50 lượt di chuyển để hồi chiêu)

# Vòng lặp gameplay
* 1. Người chơi quan sát
* 2. 1 HÀNH ĐỘNG CỦA NGƯỜI CHƠI (Bước đi / Bắn I / Dùng J)
* 3. KẺ THÙ PHẢN ỨNG (Di chuyển 1 ô / Tấn công lại) 
* 4. TÍNH TOÁN LƯỢT: Giảm hồi chiêu I/J, nhặt vật phẩm, đạn
* 5. Nếu hết máu -> Game over, tổng kết điểm (khoảng cách đã đi được tính theo trục dọc (mỗi ô là 1m))
* 6. Nếu tiêu diệt hết địch -> lặp lại bước 1
* 7. Cứ 500m là có 1 boss (trùm khủng bố)

# Mục tiêu
* 1. Đánh bại kẻ thù để rơi ra item 
    + item tăng dame: gấp đôi game gây ra trên kẻ thù trong 3 đòn tấn công cơ bản tiếp theo (tấn công bằng cách di chuyển)
    + item hồi máu: hồi ngay lập tức 25 % máu
    + item khiên: khiên có tác dụng trong phạm vi chặn 3 đòn tấn công
* 2. Mở rương để nhận vàng
    + Vàng được tổng kết mỗi khi gameover (hoặc phá đảo game)
    + Vàng kiếm được được cộng vào tổng vàng
    + Tổng vàng lưu vào cơ sở dữ liệu (để sử dụng sau)
* 3. Đi càng xa điểm cảng cao
    + Điểm được lưu vào bảng xếp hạng người chơi
    + Xem bảng xếp hạng bằng cách click vào nút (bảng xếp hạng chiến dịch) ở giao diện khi mới mở game (cùng giao diện với nút chọn chiến dịch)
    + Thông tin được hiển thị trên bảng xếp hạng là số thứ tự, tên người chơi, điểm số
* 4. Người chơi đạt đến khoảng cách 10000 m
    + Đánh bại boss, giải cứu thành công ngài tổng thống

# Xử lý kỹ thuật
* Tận dụng cơ chế lặp map, tránh tạo nhiều map (ví dụ tạo 3-5 map có thể kết nối với nhau rồi lặp lại ngẫu nhiên -> tưởng như vô hạn)
* Làm tương tự với boss
* Lựa chọn xử lý kỹ thuật phù hợp với 1 chức năng phụ bản của game chính
* Độ khó tăng dần theo khoảng cách (mật độ của kẻ thù tăng lên, ...)


