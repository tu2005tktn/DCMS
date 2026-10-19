# S58/S21/S24 — Mẫu, tiến trình điều trị và chỉ định quay lại

Nguồn nghiệp vụ: DCMS_Screen_Specifications_v2.2 và DCMS_Use_Cases_Business_Rules_Messages_v2.2, 19/10/2026; UC-13/14/17/24/51/53, GBR-129–132/135, AT30/31/32/37. Vinh sở hữu S58, phần Steps của S21 và backend chỉ định; UI S24 thuộc DCMS-106.

## Ranh giới nguồn

- Template có các phiên bản Draft/Active/Retired. Chỉ Dentist có quyền ở chi nhánh được activate/retire. Bản patient copy giữ `templateId/templateVersion` và snapshot; chỉnh/retire template không đổi kế hoạch đã copy.
- Dossier/plan/item hiện có là nguồn lâm sàng của Patient. Step có stable logical ID qua các phiên bản kế hoạch, gắn item Agreed, outcome `NotStarted/Partial/Completed/Skipped/Stopped/Cancelled` riêng với readiness `Ready/Blocked` và blocker cụ thể. Plan/item/step không đại diện cho actual, Appointment hoặc charge.
- Mỗi step có `estimatedMinutes`, `reservedMinutes` bội 15 trong 15–480 và không nhỏ hơn estimate; predecessor IDs tạo đồ thị không chu trình. Return offsets là ngày lịch theo Asia/Ho_Chi_Minh, `0 <= earliest <= target <= latest`; nếu không có offsets, Dentist chọn cửa sổ trực tiếp. Coverage chỉ là dữ liệu hoạch định gắn item thỏa thuận, không tự tạo charge hoặc miễn phí.
- `Completed` đòi actual/evidence đã post và xác nhận Dentist. Không có nguồn actual thật thì từ chối an toàn; Lab requirement chưa được xác nhận Accepted đúng Patient/step/spec version thì readiness Blocked. Hold/release lưu lý do 1–1000, thời điểm/điều kiện review, không tự đổi/hủy lịch hay thu tiền.
- Follow-up instruction độc lập với Appointment: purpose `TreatmentContinuation/PostoperativeReview/WarrantyReview/PeriodicRecall/NewProblem`, nguồn Visit/activity hoặc agreed dossier/item/step (prospective first instruction không cần Visit), reason 1–2000, guidance 0–4000, window, duration và dentist/capability. Status `NeedsBooking/Booked/Completed/Cancelled/Superseded`; overdue là cờ tính toán. No slot vẫn NeedsBooking. Appointment success mới chuyển Booked; cancel/NoShow quay NeedsBooking nếu instruction chưa hoàn tất; reschedule giữ link. Chỉ Dentist xác nhận Completed lâm sàng.
- Hoàn thành step tạo tối đa một instruction cho mỗi successor logical step và source completion event; nhiều successor độc lập có thể tạo nhiều instruction. Booking, care task và blocker events có khóa ổn định để retry không nhân đôi.
- Nguồn chỉ định phải chọn đúng một nhánh: `{dossierId,itemId,stepId}` hoặc `{visitId,activityId}`; không trộn ID hai nhánh. Nguồn Visit/activity hiện chưa có adapter xác thực Patient/branch nên API từ chối 503 thay vì nhận ID chưa kiểm chứng. Chỉ định từ agreed dossier/item/step là đường hoạt động trước; nguồn actual/lab từ các module khác phải được nối trước nghiệm thu end-to-end tương ứng.

## API và DTO chung

API dùng JSON camelCase, `requestKey` UUID cho command, `expectedVersion` cho cập nhật; cùng key + cùng payload trả cùng ID/kết quả, khác payload trả 409. Server kiểm Staff/Dentist, branch, Patient, source, version, quyền xem; client không gửi actor hoặc tự quyết status. Response command đọc lại ID/version/status sau commit.

| Route | Chức năng |
| --- | --- |
| `GET/POST /api/treatment-templates` | Lọc branch, đọc/tạo Draft |
| `GET /api/treatment-templates/{id}`, `POST /api/treatment-templates/{id}/versions` | Đọc lịch sử, tạo Draft version mới |
| `POST /api/treatment-templates/{id}/versions/{version}/draft` | Lưu bước của Draft với expectedVersion/requestKey |
| `POST /api/treatment-templates/{id}/versions/{version}/activate`, `/retire` | Chuyển trạng thái version với expectedVersion/requestKey |
| `POST /api/dossiers/{id}/plan/from-template` | Copy Active version vào Draft của Patient; giữ provenance và snapshot |
| `GET /api/dossiers/{id}/steps`, `GET /api/dossiers/{id}/steps/{stepId}/readiness` | Step và blocker theo plan version |
| `POST /api/dossiers/{id}/steps/{stepId}/progress`, `/hold`, `/release-hold`, `/complete` | Cập nhật outcome, hold hoặc dentist-confirmed completion |
| `GET/POST /api/follow-up-instructions`, `GET /api/follow-up-instructions/{id}` | Danh sách/đọc/tạo chỉ định trong phạm vi quyền |
| `POST /api/follow-up-instructions/{id}/revise-window`, `/complete`, `/cancel`, `/supersede` | Command theo version, ghi history |

`templateStep` và `planStep` có `logicalStepId`, `itemId` (plan), `name`, `region`, `estimatedMinutes`, `reservedMinutes`, `predecessorIds`, `capability`, `labRequirement`, `returnOffsets`, `chargingCoverage`, `outcome`, `readiness`, `blockers`, `version`. `instruction` có `id`, `patientId`, `source`, `purpose`, `reason`, `guidance`, `earliestOn/targetOn/latestOn`, `durationMinutes`, `dentistId?/capability?`, `status`, `overdue`, `activeAppointmentId?`, `version`, `allowedActions`. Ví dụ prospective: source `{dossierId,itemId,stepId}` của item Agreed, không có `visitId`, window do Dentist chọn; kết quả `NeedsBooking` và MSG231, không tạo Visit/Appointment.

`GET /api/treatment-templates?branchId=...` trả `[]` khi chi nhánh chưa có mẫu, và danh sách theo tên khi đã có mẫu; mỗi dòng gồm `activeVersion` nếu có. Lỗi truy vấn ở server không được biến thành trạng thái danh sách rỗng của UI.

Ví dụ request cho UI S24 của Tuấn (IDs lấy từ GET dossier/steps, Dentist đang đăng nhập):

```json
{
  "patientId": 123,
  "branchId": 1,
  "source": { "dossierId": "11111111-1111-4111-8111-111111111111", "itemId": "22222222-2222-4222-8222-222222222222", "stepId": "33333333-3333-4333-8333-333333333333", "visitId": null, "activityId": null },
  "purpose": "TreatmentContinuation",
  "reason": "Quay lại thực hiện bước điều trị đầu tiên",
  "guidance": "Bác sĩ xem lại trước khi làm",
  "earliestOn": "2026-11-01", "targetOn": "2026-11-03", "latestOn": "2026-11-05",
  "durationMinutes": 30, "dentistId": 2, "capability": null,
  "requestKey": "550e8400-e29b-41d4-a716-446655440000"
}
```

`POST /api/follow-up-instructions` trả HTTP 201 với `{ "instruction": { "id": "...", "status": "NeedsBooking", "version": 1, "activeAppointmentId": null, "allowedActions": ["reviseWindow", "cancel", "supersede", "findSlots"], "overdue": false, ... }, "notification": { "code": "MSG231", "message": "Đã lưu chỉ định quay lại. Chưa có lịch hẹn được đặt." } }`. UI dùng `instruction.id` làm `followUpId` trong `POST /api/appointments` khi bệnh nhân tự thân đã xác minh hoặc staff đặt; `Find slots` chỉ đọc availability, không chuyển trạng thái. `expectedVersion` lấy từ response gần nhất khi sửa chỉ định. UI S24 không được suy ra Patient từ số điện thoại hay quyền xem hồ sơ người thân.

Booking port phải dùng cùng scoped DbContext/transaction: `ValidateAsync` kiểm Patient, window, duration, dentist, readiness và một active appointment; `MarkBookedAsync` ghi link; `MarkNeedsBookingAsync` kiểm đúng appointment ID và giữ lịch sử. Reschedule kiểm lại instruction trước đổi slot. Outbox ghi trong cùng transaction; dispatcher và UI S24 thuộc task khác. `GET /api/availability` vẫn là nguồn slot, không mở calendar engine mới.

## Trạng thái, lỗi và nghiệm thu

- MSG229: graph/duration/window/coverage sai; MSG230: prerequisite/lab/hold chặn; MSG231: đã lưu instruction, chưa đặt lịch; MSG233: thay đổi ảnh hưởng booking; MSG235: không có slot; MSG236: template không còn Active/version thay đổi; MSG241: đã lưu hold; MSG111: stale; MSG144: lỗi nguồn. Lỗi giữ draft; loading/empty/validation/401/403/409/network phân biệt.
- AT30/31/32/37: snapshot template bất biến; bước Partial/Blocked không mở successor; retry completion giữ instruction IDs; prospective instruction không tạo Visit giả; booking → reschedule → cancel/NoShow giữ provenance; hai booking cạnh tranh chỉ một active; scan lặp không nhân care task; hold/release không tạo actual/charge/payment.
- Test PostgreSQL dùng schema cách ly và fixture cho actual/lab/care đến khi adapter thật sẵn sàng. Không chạy migration hoặc seed lên database dự án đang dùng chỉ để thử nghiệm. Không tuyên bố nghiệm thu end-to-end actual/lab/care khi chưa nối nguồn thật.
