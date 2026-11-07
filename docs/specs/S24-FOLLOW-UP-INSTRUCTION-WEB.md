# S24 — Ghi chỉ định quay lại & chuyển đặt lịch (Web)

Nguồn: task DCMS-92 (Tống Nguyên Tuấn, Epic DCMS-92) trích từ DCMS_Screen_Specifications_v2.2.docx và DCMS_Use_Cases_Business_Rules_Messages_v2.2.docx (19/10/2026), cùng [TREATMENT-PROGRESSION.vi.md](TREATMENT-PROGRESSION.vi.md) — đặc tả backend UC-17/GBR-129–135/AT37 của Vinh. Backend instruction/booking port thuộc DCMS-97 (Vinh) và **đã có trong nhánh này** (`/api/follow-up-instructions`, `FollowUpAppointmentPort`); task này chỉ dựng UI/UX và gọi API thật.

## 1. Luồng

1. Từ S21 (đợt điều trị `DossierPage`, kế hoạch/Steps) mở khối **Chỉ định quay lại (S24)** với nguồn cụ thể = `{dossierId, itemId (Agreed), stepId}` của hồ sơ đang mở.
2. Bác sĩ nhập `purpose` / `reason` / `guidance` / `window` / `duration` / `responsible dentist` → **Lưu chỉ định** → server trả `instruction` với `status = NeedsBooking` (MSG231) → hiển thị trạng thái từ response.
3. Bấm **Tìm khung giờ (S11)** mở `BookingDrawer` đã có, truyền `instructionId`, `version`, Patient, `duration`, `dentist`, `window`; **không** tự chọn sẵn slot, **không** biến việc lưu form thành tạo booking.
4. Đặt lịch thành công (`POST /api/appointments` với `followUpId`, kind `FollowUp`) → server commit Appointment rồi mới chuyển instruction sang `Booked` (qua `MarkBookedAsync`) → UI tải lại instruction từ server; **không bao giờ** hiển thị `Booked` từ state local.
5. Đã có lịch: hiển thị `activeAppointmentId` + mã/giờ lịch (đọc lại `GET /api/appointments/{id}`).

## 2. API dùng lại (không tạo API mới)

| Method | Route | Dùng trong S24 |
| --- | --- | --- |
| GET | `/api/follow-up-instructions?patientId=` | Danh sách chỉ định của bệnh nhân trong quyền Dentist/chi nhánh |
| POST | `/api/follow-up-instructions` | Tạo: `{patientId, branchId, source, purpose, reason, guidance, earliestOn, targetOn, latestOn, durationMinutes, dentistId?, capability?, requestKey}` → 201 `{instruction, notification}` |
| GET | `/api/follow-up-instructions/{id}` | Đọc lại sau command |
| POST | `/api/follow-up-instructions/{id}/revise-window` | `{expectedVersion, earliestOn, targetOn, latestOn, reason, requestKey}` |
| POST | `/api/follow-up-instructions/{id}/cancel` \| `/supersede` \| `/complete` | `{expectedVersion, reason, requestKey}` — chỉ khi có trong `allowedActions` |
| GET | `/api/dossiers/{id}/steps` | Nguồn `stepId` cho form |
| GET | `/api/appointments/options` | Chi nhánh/nha sĩ/dịch vụ (dùng chung S11) |
| GET | `/api/availability` | Đọc slot trong handoff S11 (chỉ đọc, không đổi trạng thái) |
| POST | `/api/appointments` | Tạo lịch với `followUpId` (lệnh dùng chung DCMS-84) |
| GET | `/api/appointments/{id}` | Hiển thị lịch đã gắn (`activeAppointmentId`) |

DTO `FollowUpDto`: `id, patientId, branchId, source{dossierId,itemId,stepId,visitId,activityId}, purpose, reason, guidance, earliestOn, targetOn, latestOn, durationMinutes, dentistId, capability, status, overdue, activeAppointmentId, version, allowedActions`.

## 3. Form và validation (client mirror, server vẫn quyết định)

- `purpose`: bắt buộc, một trong `TreatmentContinuation | PostoperativeReview | WarrantyReview | PeriodicRecall | NewProblem` (nhãn tiếng Việt rõ nghĩa).
- `reason`: bắt buộc 1–2000 ký tự (server nhận `<= 2000`; rỗng → MSG04).
- `guidance` (instructions): 0–4000 ký tự.
- `window`: ba ngày `earliestOn <= targetOn <= latestOn`, bắt buộc; mirror quy tắc `start <= end`, lỗi hiển thị ngay trên trường.
- `duration` (reservedMinutes): selector tường minh 15–480 phút, bội số 15 (hợp đồng staff); **không** suy ra từ estimated duration. Khi chọn step có thể hiển thị `reservedMinutes` của step làm gợi ý thông tin, giá trị gửi vẫn do bác sĩ chọn. Contract public (30/60) chỉ áp cho client công khai, không áp ở đây.
- `dentist`/`capability`: chọn từ `options.dentists` của chi nhánh; mặc định = bác sĩ phụ trách của dossier nếu API có trong danh sách; để trống được (server cho phép `dentistId = null`). Không tự đổi nha sĩ khi hết slot.
- Nguồn `source`: đúng một nhánh `{dossierId, itemId, stepId}` (Visit/Activity chưa có adapter → server trả 503 MSG144, UI không mở nhánh này khi module chưa có). Item phải `Agreed`, dossier `Open` + plan `Committed`; nếu chưa chốt plan thì nút tạo bị khóa kèm lý do.
- Gửi `requestKey` UUID: giữ key khi lỗi mạng (status 0) để retry không tạo trùng; key mới sau thành công hoặc lỗi 4xx.

## 4. Trạng thái, actions, lịch sử

- Hiển thị `status` (`NeedsBooking | Booked | Completed | Cancelled | Superseded`) + cờ `overdue` từ server; mỗi action chỉ hiện khi nằm trong `allowedActions`.
- `reviseWindow`: form cửa sổ + lý do (1–1000, bắt buộc) — dùng `expectedVersion` từ bản đọc gần nhất.
- `cancel`/`supersede`/`complete`: dialog bắt buộc nhập lý do (1–1000); gửi `{expectedVersion, reason, requestKey}`; lỗi MSG111 → tải lại, giữ nguyên dữ liệu nhập; MSG230 (blocked/đã kết thúc), MSG233 (ảnh hưởng lịch) hiển thị đúng nguyên nhân.
- Mở rộng (Collapse) từng chỉ định: reason, guidance, nguồn, version, lịch hẹn đang gắn — **không** thêm form phụ. Endpoint đọc lịch sử cửa sổ trước của instruction chưa có → phần này hiển thị “chưa có dữ liệu lịch sử từ máy chủ” và được ghi nhận là khoảng trống DCMS-97; không tự bịa lịch sử.
- Lỗi tạo/lưu (MSG04/MSG229/MSG230/MSG233/MSG144/MSG111/MSG06): giữ nguyên toàn bộ dữ liệu form, hiển thị thông điệp server.

## 5. Handoff S11 (Find slots)

- `BookingDrawer` nhận bối cảnh chế độ tái khám: `followUpId`, `instructionVersion`, `patientId`, `branchId`, `dentistId`, `durationMinutes`, cửa sổ ngày.
- Trong chế độ này: `kind` khóa thành `FollowUp` (bắt buộc theo contract `POST /api/appointments`), `patientId`/nha sĩ/thời lượng/khoảng ngày bị khóa hoặc giới hạn trong cửa sổ đã chỉ định; ngày chọn phải nằm trong `[earliestOn, latestOn]` (server kiểm lại bằng `InWindow`).
- Không có slot: hiển thị rõ “không có khung giờ trống trong cửa sổ đã chỉ định”, giữ nguyên dữ liệu, **không** tự đổi nha sĩ/cửa sổ/thời lượng.
- Thành công: dùng `AppointmentResult` từ server (mã lịch, MSG29), tải lại instruction để lấy trạng thái `Booked` thật; lỗi slot bị tranh (MSG25/27/28/34) tải lại slot, giữ key theo quy tắc hiện hành.

## 6. Ranh giới

- Không code backend instruction/booking port/`MarkNeedsBookingAsync` (DCMS-97 – Vinh); không nhúng coverage assessment hay task-contact form vào S24.
- Link màn hình: `PostoperativeReview`/`WarrantyReview` → S63, aftercare → S60 — hai màn này chưa tồn tại trong repo nên UI hiển thị nhãn chờ triển khai, không tạo route giả.
- S63/DCMS-99 (review source) và care queue DCMS-105 ngoài phạm vi.
