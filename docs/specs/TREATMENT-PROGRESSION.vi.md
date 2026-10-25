# S58/S21/S24 — Mẫu, tiến trình điều trị và chỉ định quay lại

Nguồn nghiệp vụ hiện hành: DCMS_Screen_Specifications_v2.3 và DCMS_Use_Cases_Business_Rules_Messages_v2.3, 24/10/2026; bổ sung phân công 25/10/2026; UC-13/14/17/24/51/53, GBR-129–132/135/153/154/156/162, AT30/31/32/37/42/45/46/47/52. Vinh sở hữu S58, phần Steps của S21 và backend chỉ định; UI S24 thuộc DCMS-106. Quyết định không quản lý thuốc theo lô vẫn có hiệu lực.

## Bổ sung hợp đồng v2.3

- Step template và step của Patient có cặp `estimatedMinMinutes?`/`estimatedMaxMinutes?` tùy chọn, `estimatedMinutes` bác sĩ chọn và `reservedMinutes` giữ ghế. Cặp khoảng hoặc cùng rỗng, hoặc thỏa `0 < min <= estimated <= max <= 480`. `reservedMinutes` là bội 15 trong 15–480 và >= estimate. Khi reserved < max, bác sĩ phải xác nhận cảnh báo; lưu actor và thời điểm xác nhận trong bản sao Patient. Step cũ có khoảng rỗng tiếp tục hợp lệ.
- Mẫu là gợi ý, không áp số buổi hay khoảng chờ cố định cho Implant tại phòng khám hoặc niềng răng. Bác sĩ có thể thêm bước vào item đã Agreed, ghi Partial/Repeat/Deferred và tạo thêm chỉ định quay lại cho cùng bước sau đánh giá, kèm lý do/cửa sổ; các lệnh này không tăng quantity hoặc giá, không tự tạo actual/Appointment/charge. Thay đổi phạm vi đã thỏa thuận đi qua S62.
- `POST /api/dossiers/{id}/steps` thêm bước Patient-specific trong phạm vi item Agreed, nhận `{expectedVersion, itemId, name, region?, estimatedMinMinutes?, estimatedMaxMinutes?, estimatedMinutes, reservedMinutes, predecessorIds[], capability?, labRequirement?, earliestOffsetDays?, targetOffsetDays?, latestOffsetDays?, chargingCoverage, reason, acknowledgeShortReservation, requestKey}`. Response `StepChange` trả ID/version/readiness đã lưu. `POST .../plan/from-template` cũng nhận `acknowledgeShortReservation`; `GET .../steps` trả khoảng và dấu xác nhận. Bác sĩ tạo instruction qua `POST /api/follow-up-instructions` với source step hiện có; `AddVisit` trong progress chỉ ghi quyết định, không tự đặt lịch.
- Instruction đầu tiên có thể tham chiếu `{dossierId,itemId,stepId:null}` của item Agreed khi chưa có Visit hay bước đã lập; cửa sổ và duration do Dentist nhập. Mỗi step, hoặc mỗi nguồn item-only, có tối đa một instruction `NeedsBooking`/`Booked`. Đổi/hủy/NoShow giữ lịch sử và cập nhật instruction trong transaction đặt lịch. Due event dùng khóa instruction + cửa sổ, blocker event dùng khóa episode; consumer care task thuộc DCMS-99. Nguồn actual, Labo và Visit/activity thiếu adapter thật phải từ chối an toàn, không báo hoàn tất.
- Availability sử dụng danh sách ghế cấu hình cho chi nhánh và kiểm chiếm dụng liên tục suốt thời gian đặt cùng mọi staff được phân công. Phòng khám xác nhận có ba ghế; không hardcode con số này vào engine hay tự tạo ghế trong dữ liệu vận hành. Recall do bác sĩ chọn, không tự sinh sáu hoặc mười hai tháng. Kênh SMS/Zalo chỉ được báo đã gửi khi adapter thật xác nhận.

## Ranh giới nguồn

- Template có các phiên bản Draft/Active/Retired. Chỉ Dentist có quyền ở chi nhánh được activate/retire. Bản patient copy giữ `templateId/templateVersion` và snapshot; chỉnh/retire template không đổi kế hoạch đã copy.
- Dossier/plan/item hiện có là nguồn lâm sàng của Patient. Step có stable logical ID qua các phiên bản kế hoạch, gắn item Agreed, outcome `NotStarted/Partial/Completed/Skipped/Stopped/Cancelled` riêng với readiness `Ready/Blocked` và blocker cụ thể. Plan/item/step không đại diện cho actual, Appointment hoặc charge.
- Mỗi step có `estimatedMinutes`, `reservedMinutes` bội 15 trong 15–480 và không nhỏ hơn estimate; predecessor IDs tạo đồ thị không chu trình. Return offsets là ngày lịch theo Asia/Ho_Chi_Minh, `0 <= earliest <= target <= latest`; nếu không có offsets, Dentist chọn cửa sổ trực tiếp. Coverage chỉ là dữ liệu hoạch định gắn item thỏa thuận, không tự tạo charge hoặc miễn phí.
- `Completed` đòi actual/evidence đã post và xác nhận Dentist. Không có nguồn actual thật thì từ chối an toàn; Lab requirement chưa được xác nhận Accepted đúng Patient/step/spec version thì readiness Blocked. Hold/release lưu lý do 1–1000, thời điểm/điều kiện review, không tự đổi/hủy lịch hay thu tiền.
- Follow-up instruction độc lập với Appointment: purpose `TreatmentContinuation/PostoperativeReview/WarrantyReview/PeriodicRecall/NewProblem`, nguồn Visit/activity hoặc agreed dossier/item/step (prospective first instruction không cần Visit), reason 1–2000, guidance 0–4000, window, duration và dentist/capability. Status `NeedsBooking/Booked/Completed/Cancelled/Superseded`; overdue là cờ tính toán. No slot vẫn NeedsBooking. Appointment success mới chuyển Booked; cancel/NoShow quay NeedsBooking nếu instruction chưa hoàn tất; reschedule giữ link. Chỉ Dentist xác nhận Completed lâm sàng.
- Hoàn thành step tạo tối đa một instruction cho mỗi successor logical step và source completion event; nhiều successor độc lập có thể tạo nhiều instruction. Booking, care task và blocker events có khóa ổn định để retry không nhân đôi.
- Nguồn chỉ định phải chọn đúng một nhánh: `{dossierId,itemId,stepId?}` hoặc `{visitId,activityId}`; không trộn ID hai nhánh. Nguồn Visit/activity hiện chưa có adapter xác thực Patient/branch nên API từ chối 503 thay vì nhận ID chưa kiểm chứng. Chỉ định từ agreed dossier/item là đường hoạt động trước; nguồn actual/lab từ các module khác phải được nối trước nghiệm thu end-to-end tương ứng.

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

## Phụ lục — phân tích nghiệp vụ ngày 16/10/2026 từ main

Phần dưới được giữ để truy vết thiết kế ban đầu. Khi nội dung về API đã triển khai hoặc thời lượng giữ lịch khác với hợp đồng S58/S21/S24 ở trên và cặp đặc tả v2.3 ngày 24/10/2026, áp dụng hợp đồng và đặc tả mới hơn. Cụ thể, thời lượng đặt lịch staff theo đơn vị 15 phút; mô tả 1–480 phút không làm tròn bên dưới là đề xuất lịch sử.

# Điều trị nhiều buổi và đặt lịch buổi tiếp theo trong DCMS

Ngày quyết định: 16/10/2026. Người dùng đã chọn áp dụng hướng phân tích vào **đặc tả và tài liệu trước**. Tài liệu này làm rõ UC-03/04/06/07/10/13/14/17/19/20/24/25/34/39/40/47/48/50/51/53, các màn hình liên quan và GBR-129–136 trong [bộ đặc tả v2](odental-v2/README.vi.md). Đây là hợp đồng để triển khai, không phải báo cáo chức năng đã chạy.

DCMS quản lý các bước điều trị xuyên nhiều lượt khám bằng kế hoạch có phiên bản, điều kiện sẵn sàng và chỉ định buổi tiếp theo. Nha sĩ quyết định nội dung chuyên môn cho từng bệnh nhân. Hệ thống hỗ trợ ghi nhận, kiểm tra và điều phối; mẫu dùng chung không tự quyết định điều trị hay tự xác nhận chuỗi lịch hẹn.

## Phân luồng đặt khám mới và tiếp tục điều trị — 19/10/2026

- Khám lần đầu hoặc đợt/vấn đề mới: chỉ dịch vụ Khám, tự lấy dịch vụ theo chi nhánh; thời lượng theo cấu hình phòng khám và chọn slot đủ nguồn lực. Các dịch vụ điều trị không xuất hiện trong luồng này.
- Tiếp tục cùng đợt điều trị: chọn đúng phác đồ và chỉ định buổi tiếp theo do nha sĩ lập. Dịch vụ/bước, thời lượng giữ lịch, nha sĩ/nguồn lực và khoảng ngày lấy từ chỉ định, không từ lựa chọn tự do của bệnh nhân.
- Một bệnh nhân có thể vừa có phác đồ đang thực hiện vừa có vấn đề khám mới; phân luồng theo đợt và mục đích, không theo một cờ đang điều trị toàn tài khoản.
- Server kiểm tra quyền bệnh nhân, phiên bản phác đồ/chỉ định, hiệu lực, tiền nhiệm, readiness, khoảng ngày và tài nguyên trước khi đặt/đổi. Không để bệnh nhân chọn dịch vụ bất kỳ để thay thế bước đang điều trị; không tự xác nhận hàng loạt lịch của cả phác đồ.
- Cấu hình bước slot dùng chung; ReservedMinutes của bước là số nguyên 1–480, không nhỏ hơn EstimatedMinutes và không bị làm tròn theo bước slot. Lịch cũ giữ thời lượng đã lưu.
- Đặc tả này chưa tương đương chức năng đã chạy: phác đồ và chỉ định chưa có API/dữ liệu runtime trong checkout. Adapter FollowUp phải tiếp tục báo không khả dụng thay vì tạo hẹn không có nguồn phác đồ.

## 1 Phạm vi và thứ tự triển khai

| Thứ tự | Phạm vi | Kết quả cần có |
| --- | --- | --- |
| 1 | Nối luồng đang được đặc tả | Kế hoạch → hoạt động thực tế → phí/hóa đơn → thu/phân bổ → tái khám, giữ nguyên ID nguồn |
| 2 | Mẫu và bước điều trị | Phiên bản mẫu, bản sao theo bệnh nhân, phụ thuộc, thời lượng và bằng chứng điều kiện |
| 3 | Buổi tiếp theo và CSKH | Cần đặt lịch, chờ điều kiện, đã đặt nhưng bị chặn, quá hạn và lịch sử liên hệ |
| Sau khi có dữ liệu | Gợi ý tối ưu lịch | Đánh giá thời lượng dự kiến/thực tế, thời gian chờ và nhu cầu dự phòng trước khi chọn chính sách |
| Phần mở rộng riêng | Xếp so le nhiều ghế | Cần mô hình giữ nguồn lực theo từng đoạn và đặc tả độc lập; chưa áp dụng ở đây |

Giữ lịch liên tục cho bác sĩ, phụ tá được phân công và một ghế trong toàn bộ khoảng hẹn. Không thêm đệm 20–30 phút cố định, tự gom bệnh nhân theo làn sóng, tự ưu tiên ca dài đầu buổi hoặc gửi Zalo/SMS chỉ từ nội dung đề xuất. Chính sách này không dựa trên giả định mọi phòng khám Việt Nam đều có quy mô hoặc thói quen giống nhau.

## 2 Thuật ngữ và trách nhiệm

| Đối tượng | Ý nghĩa | Không suy ra từ đối tượng này |
| --- | --- | --- |
| Lịch hẹn | Khoảng dự kiến có nguồn lực được giữ | Đã đến, đã điều trị hoặc đã thu tiền |
| Lượt khám | Một lần tiếp nhận/khám thực tế | Toàn bộ đợt điều trị đã kết thúc |
| Hồ sơ đợt điều trị | Nhóm công việc xuyên nhiều lượt khám | Một buổi duy nhất hoặc một hóa đơn duy nhất |
| Mẫu điều trị | Các bước tham khảo có phiên bản | Chỉ định áp dụng bắt buộc cho mọi bệnh nhân |
| Kế hoạch bệnh nhân | Phạm vi đã đề xuất/đồng ý và các bước riêng | Dịch vụ đã được thực hiện |
| Bước điều trị | Công việc có điều kiện và kết quả chuyên môn | Một lịch hẹn bắt buộc; một bước có thể cần nhiều lượt thực hiện |
| Chỉ định buổi tiếp theo | Công việc cần đặt trong một khoảng ngày | Lịch đã xác nhận |
| Hoạt động thực hiện | Công việc đã làm thật, người làm và kết quả | Tiền đã nhận |

Nha sĩ duyệt mẫu, kế hoạch, đồng ý điều trị, điều kiện và kết quả bước. Nhân viên được phân quyền thực hiện tiếp nhận, đặt/đổi lịch, theo dõi giao nhận labo và CSKH. Nghiệm thu sản phẩm labo là quyết định của nha sĩ; giao nhận không thay nghiệm thu. Phân quyền phần mềm không xác nhận năng lực hay phạm vi hành nghề. Không suy quyền tiêm tê, lấy cao răng hoặc làm thủ thuật chỉnh nha từ role `ClinicAssistant`.

Sơ đồ răng hỗ trợ chọn răng, nhiều răng, vùng hàm hoặc toàn hàm. Ảnh có thể gắn một vùng hoặc toàn hàm theo người nhập; không tự khẳng định mọi phim thuộc một răng hoặc các vùng tải tệp tự đồng bộ nếu chưa có hợp đồng.

## 3 Mẫu và kế hoạch có phiên bản

Mẫu được quản lý tại S58, có trạng thái `Draft`, `Active`, `Retired`. Nha sĩ có quyền quản lý mẫu tạo phiên bản nháp, kiểm tra rồi kích hoạt. Quyền sửa danh mục thông thường không tự cấp quyền duyệt mẫu chuyên môn. Phiên bản Active đã được dùng được giữ nguyên; sửa mẫu tạo phiên bản mới. Ngừng sử dụng chỉ ảnh hưởng việc chọn mẫu sau đó.

S21 cho chọn một phiên bản Active, xem trước và sao chép vào **kế hoạch nháp** của bệnh nhân. Lưu `TemplateId`, `TemplateVersion` và bản chụp các bước. Nha sĩ chỉnh theo trường hợp thực tế trước khi ghi nhận đồng ý; áp dụng mẫu không tự chấp thuận kế hoạch.

Kế hoạch thủ công và điều trị trực tiếp một buổi vẫn hợp lệ. Phân loại một/nhiều buổi là quyết định theo trường hợp, không khóa cứng bằng tên dịch vụ.

Mỗi bước có các trường sau:

| Trường | Quy tắc |
| --- | --- |
| `StepId` | ID logic ổn định trong kế hoạch; thay đổi phiên bản không xóa dấu vết bước cũ |
| `Name`, `PlanItemIds`, vùng răng/hàm | Bắt buộc tên và hạng mục liên quan; cùng bệnh nhân/đợt điều trị |
| `EstimatedMinutes` | Số phút nguyên dương do nha sĩ dự kiến; không tự cắt ngắn công việc để vừa lịch |
| `ReservedMinutes` | Nhập số nguyên 1–480, không nhỏ hơn thời lượng dự kiến; độc lập bước chia slot. Ca vượt giới hạn cần nha sĩ thiết kế bước phù hợp |
| `PredecessorStepIds` | Không tự phụ thuộc; không trùng; cùng kế hoạch; toàn đồ thị không có chu trình |
| `RequiredCapability` | Yêu cầu người thực hiện đã được phòng khám xác định phù hợp chuyên môn |
| `Prerequisites` | Bằng chứng thủ công do nha sĩ ghi nhận và/hoặc sản phẩm labo cần nghiệm thu đúng phiên bản |
| `EarliestOffsetDays`, `TargetOffsetDays`, `LatestOffsetDays` | Cả bộ có hoặc không; nếu có: số ngày nguyên, `0 ≤ earliest ≤ target ≤ latest` |
| `ChargingCoverage` | Liên kết giá/khối lượng của hạng mục đã đồng ý; bước Included trong gói không thu thêm |

Ví dụ dự kiến 20 phút: giao diện gợi ý giữ 30 phút; người dùng phải chọn/xác nhận giá trị hợp lệ. Không tự làm tròn dữ liệu người dùng đang nhập. Giới hạn đặt công khai 30/60 phút, thời gian báo trước và cửa sổ tự đổi/hủy hiện hành được giữ nguyên. Khi ứng dụng không hỗ trợ thời lượng hoặc khoảng ngày cần thiết, hiển thị hướng dẫn nhân viên đặt lịch và giữ yêu cầu cần đặt.

Thay phạm vi, giá, phụ thuộc hoặc thời lượng của kế hoạch bệnh nhân phải tạo phiên bản mới, có lý do và kiểm tra các chỉ định/lịch đã liên kết. Không tự chuyển khoản phí hay kết quả đã ghi sang phiên bản mới. Lịch không còn phù hợp phải được xử lý rõ trước khi chốt bản sửa gây xung đột; không sửa ngầm lịch bệnh nhân.

## 4 Kết quả bước và điều kiện sẵn sàng

`Outcome` gồm `NotStarted`, `Partial`, `Completed`, `Skipped`, `Stopped`, `Cancelled`. `Readiness` là thông tin suy ra riêng, gồm `Ready` hoặc `Blocked` với danh sách lý do và phiên bản nguồn.

Một bước có thể chưa làm và bị chặn vì thiếu nghiệm thu labo. Một bước đã làm một phần vẫn còn việc chưa xong. Chỉ nha sĩ xác nhận kết quả chuyên môn mới hoàn tất bước; thời gian đã trôi, tiền đã thu hoặc tỷ lệ phần trăm không thay quyết định này.

Nếu các hoạt động thực tế đã được ghi trước đó, nha sĩ có thể xác nhận hoàn tất bước sau khi xem lại kết quả và khối lượng/việc còn lại. Lệnh này tham chiếu các hoạt động cũ, không tạo thêm số lượng thực hiện hoặc khoản phí. Hai cách hoàn tất — ngay khi ghi hoạt động cuối hoặc khi rà soát sau — dùng cùng một sự kiện hoàn tất bước duy nhất để không sinh trùng chỉ định kế tiếp.

Điều kiện thực hiện gồm kế hoạch/hạng mục đã đồng ý, khối lượng còn lại, các bước tiền nhiệm bắt buộc Completed và bằng chứng điều kiện còn hiệu lực. Skipped không được tính là Completed; muốn bỏ bước tiền nhiệm phải duyệt lại phụ thuộc trong phiên bản kế hoạch. Không tự tạo hoạt động hay phí khi bỏ/dừng/hủy bước.

Điều kiện được kiểm tra lại tại lúc xác nhận lịch và lúc ghi hoạt động. `Ready` trong một lần đọc không giữ chỗ và không đảm bảo bằng chứng không đổi. Quyền đọc của Patient không tự cấp quyền điều trị hoặc quyền ghi nhận kết quả.

## 5 Khám tư vấn và thực hiện ngay trong cùng lượt

1. Tiếp nhận khách có lịch hoặc tạo lịch walk-in hợp lệ rồi check-in theo UC-04/07.
2. Nha sĩ khám, trao đổi công việc/giá và ghi nhận đồng ý.
3. Nếu làm trong khoảng đã giữ, mở S22 trong cùng `VisitId` và ghi thực tế.
4. Nếu cần thêm thời gian, mở S12 để xem trước giờ kết thúc mới và các lịch bị ảnh hưởng.
5. Xác nhận kéo dài bằng giao dịch kiểm tra toàn bộ bác sĩ/phụ tá/ghế/bệnh nhân, bỏ chính lịch đang sửa khỏi tập kiểm tra trùng.
6. Nếu xung đột, giữ lịch và bản nhập; nhân viên/nha sĩ quyết định giải pháp hoặc chuyển phần chưa làm sang buổi sau. Không âm thầm dời người kế tiếp.

Trễ thực tế vẫn được ghi trung thực theo AT15; lỗi kéo dài lịch dự kiến không được làm mất ghi chép chuyên môn. Chuyển nhãn “Đang điều trị” không tạo năng lực phục vụ mới.

Ví dụ hai ghế: A chiếm ghế 9:00–10:00, cần bác sĩ 9:00–9:20 và 9:40–10:00; B cần bác sĩ 9:30–9:50. Dù tổng phút bác sĩ là 60, vẫn trùng 9:40–9:50. Đặc tả hiện tại chặn lịch chồng cho cùng bác sĩ trên toàn khoảng; ví dụ không cấp phép xếp so le.

Hàng chờ hiển thị giờ hẹn, giờ đến, thời gian chờ và ưu tiên có lý do. Người đến trước không mặc nhiên đứng trước mọi khách hẹn giờ hoặc trường hợp ưu tiên chuyên môn. Giữ người điều chỉnh, lý do và lịch sử thứ tự.

## 6 Buổi tiếp theo được tạo cuốn chiếu

Khi đã đồng ý kế hoạch nhưng chưa bắt đầu thực hiện trong ngày, nha sĩ tạo chỉ định cần đặt cho bước đầu với khoảng ngày lựa chọn rõ ràng qua UC-17. Không tạo một hoạt động hoặc tiền nhiệm hoàn tất giả để mở lịch đầu tiên. Chỉ định thủ công giữ nguồn kế hoạch/bước và người chỉ định; mốc hoàn tất tiền nhiệm được để trống nếu chưa có.

Khi nha sĩ xác nhận hoàn tất bước, xác định các bước kế tiếp chưa hoàn tất có **tất cả tiền nhiệm bắt buộc Completed**. Mỗi bước kế tiếp có tối đa một chỉ định đang hoạt động. Chờ labo không làm mất chỉ định; vẫn hiển thị `NeedsBooking` và lý do `Blocked`.

Mốc tính khoảng quay lại là ngày hoàn tất chuyên môn thực tế của bước trước theo múi giờ phòng khám. Nếu có nhiều tiền nhiệm, dùng ngày hoàn tất muộn nhất trong các tiền nhiệm bắt buộc. Ví dụ minh họa cấu hình: hoàn tất ngày 16/10/2026, khoảng +7/+10/+14 ngày → sớm nhất 23/10, mục tiêu 26/10, muộn nhất 30/10. Đây là ví dụ tính ngày, không phải chỉ định khoảng cách điều trị.

S24 cho xem nguồn bước/kế hoạch, khoảng ngày và nút tìm lịch. Chỉ sau khi người dùng chọn giờ, xác nhận và giao dịch đặt lịch thành công mới chuyển chỉ định sang `Booked`. Một chỉ định có tối đa một lịch đang hoạt động. Xem trước, không có giờ trống hoặc lỗi mạng không đổi thành Booked.

Chỉ nhân viên có quyền theo bệnh nhân/chi nhánh hoặc Patient có liên kết tự thân Verified mới được mở đặt từ chỉ định chuyên môn. Người đặt hộ vẫn có thể đặt lịch thông thường nhưng không được tra cứu kế hoạch/bước/điều kiện của người thân hoặc gắn một ID chỉ định đoán được. ID nguồn không thay kiểm tra quyền.

Hủy hoặc NoShow không hoàn tất chỉ định. Nếu không còn lịch hoạt động, chỉ định còn hiệu lực trở về `NeedsBooking`; giữ liên kết lịch cũ và lịch sử. Đổi lịch giữ cùng chỉ định. Chỉ nha sĩ được xác nhận hoàn thành, thay thế hoặc hủy chỉ định chuyên môn.

Ghi hoàn tất bước và sự kiện tạo/cập nhật chỉ định phải cùng giao dịch hoặc outbox giao dịch có khóa duy nhất. Nếu xử lý bất đồng bộ, hiển thị đang xử lý và có thử lại; không báo đã đặt lịch. Nhiều bước độc lập có thể sinh nhiều chỉ định khác nhau; gửi lại cùng sự kiện không sinh bản sao.

## 7 Labo và lịch đã đặt

Liên kết đơn labo đến đúng kế hoạch/bước, bệnh nhân, vùng răng/hàm và phiên bản yêu cầu. Luồng `Ordered → Sent → Received → Accepted` giữ riêng người nhận và người nghiệm thu. `Received` chưa đáp ứng điều kiện cần Accepted; sản phẩm bị `Rework` hoặc hủy không được dùng làm bằng chứng sẵn sàng hiện tại.

Khi điều kiện thay đổi, hệ thống tính lại các bước phụ thuộc. Nếu có lịch đã xác nhận, giữ lịch và gắn cảnh báo điều kiện, liệt kê lịch bị ảnh hưởng và tạo việc xử lý cho nhân viên. Nhân viên có thể liên hệ, đề nghị nha sĩ xem lại nội dung buổi khám hoặc đổi lịch qua UC-06. Không tự hủy hoặc tự xác nhận rằng bệnh nhân đã được báo.

Mỗi lần điều kiện chuyển từ đạt sang bị chặn là một `BlockerEpisodeId`. Một việc xử lý cho mỗi tổ hợp lịch/điều kiện/episode; thử lại sự kiện trả cùng việc. Nếu đã giải quyết rồi phát sinh một lần chặn mới, tạo episode mới với lịch sử cũ giữ nguyên.

Thông báo trong ứng dụng theo UC-34; gửi Zalo/SMS cần đặc tả nhà cung cấp, quyền gửi, mẫu nội dung, kết quả giao và cơ chế thử lại riêng. Lưu ghi chú hay tạo task không phải bằng chứng đã gửi tin.

## 8 Tiến độ và tài chính

Giữ riêng: kết quả chuyên môn, khối lượng đã thực hiện, phí đã ghi, hóa đơn đã chốt, lịch thanh toán nếu có thỏa thuận, tiền đã nhận, phân bổ/cọc và hoa hồng.

Ví dụ minh họa: giá thỏa thuận 12 triệu, nhận cọc 5 triệu trước thực hiện thì có tiền nhận nhưng chưa có tiến độ điều trị. Khi ghi một bước Included trong gói không sinh lại toàn bộ giá gói. Khi ghi công việc có phí, dùng hạng mục/giá/khối lượng đã đồng ý và kiểm tra phần còn lại. Không nhân giá gói với một phần trăm hoàn thành tùy ý.

Nếu phòng khám thu theo mốc, phải xác định trước mốc đến hạn và chứng từ nguồn theo quy tắc phí hiện hành. Hoàn tất bước không xác nhận đã thu tiền. Ghi cọc/thu theo UC-20/21 và phân bổ có bằng chứng; không thực hiện chuyển khoản thực tế từ nút hoàn tất điều trị.

Hoa hồng theo GBR-103: một cơ sở tính được chọn rõ theo giá trị thực hiện hoặc khoản thu đã phân bổ, gắn đúng người thực hiện và chụp dữ liệu nguồn khi lập lương. Các buổi đổi bác sĩ không đổi người làm ở lịch sử. Không tự chia hoa hồng cho phụ tá theo tiến độ; cách chia nhiều người cần thỏa thuận và đặc tả riêng.

## 9 Danh sách cần xử lý và trạng thái giao diện

S24/S60 có bộ lọc riêng cho cần đặt lịch, đã đặt nhưng bị chặn, vắng mặt và chỉ định quá hạn chuyên môn. Quá hạn là sau ngày muộn nhất của chỉ định còn hiệu lực, không phải mọi trường hợp sau ngày mục tiêu. Không gán tự động “bỏ điều trị” khi bệnh nhân vắng một buổi hoặc không trả lời.

Job mỗi ngày theo giờ phòng khám cập nhật việc CSKH; khóa chống trùng theo `InstructionId + WindowVersion`, tối đa một việc đang mở. Nếu đã có việc do NoShow cho cùng chỉ định, cập nhật/liên kết việc đó thay vì sinh thêm callback. Đổi khoảng ngày đánh giá lại việc cũ, giữ lịch sử và áp dụng phiên bản mới. Lưu từng lần liên hệ, kết quả và lịch đặt lại đã commit.

Tất cả màn hình có tải/rỗng/lỗi/quyền/xung đột rõ ràng. Lỗi không hiển thị thành danh sách rỗng hoặc số tiền 0. Lỗi xác thực/phiên bản không xóa bản nhập; luôn kết thúc loading và cho sửa/lưu lại. Patient đã xác minh xem các bước chính thức và chỉ định của mình; ghi chú phối hợp nhân viên, chi phí nhà cung cấp, lương và dữ liệu người khác không được đưa vào nội dung cá nhân.

## 10 Hợp đồng API và dữ liệu

Các đường dẫn là **hợp đồng dự kiến**, chưa khẳng định đã triển khai. Hợp đồng đầy đủ đồng bộ trong [nguồn UC v2](odental-v2/USE-CASES-RULES-MESSAGES.md).

| Thao tác | Hợp đồng chính |
| --- | --- |
| `POST /api/treatment-templates/{id}/versions` | Tạo bản nháp; activate/retire là lệnh có quyền nha sĩ, phiên bản và lý do phù hợp |
| `POST /api/treatment-dossiers/{id}/plans/from-template` | TemplateId/version, dữ liệu theo bệnh nhân, expectedDossierVersion, requestKey; trả kế hoạch nháp/bước/provenance |
| `POST /api/plans/{id}/versions` | Phiên bản dự kiến, thay đổi và danh sách chỉ định/lịch bị ảnh hưởng đã xử lý; không ghi đè lịch sử |
| `GET /api/plans/{id}/steps/{stepId}/readiness` | Trả outcome, Ready/Blocked, lý do và phiên bản bằng chứng; không giữ nguồn lực |
| `POST /api/plans/{id}/steps/{stepId}/complete` | Nha sĩ xác nhận từ các hoạt động đã ghi; kiểm tra phiên bản, khối lượng và kết quả; cùng sự kiện hoàn tất duy nhất, không thêm phí |
| `POST /api/activities` | Bổ sung plan/version/step, expectedStepVersion, kết quả Partial/Completed; trả hoạt động, phí hợp lệ và chỉ định kế tiếp hoặc trạng thái xử lý rõ |
| `POST /api/appointments` | Liên kết FollowUpId, kiểm tra bước/khoảng ngày/nguồn lực cùng giao dịch; lỗi giữ NeedsBooking |
| `POST /api/appointments/{id}/extend` | expectedVersion, giờ kết thúc mới, lý do, requestKey; kiểm tra toàn khoảng, trả xung đột không sửa lịch khác |
| `POST /api/follow-up-instructions/{id}/revise-window` | Nha sĩ đổi khoảng ngày có lý do/lịch sử, kiểm tra ảnh hưởng lịch hiện hữu |

Lệnh thay đổi dùng `requestKey` và `expectedVersion` của nguồn đang sửa; phiên bản bước/kế hoạch được kiểm tra trong giao dịch, không tin bản chụp từ client. Cùng key/cùng payload trả cùng ID; cùng key/khác payload trả 409. Validation 400; chưa đăng nhập 401; không có quyền 403; không tồn tại/không thuộc phạm vi 404; stale/xung đột 409; dịch vụ không sẵn sàng 503. Thành công trả ID, phiên bản, trạng thái, số tiền từ server và mã thông báo, có thể đọc lại.

Giữ Clean Architecture và feature folders. Domain chứa quy tắc bước/đồ thị/trạng thái, Application chứa hợp đồng, Infrastructure lưu dữ liệu và outbox/job, Api kiểm tra truy cập và ghép lớp. Tất cả writer giữ lịch tiếp tục dùng giao dịch/khóa nguồn lực chung của S09; không tạo cơ chế đặt riêng chỉ vì đi từ kế hoạch.

Thiết kế quan hệ mới ở [sơ đồ tiến trình điều trị](../architecture/TREATMENT-PROGRESSION.mmd). Đây là phần bổ sung thiết kế cho ERD lịch sử 37 bảng; chưa có migration từ lần cập nhật tài liệu này. Thực thể/phiên bản/bằng chứng và ID nguồn phải được chốt khi triển khai, không lưu tiến độ chỉ bằng một trường phần trăm.

## 11 Ma trận nghiệm thu và truy vết

| Ca | Kết quả cần quan sát | Quy tắc |
| --- | --- | --- |
| AT17 | Sao chép mẫu không trùng; nguồn đổi không đổi bản sao; chặn chu trình/thời lượng sai | GBR-129/130 |
| AT18 | Khám rồi làm cùng lượt; kéo dài kiểm tra trùng và giữ bản nhập khi lỗi | GBR-133 |
| AT19 | Làm một phần không mở bước sau; hoàn tất/retry tạo đúng số chỉ định | GBR-131/132 |
| AT20 | Khoảng ngày từ hoàn tất thực tế; hết chỗ vẫn cần đặt; xác nhận mới Booked | GBR-130/132 |
| AT21 | Nhận khác nghiệm thu; làm lại chặn bước, giữ lịch, một task/episode | GBR-131/135 |
| AT22 | Hủy/NoShow không kết thúc điều trị; đặt lại không trùng chỉ định/task | GBR-132/135 |
| AT23 | Cọc không tăng tiến độ; bước gói không thu lặp; hoa hồng đúng người/nguồn | GBR-134 |
| AT24 | Sửa đồng thời/mất phản hồi/lỗi dịch vụ không tạo một phần hay bản sao | GBR-081/129/132 |
| AT25 | Lưu ưu tiên hàng chờ; phụ tá không tự có quyền chuyên môn | GBR-136 |
| AT26 | Patient thấy đúng tiến độ chính thức; không lộ dữ liệu phối hợp và không báo gửi giả | GBR-075/135 |

Giữ các ca AT01–AT16 và chạy thêm AT17–AT26 khi triển khai. Các ca trên là tiêu chí nghiệm thu được bổ sung, **chưa phải kết quả test đã chạy**.

## 12 Nguồn và giới hạn bằng chứng

Các quyết định trong tài liệu là thiết kế DCMS được người dùng chọn ngày 16/10/2026; không gắn nhãn là tính năng oDental đã được quan sát. Khảo sát hiện có và giới hạn quan sát giữ trong `docs/research/`.

- [Luật Khám bệnh chữa bệnh 15/2023/QH15 trên Cổng Chính phủ](https://xaydungchinhsach.chinhphu.vn/toan-van-luat-15-2023-qh15-kham-benh-chua-benh-119231127164453959.htm): tham khảo nguyên tắc quyền quyết định điều trị và phạm vi hành nghề, không thay việc xác minh quyền thực hiện từng kỹ thuật tại cơ sở.
- [NHS về điều trị tủy](https://www.nhs.uk/tests-and-treatments/root-canal-treatment/): ví dụ điều trị có thể cần nhiều lần hẹn, không dùng làm protocol cố định cho DCMS.

Các nhận định về tỷ trọng phòng khám nhỏ, doanh thu, hiệu suất hai ghế, tỷ lệ khách đến qua Zalo và các thời lượng lâm sàng trong đề xuất gốc chưa được chứng minh bằng khảo sát phòng khám mục tiêu. Không dùng chúng làm định mức bắt buộc. Việc tối ưu lịch cần số liệu thời lượng thực tế, chờ, quá giờ, NoShow và số buổi chưa được đặt trước khi quyết định thuật toán.
