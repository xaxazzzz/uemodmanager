# Nhật ký thay đổi (Tiếng Việt)

Bản tiếng Việt của CHANGELOG.md: giữ nguyên tiêu đề, mỗi thay đổi một dòng. Phiên bản chưa dịch hiển thị bằng tiếng Anh.

## 1.6.0-manual.2 (2026-11-05)
- Bỏ qua tệp .log và .tmp trong cảnh báo tệp chưa quản lý, không xóa chúng; vẫn hiển thị mod và cấu hình chưa quản lý.

## 1.6.0-manual.1 (2026-11-05)
- Bản sửa đổi PBZ Mod Hub dựa trên 1.5.5; không phải bản phát hành UE MM chính thức.
- Quét và tiếp quản mod PAK, UE4SS, PBZ đã cài mà không di chuyển tệp game; giữ cấu hình và bản sao lưu.
- Nhận diện gói PBZModLoader, đường dẫn Mods Menu và proxy thay thế; ánh xạ thủ công gói chưa nhận diện với bản xem trước.
- Phân biệt trạng thái bật, triển khai và kết quả tải được nhật ký xác nhận.

## 1.5.5 (2026-11-04)
- Mod script: nay cài cả các tệp trong Content/JavaScript, ví dụ Mods Menu
- Nút Làm mới ở trang Mod: kiểm tra lại thư mục mod mà không cần khởi động lại
- Mod loại "Tệp game" và "Tệp cạnh exe" có hàng màu xanh, và mod mới hiện một lưu ý ngắn trước khi cài
- Cửa sổ không còn nhỏ hơn kích thước lúc khởi động

## 1.5.4 (2026-11-04)
- Khi mod đề xuất có phiên bản mới, mục Mod ở thanh bên hiện chữ CẬP NHẬT màu xanh

## 1.5.3 (2026-11-04)
- Thẻ mod đề xuất riêng ở đầu trang Mod, kiểm tra cập nhật mỗi lần khởi động
- Cài bằng một cú nhấp, đồng thời cài hoặc cập nhật UE4SS khi mod cần
- Mod đầu tiên: UE4SS C++ BYPASS giúp các mod pak dạng script hoạt động trở lại sau đợt kiểm tra script mới của game (thử nghiệm)
- Ghi chú kỹ thuật trên thẻ: cách mod hoạt động và nơi có mã nguồn

## 1.5.2 (2026-11-03)
- Dữ liệu, bản cập nhật ứng dụng và tải UE4SS lấy từ Cloudflare trước, GitHub làm dự phòng
- Ô REMOTE ở thanh bên: cho biết dữ liệu lấy từ đâu, kèm giải thích ngắn
- Nút "Thử lại" trong cửa sổ đó khi lúc khởi động không lấy được dữ liệu
- Báo cáo sự cố ghi rõ dữ liệu và kiểm tra cập nhật lấy từ đâu
- Tiếng Trung phồn thể: nút "Open exe folder" nay ghi đúng thư mục (cảm ơn CooperZhuang)

## 1.5.1 (2026-11-02)
- Liên kết "Mở thư mục" trong chi tiết mod: mở từng thư mục của mod trong game

## 1.5.0 (2026-11-02)
- Tiếng Trung phồn thể (cảm ơn CooperZhuang)
- Câu chữ tiếng Trung giản thể tự nhiên hơn (cảm ơn CooperZhuang)
- Tiếng Trung phồn thể hiển thị bằng phông chữ Đài Loan
- Nhật ký UE4SS chia thành các thẻ: UE4SS, Mod và Không tên
- Các dòng UE4SS vô hại đã biết không còn tính là lỗi
- Giữ bản của tôi: tệp mod bị sửa bên ngoài trình quản lý không còn chặn mod
- Tiêu đề các trang cùng cỡ chữ; dòng người đóng góp dài được xuống dòng

## 1.4.1 (2026-11-01)
- Liên kết Changelog trong ô cập nhật: xem thay đổi của từng phiên bản
- Tệp bị chương trình khác giữ có thông báo rõ ràng thay vì lỗi (cảm ơn CooperZhuang)

## 1.4.0 (2026-11-01)
- Tệp gán phím và cấu hình trong thư mục riêng của game (cảm ơn CooperZhuang)
- ReShade, RenoDX và các mod loader khác đặt cạnh tệp exe của game
- Giữ lại và khôi phục tệp gốc của game; không ảnh hưởng đến bản lưu
- Cài kèm tệp cấu hình .txt cạnh pak ở bất kỳ thư mục nào
- Cửa sổ cài đặt hiển thị nơi lưu tệp và tên tệp bị bỏ qua

## 1.3.2 (2026-11-01)
- Đổi tên mod đã cài: ô "Sửa" màu xanh trong phần chi tiết mod
- Đã dịch gợi ý "Không bắt buộc"

## 1.3.1 (2026-10-31)
- Mod UE4SS có Scripts/config.json hỗ trợ trình sửa cấu hình

## 1.3.0 (2026-10-31)
- Tiếng Việt
- Cảnh báo bản UE4SS không chính thức
- Tên mod vẫn hiện khi cửa sổ hẹp
- Câu chữ tiếng Trung tự nhiên hơn

## 1.2.2 (2026-10-30)
- Cài nguyên thư mục UE4SS không chứa mã để hỗ trợ trang cấu hình mod

## 1.2.1 (2026-10-30)
- Mod UE4SS có thư viện Lua dùng chung được cài đầy đủ
- Nút xem cây thư mục cho từng mod
- "Xóa sạch" xóa nhật ký UE4SS
- Hiển thị ghi chú cập nhật có định dạng

## 1.2.0 (2026-10-30)
- Chi tiết mod liệt kê toàn bộ tệp đã cài
- Bộ mod: bật cả nhóm mod bằng một cú nhấp
- Chạy thử game không có mod
- Khôi phục bản lưu từ bản sao lưu
- Trình sửa cấu hình riêng có tô màu; nhận tệp .txt cạnh pak là tệp cấu hình
- Lỗi UE4SS: thông báo khi game đóng, đánh dấu đỏ trong danh sách, tìm kiếm trong nhật ký
- Tiếng Trung giản thể (cảm ơn Null993)
- Mở lại cửa sổ ở vị trí cũ; Ctrl+F và F5

## 1.1.1 (2026-10-29)
- Cài kèm tệp cấu hình cạnh pak (.json, .ini, v.v.)
- Tên mod tải về gọn hơn

## 1.1.0 (2026-10-29)
- Bản phát hành chính thức đầu tiên
- Cài UE4SS cho Phantom Blade Zero bằng một cú nhấp
- Giữ cấu hình đã chỉnh khi cập nhật UE4SS
- Nhận diện tệp exe có tên khác, như ở bản game từ cửa hàng khác

## 1.0.1 (2026-10-29)
- Hỗ trợ thư mục bản game chính thức (Steam)
- Sao lưu bản lưu
- "Tìm thư mục game" chỉ nhận thư mục của game

## 1.0.0 (2026-10-28)
- Bản thử nghiệm đầu tiên
