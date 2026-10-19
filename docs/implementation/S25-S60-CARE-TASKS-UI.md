# S25/S60 — Danh sách việc chăm sóc, lịch chăm sóc và ghi kết quả liên hệ (UI web)

Task UI của Ngô Minh Trí (Epic DCMS-92). Backend care task / contact attempt / escalation, chống trùng và quyền thuộc **DCMS-99 (Vinh)**; chỉ dẫn tái khám thuộc DCMS-97 (backend) và DCMS-106 (UI, Tuấn); đặt lịch dùng S11 và lệnh `POST /api/appointments` có sẵn. Truy vết: S25/S60; UC-24/25/53; AT37/38; MSG246.

> Trạng thái (19/10/2026): **chỉ có UI**. Backend DCMS-99 chưa có trên `main`; hợp đồng dưới đây là **[PROPOSED]** để Vinh xác nhận, UI chỉ đổi trong `web/src/features/care-tasks/care.api.ts`. Đặc tả v2.2 chưa có trong repo.

## 1. Màn hình

- **S25 `/care-tasks`** — danh sách việc cần xử lý: lọc ngày đến hạn, người phụ trách, bệnh nhân, loại, trạng thái, chi nhánh (trong phạm vi server cho phép), phân trang 20/50/100.
- **S60 `/care-tasks/calendar`** — lịch tháng theo ngày đến hạn, cùng bộ lọc.
- Cả hai mở **cùng một bảng chi tiết theo `taskId`** (dữ liệu đọc lại từ server mỗi lần mở), nên lịch sử/người phụ trách/trạng thái luôn giống nhau; không tạo task trùng giữa hai màn.

## 2. Hành vi

- **Cờ riêng biệt** (không gộp thành "chưa làm"): `NeedsBooking`, `ScheduledBlocked`, `MissedAppointment`, `ClinicallyOverdue`, `DeliveryFailure` khi server trả về.
- **Trạng thái task:** `Open`, `InProgress`, `Done`, `Cancelled`; nút đổi trạng thái chỉ hiện theo `allowedActions`. Done/Cancelled chỉ đóng việc chăm sóc, không hoàn thành điều trị hay chỉ dẫn lâm sàng.
- **Nguồn:** chỉ dẫn/hoạt động/lịch hẹn nguồn và lịch sử, chỉ đọc. Với chăm sóc sau điều trị: nha sĩ yêu cầu, lượt khám/hoạt động nguồn, hạn đã chọn, câu hỏi được duyệt.
- **Ghi lần liên hệ:** thời điểm, kênh, kết quả (`NotContacted`, `NoAnswer`, `Contacted`, `Rebooked`, `NoLongerInterested`), ghi chú 0–1000, triệu chứng bệnh nhân tự báo (ghi nguyên văn, không gợi ý chẩn đoán), lần liên hệ tiếp theo (tùy chọn — không phải lịch hẹn). Mọi lần trước được giữ và hiển thị. Retry dùng lại `requestKey` nên không nhân đôi.
- **Rebooked:** chỉ chọn được sau khi đã đặt lịch thành công qua S11 (mở từ task, điền sẵn bệnh nhân); UI chỉ gửi `appointmentId` đã được server xác nhận. Đặt lịch lỗi → task giữ nguyên, chưa Rebooked.
- **Escalation:** chọn nha sĩ, lý do 1–500; hiển thị `Pending` / `Acknowledged` / `Resolved` đúng như server, kèm thời điểm/người xác nhận khi có. Trợ lý không tự đánh dấu bác sĩ đã nhận/kết luận.
- **Không có** nút gửi SMS/email/Zalo, không sửa kết quả lâm sàng, không form bảo hành/đổi thuốc (dùng S63 khi cần).
- Lỗi tải ≠ danh sách rỗng; mất mạng/xung đột (MSG111) giữ nội dung form, hiện nút tải lại.

## 3. Hợp đồng API đề xuất (DCMS-99)

| Method | Đường dẫn | Mô tả |
| --- | --- | --- |
| GET | `/api/care-tasks/options` | `{branches:[{id,name}], owners:[{id,name}], clinicians:[{id,name,branchId}], types:[...]}` trong phạm vi |
| GET | `/api/care-tasks?from&to&ownerId?&patient?&type?&status?&branchId?&page&pageSize` | `{items:CareTaskItem[], total, page, pageSize}` |
| GET | `/api/care-tasks/{taskId}` | `CareTaskDetail` |
| POST | `/api/care-tasks/{taskId}/attempts` | `{expectedVersion, requestKey, attemptedAt, channel, result, notes?, reportedSymptoms?, nextContactAt?, appointmentId?}` → `{task: CareTaskDetail}` |
| POST | `/api/care-tasks/{taskId}/status` | `{expectedVersion, requestKey, status, reason?}` → `{task}` |
| POST | `/api/care-tasks/{taskId}/escalations` | `{expectedVersion, requestKey, clinicianId, reason}` → `{task}` |

`CareTaskItem = {taskId, version, type, status, flags[], title, patient:{id,patientCode,fullName}, branchId, branchName, owner:{id,name}|null, dueAt}`.

`CareTaskDetail = CareTaskItem + {content, source:{instruction?:{id,label,status}, activity?:{id,label}, appointment?:{id,code,startAt,status}}, aftercare?:{requesterDentist, sourceVisit, selectedDue, questions[]}, attempts:[{id, attemptedAt, channel, result, notes, reportedSymptoms, nextContactAt, appointmentCode, actorName, recordedAt}], escalations:[{id, clinicianName, reason, status, createdAt, acknowledgedAt, acknowledgedBy, resolvedAt, evidence}], allowedActions[]}`.

`allowedActions`: `start`, `done`, `cancel`, `recordAttempt`, `rebook`, `escalate`.

## 4. Chế độ xem trước giao diện

Giống S20: `VITE_UI_PREVIEW=true` bật dữ liệu minh họa trong `preview.ts` kèm banner rõ ràng; mặc định chỉ gọi API thật.
