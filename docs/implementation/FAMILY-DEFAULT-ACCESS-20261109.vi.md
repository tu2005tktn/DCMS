# Xem hồ sơ người thân mặc định — 09/11/2026

Triển khai theo mục bổ sung trong `../specs/FAMILY-DELEGATED-RECORD-ACCESS.vi.md`. Thay bước xin/duyệt riêng cho lịch đặt hộ đã được tiếp nhận đúng Patient; nhánh thủ công vẫn dành cho hồ sơ cũ không có liên kết tiếp nhận phù hợp.

- `FamilyDefaultAccess.cs` kiểm quan hệ owner → FamilyMember → Appointment → Visit → Patient, tên/ngày sinh snapshot và chi nhánh, nhân viên ghi nhận, Patient chưa lưu trữ và Visit chưa hủy. Không chọn theo tên/SĐT đơn lẻ, không tạo Patient/tài khoản B, không tự chọn khi nhiều Patient phù hợp.
- Mở overview hoặc đọc trực tiếp header/section/file đều đi qua kiểm tra mặc định. Transaction khóa owner 90121, đọc lại sau khóa, tạo một quyền hoặc nâng Pending hợp lệ thành Verified; nguồn `DefaultFamilyBooking`, đủ bốn scope chính thức, một chi nhánh, thời hạn 365 ngày và audit nguồn Appointment/Visit. Không ghi giả bằng chứng đồng ý. Không thêm bảng/migration hoặc backfill hàng loạt.
- Bất kỳ quyết định đã có (Verified kể cả hết hạn, Revoked/Withdrawn/Rejected/Expired) đều được giữ. Không mở rộng scope thủ công hoặc cấp lại sau chấm dứt; quyền bị thu hồi không sống lại khi tải lại, gọi API trực tiếp hay gửi Pending mới. Sửa định danh/xóa vẫn chấm dứt quyền theo giao dịch hiện có.
- Web tự mở tab Lịch sử khám khi có quyền. Chưa tiếp nhận thì hiện chưa có hồ sơ liên kết và lịch đặt hộ; form liên kết hồ sơ cũ nằm trong mục mở rộng. Tải lại/focus/poll và dọn dữ liệu khi thu hồi/hết hạn tiếp tục hoạt động.

## Kiểm chứng

- .NET 10 restore/build và web production build: đạt. Cảnh báo Pkcs, tham số chưa dùng, SignalR annotation/bundle size đã có vẫn còn.
- API/PostgreSQL cách ly: **37/37 đạt**, gồm quyền mặc định trực tiếp/overview/concurrent, Pending, thu hồi/token cũ, giữ scope thủ công, sai owner/snapshot/Patient/branch, chưa tiếp nhận/hủy, archived, nhiều Patient, thay định danh, self-link độc lập và các hồi quy người thân/booking.
- Browser mock API: **9/9 đạt**, gồm mở thẳng lịch sử khám không xin quyền, trạng thái chưa tiếp nhận và CRUD/booking/responsive hiện có.
- Browser với API/PostgreSQL thật: **1/1 đạt**. Đăng ký A, thêm B không có tài khoản, đặt lịch qua UI; tạo Patient/check-in/phiếu khám chính thức qua API; mở hồ sơ qua UI thấy ngay nội dung mà không gửi/duyệt yêu cầu; nhân viên xem và thu hồi qua UI; cùng token bị API chặn và nội dung web biến mất. 0 pageerror, không có note nội bộ, kiểm viewport 1440/390 không tràn ngang. Không tuyên bố đã kiểm UI tiếp nhận/khám trong bài này.
- Bài thật dùng đúng schema synthetic `family_default_e2e_20261109_4a5016e8` trên API 5086/web 5186. Lịch/ghế riêng của fixture mở 24 giờ để thử đúng ngày vào buổi tối; không đổi lịch phòng khám chính. Đã dừng dịch vụ thử và xóa đúng schema này sau kiểm chứng. API chính 5080/database healthy; web 5173 trả HTTP 200.
- Bằng chứng local: `.artifacts/runtime-20261109/family-default-{restore,build,web-build,api,browser,live}.log`, `family-default-api.trx`, `family-default-family-record-{1440,390}.png`.

Lượt thử đầu cần bổ sung schema/CORS riêng của fixture; thao tác bộ lọc Ant Select dùng selector thay vì input bị overlay che. Kết quả cuối ở trên đã chạy lại thành công. Không lưu mật khẩu/token hoặc dữ liệu bệnh nhân thật vào Git, không commit/push trong lượt thay đổi này.
