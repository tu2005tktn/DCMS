# Phân hệ Quản lý & Kê đơn thuốc (Prescription Management)

> Luồng staff/mobile đơn giản hiện hành được quy định tại [S23 — Kê đơn trực tiếp từ hồ sơ bệnh nhân](./S23-PATIENT-PRESCRIPTIONS.vi.md). Không hiển thị mục/quản lý mẫu đơn thuốc hoặc bắt staff đi qua luồng nhiều bước theo Visit. Các contract Visit/template bên dưới được giữ để tương thích dữ liệu và API cũ; chúng không phải đường đi mặc định cho demo hoặc thao tác kê đơn mới.

Ngày cập nhật: 07/11/2026 (bản 2 — header đơn thuốc, phiên bản điều chỉnh, migration, mobile). Trạng thái: đặc tả triển khai.
Tài liệu liên quan: [S19-OPTIONAL-PRESCRIPTION.vi.md](S19-OPTIONAL-PRESCRIPTION.vi.md), [S58-SUPPLY-CATALOGS.md](S58-SUPPLY-CATALOGS.md), [S41-MOBILE-MEDICAL-RECORDS.md](S41-MOBILE-MEDICAL-RECORDS.md).

## Khôi phục điều hướng web (08/11/2026)

### Sửa tích hợp sau PR #53 (10/11/2026)

- Router chỉ import một lần trang danh sách đơn thuốc bệnh nhân bằng default export và trang chi tiết bằng named export; giữ hai route `/customer/prescriptions` và `/customer/prescriptions/:id` trong vùng xác thực khách hàng hiện hữu.
- `/commercial/treatment` tiếp tục chuyển tới `/settings/treatment-services`, giữ nguyên query string; khai báo hàm chuyển hướng độc lập với default export `App`.
- Giữ luồng kê đơn trực tiếp S23 và quyết định ẩn mẫu đơn/luồng Visit trong PR #53. Các ghi chú khôi phục menu mẫu ngày 08–09/11 bên dưới là lịch sử, không tái bật menu/route trong lần sửa tích hợp này. Không thay đổi API hoặc quyền truy cập.

- Mục **Mẫu đơn thuốc** mở `/prescriptions/templates` trong workspace nhân viên, yêu cầu đăng nhập và quyền `prescriptions.manage`, giống điều kiện hiển thị menu. Truy cập trực tiếp cũng phải kiểm tra quyền; không có quyền thì hiển thị 403.
- Entry point web phải render router `App` qua default export của `App.tsx`. Hàm chuyển hướng `/commercial/treatment` là hàm riêng, giữ nguyên query string khi chuyển tới `/settings/treatment-services`.
- Bản sửa khôi phục route bị mất khi merge và default export của router; không thay đổi API hoặc hợp đồng dữ liệu đơn thuốc.
- Tích hợp main ngày 09/11/2026: khi giữ route `/prescriptions/templates`, router phải import `PrescriptionTemplatesPage` từ trang quản lý mẫu đơn hiện có để build TypeScript thành công; giữ nguyên kiểm tra quyền `prescriptions.manage`.
Điều hướng web nhân viên: mục "Mẫu đơn thuốc" trong nhóm Chuyên môn mở `/prescriptions/templates`, yêu cầu quyền `prescriptions.manage` và hiển thị `PrescriptionTemplatesPage`. Tuyến này phải được khai báo trong router để menu hoạt động và bản build web thành công.

---

## 1. Nguyên tắc cốt lõi

1. **Đơn thuốc mua ngoài (External Prescription Only)**
   - Phòng khám không bán thuốc. Mọi đơn là **đơn mua ngoài**; bệnh nhân mang ra hiệu thuốc.
   - Nghiêm cấm: nút "Nhận thuốc tại kho phòng khám", luồng "Thanh toán tiền thuốc tại quầy", hạch toán tiền thuốc vào viện phí/công nợ. Đơn thuốc không tạo `FINANCE_CHARGE`, không trừ tồn kho.
   - `ReferencePrice` của danh mục thuốc chỉ để tham khảo, không hiển thị trên bản in/app bệnh nhân.

2. **Global Master vs Branch-Scoped**
   - Global: danh mục thuốc `CATALOG_MEDICINE` (quản lý tại `/settings/medicines`, xem [S58-SUPPLY-CATALOGS.md](S58-SUPPLY-CATALOGS.md)), mẫu đơn `PRESCRIPTION_TEMPLATE`.
   - Branch-scoped: đơn theo lượt khám `EXTERNAL_PRESCRIPTION` + `EXTERNAL_PRESCRIPTION_LINE`, gắn `VisitId`, `PatientId`, `BranchId`, `PrescribedByDoctorId`. Mọi thao tác đọc/ghi đơn của staff kiểm quyền và phạm vi chi nhánh của **Visit** ở server (`prescriptions.read` / `prescriptions.manage` + branch scope), không tin `branchId` từ UI. Lệnh ghi trên đơn của lượt khám còn yêu cầu tài khoản có vai trò **Dentist** (bác sĩ được kê đơn); quản lý mẫu đơn chỉ cần `prescriptions.manage`.

3. **Concurrency / Idempotency**
   - Mọi lệnh ghi có `requestKey` (UUID, khác `Guid.Empty`) và `expectedVersion`.
   - Dữ liệu nghiệp vụ + `PRESCRIPTION_AUDIT` + `PRESCRIPTION_COMMAND_RECEIPT` ghi trong **một transaction** (khóa advisory chung của phân hệ).
   - Retry cùng `requestKey` + cùng fingerprint payload trả lại kết quả đã lưu; cùng `requestKey` nhưng payload khác trả `409 MSG111`. Áp dụng cho cả lệnh xóa dòng.
   - Lệnh trên dòng thuốc kiểm `expectedVersion` của **dòng**; lệnh trên cả đơn (áp dụng mẫu, sửa lời dặn chung, hoàn tất, tạo bản điều chỉnh) kiểm `expectedVersion` của **đơn**. Mọi thay đổi dòng cũng tăng `Version` của đơn để lệnh hoàn tất luôn ký đúng nội dung bác sĩ vừa xem. Sai version → `409 MSG111` kèm `currentVersion`.

4. **Vòng đời & bất biến**
   - Mỗi lượt khám có chuỗi phiên bản đơn `Revision = 1, 2, ...`. Tại một thời điểm có tối đa một phiên bản `Draft`.
   - `Draft` → `Finalized` (bác sĩ bấm "Hoàn tất kê đơn"). Phiên bản `Finalized` **bất biến**: không thêm/sửa/xóa dòng, không áp dụng mẫu, không sửa lời dặn (`409 MSG205`).
   - Muốn thay đổi đơn đã ký: tạo **bản điều chỉnh** (correction/addendum) với lý do lâm sàng bắt buộc (1–500 ký tự). Bản điều chỉnh là `Draft` mới `Revision = n+1`, sao chép dòng và lời dặn của bản đã ký, `SupersedesId` trỏ về bản cũ. Khi bản điều chỉnh được hoàn tất, bản cũ chuyển `Superseded` (vẫn đọc được trong lịch sử, không bao giờ bị xóa).
   - Đơn `Draft` rỗng (chưa có dòng) không thể hoàn tất (`400 MSG04`).
   - Điều kiện lượt khám: Revision 1 chỉ ghi được khi Visit `InProgress`. Bản điều chỉnh (Revision > 1) được tạo/sửa/hoàn tất khi Visit `InProgress` hoặc `Completed`. Visit `Queued`/`Cancelled` không cho ghi (`409 MSG205`).
   - Không hard-delete đơn đã ký. Xóa dòng chỉ áp dụng cho dòng thuộc bản `Draft`.

---

## 2. Mô hình dữ liệu

### 2.1. `CATALOG_MEDICINE` (đã có, S58)
`Code`, `Name`, `ActiveIngredient`, `Strength`, `DosageForm`, `BaseUnit`, `Route`, `Packaging`, `Contraindications`, `AllergyWarnings`, `Instructions`, `ReferencePrice`, `PrescriptionEligible`, `IsActive`, `Version`. Ngừng dùng/khôi phục, không xóa cứng. Chỉ thuốc `IsActive && PrescriptionEligible` mới chọn được khi kê/lập mẫu.

### 2.2. `PRESCRIPTION_TEMPLATE` / `PRESCRIPTION_TEMPLATE_LINE`
- Template: `Id`, `Code` (unique, 1–40), `Name` (1–200), `DiagnosisKind` (≤100), `Description` (≤1000), `IsActive`, `Version`, `CreatedBy/At`, `UpdatedBy/At`.
- Line: `TemplateId`, `MedicineId` + snapshot thuốc (`MedicineCode`, `MedicineName`, `ActiveIngredient`, `Strength`, `DosageForm`, `Unit`), `Route`, `MorningDose`, `NoonDose`, `AfternoonDose`, `EveningDose` (≤20 ký tự, `"0"` = không dùng buổi đó), `MealTiming` (`BeforeMeal|AfterMeal|WithMeal`), `Days` (1–365), `Quantity` (1–10.000), `Instructions` (≤1000), `SortOrder`.
- Mẫu cần ít nhất 1 dòng, tối đa 30 dòng. Mã trùng → `409 MSG201`. Ngừng dùng/khôi phục qua `/state` kèm lý do; mẫu ngừng dùng không xuất hiện khi kê nhưng vẫn đọc được trong trang quản lý (lọc `status=Active|Inactive|All`).

### 2.3. `EXTERNAL_PRESCRIPTION` (header — mới)
- `Id` (Guid), `VisitId`, `PatientId`, `BranchId`, `PrescribedByDoctorId`
- `Revision` (int ≥ 1; unique `(VisitId, Revision)`)
- `Status`: `Draft | Finalized | Superseded`
- `GeneralAdvice` (≤1000) — lời dặn chung của bác sĩ
- `Diagnosis` (≤1000) — snapshot chẩn đoán từ phiếu khám lúc hoàn tất
- `CorrectionReason` (≤500, bắt buộc khi Revision > 1), `SupersedesId` (Guid?)
- `FinalizedAt`, `FinalizedBy`, `VerificationCode` (16 ký tự hex, sinh lúc hoàn tất từ SHA-256 nội dung đơn)
- `Version`, `CreatedBy/At`, `UpdatedBy/At`

### 2.4. `EXTERNAL_PRESCRIPTION_LINE`
Như S19 + các cột liều: `PrescriptionId` (FK header, bắt buộc), `ActiveIngredient`, `Route`, `MorningDose`, `NoonDose`, `AfternoonDose`, `EveningDose`, `MealTiming`, `Days`, `Status` (đồng bộ với header), `FinalizedAt`, `PrescribedByDoctorId`, `Version`. Validation: `Quantity` 1–10.000; `Days` 1–365; mỗi liều ≤20 ký tự; `MealTiming` thuộc enum; `Route` ≤40; `Instructions` ≤1000 → sai trả `400 MSG04` kèm `field`.

Cho phép bác sĩ thêm thuốc dùng riêng cho một đơn khi chưa có trong danh mục: `MedicineId` nullable, các trường tên/hàm lượng/dạng dùng/đơn vị tiếp tục được lưu snapshot trên dòng. Dòng tùy chỉnh không được đưa vào mẫu đơn, không tạo/cập nhật `CATALOG_MEDICINE`, không có cảnh báo chống chỉ định dựa trên danh mục. Dòng danh mục vẫn bắt buộc tham chiếu thuốc đang hoạt động và được phép kê. Dữ liệu custom bắt buộc có `CustomMedicineName` 1–200 ký tự và `CustomUnit` 1–100 ký tự; mọi quy tắc liều/lượng/lời dặn hiện có vẫn áp dụng.

### 2.5. `PRESCRIPTION_AUDIT`, `PRESCRIPTION_COMMAND_RECEIPT`
- Audit: `ActorId`, `BranchId`, `VisitId?`, `TemplateId?`, `Action` (`LineCreate|LineUpdate|LineDelete|TemplateApply|AdviceUpdate|Finalize|CorrectionCreate|TemplateCreate|TemplateUpdate|TemplateState`), `Reason`, `BeforeJson`, `AfterJson`, `At`.
- Receipt: PK `(ActorId, RequestKey)`, `Fingerprint` (SHA-256 hex), `ResultJson`.

### 2.6. Migration
Migration `20261107050902_PrescriptionsAndBranchProfiles` (dùng chung với S58 chi nhánh; Up/Down viết tay, Designer + snapshot sinh bằng `dotnet ef`):
- Thêm cột liều/đường dùng/hoạt chất/trạng thái vào `EXTERNAL_PRESCRIPTION_LINE` với default an toàn cho dòng cũ.
- Tạo `EXTERNAL_PRESCRIPTION`, `PRESCRIPTION_TEMPLATE`, `PRESCRIPTION_TEMPLATE_LINE`, `PRESCRIPTION_AUDIT`.
- Backfill: mỗi `VisitId` đã có dòng cũ sinh một header `Revision 1, Status Draft`, gắn `PrescriptionId` cho các dòng rồi đặt cột `NOT NULL`. Không tạo dữ liệu demo.
- Migration `20261107130000_AllowCustomPrescriptionMedicines` cho phép `MedicineId` null ở dòng đơn thuốc; khóa ngoại danh mục vẫn được giữ cho thuốc tham chiếu.

---

## 3. Kê đơn tại lượt khám (Staff Web)

1. **Ngữ cảnh**: khối `PrescriptionPanel` trong Tổng kết khám (S19) và trang riêng `/visits/{visitId}/prescription`. `VisitId`, `PatientId`, `BranchId` lấy từ Visit ở server; bác sĩ kê đơn = tài khoản đang thao tác (`PrescribedByDoctorId`).
2. **Chọn mẫu đơn**: nút "Chọn mẫu đơn thuốc" mở modal, tìm theo từ khóa/chẩn đoán, xem trước dòng thuốc, bấm "Áp dụng" → server thêm các dòng của mẫu vào bản `Draft` hiện tại (bỏ qua dòng có thuốc đã ngừng dùng và báo số dòng bỏ qua). Bác sĩ chỉnh liều/số ngày/xóa dòng sau đó.
3. **Từng dòng thuốc**: chọn thuốc bằng Select có tìm kiếm server (`q` theo mã, biệt dược, hoạt chất); đường dùng; liều Sáng/Trưa/Chiều/Tối; trước/sau/trong ăn; số ngày; tổng số lượng tự tính `ceil(tổng liều số/ngày × số ngày)` khi liều là số, vẫn cho chỉnh tay; lời dặn từng dòng.
4. **Lời dặn chung**: ô "Lời dặn của bác sĩ" cho cả đơn, lưu bằng `PUT .../prescription` (Draft).
5. **Cảnh báo an toàn lâm sàng** (`GET .../safety-check`, không chặn lưu):
   - Dị ứng: so khớp hoạt chất/tên thuốc với `Allergies.Detail` của phiếu khám lượt này (khi `State = Present`) và tiền sử hành chính `IntakeHistoryOther` của hồ sơ bệnh nhân → cảnh báo đỏ.
   - Nhóm dị ứng: so khớp từ khóa trong `AllergyWarnings` của thuốc với tiền sử dị ứng → cảnh báo đỏ.
   - Trùng hoạt chất trong cùng đơn → cảnh báo cam.
   - Có `Contraindications` → hiển thị lưu ý chống chỉ định (vàng) để bác sĩ đối chiếu.
6. **Hoàn tất kê đơn**: xác nhận → `POST .../finalize` với `expectedVersion` của đơn. Sau khi hoàn tất, UI khóa sửa, hiện nút "In đơn thuốc" và "Tạo bản điều chỉnh".
7. **Bản điều chỉnh**: modal nhập lý do lâm sàng bắt buộc → `POST .../corrections`. Panel hiển thị bản nháp điều chỉnh + lý do; lịch sử các phiên bản (ngày ký, bác sĩ, trạng thái) đọc được.
8. **Xung đột 409 MSG111**: giữ nguyên form đang nhập, tải lại đơn để bác sĩ so sánh rồi lưu lại.
9. **In đơn** (A5 mặc định, chọn A4): tên chi nhánh, địa chỉ, điện thoại, logo (nếu chi nhánh có logo đã upload), bác sĩ kê đơn (CCHN để trống dòng ký nếu hồ sơ nhân sự chưa có dữ liệu — không in giá trị giả), thông tin bệnh nhân (mã BN, họ tên, năm sinh, giới tính, địa chỉ), chẩn đoán, bảng thuốc đánh số (tên, hàm lượng, số lượng bằng số và chữ, đường dùng, liều cụ thể, số ngày), lời dặn chung, ngày kê, chữ ký bác sĩ, dòng "Đơn thuốc mua ngoài — phòng khám không bán thuốc", mã QR xác thực. Bản nháp in có watermark "BẢN NHÁP — CHƯA CÓ GIÁ TRỊ".
   - QR chứa `ODENTAL-RX|{prescriptionId}|R{revision}|{verificationCode}`; `verificationCode` là 16 ký tự đầu SHA-256 của nội dung đơn đã ký.

### 3.2 Quản lý mẫu và dữ liệu demo

- Mục menu **Mẫu đơn thuốc** tại `/prescriptions/templates` phải được đăng ký route staff và bảo vệ bằng `prescriptions.manage`; không được để menu dẫn tới trang 404.
- Trang quản lý hỗ trợ tạo/sửa/ngừng dùng/khôi phục mẫu. Modal **Chọn mẫu đơn thuốc** trên màn kê đơn chỉ liệt kê mẫu đang hoạt động và cho xem trước trước khi áp dụng.
- Môi trường Development có dữ liệu demo tổng hợp (synthetic), gồm tối thiểu ba mẫu theo nhóm chăm sóc sau điều trị, vệ sinh răng miệng và viêm nha chu. Mã mẫu có tiền tố `DEMO-`, mô tả ghi rõ chỉ phục vụ demo; không được xem các liều mẫu là hướng dẫn điều trị thực tế.
- Seeder demo chạy lặp an toàn, chỉ thêm mẫu còn thiếu, tham chiếu thuốc đang hoạt động trong danh mục và không tạo đơn bệnh nhân/ký đơn. Production không được chạy seeder này.

### 3.1 Thêm thuốc vào danh mục ngay khi kê đơn

- Nút `+` nhỏ cạnh ô chọn thuốc mở popup tạo thuốc theo luồng Odental: tên, mã thuốc/mã sản phẩm, hoạt chất/biệt dược, hàm lượng, dạng bào chế, đơn vị gốc, quy cách đóng gói, giá tham khảo và hướng dẫn sử dụng mặc định.
- Lưu qua API danh mục thuốc hiện có (`catalog.medicines.manage`), không tạo một dòng thuốc chỉ thuộc đơn hiện tại. Thuốc mới mặc định được bật `prescriptionEligible=true`, `internalStockTracked=false`; thao tác không tạo tồn kho, xuất kho hay phí. Kiểm tra trùng mã/tên và lỗi quyền theo contract danh mục; không tiếp tục kê nếu API báo lỗi.
- Khi tạo thành công, thêm thuốc mới vào lựa chọn, chọn ngay cho dòng kê hiện tại, điền đơn vị và hướng dẫn mặc định vào dòng; bác sĩ tiếp tục nhập liều, số ngày, số lượng và có thể sửa lời dặn riêng trước khi thêm vào đơn.
- Người chỉ có quyền kê đơn nhưng không có quyền quản lý danh mục không được tạo thuốc qua nút này. Thuốc tùy chỉnh chỉ dùng cho đơn hiện tại tiếp tục là luồng riêng, không đưa vào danh mục.
- Nút thêm dòng lớn màu xanh, lời dặn chung, bác sĩ kê đơn, ngày kê, chọn mẫu, cảnh báo lâm sàng, sửa/xóa dòng và hoàn tất đơn giữ nguyên. Bản in và app bệnh nhân hiển thị snapshot tên/đơn vị/liều/hướng dẫn.

---

## 4. Bệnh nhân xem đơn thuốc (Mobile — Expo)

1. **Phạm vi**: chỉ đơn `Finalized` (không gồm `Draft`, `Superseded`) của lượt khám thuộc **mọi** hồ sơ bệnh nhân mà tài khoản đã liên kết `Verified`. Không có liên kết Verified → danh sách rỗng kèm hướng dẫn liên kết (không lộ thông tin). Đơn của hồ sơ khác → `404 MSG113`. Thu hồi liên kết → mất quyền ngay; app xóa dữ liệu đơn đang hiển thị khi phiên/liên kết đổi.
2. **Danh sách** (Hồ sơ y tế → "Đơn thuốc", route `/(account)/prescriptions`): ngày kê, chi nhánh, bác sĩ, chẩn đoán, số thuốc, nhãn "Bản điều chỉnh" nếu Revision > 1.
3. **Chi tiết**: thẻ từng thuốc (tên, hàm lượng, dạng dùng, số lượng + đơn vị, đường dùng), bảng liều Sáng/Trưa/Chiều/Tối, trước/sau/trong ăn, số ngày, lời dặn từng thuốc; lời dặn chung đóng khung nổi bật; ghi chú cố định "Đơn thuốc mua ngoài. Phòng khám không bán thuốc tại chỗ."
4. **Không có** nút nhận thuốc tại kho/thanh toán tiền thuốc.
5. **"Xem bản in đơn thuốc"**: màn hình bản in (bố cục như bản in web: chi nhánh, logo, bác sĩ, bệnh nhân, chẩn đoán, bảng thuốc, lời dặn, ngày kê) kèm **mã xác thực** dạng chữ để bệnh nhân đưa cho hiệu thuốc hoặc chụp màn hình. App hiện chưa có thư viện QR/PDF (`expo-print`, `react-native-svg`) nên QR và tải PDF chỉ có trên bản in web; khi bổ sung các gói này, màn bản in dùng cùng DTO `print` để sinh QR/PDF mà không đổi API.
6. Không hiển thị ghi chú nội bộ, giá tham khảo, lý do điều chỉnh nội bộ (chỉ hiển thị nhãn "Bản điều chỉnh").

### 4.1 Truy cập nhanh, danh sách và chia sẻ trên mobile

- Trang chủ tài khoản Public có card truy cập nhanh **Đơn thuốc của tôi**, mô tả ngắn rằng người bệnh có thể xem thuốc và hướng dẫn của bác sĩ; chọn card mở `/(account)/prescriptions`. Card không tự tải danh sách riêng để đếm badge.
- Danh sách sắp xếp mới nhất trước (theo API), mỗi mục hiển thị ngày kê, chi nhánh, bác sĩ, chẩn đoán, số thuốc và nhãn bản điều chỉnh khi có. Không tự tạo mã đơn để trình bày khi API không cung cấp mã đó.
- Chỉ hiển thị badge số đơn mới nếu API cung cấp trạng thái chưa xem; hiện chưa có trường/API này nên không hiển thị số giả.
- Chi tiết giữ thông tin chi nhánh, bác sĩ, ngày kê, chẩn đoán, từng thuốc, liều/lượng và lời dặn nổi bật như mục 3 ở trên.
- Bản in hiện hiển thị trong app để người bệnh lưu/chụp màn hình. Bổ sung nút **Chia sẻ đơn thuốc** dùng native share sheet, chia sẻ nội dung văn bản tối thiểu (ngày kê, chi nhánh, bác sĩ, thuốc/liều/lời dặn và mã xác thực nếu có). Không chia sẻ thông tin hồ sơ ngoài dữ liệu của đơn. Chưa tạo hoặc tải PDF trên mobile; PDF vẫn thuộc bản in web cho đến khi tích hợp thư viện tạo/tải PDF.

---

## 5. API Contracts

Lỗi theo chuẩn `{ code, message, field?, currentVersion?, blockers? }`.

### 5.1. Mẫu đơn thuốc (`prescriptions.manage`)
- `GET /api/prescriptions/templates?search=&diagnosis=&status=Active|Inactive|All&page=1&pageSize=20|50|100` → mảng mẫu (search ≤100 ký tự).
- `GET /api/prescriptions/templates/{id}`
- `POST /api/prescriptions/templates` → `{ requestKey, expectedVersion: 0, code, name, diagnosisKind, description, lines: [...] }`
- `PUT /api/prescriptions/templates/{id}` → `{ requestKey, expectedVersion, code, name, diagnosisKind, description, lines }` (mã không đổi sau khi tạo)
- `POST /api/prescriptions/templates/{id}/state` → `{ requestKey, expectedVersion, active, reason }`

### 5.2. Kê đơn theo lượt khám
- `GET /api/prescriptions/medicine-options?branchId=&q=` (`prescriptions.manage`; có `branchId` thì kiểm phạm vi chi nhánh khi kê tại lượt khám, bỏ `branchId` khi lập mẫu đơn toàn hệ thống)
- `GET /api/prescriptions/visits/{visitId}/prescription` → `{ current: PrescriptionDto | null, history: PrescriptionSummaryDto[] }`; `PrescriptionDto` gồm header + `lines`.
- `GET /api/prescriptions/visits/{visitId}/lines` → dòng của phiên bản hiện hành (tương thích S19).
- `PUT /api/prescriptions/visits/{visitId}/prescription` → `{ requestKey, expectedVersion, generalAdvice }`
- `POST /api/prescriptions/visits/{visitId}/lines` → `{ requestKey, expectedVersion: 0, medicineId, route, morningDose, noonDose, afternoonDose, eveningDose, mealTiming, days, quantity, instructions }` (tạo header Draft Revision 1 nếu chưa có)
- `PUT /api/prescriptions/visits/{visitId}/lines/{lineId}` → cùng body, `expectedVersion` của dòng

Lệnh tạo/sửa dòng chọn đúng một nguồn thuốc: `medicineId` cho thuốc danh mục, hoặc `customMedicineName` + `customUnit` cho thuốc tùy chỉnh (không gửi `medicineId`).
`MedicineId` trong DTO dòng có thể null chỉ với dòng tùy chỉnh.
- `DELETE /api/prescriptions/visits/{visitId}/lines/{lineId}?requestKey=&expectedVersion=`
- `POST /api/prescriptions/visits/{visitId}/apply-template` → `{ requestKey, expectedVersion (đơn; 0 khi chưa có đơn), templateId }` → `PrescriptionDto` + `skippedLines`
- `POST /api/prescriptions/visits/{visitId}/finalize` → `{ requestKey, expectedVersion, clinicalNote? }` → `PrescriptionDto`
- `POST /api/prescriptions/visits/{visitId}/corrections` → `{ requestKey, expectedVersion (bản đã ký), reason }` → `PrescriptionDto` (Draft mới)
- `GET /api/prescriptions/visits/{visitId}/safety-check`
- `GET /api/prescriptions/visits/{visitId}/print` → dữ liệu in của phiên bản hiện hành (Draft có cờ `isDraft`)

### 5.3. Bệnh nhân (tài khoản công khai đã liên kết Verified)
- `GET /api/patients/me/prescriptions` → `PatientPrescriptionDto[]` mới nhất trước
- `GET /api/patients/me/prescriptions/visits/{visitId}` → chi tiết bản `Finalized` hiện hành của lượt khám, gồm dữ liệu bản in (`verificationCode`, `qrData`, thông tin chi nhánh)

### 5.4. Mã lỗi
| Tình huống | Mã |
| --- | --- |
| Thiếu `requestKey`, sai enum/range/độ dài, đơn rỗng khi hoàn tất, thiếu lý do điều chỉnh | `400 MSG04` / `MSG02` |
| Không có quyền / ngoài phạm vi chi nhánh / tài khoản chưa liên kết Verified khi xem chi tiết | `403 MSG06` |
| Visit, dòng, mẫu, thuốc không tồn tại hoặc thuốc đã ngừng dùng | `404 MSG113` |
| Mã mẫu trùng | `409 MSG201` |
| Sửa đơn đã ký, Visit không ở trạng thái cho phép, tạo điều chỉnh khi còn bản nháp | `409 MSG205` |
| Sai version / requestKey dùng lại với payload khác | `409 MSG111` (+ `currentVersion`) |


## Sửa lỗi biên dịch trùng tên DTO (08/11/2026)

`PatientPrescriptionDto` bị khai báo hai lần trong `PrescriptionContracts.cs` (bản cho cổng bệnh nhân theo lượt khám và một bản cũ theo bệnh nhân), làm backend không build được. Chỉ giữ bản theo lượt khám đang được `PatientPrescriptionsAsync` và API dùng; bản cũ không có nơi nào dùng nên bị xóa. API và hợp đồng dữ liệu không đổi.
