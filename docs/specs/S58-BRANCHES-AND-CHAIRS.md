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

### 1.5. Quy tắc triển khai (bản 07/11/2026)

**Lưu trữ & khóa chính**
- `BRANCH_PROFILE.Id` = `SCHEDULING_BRANCH.Id` (khóa chung, FK, không tự sinh). `BRANCH_CHAIR.Id` = `SCHEDULING_CHAIR.Id` (khóa chung, FK). Bảng `SCHEDULING_*` vẫn là nguồn cho lịch/đặt hẹn; profile giữ thông tin hành chính, mã, logo, tài khoản ngân hàng và `Version`.
- Tạo chi nhánh: tạo `SCHEDULING_BRANCH` trước rồi tạo profile cùng Id trong cùng transaction. Tạo ghế tương tự với `SCHEDULING_CHAIR`.
- Chi nhánh/ghế cũ chưa có profile hiển thị `Version = 1`, mã sinh `BR-{id:00}` / `CHAIR-{id:00}`; lần lưu đầu tiên (expectedVersion = 1) tạo profile với `Version = 2`.
- Migration `20261107050902_PrescriptionsAndBranchProfiles` tạo `BRANCH_PROFILE`, `BRANCH_CHAIR`, `BRANCH_ASSET` (không seed; dữ liệu cũ dùng giá trị sinh như trên).

**Validation (400 MSG02/MSG04, có `field`)**
- `code` 1–40 ký tự `[A-Za-z0-9_-]`, `name` 1–160, `address` ≤1000, `phone` ≤20, `email` ≤254 đúng định dạng, `website` ≤255 http/https, `timeZoneId` phải là IANA timezone hợp lệ, `bankCode` ≤20, `accountNumber` ≤50 chỉ chữ số, `payeeName` ≤160. Ba trường ngân hàng nhập đủ cả ba hoặc để trống cả ba.
- Ghế: `openMinute`/`closeMinute` trong 0–1440, bội số 15, mở < đóng. Mã ghế unique trong chi nhánh cả khi tạo và khi đổi mã (`409 MSG201`). Chi nhánh ngừng hoạt động không nhận ghế mới (`409 MSG205`).
- `logoAssetId` phải là asset đã upload cho đúng chi nhánh đó (`404 MSG113` nếu không).

**Kiểm tra tác động (blockers)**
- Chi nhánh — kiểm trong cùng transaction với lệnh đổi trạng thái (sau khóa advisory):
  1. Lịch hẹn tương lai còn hiệu lực: `StartAt >= now` và trạng thái không thuộc `Cancelled | NoShow | Withdrawn` → `Appointments`.
  2. Lượt khám chưa đóng: `Queued | InProgress` → `Visits`.
  3. Giữ chỗ tài nguyên `Confirmed` chưa kết thúc (`End > now`) → `Reservations`.
  4. Tài chính: phiếu thu `Pending` và khoản phí còn số dư chưa phân bổ từ phiếu thu `Posted` → `Finance`.
  5. Tồn kho: hiện tồn kho vật tư là toàn hệ thống (không có cột `BranchId` trên `MATERIAL_PURCHASE`) nên không có nghĩa vụ tồn kho theo chi nhánh; bước này không phát sinh blocker cho đến khi có kho theo chi nhánh.
  6. Labo: qua port `IBranchLabObligationPort`. Bản hiện tại chưa có module phiếu Labo nên adapter trả `Available = false` → lệnh ngừng chi nhánh trả **`503 MSG144`** và không đổi dữ liệu (đúng quy tắc "module phụ thuộc chưa có adapter").
- Ghế: lịch hẹn tương lai còn hiệu lực trên ghế, lượt khám chưa đóng đang dùng ghế (theo lịch hẹn nguồn), giữ chỗ `Confirmed` chưa kết thúc.
- Kích hoạt lại (Restore) không có blocker, nhưng khôi phục ghế khi chi nhánh đang ngừng → `409 MSG205`.
- Có blocker → `409` với body `{ code: "MSG205", message, blockers: [{ kind, message, count, navigationUrl }] }`. `state-preview` trả `200 { allowed, blockers, impact }` cho cùng dữ liệu. Không tự hủy/dời lịch.

**Upload logo**
- Lưu dưới thư mục cấu hình `BranchAssets:StoragePath` (đường dẫn tuyệt đối, ngoài repository Git; thiếu/sai → `503 MSG144`), tên tệp là `assetId` không có phần mở rộng do người dùng gửi.
- Kiểm tra: kích thước 1 byte–2 MiB, magic bytes PNG/JPEG/WebP, khai báo `Content-Type` phải khớp loại thực tế; mọi định dạng khác (SVG, HTML, script...) → `400 MSG04`.
- Strip metadata trước khi lưu: PNG bỏ chunk `tEXt/zTXt/iTXt/eXIf/tIME`; JPEG bỏ segment `APP1–APP15` và `COM`; WebP bỏ chunk `EXIF`/`XMP ` và xóa cờ tương ứng trong `VP8X`. Tệp hỏng cấu trúc → `400 MSG04`.
- Ghi bản ghi `BRANCH_ASSET { Id, BranchId, ContentType, SizeBytes, Sha256, UploadedBy, UploadedAt }` + `SettingsAudit` (`LogoUpload`). Trả `{ assetId, fileName, sizeBytes, contentType }`.
- Đọc logo: `GET /api/settings/branches/{id}/logo/{assetId}` (`branches.read`) trả ảnh với `Content-Type` đã kiểm, `X-Content-Type-Options: nosniff`, `Cache-Control: private`. Web tải bằng request có token rồi hiển thị qua object URL (không đặt token vào URL).
- Bản in đơn thuốc nhận logo dạng `data:` URL trong DTO in (đọc server-side từ asset đã kiểm), để bác sĩ chỉ có `prescriptions.read` và bệnh nhân trên app vẫn thấy logo mà không cần quyền thiết lập.

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


### 2.3. Sửa đường dẫn menu (08/11/2026)

Menu "Thiết lập hệ thống" trỏ `/settings` nhưng router chỉ có `/system-settings`, nên bấm menu ra "Không tìm thấy trang". Router phải có `/settings` và `/settings/:section` (deep link của 11 nhóm) cùng quyền `settings.read`; đường dẫn cũ `/system-settings` vẫn mở được cùng trang.


### 2.4. Điều hướng 11 nhóm dạng danh sách dọc (08/11/2026)

Hàng tab ngang bị tràn, nhiều nhóm bị gom vào nút "...". Từ 992px trở lên, danh sách 11 nhóm xếp **dọc bên trái** (rộng cố định 260px, tên nhóm xuống dòng khi dài, nhóm đang chọn tô nổi), nội dung bên phải. Dưới 992px giữ tab ngang cuộn được. Thứ tự, quyền, deep link `/settings/{nhóm}` và hỏi xác nhận khi bỏ thay đổi chưa lưu không đổi.

### 2.5. Trang chủ thiết lập dạng thẻ (08/11/2026, thay thế 2.4)

Màn "Thiết lập hệ thống" không còn danh sách tab/dọc. `/settings` (không có nhóm) là **trang chủ dạng thẻ**:

- Ô tìm "Tìm mục thiết lập…" lọc các mục theo tên (không phân biệt hoa thường, bỏ dấu không bắt buộc).
- 5 thẻ nhóm, mỗi thẻ có biểu tượng, tên, mô tả ngắn và danh sách mục bên trong (chỉ mục tài khoản được quyền xem; nhóm không còn mục nào thì ẩn):
  - **Phòng khám:** Thiết lập chung, Chi nhánh và ghế điều trị.
  - **Danh mục:** Chẩn đoán / Tên bệnh, Dịch vụ, sản phẩm và giá, Thuốc / Dược phẩm.
  - **Vật liệu và labo:** Công ty cung cấp vật liệu, Loại dụng cụ / Vật liệu, Công ty labo, Loại công việc labo.
  - **Khách hàng:** Nhóm khách hàng.
  - **Bảo mật:** Phân quyền truy cập.
- Bấm một mục mở `/settings/{mục}` (deep link cũ giữ nguyên). Trang mục có đường dẫn "Thiết lập › {Nhóm} › {Mục}" và nút "Tất cả thiết lập" quay về trang chủ. Tên mục không còn đánh số.
- Thứ tự mục, quyền xem từng mục, quyền sửa và hộp thoại "Bỏ thay đổi chưa lưu?" (khi chuyển mục hoặc về trang chủ lúc còn thay đổi chưa lưu) không đổi. `?tab=` vẫn mở đúng mục. Tài khoản chỉ có một mục vẫn thấy trang chủ với một thẻ.
- Trang chủ không có mục nào (tài khoản không quyền) vẫn hiện 403 như cũ.

### 2.5. Thiết kế lại bố cục: trang chủ dạng thẻ (mẫu B, 08/11/2026)

Thay mục 2.4 (danh sách dọc) và hàng tab ngang. 11 mục vẫn giữ nguyên nội dung, quyền và deep link `/settings/{mục}`.

- **Trang chủ `/settings`**: ô tìm mục (lọc theo tên, không phân biệt dấu/hoa thường) và các **nhóm** dạng thẻ. Mỗi nhóm là một khối có tiêu đề, bên trong là thẻ cho từng mục; bấm thẻ mở mục. Chỉ hiện mục (và nhóm) mà tài khoản có quyền; nhóm không còn mục thì ẩn. Không tìm thấy thì hiện "Không có mục phù hợp".
- **Nhóm**: Phòng khám (Thiết lập chung, Chi nhánh và ghế, Nhóm khách hàng); Danh mục (Chẩn đoán / tên bệnh, Dịch vụ sản phẩm và giá, Thuốc / dược phẩm); Vật liệu và labo (Công ty cung cấp vật liệu, Loại dụng cụ / vật liệu, Công ty labo, Loại công việc labo); Bảo mật (Phân quyền truy cập). Số thứ tự cũ trước tên mục bị bỏ khỏi giao diện.
- **Trong một mục `/settings/{mục}`**: đường dẫn "Thiết lập › Nhóm › Mục" (bấm "Thiết lập" về trang chủ) và, nếu nhóm có từ 2 mục được phép, thanh chuyển nhanh giữa các mục cùng nhóm. Nội dung mục nằm ngay dưới, rộng toàn trang.
- `/settings/{mục}` không tồn tại hoặc không có quyền: về trang chủ. `?tab=` cũ vẫn mở đúng mục.
- Mọi lần chuyển (đường dẫn, thanh chuyển nhanh, thẻ) vẫn hỏi "Bỏ thay đổi chưa lưu?" khi mục đang có bản nhập chưa lưu.
- Màn hẹp: thẻ xếp một cột, thanh chuyển nhanh cuộn ngang được.
- **Nút quay lại (08/11/2026)**: trong một mục có nút "Quay lại" ở đầu trang, bấm để về trang chủ thiết lập (vẫn hỏi "Bỏ thay đổi chưa lưu?" nếu có bản nhập chưa lưu).
