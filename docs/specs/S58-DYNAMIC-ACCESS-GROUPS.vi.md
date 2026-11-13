# S58 — Thiết lập phân quyền động

C?p nh?t l?u tr? 12/11/2026: t?i kho?n, h? s? nh?n vi?n, g?n nh?m, phi?n v? d? li?u x?c th?c n?m trong `USER_ACCOUNT`; nh?m quy?n ??ng v? vai tr? h? th?ng n?m trong `ACCESS_GROUP`. C?c t?n b?ng c? trong m? t? b?n d??i l? c?u tr?c l?ch s?. H?p ??ng API v? quy t?c quy?n gi? nguy?n. Xem [thi?t k? hai b?ng](ACCOUNT-TWO-TABLES.vi.md).

Cập nhật lưu trữ 12/11/2026 theo [hợp đồng rút gọn database](DATABASE-SIMPLIFICATION.vi.md). Các grant nằm trong `Grants` JSONB của `ACCESS_GROUP`. Server tiếp tục kiểm tra quyền trước khi xử lý hoặc trả kết quả replay; lịch sử và receipt dùng kho chung toàn hệ thống.

Mở rộng theo yêu cầu tiếp theo cho tất cả màn hình: [ALL-SCREENS-DYNAMIC-PERMISSIONS.vi.md](ALL-SCREENS-DYNAMIC-PERMISSIONS.vi.md). Tài liệu mở rộng thay thế giới hạn phạm vi của bản đầu bên dưới.

Cập nhật 02/11/2026 theo ảnh tham chiếu người dùng. Hoàn thiện mục Nhóm quyền/nhân viên trong S58; tận dụng AccessGroup, StaffAccessAssignment và SettingsAccess đã có.

## Giao diện và quy trình

- Cập nhật 09/11/2026: trang **Tài khoản của tôi** chỉ hiển thị tên nhân viên, tên đăng nhập và thao tác cập nhật quyền; bỏ khối **Loại tài khoản**, **Vai trò được cấp**, **Vai trò hiệu lực**. Các nhãn vai trò không dùng để diễn giải quyền thao tác của nhân viên trong giao diện này. Menu, route và thao tác tiếp tục dùng danh sách quyền động từ phiên; không thay hợp đồng API, nhóm quyền, vai trò nền hay kiểm tra quyền ở server. Thông báo liên kết hồ sơ cho tài khoản Public vẫn theo contract hiện hành.
- Trong Thiết lập hệ thống có tab **Phân quyền** theo quyền `access-groups.read`. Danh sách nhóm hiển thị mã, tên, vai trò nền, trạng thái và số nhân viên thực tế. Có tạo, sửa, xem nội dung và ngừng/khôi phục nhóm theo quyền quản lý.
- Form **Thiết lập phân quyền** có tên bắt buộc, mã, mô tả, vai trò nền và danh sách checkbox theo màn hình/nghiệp vụ, thụt lề thao tác con như ảnh. Nội dung dài cuộn trong modal, dùng được trên màn hình nhỏ; có tìm quyền theo tên.
- Chọn quyền con tự chọn đủ quyền cha. Bỏ quyền cha bỏ mọi quyền phụ thuộc, kể cả quyền đang bị bộ lọc tìm kiếm che đi. Xem trước quyền tăng/giảm và số nhân viên bị ảnh hưởng trước khi lưu sửa. Bắt buộc lý do khi sửa hoặc ngừng/khôi phục; lỗi giữ nguyên bản nhập, stale version cho đọc lại phiên bản trước khi xem trước/lưu tiếp.
- Vai trò nền vẫn ràng buộc nghiệp vụ. Checkbox không biến assistant thành nha sĩ hoặc cấp quyền vượt quyền người quản lý. Nhóm đã có nhân viên không đổi vai trò nền. Owner giữ đặc quyền quản trị theo cơ chế hiện có; không tự gán/đổi nhóm của mình hoặc gán nhóm cho Owner qua màn thông thường.
- Mục **Gán cho nhân viên** chọn nhóm đang hoạt động tương thích vai trò đã có, chi nhánh trong membership đang hoạt động của nhân viên và phạm vi người quản lý, kèm lý do. Mỗi tài khoản tối đa một nhóm. Gán nhóm không thay Identity role hoặc lịch làm việc.
- Nhóm còn nhân viên không được ngừng; hướng dẫn chuyển nhóm trước. Giữ dữ liệu/audit, không hard-delete.
- Chỉ cung cấp checkbox cho grant có consumer thật. Quyền Excel, ẩn liên hệ bệnh nhân, khoảng thời gian báo cáo và các thao tác điều trị chi tiết trong ảnh chưa có contract/consumer tương ứng không được hiển thị thành lựa chọn có hiệu lực. Giữ luồng điều trị của Vinh.
- Thiết lập chung vẫn chỉ Owner theo contract hiện hành; không đưa `settings.general.read/manage` thành checkbox ủy quyền. Danh mục tham chiếu đã có consumer grant thì hiện đúng danh mục và nút Thêm/Sửa/Ngừng dùng theo quyền; lịch sử nhóm chỉ mở khi có `settings.audit.read`.
- Tab Khám/Hình ảnh/Đơn thuốc trong hồ sơ cần `examinations.read`. Liên kết mở lượt khám dùng quyền đọc khám, không bắt thêm quyền mở hàng chờ tiếp nhận. Deep link tới tab lâm sàng khi không được cấp quyền trở về thông tin cơ bản; API vẫn kiểm quyền đọc chuyên môn/phạm vi độc lập.

## Quyền hồ sơ bệnh nhân

Tách thao tác `patients.create`, `patients.update`, `patients.archive` (ngừng/khôi phục) cùng cha `patients.read`, vẫn yêu cầu vai trò ClinicAssistant hiện hành. API kiểm từng thao tác; giao diện chỉ hiển thị nút được cấp. Grant cũ `patients.manage` vẫn hợp lệ và bao hàm ba thao tác để không cắt quyền đã có. Form mới chuyển grant cũ sang ba checkbox tương đương khi sửa/lưu nhóm, không tự ghi chuyển đổi khi chỉ xem.

Phạm vi chi nhánh của assignment tiếp tục áp dụng ở các consumer nghiệp vụ đã dùng SettingsAccess (tiếp nhận, khám, tài chính...). Danh mục hành chính bệnh nhân dùng chung phòng khám theo contract hiện có; bản này không thêm tuyên bố lọc toàn bộ hồ sơ/ẩn liên hệ theo chi nhánh.

## API và hiệu lực

- Giữ API `/api/system-settings/access-groups`, `/{id}/preview`, `/{id}/state`, `/staff`, `/api/system-settings/staff/{employeeId}/access-group`, `/access-capabilities` và audit hiện có.
- Thêm `GET /api/system-settings/access-groups/grant-options`: mỗi grant gồm key/label/requiredRole/parent và `allowedBaseRoles` được tính ở server. UI không tự suy đoán quyền hợp lệ theo vai trò.
- Bổ sung metadata đọc `roles` (vai trò hiệu lực), `membershipBranchIds` vào `StaffAccessDto`. Trường `branchIds` vẫn là scope assignment hiện tại; metadata membership cho chọn lại chi nhánh hợp lệ, không làm rộng scope tự động.
- Writes giữ version/requestKey/audit/transaction hiện có; server kiểm lại quyền, scope, role, active và version khi commit. API đọc quyền từ DB trong mỗi request, không tin quyền cũ trong JWT. UI dùng refresh phiên hiện có khi focus và định kỳ.
- Không cần migration: nhóm, grant, assignment và audit đã có bảng. Không tự gán nhóm cho mọi nhân viên hoặc sửa quyền nhóm đang dùng trong dữ liệu thật.

## Kiểm chứng bắt buộc

API/PostgreSQL: nhóm/version/retry/preview/audit, role/scope/self-assignment, inactive/in-use; thêm quyền hồ sơ từng thao tác và thu hồi với cùng access token. UI: cây cha/con, lọc không bỏ sót quyền phụ thuộc, giữ bản nhập khi lỗi, xem trước/sửa/ngừng, nhóm và chi nhánh tương thích khi gán. Browser API thật trên schema cách ly: tạo nhóm bằng checkbox, gán nhân viên, kiểm read-back rồi thu hồi một thao tác và chứng minh API/UI bị chặn đúng.
