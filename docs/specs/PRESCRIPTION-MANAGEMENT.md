# Phân hệ Quản lý & Kê đơn thuốc (Prescription Management)

Ngày cập nhật: 07/11/2026. Trạng thái: Đặc tả kiến trúc và thiết kế kỹ thuật chuẩn oDental.
Tài liệu liên quan: [S19-OPTIONAL-PRESCRIPTION.vi.md](S19-OPTIONAL-PRESCRIPTION.vi.md), [S58-SUPPLY-CATALOGS.md](S58-SUPPLY-CATALOGS.md), [S41-MOBILE-MEDICAL-RECORDS.md](S41-MOBILE-MEDICAL-RECORDS.md).

---

## 1. Nguyên tắc cốt lõi & Quy chuẩn nghiệp vụ oDental

1. **Đơn thuốc mua ngoài (External Prescription Only)**:
   - Phòng khám nha khoa không bán lẻ thuốc tại cơ sở.
   - Mọi đơn thuốc bác sĩ kê đều là **Đơn thuốc mua ngoài** để bệnh nhân mang ra hiệu thuốc tư nhân hoặc nhà thuốc bệnh viện mua.
   - **Nghiêm cấm tuyệt đối**: Không có nút "Nhận thuốc tại kho phòng khám", không có luồng "Thanh toán tiền thuốc tại quầy thu ngân", không hạch toán tiền thuốc vào viện phí hay công nợ bệnh nhân.

2. **Phân cấp dữ liệu Global Master vs Branch-Scoped**:
   - **Global Master Data**: Danh mục dược phẩm (`CatalogMedicine`), Mẫu đơn thuốc chuẩn (`PrescriptionTemplate`). Dùng chung toàn hệ thống, không phụ thuộc chi nhánh.
   - **Operational Data (Branch-Scoped)**: Đơn thuốc phát sinh theo lượt khám (`Prescription` / `ExternalPrescriptionLine`) gắn chặt với `VisitId`, `PatientId`, `PrescribedByDoctorId` và `BranchId`.

3. **Tính toàn vẹn lâm sàng & Concurrency / Idempotency**:
   - Mọi thao tác ghi (tạo/sửa dòng thuốc, áp dụng mẫu, hoàn tất đơn) bắt buộc có `expectedVersion` và `requestKey` (UUID).
   - Ghi dữ liệu nghiệp vụ, `PrescriptionAudit` và `PrescriptionCommandReceipt` trong cùng một Database Transaction nguyên tử.
   - Khi retry cùng `requestKey` và fingerprint payload, trả kết quả đã lưu từ `PrescriptionCommandReceipt`, không ghi đè hay sinh trùng lặp.
   - Trạng thái đơn thuốc: `Draft` (Đang kê nháp) -> `Finalized` (Đã hoàn tất/ký xác nhận). Đơn đã `Finalized` là bất biến (immutable), không được sửa trực tiếp; muốn thay đổi phải tạo bản bổ sung (Addendum) có lý do lâm sàng.

---

## 2. Mô hình dữ liệu & Quan hệ thực thể

### 2.1. Danh mục Thuốc dùng chung (`CatalogMedicine`)
Kế thừa và chuẩn hóa từ `CATALOG_MEDICINE`:
- `Id`: `Guid` (PK)
- `Code`: `string(40)` (Unique, mã thuốc theo quy chuẩn hoặc mã nội bộ)
- `Name`: `string(200)` (Tên biệt dược)
- `ActiveIngredient`: `string(200)` (Hoạt chất chính)
- `Strength`: `string(200)` (Hàm lượng, ví dụ: 500mg, 100ml)
- `DosageForm`: `string(200)` (Dạng bào chế: Viên nén, viên nang, hỗn dịch, mỡ bôi...)
- `BaseUnit`: `string(40)` (Đơn vị tính: viên, gói, chai, tuýp...)
- `Route`: `string(40)` (Đường dùng: Uống, Bôi, Súc miệng, Đặt, Nhỏ...)
- `Packaging`: `string(160)` (Quy cách đóng gói)
- `Contraindications`: `string(1000)` (Chống chỉ định)
- `AllergyWarnings`: `string(1000)` (Cảnh báo dị ứng, nhóm hoạt chất cảnh báo)
- `Instructions`: `string(1000)` (Hướng dẫn sử dụng gợi ý mặc định)
- `ReferencePrice`: `long?` (Giá tham khảo nếu có)
- `PrescriptionEligible`: `bool` (Được phép kê đơn, mặc định `true`)
- `IsActive`: `bool` (Trạng thái sử dụng)
- `Version`: `int` (Concurrency token)

### 2.2. Mẫu đơn thuốc chuẩn (`PrescriptionTemplate` & `PrescriptionTemplateLine`)
Bảng `PRESCRIPTION_TEMPLATE`:
- `Id`: `Guid` (PK)
- `Code`: `string(40)` (Unique)
- `Name`: `string(200)` (Ví dụ: "Đơn thuốc sau nhổ răng khôn", "Đơn thuốc viêm quanh cuống", "Đơn thuốc sau cấy ghép Implant", "Đơn thuốc viêm nướu hoại tử lở loét")
- `DiagnosisKind`: `string(100)` (Loại chẩn đoán gợi ý)
- `Description`: `string(1000)` (Mô tả, ghi chú lâm sàng cho mẫu)
- `IsActive`: `bool` (Mặc định `true`)
- `Version`: `int` (Concurrency token)
- `CreatedBy`: `int` (FK `USER_ACCOUNT`)
- `CreatedAt`: `DateTimeOffset`
- `UpdatedBy`: `int`
- `UpdatedAt`: `DateTimeOffset`

Bảng `PRESCRIPTION_TEMPLATE_LINE`:
- `Id`: `Guid` (PK)
- `TemplateId`: `Guid` (FK `PRESCRIPTION_TEMPLATE`)
- `MedicineId`: `Guid` (FK `CATALOG_MEDICINE`)
- `MedicineCode`: `string(40)`
- `MedicineName`: `string(200)`
- `ActiveIngredient`: `string(200)`
- `Strength`: `string(200)`
- `DosageForm`: `string(200)`
- `Unit`: `string(40)`
- `Route`: `string(40)`
- `MorningDose`: `string(20)` (Liều sáng: ví dụ "1 viên", "0")
- `NoonDose`: `string(20)` (Liều trưa: ví dụ "1 viên", "0")
- `AfternoonDose`: `string(20)` (Liều chiều)
- `EveningDose`: `string(20)` (Liều tối: ví dụ "1 viên")
- `MealTiming`: `string(30)` (`BeforeMeal` - Trước ăn, `AfterMeal` - Sau ăn, `WithMeal` - Trong ăn)
- `Days`: `int` (Số ngày uống gợi ý, ví dụ 5 ngày, 7 ngày)
- `Quantity`: `int` (Tổng số lượng = tổng liều/ngày * số ngày)
- `Instructions`: `string(1000)` (Cách dùng chi tiết)
- `SortOrder`: `int`

### 2.3. Đơn thuốc theo Lượt khám (`ExternalPrescription` & `ExternalPrescriptionLine`)
Bảng `EXTERNAL_PRESCRIPTION_LINE`:
- `Id`: `Guid` (PK)
- `VisitId`: `Guid` (FK `VISIT`)
- `PatientId`: `int` (FK `PATIENT_PROFILE`)
- `BranchId`: `int` (FK `SCHEDULING_BRANCH`)
- `PrescribedByDoctorId`: `int` (FK `USER_ACCOUNT`)
- `MedicineId`: `Guid` (FK `CATALOG_MEDICINE`)
- `MedicineCode`: `string(40)`
- `MedicineName`: `string(200)`
- `ActiveIngredient`: `string(200)`
- `Strength`: `string(200)`
- `DosageForm`: `string(200)`
- `Unit`: `string(100)`
- `Route`: `string(40)`
- `MorningDose`: `string(20)`
- `NoonDose`: `string(20)`
- `AfternoonDose`: `string(20)`
- `EveningDose`: `string(20)`
- `MealTiming`: `string(30)`
- `Days`: `int`
- `Quantity`: `int`
- `Instructions`: `string(1000)`
- `Status`: `string(20)` (`Draft` | `Finalized`)
- `FinalizedAt`: `DateTimeOffset?`
- `Version`: `int`
- `CreatedBy`: `int`
- `CreatedAt`: `DateTimeOffset`
- `UpdatedBy`: `int`
- `UpdatedAt`: `DateTimeOffset`

Bảng `PRESCRIPTION_COMMAND_RECEIPT`:
- `ActorId`: `int` (PK 1)
- `RequestKey`: `Guid` (PK 2)
- `Fingerprint`: `string(64)` (SHA-256)
- `ResultJson`: `jsonb`

---

## 3. Quy trình & Giao diện Kê đơn tại Lượt khám (Staff Web / Doctor UI)

1. **Ngữ cảnh kê đơn**:
   - Tích hợp trực tiếp trong màn hình Lượt khám (`VisitPage` / `PrescriptionPanel`) hoặc trang kê đơn riêng (`/visits/{visitId}/prescription`).
   - Tự động nhận `VisitId`, `PatientId`, `BranchId`, và Bác sĩ kê đơn từ phiên làm việc (`PrescribedByDoctorId`).

2. **Điền nhanh từ Mẫu đơn thuốc (Prescription Templates)**:
   - Nút "Chọn mẫu đơn thuốc" mở Modal danh sách mẫu (`PrescriptionTemplateSelectModal`).
   - Lọc nhanh theo chẩn đoán của lượt khám hoặc tìm kiếm từ khóa.
   - Bấm "Áp dụng": Tự động nạp toàn bộ các dòng thuốc trong mẫu vào đơn của lượt khám hiện tại. Bác sĩ có thể chỉnh sửa liều lượng, số ngày hoặc xóa bớt dòng tùy tình trạng bệnh nhân.

3. **Cảnh báo an toàn lâm sàng (Clinical Safety Warnings)**:
   - Kiểm tra tiền sử dị ứng: So khớp tên hoạt chất (`ActiveIngredient`) và tên thuốc với tiền sử dị ứng trong hồ sơ bệnh nhân (`PatientProfile.MedicalHistory` / `AllergyHistory`).
   - Cảnh báo trùng lặp: Nếu trong cùng đơn có 2 thuốc chứa cùng hoạt chất, cảnh báo màu cam/đỏ.
   - Cảnh báo tương tác/chống chỉ định: Hiển thị ngay dưới ô chọn thuốc trước khi bấm lưu.

4. **Ký / Hoàn tất đơn thuốc (Finalize)**:
   - Nút "Hoàn tất kê đơn (Ký số/Xác nhận)" chuyển trạng thái đơn sang `Finalized`.
   - Đơn đã hoàn tất sẽ khóa sửa trực tiếp để bảo vệ tính pháp lý của hồ sơ bệnh án.

5. **In đơn thuốc quy chuẩn Bộ Y Tế**:
   - Nút "In đơn thuốc" hỗ trợ khổ A4 và A5.
   - Nội dung in bao gồm:
     - Tên cơ sở khám chữa bệnh / Chi nhánh, địa chỉ, số hotline, logo.
     - Họ tên bác sĩ kê đơn, chức danh, số Chứng chỉ hành nghề (CCHN).
     - Thông tin bệnh nhân: Họ tên, Năm sinh/Tuổi, Giới tính, Địa chỉ, Mã bệnh nhân (`PatientCode`).
     - Chẩn đoán lâm sàng / Chẩn đoán ICD-10.
     - Bảng danh sách thuốc: Đánh số 1, 2, 3... Tên biệt dược, hàm lượng, số lượng (ghi số và chữ), đường dùng, liều dùng cụ thể (Sáng/Trưa/Chiều/Tối, Trước/Sau ăn), số ngày.
     - Lời dặn chung của bác sĩ (ví dụ: Tái khám khi có dấu hiệu bất thường, uống nhiều nước...).
     - Ngày tháng năm kê đơn, chữ ký bác sĩ.
     - Mã QR Code xác thực mã đơn thuốc để quét kiểm tra tính nguyên bản.

---

## 4. Bệnh nhân xem đơn thuốc trên Mobile App (Patient App - React Native / Expo)

1. **Phạm vi hiển thị**:
   - Chỉ hiển thị các đơn thuốc ở trạng thái **`Finalized`** của lượt khám thuộc hồ sơ bệnh nhân đã được liên kết và xác minh (`Verified`).
   - Hiển thị theo từng đợt khám / lượt khám trong mục "Hồ sơ sức khỏe / Đơn thuốc".

2. **Giao diện thân thiện, dễ đọc**:
   - Thẻ đơn thuốc hiển thị: Ngày kê đơn, Chi nhánh khám, Bác sĩ kê đơn, Chẩn đoán.
   - Danh sách thuốc dạng thẻ trực quan:
     - Tên thuốc, nồng độ/hàm lượng, dạng dùng.
     - Số lượng và đơn vị.
     - Bảng phân chia liều uống trong ngày có icon rõ ràng: Sáng, Trưa, Chiều, Tối kèm hướng dẫn Uống trước ăn hay Sau ăn.
   - Lời dặn dò của bác sĩ được đóng khung nổi bật.
   - Ghi chú minh bạch: "Đơn thuốc chỉ định mua tại hiệu thuốc ngoài. Phòng khám không bán thuốc tại chỗ."

3. **Tính năng tiện ích**:
   - Nút "Xem bản in đơn thuốc có chữ ký" / "Tải PDF đơn thuốc" để bệnh nhân lưu về máy hoặc mang ra quầy thuốc ngoài mua dễ dàng.

---

## 5. Danh mục API Contracts

### 5.1. Danh mục Mẫu đơn thuốc (Prescription Templates)
- `GET /api/prescriptions/templates?search=&diagnosis=&page=1&pageSize=20` -> Danh sách mẫu paged
- `GET /api/prescriptions/templates/{id}` -> Chi tiết mẫu kèm danh sách thuốc
- `POST /api/prescriptions/templates` -> `{ requestKey, code, name, diagnosisKind, description, lines: [...] }`
- `PUT /api/prescriptions/templates/{id}` -> `{ expectedVersion, requestKey, name, diagnosisKind, description, lines: [...] }`
- `POST /api/prescriptions/templates/{id}/state` -> `{ expectedVersion, requestKey, active, reason }`

### 5.2. Kê đơn theo Lượt khám (Staff / Doctor API)
- `GET /api/prescriptions/visits/{visitId}/lines` -> Danh sách dòng thuốc của lượt khám
- `POST /api/prescriptions/visits/{visitId}/lines` -> Thêm dòng thuốc mới vào đơn
- `PUT /api/prescriptions/visits/{visitId}/lines/{lineId}` -> Sửa dòng thuốc
- `DELETE /api/prescriptions/visits/{visitId}/lines/{lineId}` -> Xóa dòng thuốc khỏi đơn nháp
- `POST /api/prescriptions/visits/{visitId}/apply-template` -> Áp dụng cả mẫu đơn thuốc vào lượt khám
- `POST /api/prescriptions/visits/{visitId}/finalize` -> Hoàn tất/ký đơn thuốc
- `GET /api/prescriptions/visits/{visitId}/print` -> Dữ liệu chuẩn phục vụ in ấn đơn thuốc (kèm QR code & thông tin chi nhánh)

### 5.3. Bệnh nhân xem đơn thuốc (Patient App API)
- `GET /api/patients/me/prescriptions` -> Danh sách đơn thuốc chính thức của bệnh nhân
- `GET /api/patients/me/visits/{visitId}/prescription` -> Chi tiết đơn thuốc chính thức theo lượt khám
