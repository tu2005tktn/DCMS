# DCMS-124 / DCMS-120: Quản lý Chi nhánh, Ghế vật lý & Điều hướng Thiết lập 11 nhóm

Ngày cập nhật: 07/11/2026. Trạng thái: Đặc tả thiết kế và kiến trúc triển khai.
Tài liệu liên quan: [S58-SYSTEM-SETTINGS.vi.md](S58-SYSTEM-SETTINGS.vi.md), [S58-SETTINGS-MERGE.vi.md](S58-SETTINGS-MERGE.vi.md).

---

## 1. DCMS-124: Quản lý Chi nhánh, Logo & Ghế vật lý với Kiểm tra tác động

### 1.1. Thực thể Chi nhánh (`BranchProfile` / `SCHEDULING_BRANCH`)
- `Id`: `int` (Identity/PK)
- `Code`: `string(40)` (Unique toàn hệ thống)
- `Name`: `string(160)` (Tên chi nhánh, ví dụ: "Cơ sở Cầu Giấy")
- `Address`: `string(1000)` (Địa chỉ cơ sở)
- `Phone`: `string(20)`
- `Email`: `string(254)`
- `Website`: `string(255)`
- `LogoAssetId`: `Guid?` (Liên kết logo đã qua kiểm duyệt bảo mật)
- `TimeZoneId`: `string(64)` (`Asia/Ho_Chi_Minh`)
- `BankCode`: `string(20)` (Mã ngân hàng, ví dụ: "VCB", "MB", "TCB")
- `AccountNumber`: `string(50)` (Số tài khoản nhận chuyển khoản công khai tại quầy)
- `PayeeName`: `string(160)` (Tên chủ tài khoản)
- `SlotIntervalMinutes`: `int` (Mặc định 15 phút)
- `BookingSettingsVersion`: `int` (Version cấu hình đặt lịch)
- `IsActive`: `bool` (Trạng thái hoạt động)
- `Version`: `int` (Concurrency token)

### 1.2. Thực thể Ghế vật lý (`BranchChair` / `SCHEDULING_CHAIR`)
- `Id`: `int` (Identity/PK)
- `BranchId`: `int` (FK)
- `Code`: `string(40)` (Mã ghế, unique trong phạm vi `BranchId`)
- `Name`: `string(160)` (Tên ghế, ví dụ: "Ghế 01 - Khoa Tổng quát")
- `OpenMinute`: `int` (Giờ mở, mặc định 480 = 08:00)
- `CloseMinute`: `int` (Giờ đóng, mặc định 1080 = 18:00)
- `IsActive`: `bool` (Trạng thái sử dụng)
- `Version`: `int` (Concurrency token)
- **Ràng buộc nghiệp vụ**:
  - Không hard-code cố định 3 ghế cho toàn bộ phòng khám. Hỗ trợ cấu hình linh hoạt số ghế cho từng cơ sở (`Branch.Chairs: 1..N`).
  - Một ghế chỉ tiếp nhận 1 bệnh nhân tại một thời điểm; trong thời gian chờ ngấm thuốc tê hoặc chờ kết quả chẩn đoán hình ảnh vẫn giữ ghế.

### 1.3. Upload Logo Chi nhánh an toàn (`POST /api/settings/branches/{id}/logo`)
- Định dạng cho phép: PNG, JPEG, WebP. Dung lượng <= 2 MiB.
- Kiểm tra Magic Bytes nhị phân nghiêm ngặt:
  - PNG: `89 50 4E 47`
  - JPEG: `FF D8 FF`
  - WebP: `52 49 46 46` ... `57 45 42 50`
- Tuyệt đối nghiêm cấm: File SVG, script, HTML hoặc binary thực thi.
- Loại bỏ EXIF / metadata trước khi lưu.
- Lưu trữ ngoài Git và ngoài webroot public để phòng tránh thực thi mã độc; sinh `assetId` duy nhất.
- Quy trình 2 bước:
  1. Upload file qua endpoint logo -> kiểm tra -> trả về `assetId`.
  2. Gán `logoAssetId` vào lệnh lưu thông tin chi nhánh có `expectedVersion` và `requestKey`.

### 1.4. Kiểm tra tác động độc lập (Impact Check & State Preview)
- Endpoints:
  - `POST /api/settings/branches/{id}/state-preview`
  - `POST /api/settings/branches/{id}/chairs/{chairId}/state-preview`
- Kiểm tra các nghĩa vụ chặn (Blockers):
  1. Lịch hẹn trong tương lai (`Appointments` có trạng thái chưa hủy).
  2. Lượt khám đang diễn ra (`Visits` có trạng thái `InProgress`).
  3. Phiếu giữ chỗ tài nguyên còn hiệu lực (`ResourceReservation`).
  4. Tồn kho vật tư chi nhánh (`BranchInventory`).
  5. Công nợ / phiếu thu tài chính chưa quyết toán (`FinanceCharges` / `Payments`).
  6. Phiếu gửi Labo đang xử lý.
- Nếu có blocker:
  - Trả lỗi HTTP `409` với mã lỗi chuẩn `MSG205`.
  - Kèm mảng `blockers: [{ kind, message, count, navigationUrl }]` để điều hướng nhân viên xử lý; tuyệt đối không tự động hủy hay dời lịch hẹn.
- Nếu module phụ thuộc chưa có adapter:
  - Trả HTTP `503` với mã lỗi `MSG144`.
- Mọi thao tác cập nhật chi nhánh/ghế đều ghi `expectedVersion`, `requestKey`, đồng thời lưu vào `SettingsAudit` và `CommandReceipt` trong cùng một transaction.

---

## 2. DCMS-120: Giao diện Thiết lập hệ thống 11 nhóm & Deep link (`/settings`)

### 2.1. Cấu trúc điều hướng 11 nhóm chuẩn
Thứ tự chuẩn theo đặc tả oDental:
1. `general` — Thiết lập chung (`/settings/general`)
2. `diagnoses` — Chẩn đoán / Tên bệnh (`/settings/diagnoses`)
3. `services` — Dịch vụ, sản phẩm, công việc (`/settings/services`)
4. `material-suppliers` — Công ty cung cấp vật liệu (`/settings/material-suppliers`)
5. `materials` — Loại dụng cụ / Vật liệu (`/settings/materials`)
6. `lab-providers` — Công ty labo (`/settings/lab-providers`)
7. `lab-work-types` — Loại công việc labo (`/settings/lab-work-types`)
8. `customer-groups` — Nhóm khách hàng (`/settings/customer-groups`)
9. `medicines` — Thuốc / Dược phẩm (`/settings/medicines`)
10. `access-groups` — Phân quyền truy cập (`/settings/access-groups`)
11. `branches` — Chi nhánh & Ghế vật lý (`/settings/branches`)

### 2.2. Quy chuẩn Form & An toàn dữ liệu
1. **Phân quyền chặt chẽ (RBAC + Action Grant)**:
   - Nếu không có quyền xem nhóm: Ẩn nhóm trên thanh điều hướng; gọi API trực tiếp trả 403 `MSG06`.
   - Nếu có quyền xem (`*.read`) nhưng không có quyền sửa (`*.manage` / `*.create` / `*.update`): Hiển thị giao diện chỉ đọc (Read-only), vô hiệu hóa các nút thêm/sửa/đổi trạng thái.
2. **Cảnh báo mất bản nhập (Dirty Guard)**:
   - Khi form đang có dữ liệu chưa lưu, việc bấm đổi tab, đổi chi nhánh hoặc chuyển route sẽ kích hoạt Modal cảnh báo mất dữ liệu.
3. **Chống Double-click (Idempotency & Guard)**:
   - Khóa nút Submit khi đang gửi request (`busy` state / ref guard). Gắn kèm `requestKey` UUID.
4. **Không xóa cứng (Soft Deactivate / Restore)**:
   - Cơ chế ngừng sử dụng (Deactivate) và khôi phục (Restore), lưu lý do và ghi nhận audit lịch sử.
5. **Chống CSV / Excel Formula Injection**:
   - Khi xuất dữ liệu ra Excel, các ô văn bản bắt đầu bằng ký tự nguy hiểm (`=`, `+`, `-`, `@`) bắt buộc phải được tiền tố bằng dấu nháy đơn (`'`) hoặc escape an toàn.
6. **Xử lý xung đột ghi đồng thời (Concurrency 409 MSG111)**:
   - Khi xảy ra xung đột phiên bản `MSG111`, giữ nguyên toàn bộ dữ liệu người dùng đang nhập trên form để người dùng so sánh và không bị mất công gõ lại.
