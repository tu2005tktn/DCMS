# Mobile bệnh nhân — Xem hình ảnh, kết quả khám và tài liệu theo lượt

Ngày 08/11/2026. Người thực hiện: Ngô Minh Trí. Tiếp nối DCMS-107 (Done) và [S41 hồ sơ y tế](S41-MOBILE-MEDICAL-RECORDS.md).

Liên quan:
- [DCMS-112 khám](S19-S20-DCMS-112.md)
- [Lịch sử khám staff](PATIENT-EXAMINATION-HISTORY.vi.md)
- [Ảnh phiếu điều trị](TREATMENT-SHEET-IMAGES.md)
- Đơn thuốc bệnh nhân `/api/patients/me/prescriptions` ([PRESCRIPTION-MANAGEMENT](PRESCRIPTION-MANAGEMENT.md))

## 1. Đối chiếu trước khi làm

| Phần | Đã có trên `main` | Bổ sung trong task này |
| --- | --- | --- |
| Gate liên kết, identity, nhóm hồ sơ chính thức | S41 `MedicalRecordsScreen`, `/api/patient-links/me`, `/record`, `/api/patients/me/treatment-summary` | Giữ nguyên |
| Lượt khám | `/api/me/appointments` (lịch hẹn, không có kết quả) | **API mới** lịch sử lượt khám có phiếu đã xác nhận |
| Kết quả khám chính thức | Chỉ API staff `/api/visits/{id}/examination`, `/api/patients/{id}/examinations` | **API mới** chi tiết kết quả cho bệnh nhân |
| Ảnh và tài liệu | Chỉ API staff `/api/clinical-documents` | **API mới** danh sách tài liệu `Ready` và nội dung tệp cho bệnh nhân |
| Đơn thuốc mua ngoài | `/api/patients/me/prescriptions` và màn `prescriptions/[visitId]` | Liên kết từ chi tiết lượt khám |

**Không dùng API staff cho bệnh nhân.** Phần backend own-record do S41 ghi chờ DCMS-95. Task này bổ sung phần đọc tối thiểu ở mục 3, theo đúng nguyên tắc của S41:
- Patient luôn lấy từ session, không lấy từ client.
- Liên kết VERIFIED được kiểm lại ở mọi request.
- Chỉ trả dữ liệu chính thức.

Người phụ trách DCMS-95/DCMS-104 rà lại contract khi review PR.

## 2. Màn hình và luồng

1. **Hồ sơ y tế của tôi** (S41) → thẻ **Kết quả khám theo lượt** → route `(account)/visit-results`.
2. **Danh sách lượt khám.** Mỗi dòng gồm:
   - ngày khám, bác sĩ, chi nhánh;
   - chẩn đoán tóm tắt;
   - nhãn **Phiếu đã xác nhận** và **Lượt đã kết thúc** (hai trạng thái riêng);
   - số ảnh/tài liệu; có đơn thuốc hay không.

   Danh sách có kéo để làm mới và **Tải thêm** theo trang (20 lượt/trang). Có trạng thái đang tải, rỗng ("Chưa có kết quả khám chính thức"), và lỗi kèm nút **Thử lại**.
3. **Chi tiết lượt** `(account)/visit-results/[visitId]`, chỉ đọc:
   - **Kết quả khám:** lý do khám; kết quả theo răng/vùng (răng hoặc vùng, mô tả, mặt răng, chẩn đoán); chẩn đoán cuối; kết luận; dặn dò; hướng xử trí; nhu cầu quay lại; nơi/lý do chuyển khám. Mục nào trống thì không hiển thị, không ghi giá trị giả.
   - **Trạng thái:** "Phiếu đã xác nhận · phiên bản N"; "Lượt đã kết thúc lúc …" chỉ khi có `completedAt`.
   - **Cận lâm sàng:** tên, loại, vùng, trạng thái. Kết quả và nhận định chỉ hiện khi chỉ định đã được bác sĩ đọc (`Reviewed`). Chỉ định bị hủy không hiện.
   - **Đơn thuốc mua ngoài:** nút mở màn đơn thuốc hiện có, khi `hasPrescription`.
   - **Hình ảnh và tài liệu của lượt:**
     - Lưới thumbnail, có Tải thêm.
     - Bấm ảnh mở **trình xem toàn màn hình**: vuốt hoặc nút ‹ › để chuyển ảnh; hai ngón hoặc nút + / − để phóng to, thu nhỏ; kéo để di chuyển khi đang phóng to; hiện loại ảnh, ngày và nguồn.
     - Tệp không phải ảnh (PDF…) hiện dòng tên, loại, ngày kèm thông báo "Định dạng này chưa xem được trong app. Vui lòng liên hệ phòng khám để nhận bản sao".
     - Ảnh tải lỗi hiện ô báo lỗi có **Thử lại**, không làm hỏng cả lưới.
4. **Ranh giới với dossier của S41:**
   - Chi tiết dossier giữ phần kế hoạch (dự kiến/đã đồng ý) theo DCMS-107.
   - Màn này chỉ hiển thị **kết quả của lượt khám**, không trộn việc dự kiến với việc đã làm.
   - Không hiển thị giờ hẹn khi nguồn không có.

## 3. API bệnh nhân (policy `PublicAccountPolicy`)

**Xác định Patient và quyền**
- Patient là hồ sơ của liên kết **tự thân** `PatientAccountLink.Status = Verified` của tài khoản đang đăng nhập.
- Liên kết được kiểm lại trong **mọi** request, kể cả request nội dung tệp. Không có liên kết VERIFIED → `403 MSG06`.
- Không nhận `patientId` từ client. ID lượt hoặc tài liệu không thuộc Patient đó → `404 MSG113`, không tiết lộ tồn tại.
- Người đặt hoặc trả hộ không có quyền nếu không có liên kết tự thân.

| Method | Route | Nội dung |
| --- | --- | --- |
| GET | `/api/patients/me/visits?page=1&pageSize=20` | `{ items, total, page, pageSize }`. Chỉ các Visit của Patient có `VisitExamination.Confirmed = true`, sắp theo giờ đến giảm dần. `pageSize` 1–50 |
| GET | `/api/patients/me/visits/{visitId}` | Chi tiết kết quả chính thức của lượt (mục 3.2). Lượt không có phiếu đã xác nhận → 404 |
| GET | `/api/patients/me/documents?visitId=&page=1&pageSize=20` | Tài liệu `FileState = Ready` của Patient, lọc theo lượt nếu có `visitId` (tài liệu chỉ định cận lâm sàng của lượt cũng thuộc lượt). Sắp theo ngày nguồn giảm dần. `pageSize` 1–50 |
| GET | `/api/patients/me/documents/{documentId}/content` | Nội dung tệp, `Content-Disposition: inline`. Chỉ khi `Ready` và SHA-256 khớp. Chưa Ready hoặc hỏng → `409 MSG42`; lỗi đọc kho → `503 MSG144`. Ghi audit `Viewed` cho tài khoản bệnh nhân. Header `Cache-Control: no-store` |

### 3.1. Dòng lượt khám

```
{ visitId, arrivalAt, branchName, dentistName, visitStatus, completedAt, examinationVersion,
  diagnosisSummary, documentCount, hasPrescription }
```

- `diagnosisSummary` là chuỗi chẩn đoán cuối của phiếu đã xác nhận.
- `completedAt` lấy từ audit `CompleteVisit`, như lịch sử staff. Lượt chưa kết thúc thì null.
- `documentCount` chỉ đếm tài liệu Ready.
- `hasPrescription` chỉ tính đơn mua ngoài đã ký (Finalized).

### 3.2. Chi tiết lượt

```
{ visit: <dòng 3.1>,
  examination: { version, symptoms, findings: [{ tooth, region, condition, surfaces, diagnoses }], diagnoses,
                 conclusion, advice, disposition, returnNeed, referralDestination },
  investigations: [{ id, kind, name, region, state, resultText, interpretation, reviewedAt }] }
```

**Không trả:**
- ghi chú khám nội bộ (`notes`);
- tiền sử, dị ứng, thuốc đang dùng do nhân viên ghi;
- bản nháp và các phiên bản cũ;
- người tạo/nhận kết quả, lý do chỉ định;
- giá, phí, nhà cung cấp, chi phí thuê ngoài;
- chỉ định đã hủy.

`resultText` và `interpretation` chỉ có khi `state = Reviewed`.

### 3.3. Dòng tài liệu

```
{ documentId, fileName, contentType, sizeBytes, documentType, source, sourceDate, visitId, dossierId, uploadedAt }
```

Không trả mô tả nội bộ (`description`), tên người tải lên hay lý do từ chối. Tệp PendingScan hoặc Rejected không xuất hiện.

## 4. Mobile: dữ liệu nhạy cảm và hiệu năng

- **Token:** mỗi request mang access token của phiên hiện tại, không dùng URL công khai hay đường dẫn kho.
- **Cách tải ảnh:** tải bằng `fetch` có header `Authorization`, rồi hiển thị dưới dạng data URI giữ trong bộ nhớ của màn.
  - Lý do: trên Expo Go Android, `Image` không gửi header `Authorization` một cách tin cậy (đã kiểm trên emulator).
  - Ảnh không vào bộ nhớ đệm trên đĩa của trình tải ảnh, và bị xóa cùng state khi đăng xuất hoặc bị 403.
- **401:** `signOut('expired')`.
- **403:** dừng tải, xóa danh sách, chi tiết và nguồn ảnh khỏi state, hiện banner liên kết và dẫn tới `account-link`.
- **Đăng xuất hoặc đổi phiên:** state các màn bị xóa vì màn chỉ giữ dữ liệu khi session `authenticated`.
- **Danh sách:** chỉ tải metadata. Ảnh trong lưới tải lười theo hàng đang hiển thị (FlatList), mỗi trang tối đa 20 tài liệu; không tải trước toàn bộ ảnh gốc.
- **Thumbnail:** backend chưa có bản thu nhỏ, nên thumbnail hiển thị ảnh gốc thu nhỏ. Tạo thumbnail phía server thuộc nguồn tài liệu DCMS-104 (ghi ở mục 6).

## 5. Nghiệm thu

1. **Lịch sử và chi tiết:** bệnh nhân có liên kết VERIFIED thấy đúng các lượt có phiếu đã xác nhận. Nội dung chi tiết khớp phiếu chính thức trên staff.
2. **Ảnh:** ảnh Ready của đúng lượt xem được toàn màn hình, phóng to, chuyển ảnh. Tệp lỗi được báo rõ, có Thử lại.
3. **CLS, đơn thuốc, dặn dò:** hiển thị khi có dữ liệu; nguồn rỗng thì không tạo dữ liệu giả.
4. **Server từ chối:**
   - không có liên kết → 403;
   - liên kết bị thu hồi → 403;
   - ID lượt/tài liệu của bệnh nhân khác → 404;
   - tệp chưa Ready → 404 ở danh sách, 409 ở nội dung.
5. **Mobile:** sau đăng xuất hoặc thu hồi không còn dữ liệu trên màn. Kiểm loading, rỗng, lỗi, mất mạng, làm mới và nhiều ảnh trên emulator.
6. **Kiểm tự động:** typecheck và bundle mobile đạt. Test API phân quyền chạy trên PostgreSQL thật đạt.

## 6. Phụ thuộc còn mở

| Việc | Người phụ trách |
| --- | --- |
| Thumbnail phía server và trình xem PDF trên app | DCMS-104 / mobile |
| Đồng bộ trạng thái và lịch sử sửa đổi đầy đủ của hồ sơ bệnh nhân | DCMS-95 |
| Ảnh của phiếu điều trị gắn dossier (không gắn lượt) chỉ xem được qua `documents` không lọc `visitId`; chưa có màn dossier riêng | DCMS-104 |
