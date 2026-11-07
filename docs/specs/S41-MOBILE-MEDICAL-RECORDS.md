# S41 — My Medical Records (Mobile App)

Nguồn: task DCMS-92 (Tống Nguyên Tuấn, Epic DCMS-92) trích từ DCMS_Screen_Specifications_v2.2.docx và DCMS_Use_Cases_Business_Rules_Messages_v2.2.docx (19/10/2026), UC-55/UC-37. Backend own-record đầy đủ thuộc DCMS-95 (Tư) — **chưa có trong repo**; task này dựng toàn bộ UI và chỉ gọi API thật, hiển thị trạng thái “nguồn chưa sẵn sàng” cho phần backend chưa giao, không mock dữ liệu đã lưu.

## 1. Luồng

1. Từ Mobile Home (`AccountHomeScreen`, tài khoản Public) → **Hồ sơ y tế của tôi** → route `(account)/medical-records`.
2. Kiểm tra session (S01) rồi kiểm tra self-link (S07/S08 `GET /api/patient-links/me`):
   - `VERIFIED` → tải summary và nhóm hồ sơ.
   - `NOT_LINKED` → Banner hướng dẫn + nút tới `/(account)/account-link` (không hiển thị dữ liệu nhạy cảm).
   - `PENDING` → Banner “đang chờ xác minh” + link đúng màn liên kết hiện có.
   - `REJECTED` / `REVOKED` / `WITHDRAWN` → Banner nêu nguyên nhân + hướng gửi lại yêu cầu tại `account-link`.
   - Trạng thái `locked` của link không tồn tại trong contract hiện hành; tài khoản bị khóa bị chặn từ S01 (session ended) — không có nhánh dữ liệu riêng.
3. Xem Summary → chọn record → Detail (`(account)/medical-records/[dossierId]`) → quay lại màn trước giữ nguyên vị trí cuộn (stack giữ màn list được mount).
4. Nguồn chính thức tự xuất hiện theo UC-55 ngay khi điều trị còn tiếp tục; **không** có quy trình “bấm publish” hay bác sĩ duyệt công bố trong app.

## 2. API dùng lại (session quyết định Patient, không nhận PatientId từ client)

| Method | Route | Nội dung S41 |
| --- | --- | --- |
| GET | `/api/patient-links/me` | Trạng thái liên kết (gate dữ liệu) |
| GET | `/api/patient-links/me/record` | Identity: mã BN, họ tên, ngày sinh, ngày liên kết (điểm kiểm quyền, 403 MSG06 khi không Verified) |
| GET | `/api/patients/me/treatment-summary` | Nguồn chính thức: dossier đã chốt kế hoạch + item Agreed (projection của Vinh, chỉ Patient VERIFIED) |
| GET | `/api/me/appointments?scope=HISTORY` và `?scope=UPCOMING` | Các lượt khám theo ngày / nha sĩ / chi nhánh, trạng thái (UC-05 read API) |
| GET | `/api/patients/me` | Thông tin hành chính bệnh nhân tự sửa (tab hồ sơ sẵn có trên app) |

Phần backend chưa giao (hiển thị trạng thái trung thực, ghi task + người phụ trách, **không** dữ liệu giả):

| Phần việc | Task | Trạng thái UI |
| --- | --- | --- |
| Own-record list/detail/history/sync status/file access/allowedActions | DCMS-95 (Tư) | Card “Chưa kết nối nguồn hồ sơ” nêu rõ từng phần; nút Thử lại gọi lại API thật |
| Findings/diagnosis/chart (Visit, số hiệu bệnh án) | DCMS-93/96/99 | Card nguồn chưa sẵn sàng trong Detail |
| Đơn thuốc / advice / follow-up finalized | DCMS-103 | Card nguồn chưa sẵn sàng |
| Ảnh & tài liệu (chỉ khi `Ready`, authorized endpoint) | S20 / DCMS-95 | Không hiển thị ảnh; card nêu điều kiện `Ready` + endpoint được cấp quyền |
| Warranty patient-facing | API bảo hành (S21-S43 spec) | Chỉ hiện khi DTO có; hiện tại card nguồn chưa sẵn sàng |
| Bill / S42 | S42 (task khác) | Chỉ link khi màn đã có; hiện tại không render link chết |

## 3. Nội dung Summary

- **Identity**: mã BN, họ tên, ngày sinh, ngày liên kết (từ `/record`).
- **Lịch sử & sức khỏe**: health history/allergy chưa có nguồn API → card “chưa có dữ liệu từ máy chủ (chờ DCMS-95)”, phân biệt với “không có dữ liệu” (empty).
- **Các lượt khám**: từ `/api/me/appointments` — ngày/giờ, nha sĩ, chi nhánh, trạng thái; `Completed` của lượt khám **không** đồng nghĩa dossier hoàn tất (dossier hiển thị riêng với trạng thái `Open/Completed/Stopped`).
- **Nhóm hồ sơ chính thức**: danh sách `OwnTreatmentSummaryItem` (code, state, startedOn, diagnosis, planVersion, số item Agreed) → chọn để vào Detail.

## 4. Nội dung Detail (read-only)

- Header: code, trạng thái dossier, ngày bắt đầu, chẩn đoán, plan version.
- **Plan** (dự kiến/đã đồng ý): item Agreed kèm `decision`, tư vấn, giá thỏa thuận, tiến độ `progress` do server trả — tách bạch “kế hoạch” với “đã thực hiện”.
- **Actual / findings / prescription / files / warranty / bản sửa (corrected/superseded)**: chưa có DTO → card nêu rõ nguồn và task phụ trách; không tự xuất dữ liệu staff.
- Phân biệt trạng thái: `NeedsBooking` ≠ `Appointment Confirmed` (chỉ định chưa đặt lịch khác lịch đã xác nhận); hiển thị theo DTO tương ứng khi nguồn có.
- **Contact clinic**: nút yêu cầu sửa đổi — app read-only, không sửa trực tiếp nguồn clinical/financial; modal hướng dẫn liên hệ phòng khám, **không** gửi/ghi dữ liệu khi chưa có API nhận yêu cầu.

## 5. Đồng bộ, lỗi và an ninh

- Hiển thị “Lần tải thành công gần nhất” = thời điểm response thành công gần nhất (client state thật); khi request lỗi mạng → banner lỗi + **Thử lại**, phân biệt rõ với empty; `sync pending/error/last successful timestamp` đầy đủ chờ DTO từ DCMS-95.
- 401 → dừng request, gọi `session.signOut('expired')`, xóa dữ liệu hiển thị.
- 403 (link revoked/không còn quyền) → dừng request ngay, xóa summary/detail trong bộ nhớ, hiện banner trạng thái liên kết + điều hướng `account-link`; không dùng lại cache cũ.
- Mỗi request đều mang access token từ session hiện tại (không lưu link file công khai, không lưu token bền vững ngoài storage của S01).
- Không dựng UI: care/escalation notes của staff, phí nhà cung cấp, dữ liệu người thân chỉ vì đã đặt/trả hộ, export raw dữ liệu staff (chỉ hiện export khi backend có contract/allowedAction).

## 6. Ranh giới

- Không triển khai backend ACL/clinical edit/payment gateway/chatbot/toàn bộ S42 (DCMS-95 – Tư sở hữu own-record backend, sync/backfill).
- Không chọn PatientId tùy ý: mọi query đi qua session (policy `PublicAccountPolicy` phía server).
