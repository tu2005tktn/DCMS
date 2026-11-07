# Phân quyền động — triển khai và kiểm chứng 02/11/2026

Đặc tả: [S58-DYNAMIC-ACCESS-GROUPS.vi.md](../specs/S58-DYNAMIC-ACCESS-GROUPS.vi.md).

## Kết quả có hiệu lực

- `/system-settings?tab=access-groups`: danh sách nhóm, modal checkbox theo màn hình/thao tác, tìm quyền, phụ thuộc cha/con, giới hạn vai trò nền, xem trước tăng/giảm/số người bị ảnh hưởng, ngừng/khôi phục và lịch sử.
- Tab Gán cho nhân viên: nhóm đang dùng tương thích vai trò, chọn chi nhánh từ membership hợp lệ/phạm vi người quản lý, reason/version/requestKey. Chủ phòng khám và chính tài khoản đang đăng nhập không được gán qua form thường; backend kiểm lại toàn bộ điều kiện.
- Backend nhóm quyền/resolver/receipt/audit có sẵn được sử dụng trực tiếp. Bổ sung API grant-options chứa allowedBaseRoles và metadata roles/membershipBranchIds trên danh sách nhân viên; không migration, không tạo Identity role theo tên nhóm.
- `patients.create/update/archive` kiểm riêng ở API và nút tương ứng. `patients.manage` cũ vẫn cho đủ ba thao tác; khi người quản lý sửa nhóm qua form mới, quyền gộp được biểu diễn bằng ba checkbox và lưu theo grant chi tiết sau bước xem trước.
- Menu thiết lập và danh mục tham chiếu hiển thị theo grant; danh mục chỉ hiện các loại/nút có quyền. Thu hồi quyền quản lý khi form đang mở khóa thao tác ghi nhưng giữ bản nhập. Auth refresh/focus hiện có cập nhật quyền giao diện; API có hiệu lực ngay trên request tiếp theo với token cũ.
- Hồ sơ bệnh nhân ẩn các tab lâm sàng nếu thiếu quyền đọc khám, deep link trở về thông tin cơ bản; mở phiếu khám kiểm examinations.read thay vì buộc thêm reception.read. Luồng lịch sử khám và kê đơn tùy chọn tiếp tục hoạt động.

## Bằng chứng

| Lớp kiểm tra | Kết quả | Phạm vi |
| --- | --- | --- |
| .NET 10 restore/build solution Release | Qua | Cảnh báo Pkcs NU1902/NU1903 có sẵn |
| API/PostgreSQL | **39/39 qua** | SettingsApiTests, MainSettingsCompatibilityTests, PatientApiTests; bao gồm grant từng thao tác và thu hồi cùng token, quyền gộp cũ, metadata, role/scope/self-assignment, preview/version/retry/count/audit |
| Trình duyệt | **27/27 qua** | **3 luồng API thật**, **24 ca UI mock**; phân quyền, hồi quy sidebar, kê đơn tùy chọn, lịch sử khám |
| Web | Qua | npm install; npm run build; cảnh báo chunk/annotation SignalR có sẵn |
| Runtime | Qua | API/database health 5080, API 5084, web 5173 HTTP 200 |
| Kiểm tra thay đổi | Qua | git diff --check; SHA-256 tài liệu REALISTIC-DEMO-DATA sửa sẵn không đổi |

Luồng phân quyền API thật: Owner tạo nhóm bằng checkbox chỉ cho Xem/Thêm bệnh nhân → gán tài khoản staff synthetic và chi nhánh → nhân viên tạo hồ sơ qua UI → không có nút Sửa/Ngừng; API sửa trả 403 → Owner xem trước thu hồi quyền Thêm và lưu → token staff cũ lập hồ sơ trả 403, đọc vẫn 200 → UI tải lại ẩn nút Thêm → nhật ký có reason/actor/thời gian tương ứng. Nhóm còn nhân viên có nút ngừng bị khóa; kiểm tra server cùng điều kiện nằm trong bộ API.

Đã xem ảnh desktop và 390px: tên/mã/vai trò và cây quyền nằm trong modal cuộn, thao tác Lưu/Hủy vẫn dùng được; không tràn ngang trang.

## Môi trường và giới hạn

- Mọi ghi kiểm chứng runtime ở schema cách ly được chỉ định trong `.artifacts/verification-20261102/runtime.json`. Một staff synthetic mới được tạo bằng API tài khoản; membership của staff mới được bootstrap riêng trong schema này vì chưa có API quản lý membership. Nhóm quyền, assignment, sửa quyền và hồ sơ bệnh nhân được thao tác qua API/UI thật. Không đổi nhóm/role của các tài khoản demo có sẵn.
- Bộ API integration dùng PostgreSQL/auth/EF thật, các adapter contact/email/realtime/scanner theo fixture kiểm thử hiện có. Không lấy mock UI làm bằng chứng server enforcement.
- Vai trò chuyên môn và phạm vi chi nhánh hiện có tiếp tục ràng buộc quyền. Danh mục hành chính bệnh nhân vẫn dùng chung phòng khám; scope assignment không được quảng bá thành khả năng giấu toàn bộ hồ sơ/liên hệ theo chi nhánh.
- Thiết lập chung giữ Owner-only. Không có checkbox giả cho xuất Excel, giới hạn thời gian báo cáo, che liên hệ hoặc các thao tác điều trị chi tiết chưa có consumer grant; không sửa luồng điều trị của Vinh.
- UI cập nhật quyền theo cơ chế refresh phiên định kỳ/focus; API thu hồi ngay request sau. Không tuyên bố mọi màn hình đang mở trên máy khác nhận push thời gian thực.
- Báo cáo máy: `.artifacts/verification-20261102/dynamic-access-api.trx`, `dynamic-access-final-browser.json`, `dynamic-access-web-build.log`. Ảnh và log trong `dynamic-access-final-browser/`. Các lần thử đầu đã sửa nhãn truy cập nút Lưu sau loading và nhãn drawer lịch sử; bộ kiểm tra cuối qua đủ số ca ở trên.
