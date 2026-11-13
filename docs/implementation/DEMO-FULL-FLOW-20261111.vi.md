# Bộ dữ liệu demo toàn luồng ngày 11/11/2026

## Mục tiêu và quy tắc chuẩn bị

Tạo dữ liệu tổng hợp sát tình huống phòng khám, lưu thật trong PostgreSQL local qua API nghiệp vụ. Bộ mới dùng mã `DEMO1111-*`, giữ nguyên các bộ cũ và dữ liệu không thuộc bộ này. Không chứa thông tin bệnh nhân thật, token hoặc mật khẩu trong Git. Liên hệ giả, email `example.invalid`; các nội dung lâm sàng và khoản thu đều ghi rõ là mô phỏng.

Chuẩn bị ca ở nhiều điểm dừng để người trình bày tự đi tiếp: hồ sơ mới; lịch hôm nay chưa check-in; hàng chờ; khám nháp; chỉ định chờ và đã đọc kết quả; khám hoàn tất; kế hoạch điều trị; việc đã làm; thu một phần; chuyển khoản chờ xác nhận; đã thu đủ; hẹn tái khám. Thêm ca trẻ em/người giám hộ và lịch ngày tới để đổi/hủy. Lịch dùng availability thực tế và consent của bệnh nhân giả; không sửa lịch làm việc để ép có slot.

Các phân hệ phụ phải được kiểm kê theo code/API hiện tại: thuốc/đơn thuốc, vật tư nhập–xuất–tồn, labo, chăm sóc khách hàng, liên kết bệnh nhân/tài khoản công khai, thông báo, nhân sự/quyền, cấu hình/danh mục và báo cáo. Tái sử dụng danh mục development sẵn có; thêm chứng từ mô phỏng ở các trạng thái thao tác được nếu API cho phép. Ghi rõ phân hệ nào mới có dữ liệu nền hoặc còn thiếu điều kiện; không coi dữ liệu seed là bằng chứng tích hợp bên thứ ba thành công.

Phân hệ lịch làm việc cũng cần dữ liệu demo: được tạo lịch mới cho tài khoản development `dual.demo` và `owner.demo` khi chưa có lịch công bố/ngoại lệ. Preview phải không ảnh hưởng lịch hẹn nào; không ghi đè lịch đã cấu hình, không sửa lịch `dentist.demo`. Dùng các ca cùng ngày qua availability của nhân viên tương ứng; bác sĩ xử lý bằng đúng tài khoản đã được phân công.

## Hợp đồng và kiểm chứng

- Dùng API hiện có, actor đúng quyền, `requestKey` ổn định và `expectedVersion` mới nhất. Manifest ignored lưu checkpoint để chạy tiếp an toàn; không tự khôi phục ca đã được người demo thao tác.
- Appointment, Visit, phiếu khám, điều trị và thanh toán giữ trạng thái riêng. Kế hoạch chưa thực hiện không tạo doanh thu; chuyển khoản Pending chưa tính là tiền đã thu.
- Thuốc/đơn chỉ mang nội dung minh họa, không dùng làm chỉ dẫn điều trị. Không gửi tin ra ngoài, không tạo giao dịch PayOS thật chỉ để có dữ liệu.
- Kiểm tra lại API và sổ phí sau khi ghi, mở các màn hình chính bằng trình duyệt với API thật. Báo cáo số lượng đã xác minh và giới hạn thực tế.
- Bàn giao hướng dẫn local theo vai trò, mã bệnh nhân, link từng ca và trình tự demo. Mật khẩu lấy từ cấu hình local hiện hành, không chép vào tài liệu.

## Kết quả thực thi

- PostgreSQL xác nhận 21 hồ sơ `DEMO1111-01`–`DEMO1111-21`, 22 Appointment, 10 Visit và 8 dossier (7 có dòng điều trị/kế hoạch chuẩn bị, một phiếu nguồn còn trống). Visit gồm 5 Completed, 2 InProgress và 3 Queued. Ba lịch trong ngày còn chưa check-in, trong đó ca 21 là điểm bắt đầu demo toàn luồng bằng `owner.demo`.
- Dữ liệu mở rộng: 7 dòng kế hoạch/việc thực hiện; 3 bước điều trị phụ thuộc và một chỉ định tái khám NeedsBooking; 4 phiếu labo lần lượt NotSent/Sent/Received/Delivered; 6 phiếu nhập vật tư (5 đã nhận, một chờ nhận), 2 dòng sử dụng vật tư nối với việc thực hiện ca 02.
- Thu ngân có ca chưa thu, thu một phần, đã thu đủ, chuyển khoản Pending; 3 hóa đơn, 3 khoản ứng (2 Cash Posted, một Transfer Pending), một yêu cầu hoàn Requested chưa chi. Đọc lại phiếu điều trị và Finance cho số đã thu/còn nợ khớp nhau; `reconciliationIssueCount = 0`.
- Nạp thành công 49 dịch vụ và 17 kế hoạch mẫu bổ sung, 46 vật tư thuộc 10 nhóm, 2 labo mẫu và 7 loại công việc; tái sử dụng 10 thuốc/3 mẫu đơn đã có. Có thêm nhà cung cấp và labo mang mã DEMO1111. Không thay giá dịch vụ hoặc cấu hình lịch cũ.
- Có 5 việc CSKH thủ công, lịch sử liên hệ giả lập; 2 đơn theo Visit đã ký, một đơn Visit còn nháp, một đơn tại hồ sơ để cổng khách hàng đọc được. Một tài liệu PNG tự tạo ghi rõ không phải X-quang, scanner Ready và đã được rà soát.
- Hai tài khoản Public tổng hợp: một liên kết tự thân Verified, một Pending; người thân được duyệt quyền có thời hạn theo quan hệ giám hộ giả lập. Có 3 lịch khách hàng, gồm lịch đã đổi có lịch sử, lịch đã hủy và lịch có yêu cầu hỗ trợ Pending; đọc được 6 thông báo phát sinh qua workflow thật. Không gửi email/SMS bên ngoài.
- Playwright đã kiểm tra 30 màn hình bằng owner/dentist/dual/assistant/admin và hai tài khoản Public; không có pageerror hoặc API response lỗi trong lượt kiểm tra cuối. Các form thu tiền/check-in chỉ mở xem, không nộp thêm dữ liệu nghiệp vụ, giữ ca 21 cho người demo.
- Backend restore/build và web install/build thành công. Sửa lỗi seeder danh mục khi có dịch vụ trùng tên theo quy tắc cập nhật tại `docs/specs/TREATMENT-CATALOG-SEED.md`; test tích hợp trên schema riêng có dữ liệu trùng tên pass 1/1, giữ nguyên dịch vụ cũ. `git diff --check` pass.

Hướng dẫn đầy đủ theo vai trò, mã ca và đường dẫn: `.tools/demo1111-guide.md` (ignored). Manifest `.tools/demo-full-20261111.json`; bằng chứng API và trình duyệt nằm tại `.artifacts/demo1111/` (ignored). API 5080 và web 5173 đã kiểm tra health/HTTP 200.

Giới hạn: dữ liệu không xác nhận tích hợp PayOS thật, Google sign-in, gửi email/SMS, AI hoặc thiết bị mobile. Phần hỗ trợ đổi lịch hiện có cột lý do đề xuất giới hạn 40 ký tự; ca demo dùng lý do ngắn, lỗi nhập lý do dài chưa được sửa trong đợt này. Chỉ tiếp nhận được lịch hôm nay trong đúng ngày phòng khám 11/11/2026; sau ngày này cần đặt lịch mới theo quy tắc bình thường.
