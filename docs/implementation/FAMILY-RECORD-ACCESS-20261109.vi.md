# Người thân: đặt lịch và xem hồ sơ — triển khai 09/11/2026

Trạng thái: đã triển khai theo `docs/specs/FAMILY-DELEGATED-RECORD-ACCESS.vi.md`. Phạm vi web khách hàng và hàng đợi nhân viên, API và migration; mobile không thuộc lượt này.

Luồng: A lưu/chọn người thân → đặt lịch hộ → gửi yêu cầu quyền hồ sơ → ClinicAssistant đối chiếu Patient/căn cứ đồng ý → cấp scope/chi nhánh/thời hạn → A xem nội dung chính thức. Pending, thu hồi, hết hạn và người thân xóa/đổi định danh đều bị chặn ở server. Booking không tự tạo Patient hay cấp quyền. Dùng Patient và các nguồn lâm sàng hiện có, không tạo hồ sơ bệnh nhân thứ hai.

Kiểm tra Jira trước triển khai: các ticket self-link DCMS-80/81 và app hồ sơ DCMS-107 là phần liên quan đã có; không sửa owner/trạng thái hoặc tạo ticket trùng trong lượt code này.

## Điểm vào

- Khách hàng: `/customer/family-members`, nút Đặt lịch và Xem hồ sơ; chọn người thân đã lưu trong `/customer/book`. Người thân không cần tài khoản. Booking giữ người đặt/liên hệ riêng, không tự tạo Patient.
- Nhân viên: `/link-verification` → tab Hồ sơ người thân. Dùng lại quyền `patient-links.read`/`patient-links.verify` và giới hạn role ClinicAssistant; middleware và service đều kiểm quyền. Nhân viên đối chiếu Patient/chi nhánh, ghi căn cứ, chọn scopes và thời hạn, xác nhận đã kiểm tra đồng ý rồi cấp quyền. Từ chối/thu hồi có lý do; lịch sử xử lý giữ lại.
- Hồ sơ người thân có lịch đặt hộ tách khỏi quyền bệnh án. Sau khi được duyệt, chỉ hiện các tab đã cấp: lịch sử khám, điều trị đã chốt, đơn thuốc đã ký, tài liệu đã quét sạch/nguồn chính thức. Không trả ghi chú nội bộ, giá/công nợ hoặc đường dẫn lưu trữ.

## Migration và dữ liệu

Migration `20261109080620_FamilyRecordAccess` bổ sung ba bảng yêu cầu/quyền, audit, receipt và FK nullable `APPOINTMENT.FamilyMemberId`; thay index BookerAccountId bằng index có cùng prefix, không xóa Patient/bệnh án/lịch cũ hay backfill quyền. Schema phát triển đã cập nhật, API `/health` báo api/database healthy. Kiểm tra database demo sau thử nghiệm: 0 yêu cầu quyền người thân và 0 booking có FamilyMemberId được tạo bởi lượt thử này.

Lượt browser thật dùng schema `family_e2e_20261109_*`, API 5086 và web 5186 tách khỏi database demo. Người dùng A được đăng ký bằng API thật và đăng nhập qua web bằng số điện thoại; chỉ tạo FamilyMember/Patient cho B, không tạo account B. Việc tiếp nhận và tạo phiếu khám chính thức được chuẩn bị qua API thật; phần đặt lịch, xin quyền, duyệt/xem/thu hồi chạy qua UI. Không tuyên bố đã thử UI tiếp nhận/khám trong bài này.

Sau khi kiểm chứng, đã dừng API/web cách ly và xóa đúng schema synthetic đã tạo. Giữ API chính 5080 và web 5173 hoạt động; health API/database đều healthy, hai route khách hàng/nhân viên trả HTTP 200.

## Kiểm chứng

- Backend API/PostgreSQL: **24/24** (FamilyRecordApiTests, FamilyMemberApiTests, AppointmentApiTests), gồm snapshot booking/owner/replay, không auto Patient/grant, Pending/IDOR, consent/scope/branch/expiry, read-only staff qua HTTP và gọi service trực tiếp, nội dung Draft/internal bị loại, private file Ready/SHA256, cùng token bị chặn sau rút/thu hồi/sửa định danh/xóa/hết hạn.
- Phân loại quyền cho toàn bộ endpoint: **1/1**, gồm các route mới.
- Snapshot EF khớp model hiện tại: **1/1**.
- Web build đạt. Browser giả lập người thân: **8/8**. Browser với API/PostgreSQL thật: **1/1**, đi từ tạo người thân/đặt lịch đến xác minh, đọc kết quả và thu hồi; không lộ note nội bộ, không mở tab ngoài scope, sau thu hồi UI bỏ nội dung và direct API trả 403. Ảnh desktop 1440px và mobile 390px, không tràn ngang trang; 0 pageerror trong bài luồng thật.
- Lượt browser cuối chạy cả hai bộ: **9/9 đạt** (`family-browser-final.log`).
- Bằng chứng: `.artifacts/runtime-20261109/family-api.trx`, `family-api-tests.log`, `family-route-permissions.log`, `family-model-tests.log`, `family-web-build.log`, `family-ui-tests.log`, `family-live-tests.log`, `family-browser-final.log`, `family-record-live-1440.png`, `family-record-live-390.png`.

Các tài khoản/Patient/ca khám thử đều synthetic; không ghi mật khẩu/token vào báo cáo. Không commit/push hoặc thay đổi Jira trong lượt này.
