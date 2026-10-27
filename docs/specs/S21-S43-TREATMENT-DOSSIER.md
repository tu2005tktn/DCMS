# S21/S43 — Hồ sơ và kế hoạch điều trị (UC-13/39)

Baseline hiện hành cho màn tích hợp: [Điều trị & Thanh toán v2.5](TREATMENT-PAYMENT-V25.md). Phần này mở rộng DCMS-96 bằng hàng nhập S21/S22 ngay trong S43, trạng thái đóng hồ sơ thật và contract S38/S39; các mô tả cũ về S43 chỉ tóm tắt, cờ hoàn tất tự do hoặc chưa có actual không áp dụng cho luồng v2.5.

Nguồn hồ sơ gốc: DCMS_Screen_Specifications_v2.2.docx và DCMS_Use_Cases_Business_Rules_Messages_v2.2.docx, 19/10/2026. Phần bước/mẫu/chỉ định áp dụng v2.3 ngày 24/10/2026 và bổ sung ngày 25/10/2026 theo [TREATMENT-PROGRESSION.vi.md](TREATMENT-PROGRESSION.vi.md). Điều chỉnh plan đã chốt dùng [S62](S62-TREATMENT-PLAN-REVISION.md), không mở editor thứ hai ở S21/S43. Phạm vi S21/S43 không bao gồm UI patient app, actual S22, file S20 hoặc ledger S38/S39.

Phần mở rộng S58/S21 và backend S24 theo [TREATMENT-PROGRESSION.vi.md](TREATMENT-PROGRESSION.vi.md): template version được copy vào Draft riêng của Patient, tab Steps có outcome/readiness tách biệt, completion dựa trên actual và chỉ định quay lại không tự tạo Appointment hoặc charge. UI S24 do DCMS-106 thực hiện.

## Nghiệp vụ và quyền

- Một Patient có nhiều dossier độc lập; dossier Open có thể đi qua nhiều lượt khám. S43 là tóm tắt theo thời gian, S21 là một editor kế hoạch; không sao chép form ở S43.
- Dentist được phân công cho chi nhánh có quyền tạo/sửa/đóng dossier và kế hoạch. Mỗi request kiểm lại staff account, vai trò hiệu lực, EmployeeProfile, SchedulingMember, chi nhánh, Patient và nguồn liên kết. Admin, Guest, ClinicAssistant không có quyền lâm sàng chỉ vì xem được S06. Patient tự thân VERIFIED chỉ được đọc projection chính thức của mình.
- Dossier state Open/Completed/Stopped và closureOutcome CompletedAsAgreed/CompletedRevisedScope/StoppedIncomplete tách khỏi Visit, item decision, item progress, step readiness, actual và money. Trước khi đóng phải xử lý từng agreed item chưa hoàn tất bằng disposition và lý do; thiếu trả MSG243. Đóng/mở lại giữ phiên bản, actor, time và lịch sử.
- Kế hoạch có mục tiêu/phương án 1–4000 ký tự và các item có stable ID. Mỗi item tham chiếu active service cùng branch, snapshot tên/đơn vị/giá thỏa thuận VND không âm, số lượng dương, vùng/răng, thứ tự và thời gian dự kiến. Không tự tạo Appointment, actual, charge hoặc payment. Catalog đổi không sửa snapshot cũ.
- Quyết định Proposed/Agreed/Refused/Deferred theo từng item, lưu người quyết định/thời điểm, tư vấn, phương án thay thế, lợi ích/rủi ro, vật liệu, số buổi, giá/điều khoản và căn cứ đại diện khi áp dụng. Consent S20 gắn đúng plan version/item/procedure. Chỉ item Agreed với evidence còn hiệu lực mới đủ điều kiện chuyển S22; đổi scope sau khi chốt đi S62 và thiếu consent mới trả MSG240.
- Payment terms là lịch dự kiến/mốc/số tiền và evidence đã thỏa thuận; không tự ghi charge/payment khi đến hạn. Tài chính tại S43 chỉ là summary read-only từ DCMS-101, và khi nguồn chưa có không hiển thị số 0 giả. Bảo hành bệnh nhân được Dentist xác nhận riêng cho completed item đủ điều kiện với policy/version/phạm vi/ngày, không suy từ Lab Accepted; thiếu căn cứ trả MSG244.
- Failed load không được hiển thị như empty. Save lỗi/conflict giữ draft. Command dùng requestKey UUID ổn định; cùng key/cùng payload trả kết quả cũ, cùng key/khác payload trả 409. Cập nhật dùng expectedVersion; thành công trả ID/version/state đã đọc lại sau commit.

## Hợp đồng API đề xuất

Các route dưới đây thuộc module mới; không khẳng định đã tồn tại. DTO JSON dùng camelCase như API hiện có.

| Method | Route | Kết quả |
| --- | --- | --- |
| GET | `/api/patients/{patientId}/dossiers?branchId&page&pageSize` | `items,total,page,pageSize`; pageSize 20/50/100 |
| POST | `/api/patients/{patientId}/dossiers` | 201 `{dossier,notification}`; request `{branchId,dentistId,diagnosis,note?,startedOn?,requestKey}` |
| GET | `/api/dossiers/{dossierId}` | dossier, plan hiện tại, timeline nguồn và financial summary có trạng thái available/unavailable |
| POST | `/api/dossiers/{dossierId}/plan/draft` | `{expectedVersion,goals,items[],paymentTerms[],requestKey}`; lưu bản Draft, không post actual/money |
| POST | `/api/dossiers/{dossierId}/plan/commit` | `{expectedVersion,requestKey}`; chốt phiên bản và quyết định ban đầu |
| GET | `/api/dossiers/{dossierId}/plan/history` | các version và item snapshot cũ |
| POST | `/api/dossiers/{dossierId}/close` | `{expectedVersion,outcome,reason,dispositions[],requestKey}` |
| POST | `/api/dossiers/{dossierId}/reopen` | `{expectedVersion,reason,requestKey}` |
| POST | `/api/dossiers/{dossierId}/items/{itemId}/warranty/confirm` | `{expectedVersion,policy,policyVersion,scope,endsOn?,requestKey}` |
| GET | `/api/patients/me/treatment-summary` | chỉ official committed sources cho VERIFIED self link, không có draft/relative data |

`items[]` gồm `id?`, `sourceDiagnosisId?`, `serviceId`, `region?`, `unit`, `quantity`, `plannedOrder`, `estimatedMinutes?`, `estimatedStart?`, `estimatedEnd?`, `agreedUnitPriceVnd`, `decision`, `counseling`, `decisionMaker?`, `representativeBasis?`, `consentDocumentId?`. Service name/active/branch được server xác thực và snapshot; client không quyết định trạng thái nguồn. Version của plan/item và IDs ổn định được trả trong kết quả đọc.

Ví dụ lệnh lưu nháp sau khi đã tạo dossier (ID và version lấy từ phản hồi `POST /api/patients/{patientId}/dossiers`):

```json
{
  "expectedVersion": 1,
  "goals": "Điều trị răng 25; cân nhắc răng 46 sau tư vấn",
  "items": [
    { "id": null, "sourceDiagnosisId": null, "serviceId": 3, "region": "25", "unit": "răng", "quantity": 1, "plannedOrder": 1, "estimatedMinutes": 30, "agreedUnitPriceVnd": 300000, "decision": "Agreed", "counseling": "Đã giải thích lựa chọn, lợi ích, rủi ro, vật liệu, thời gian và giá", "decisionMaker": "Bệnh nhân" },
    { "id": null, "sourceDiagnosisId": null, "serviceId": 4, "region": "46", "unit": "răng", "quantity": 1, "plannedOrder": 2, "estimatedMinutes": 60, "agreedUnitPriceVnd": 1200000, "decision": "Deferred", "counseling": "Hoãn chữa tủy; đã trao đổi lợi ích và rủi ro", "decisionMaker": "Bệnh nhân" }
  ],
  "paymentTerms": [],
  "requestKey": "550e8400-e29b-41d4-a716-446655440000"
}
```

`id=null` là item mới; phản hồi `{dossier,notification}` trả `dossier.id`, `planId`, `version`, `planVersion`, `planState`, `items[].id`, decision và progress. Sau khi lưu nháp, client dùng `version` mới trong `POST /api/dossiers/{id}/plan/commit` với `requestKey` mới. Chỉ sau lệnh commit thành công `planState` mới là `Committed` và `planVersion` tăng. `MSG111` nghĩa là phiên bản đã thay đổi; client giữ nội dung nháp và yêu cầu người dùng đối chiếu bản mới. `MSG243` nghĩa là còn item Agreed chưa có cách giải quyết khi đóng dossier.

## Phụ thuộc và trạng thái giao diện

Visit/diagnosis DCMS-93/94, active service catalog, S20 consent, DCMS-97 progression, DCMS-99 actual, DCMS-101 ledger và DCMS-95 own-record synchronization dùng interface/fixture contract khi chưa có module. Fixture chỉ dùng test; production không khẳng định nguồn chưa có là dữ liệu thật. S21 có item table mặc định và tab Steps/Payment Terms/History; quyết định ban đầu là drawer. S43 hiển thị patient/course header, clinical state, next work, timeline và tài chính đọc-only, điều hướng giữ đúng item. Hiển thị loading, empty, validation, 401, 403, 404, MSG111, MSG144 và lỗi nguồn chưa sẵn sàng riêng biệt. Nút lưu chặn gửi lặp, giữ form khi lỗi.

Trong iteration này, backend xác thực service active cùng branch và lưu snapshot từ catalog; catalog hiện không có unit/price nên bác sĩ chọn đơn vị từ danh sách trên Web và nhập giá đã thỏa thuận, server đóng băng chúng khi chốt. `sourceDiagnosisId` khác null bị trả MSG239 cho tới khi DCMS-93/94 cung cấp nguồn xác thực; `consentDocumentId` chưa được nhận làm bằng chứng khi S20 chưa có, chốt kế hoạch có tham chiếu consent chưa kiểm chứng bị chặn MSG240. Tab Steps của S21 đọc nguồn progression mới, cho áp dụng mẫu và ghi Partial/hold; Completed bị chặn trong production cho tới khi DCMS-99 cung cấp actual/evidence thật. S43 trả `financialSummaryAvailable=false` khi DCMS-101 chưa cung cấp số liệu; không tự suy công nợ từ payment terms. API bảo hành chỉ nhận item Agreed + Completed và vì thế chưa có item đủ điều kiện qua giao diện này trước khi DCMS-99/progression nối vào. S41 chỉ nhận projection kế hoạch đã chốt của Patient có liên kết Verified, chưa thay cho nguồn actual/review của DCMS-95.

### Nhập hạng mục S21 trên Web

- Drawer **Thêm/Sửa hạng mục** ưu tiên chọn nhanh: dịch vụ từ catalog active của chi nhánh; răng theo số FDI 11–48 hợp lệ hoặc vùng điều trị thường dùng; trường "vùng khác" chỉ mở ô nhập khi danh sách không phù hợp. Không suy diễn vùng/răng từ tên dịch vụ.
- Số lượng mặc định `1` và chọn nhanh các số nguyên 1–10; trường hợp khác mới mở ô số với tối đa 3 chữ số thập phân. Hiển thị `1` thay vì `1.000` để tránh nhầm với một nghìn. Đơn vị chọn từ danh sách `răng`, `lần`, `vùng`, `hàm`, `chiếc`; dữ liệu đơn vị cũ ngoài danh sách vẫn hiển thị và được giữ khi sửa. Catalog hiện thiếu đơn vị chuẩn, nên không tự gán đơn vị dựa vào tên dịch vụ.
- `plannedOrder` được gán tự động khi thêm, không cần nhập trong drawer. Bảng hạng mục có nút đưa lên/xuống; thao tác này đánh lại thứ tự từ 1 và không đại diện cho số buổi hay Appointment.
- Đơn giá VND vẫn phải nhập vì catalog chưa có giá. Hiển thị thành tiền **dự kiến** bằng số lượng × đơn giá, cả trong drawer lẫn bảng. Giá này là snapshot thỏa thuận, không tạo charge, payment hoặc công nợ.
- Thời gian/ngày dự kiến nằm trong phần mở rộng **Dự kiến thực hiện**; để trống được, không tự tạo Appointment. Hạng mục mới luôn là `Proposed`. Quyết định `Agreed`/`Refused`/`Deferred`, tư vấn và người quyết định được ghi qua drawer riêng **Ghi quyết định** sau khi đã thêm hạng mục; khi sửa thông tin hạng mục không được làm mất quyết định đã lưu trong bản nháp. Chỉ danh tính/ngữ cảnh lâm sàng không có dữ liệu chuẩn mới cần nhập tự do; không tự điền nội dung tư vấn hoặc bằng chứng đồng ý.
- Các lựa chọn dùng nhãn rõ và điều khiển được bằng bàn phím; lưu nháp/chốt vẫn dùng API, version và requestKey hiện có. Xung đột/lỗi lưu giữ toàn bộ dữ liệu đang sửa. Không thay đổi payload hay quyền backend trong cải tiến giao diện này.

## Nghiệm thu

## Bổ sung v2.3 — thời lượng, thương mại và bảo hành

Nguồn: đặc tả màn hình và quy tắc v2.3 ngày 24/10/2026, GBR-153/154/157/158/159, AT42/45/48/49/50. Các trường dưới đây mở rộng dossier/item hiện có, không tạo loại kế hoạch thứ hai. Web S21 là nơi chỉnh nháp; S43 chỉ tổng hợp và điều hướng. Sau khi chốt, thay đổi phạm vi/giá/điều khoản của item phải qua S62.

`TreatmentItemInput` bổ sung `estimatedMinMinutes?`, `estimatedMaxMinutes?`, `reservedMinutes?`, `estimatedSessions?`, `returnEarliestOn?`, `returnTargetOn?`, `returnLatestOn?`, `acknowledgeShortReservation`, `discount?`. `TreatmentItemDto` còn trả `referenceUnitPriceVnd?` và `referencePriceVersion?` do server chụp từ catalog; client không gửi hai trường giá tham chiếu này. `expectedServiceLifeMonths?` thuộc lệnh xác nhận bảo hành sau khi hoàn tất item. Khoảng thời gian hoặc rỗng cả cặp, hoặc `0 < min <= estimatedMinutes <= max <= 480`. `reservedMinutes` khi có phải thuộc 15–480, bội 15 và không nhỏ hơn `estimatedMinutes`. Nếu nhỏ hơn max, bác sĩ xác nhận cảnh báo, server ghi actor/time. Ngày quay lại theo thứ tự earliest ≤ target ≤ latest; là dự kiến, không tạo Appointment. `estimatedSessions` là ước lượng tùy chọn, không tự tạo số bước cố định. Thời lượng step, instruction và Appointment vẫn độc lập.

Ví dụ payload item gửi tới `POST /api/dossiers/{dossierId}/plan/draft` (các trường chung `expectedVersion`, `goals`, `paymentTerms`, `requestKey` vẫn bắt buộc theo hợp đồng hiện có): Implant tại phòng khám có `serviceId` từ catalog của chi nhánh, `region: "36"`, `estimatedMinMinutes: 20`, `estimatedMinutes: 25`, `estimatedMaxMinutes: 40`, `reservedMinutes: 30`, `acknowledgeShortReservation: true`, `estimatedSessions: 3`. Một item niềng răng khác có thời gian và số buổi do bác sĩ chọn theo bệnh nhân, ví dụ `region: "Toàn hàm"`, `estimatedSessions: 8`, `returnEarliestOn: "2026-11-01"`, `returnTargetOn: "2026-11-15"`, `returnLatestOn: "2026-11-30"`. Không suy thời lượng, số buổi hay lịch xác nhận từ tên dịch vụ; trạng thái public-bookable của catalog cũng độc lập với việc lập kế hoạch.

Giá catalog của `SchedulingService` thêm `referenceUnitPriceVnd?` và `priceVersion`. Theo quyết định người dùng ngày 25/10/2026, Dentist được cập nhật giá tham chiếu, duyệt giảm trừ và duyệt ngoại lệ không cọc như Admin/ClinicOwner, nhưng chỉ trong chi nhánh có `SchedulingMember` đang hoạt động và `CanDentist`; Admin giữ phạm vi quản trị hiện có, ClinicOwner giữ phạm vi chi nhánh đang hoạt động. Server kiểm quyền trong từng command, không dựa vào việc hiện nút trên Web. Cập nhật giá dùng `PUT /api/service-prices/{serviceId}` với `expectedVersion`, `requestKey`, `referenceUnitPriceVnd?`. Khi thêm item, server snapshot giá và version, không tin giá tham chiếu client gửi. `agreedUnitPriceVnd` là giá mỗi đơn vị trước giảm trừ. `discount` của item gồm `type` (`Fixed`/`Percent`), `value`, `reason`, `qualifyingItemId?`, `status` (`Proposed`/`Approved`/`Rejected`), actor/time phê duyệt và fingerprint của giá/số lượng/item nguồn. Dentist có thể đề xuất và tự duyệt giảm trừ trong chi nhánh được phân công qua `POST /api/commercial/dossiers/{id}/items/{itemId}/discount/decision` với `expectedVersion`, `requestKey`, `decision`; hành động được audit như khi Admin/ClinicOwner duyệt. Thay đổi cơ sở tính xóa hiệu lực phê duyệt. Chỉ một giảm trừ hiệu lực trên target item; source/target phải cùng dossier và đều `Agreed`, net không âm. `GET /api/dossiers/{id}/plan/preview` trả giá từng item và tổng các item `Agreed` trước thuế; chỉ giảm trừ Approved được trừ. Item Refused/Deferred không cộng vào tổng. Ví dụ 600000 + 1000000 - 300000 = 1300000 VND. Không tạo charge/payment/receipt khi xem hoặc chốt plan.

`PaymentTermInput/Dto` mở rộng `milestone`, `planVersion`, `purpose`, `amountVnd`, `dueOn`, `evidence`. Ngoại lệ không cọc lưu lý do, actor/time riêng; Dentist trong chi nhánh được phân công, Admin hoặc ClinicOwner quyết định qua `POST /api/commercial/dossiers/{id}/deposit-exception` với `expectedVersion` và `requestKey`. Đây là thỏa thuận, không phải xác nhận đã thu và không cấp quyền ghi payment. `ConfirmWarrantyRequest` bổ sung component, exclusions, activationOn, endsOn, explanation, expectedServiceLifeMonths?; chỉ item Agreed + Completed đủ điều kiện, răng/vùng và policy/version được lưu cùng dấu xác nhận bác sĩ. Tuổi thọ dự kiến không phải hạn bảo hành.

`GET /api/dossiers/{id}` trả `financialSummary` có `available`, `plannedVnd`, và khi có ledger DCMS-101: postedCharge, received, allocated, outstanding, sourceVersion. Production hiện chưa có ledger, vì vậy các số sau chỉ hiển thị `unavailable`, không được suy từ plan/payment terms. SourceDiagnosisId DCMS-94 vẫn fail closed khi adapter chưa có; Web giữ lối vào từ Patient. Commit nháp, chốt plan, phê duyệt và bảo hành dùng version + request receipt; lỗi stale MSG111 giữ form. Snapshot plan cũ, actual và tiền đã post không bị viết lại.

AT30/31/33/34/36/39 và yêu cầu task: dossier nhiều lượt/item; đồng ý trám 25 và hoãn chữa tủy 46; snapshot giá; dự kiến không tạo Appointment; MSG243 khi còn agreed item chưa xử lý; closure StoppedIncomplete không xóa actual/debt; bảo hành xác nhận riêng, không suy từ Labo; quyền Patient/branch, retry và conflict giữ draft. Kiểm thử API trên PostgreSQL schema cách ly, Web bằng mock DTO chung, sau đó API thật khi các module nguồn sẵn sàng. Không ghi hoàn tất các ca phụ thuộc nguồn thật nếu chưa có bằng chứng.
