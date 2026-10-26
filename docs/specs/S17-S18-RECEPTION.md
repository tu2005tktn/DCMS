# DCMS-111 S17 S18 Tiếp nhận check-in và hàng chờ khám

Ngày chốt contract: **25/10/2026**. Trạng thái ngày **26/10/2026**: đã triển khai phần tiếp nhận DCMS-111 và đọc Visit bàn giao; xem [bằng chứng và giới hạn](../implementation/DCMS-111-RECEPTION.md). Nguồn: yêu cầu người dùng trong phiên này; [DCMS-111](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-111), [luồng DCMS-110](CLINIC-FLOW-DCMS-110.vi.md), [UC/GBR/MSG v2.4](DCMS_Use_Cases_Business_Rules_Messages_v2.4.docx) UC-07 và [Screen Specifications v2.4](DCMS_Screen_Specifications_v2.4.docx) S17/S18.

## Quyết định triển khai ngày 25/10/2026

Người dùng yêu cầu bắt đầu triển khai DCMS-111. Đợt này giữ các contract bên dưới, bổ sung `GET /api/reception/options` cho lựa chọn được phân quyền và `GET /api/reception/appointments/{appointmentId}/courses?patientId=...` cho nguồn đợt cũ. Các danh sách phân trang 20/50/100; lọc bác sĩ/trạng thái/từ khóa không nới quyền. Các route staff là `/reception`, `/reception/appointments/:appointmentId` và `/visits/:visitId`.

ClinicAssistant có quyền check-in/đọc tiếp nhận; Dentist (kể cả vai trò kế thừa) chỉ đọc lượt được phân công. Hồi cứu cần thêm claim `permission=reception.backdate` của tài khoản ClinicAssistant, không suy ra từ quyền quản lý lịch hoặc Admin. Dùng kho claim Identity hiện có; không thêm hệ thống ACL riêng. Form bình thường gửi `arrivalAt=null`; hồi cứu gửi giờ +07 và lý do, trong cùng ngày tiếp nhận.

Mô hình Visit, audit và receipt mở rộng `DcmsDbContext` chung; hàng chờ là truy vấn Visit. Dùng khóa transaction hiện có của booking/change, unique AppointmentId và concurrency token. Check-in giữ khoảng/ghế/nhân sự reservation đang `Confirmed`, chỉ bổ sung định danh attendee khi cần; không đổi thành reservation rảnh. Availability kiểm lại cùng khoảng đang giữ, loại chính reservation đó khi kiểm xung đột.

Database local vẫn có migration S17/S18 cũ `20261025075305_ReceptionVisits` dù code đã hoàn tác. Migration mới phải chạy được trên cả schema sạch và schema cũ: nâng cấp tại chỗ cùng bảng Visit/audit/receipt, giữ lượt và lịch sử, chuyển tên giờ đến/ghi nhận/người ghi nhận, giữ nguồn cũ thành snapshot. Không xóa hoặc nhân bản lượt để tránh trùng tên bảng. Các cột/bảng nguồn cũ chỉ được giữ để bảo toàn lịch sử, không tạo thêm API ghi hoặc engine nguồn cho DCMS-111. Cần kiểm nhánh nâng cấp trong schema test riêng trước khi áp dụng vào local.

Đợt/item/step chưa có repository nghiệp vụ trên baseline này. Port đọc/kiểm nguồn cho module điều trị cung cấp các lựa chọn có nhãn và nguồn đã xác minh; adapter mặc định trả danh sách rỗng và từ chối ID đợt tự gửi. Không thêm bảng clinical-source độc lập. FollowUpId đã lưu trên Appointment được mang theo từ server; trường nguồn tùy chọn còn thiếu giữ dấu cần bác sĩ xác nhận. Nguồn do adapter cung cấp được lưu snapshot cùng Visit.

`/visits/:visitId` trong DCMS-111 đọc dữ liệu tiếp nhận thật và kiểm quyền; phần bắt đầu/lưu khám thuộc DCMS-112, chưa được coi là hoàn tất chỉ từ trang bàn giao. Bằng chứng triển khai được ghi tại [runbook DCMS-111](../implementation/DCMS-111-RECEPTION.md).

## Kết quả và ranh giới

Lễ tân xác nhận đúng người đến, bấm Check-in và thấy bệnh nhân xuất hiện **một lần** trong hàng chờ. Bác sĩ mở **đúng Visit đã tạo** ở [DCMS-112](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-112). Một Patient có thể có nhiều Visit qua các lần đến, nhưng một Appointment hợp lệ có tối đa một Visit và một dòng hàng chờ tương ứng.

S17 hiển thị lịch theo ngày và hàng chờ. S18 đối chiếu danh tính, ghi giờ đến và lý do đến rồi xác nhận check-in. Khách chưa có lịch dùng xếp lịch nhanh S11 trước. Không có lệnh tạo Visit trực tiếp từ tên/số điện thoại hoặc đi tắt kiểm nguồn lực.

Check-in không tạo đợt điều trị, kế hoạch, chẩn đoán, Actual, đơn thuốc, charge, khoản cọc/thu hoặc liên kết hồ sơ app. Không yêu cầu có tài khoản app hay đã nộp cọc. Lễ tân không phải quyết định item/step chuyên môn để ghi nhận người đến.

## Trường nhập và dữ liệu tự lấy

| Thông tin | Nguồn và cách xử lý |
| --- | --- |
| Patient | Tìm/chọn hồ sơ, đối chiếu tên, ngày sinh/mã/liên hệ có sẵn với người thực sự đến. Phải xác nhận đối chiếu trước commit. Không tự chọn hoặc ghép hồ sơ chỉ bằng số điện thoại |
| Người đặt và người khám | Hiển thị riêng. Booker khác attendee là trường hợp hợp lệ, không phải lỗi tự động |
| Appointment, bác sĩ, chi nhánh, ghế | Lấy từ Appointment đã chọn; nhân viên không nhập lại. Server đọc lại và kiểm quyền/phạm vi |
| Giờ đến | Mặc định thời điểm hiện tại; lưu riêng giờ thực tế đến và giờ hệ thống ghi nhận. Hồi cứu/sửa giờ cần quyền và lý do; không cho giờ đến trong tương lai |
| Lý do đến | Chọn `InitialExamination`, `TreatmentContinuation`, `PostoperativeOrWarrantyReview` hoặc `PeriodicOrNewProblem`; nhãn Việt là khám mới, điều trị tiếp, kiểm tra sau điều trị/bảo hành, tái khám định kỳ/vấn đề khác |
| Ghi chú thêm | Tùy chọn, tối đa 1000 ký tự; ghi vấn đề cần bác sĩ xem, không ghi thay chẩn đoán |
| Đợt điều trị cũ | Chọn đợt của đúng Patient khi biết; tự mang theo nguồn từ lịch nếu đã có. Thiếu nguồn tùy chọn vẫn được tiếp nhận kèm dấu cần bác sĩ xác nhận |
| Item/step/nguồn hậu điều trị | Hệ thống giữ liên kết đã có; bác sĩ xác nhận/chỉnh nguồn chuyên môn sau ở S19/S22. Không bắt lễ tân nhập ID hoặc chọn chính xác step |

Hồ sơ mới được tạo bằng UC-38 sau kiểm nghi trùng, tái sử dụng hồ sơ DCMS-88/89 và API hồ sơ chung; không tạo một Patient riêng của module tiếp nhận. Nếu Appointment đã gắn Patient khác người đang được chọn, chặn và hướng dẫn xác minh/sửa lịch đúng quyền trước khi check-in. Người đặt khác người khám không được dùng làm lý do thay Patient của lịch.

Nếu đợt/item/step chưa có module dữ liệu, giao diện thể hiện chưa có nguồn để chọn và giữ dấu cần bác sĩ xem; không dùng danh sách giả hoặc ID tự nhập. Nguồn được gửi lên phải tồn tại, cùng Patient và được phép truy cập. Check-in không xóa blocker điều trị và không biến mục bị hold/chờ Labo thành sẵn sàng.

## Điều kiện tiếp nhận và thao tác S17 S18

Ngày mặc định là hôm nay theo `Asia/Ho_Chi_Minh`, không lấy ranh giới ngày UTC hoặc múi giờ trình duyệt. Có thể xem ngày khác theo quyền nhưng chỉ gọi là “Hôm nay” khi đúng ngày phòng khám. Check-in thông thường cần Appointment `Confirmed`, cùng ngày phòng khám và còn hợp lệ; lịch tương lai, khác ngày, hủy hoặc No-show không vào hàng chờ hiện tại. Đến muộn/hết khoảng đặt cần đánh giá lại khả năng phục vụ và xếp một lịch hợp lệ theo booking chung.

S17 hiển thị attendee và booker riêng, thời gian đặt, bác sĩ, trạng thái Appointment; phần hàng chờ hiển thị Patient, giờ đến, thời gian chờ và trạng thái Visit. `Queued` hiển thị “Chờ khám”; không lưu thêm trạng thái `Waiting`. Dữ liệu lỗi tải không được hiện như danh sách rỗng. Bác sĩ chỉ đọc hàng chờ/lượt thuộc phân công và chi nhánh được phép; quyền Admin không tự cấp quyền nghiệp vụ tiếp nhận.

S18 đọc lại lịch trước khi hiển thị và trước khi commit. Hiển thị điều kiện không đủ từ sớm, giữ Patient đã chọn và nháp khi lỗi. Lỗi stale yêu cầu tải lại, xem thay đổi rồi xác nhận lại; không ghi đè. Nút xác nhận có trạng thái đang lưu, nhưng khóa nút chỉ là hỗ trợ giao diện, không thay cơ chế chống trùng ở server.

Sau thành công, đọc lại hàng chờ và hiển thị nút mở lượt mang đúng `visitId`. Lặp check-in mở kết quả hiện hữu. “Xác nhận sẽ đến” trên lịch không phải check-in và không tạo dòng chờ. Lý do đến không tự thay ưu tiên lâm sàng, thời lượng hoặc giá.

## Khách chưa có lịch và tài nguyên

S17 mở S11 với ngày/chi nhánh/bác sĩ đang chọn, loại WalkIn và nguồn InPerson; nhân viên chọn hồ sơ có nhãn trong form S11 chung. S18 có lịch không hợp lệ hướng về S17 để xếp lịch hợp lệ. Booking và availability của DCMS-83/84/85 kiểm lịch làm việc, ghế và toàn bộ nhân sự được phân công, cả nha sĩ và trợ lý khi có. Phòng khám có ba ghế vật lý, không phải ba nha sĩ hoặc ba slot mặc định. Preview không giữ chỗ; commit booking kiểm lại nguồn lực/version. Không tự giải phóng reservation trong lúc chờ thuốc/chuẩn bị.

Chỉ khi booking trả về Appointment hợp lệ mới quay lại S18 để check-in. Xung đột tài nguyên giữ nguyên form và không tạo Visit. Hủy form sau khi đã đặt lịch không tự hủy Appointment. Giờ thực tế đến có thể sớm hơn lúc xếp được lịch; lưu theo quyền hồi cứu và lý do, không chế tạo lịch quá khứ. Tiếp nhận không tạo lại hoặc nới reservation; nếu cần đổi khoảng đặt, dùng command booking/change chung.

## Quyền và nguồn phải kiểm ở server

Actor còn phiên và quyền hành động; chi nhánh còn hiệu lực; Patient không bị lưu trữ và được phép sử dụng; Appointment đúng ngày/trạng thái/Patient; bác sĩ/nguồn liên quan đúng lịch. Không tin branch, dentist hoặc source ID chỉ vì xuất hiện trong route/body. Query, command, retry và deep link đều áp dụng quyền hiện tại.

Quyền check-in, đọc hàng chờ, đọc Visit và hồi cứu giờ đến phải được ánh xạ vào cơ chế phân quyền chung khi triển khai. Tài liệu này không giả định đã có các permission đó trên `main`. Trợ lý tiếp nhận không tự được quyền bắt đầu/lưu khám; DCMS-112 quản lý chuyển `Queued` sang `InProgress` theo quyền bác sĩ.

## Dữ liệu và transaction

Một transaction check-in tạo/đọc một Visit, gắn Patient đã xác nhận, đổi Appointment sang `CheckedIn`, ghi audit/receipt cần thiết và trả kết quả. Lần tạo mới đặt Visit `Queued`; retry trả **trạng thái hiện tại** của cùng Visit, không kéo một lượt `InProgress`/`Completed` trở về `Queued`.

Hàng chờ nên đọc từ Visit/trạng thái chung, không dựng engine tiếp nhận thứ hai. Persistence phải bảo đảm duy nhất theo `Visit.AppointmentId`; nếu có cấu trúc hàng chờ riêng được thống nhất với DCMS-112 thì cũng duy nhất theo `VisitId`. Một Patient không bị giới hạn một Visit cho toàn bộ lịch sử.

Command dùng `requestKey` và phiên bản Appointment. Cùng actor/key/payload trả cùng `visitId`; dùng lại key với payload khác là xung đột. Hai nhân viên/hai key cùng check-in phải hội tụ vào Visit đã commit sau khi xác minh quyền và cùng Patient, không tạo lượt thứ hai. Kiểm và trả lượt hiện hữu trước khi báo stale do chính check-in trước đó; không trả lượt của Patient khác để che một xung đột danh tính.

Fingerprint bao gồm Appointment trên route và dữ liệu command đã chuẩn hóa. Với `arrivalAt: null`, receipt giữ giờ server đã chọn ở lần commit đầu; retry không tính lại giờ đến hoặc coi thời gian hiện tại mới là payload khác.

Dùng transaction/locking, concurrency token, receipt và ràng buộc DB phù hợp với nền booking/change hiện có. Race check-in với cancel/đổi lịch phải tuần tự hóa trên cùng Appointment theo cơ chế chung; bên thua đọc lại trạng thái, không để lịch vừa hủy vừa có lượt chờ mới. Vi phạm unique do race được xử lý thành đọc kết quả hiện hữu hoặc lỗi nghiệp vụ rõ ràng, không trả thành công khi ghi chưa commit.

Rollback khi command thất bại không để Visit/dòng chờ mồ côi hoặc Appointment đổi trạng thái một phần. Patient/lịch đã được tạo hợp lệ bằng command riêng trước đó vẫn giữ nguyên. Audit lưu người thao tác, giờ ghi nhận, giờ đến, xác nhận danh tính, lý do hồi cứu nếu có và nguồn đợt cũ; không ghi token/thông tin xác thực.

## Contract API DCMS-111

Các endpoint và route trong phần này đã có trên nhánh `feat/dcms-111-reception`, chưa merge vào `main`. DCMS-112 dùng chung `GET /api/visits/{visitId}` và mô hình Visit; command khám thuộc task đó. Kiểu ID theo nền hiện tại: Patient/chi nhánh/nhân sự là số nguyên; Appointment và Visit là UUID do server tạo.

| Endpoint | Mục đích và đầu ra |
| --- | --- |
| `GET /api/reception/appointments?branchId=1&date=2026-10-25&page=1&pageSize=20` | Ngữ cảnh S17: giờ server/ngày phòng khám, lịch được phép xem, trạng thái/điều kiện tiếp nhận và `visitId` hiện hữu nếu có |
| `GET /api/reception/appointments/{appointmentId}` | Ngữ cảnh S18: lịch, booker/attendee riêng, Patient đã gắn, phiên bản, nguồn sẵn có và quyền hồi cứu; không giữ chỗ |
| `POST /api/reception/appointments/{appointmentId}/check-in` | Xác nhận danh tính/giờ đến/lý do, tạo hoặc trả đúng một Visit |
| `GET /api/reception/queue?branchId=1&date=2026-10-25&page=1&pageSize=20` | Dòng chờ/lượt được phép xem, phân trang/tổng số đúng quyền; mỗi Visit một lần |
| `GET /api/visits/{visitId}` | Contract đọc Visit dùng chung với DCMS-112; server kiểm Patient/chi nhánh/phân công. Start Visit của DCMS-112 cập nhật lượt này, không tạo lượt khác |

Tìm/tạo Patient tái sử dụng `GET /api/patients`, `POST /api/patients/duplicates`, `POST /api/patients`. Xếp lịch dùng `POST /api/appointments` cùng API availability hiện có. Không sao chép request tạo lịch, resolver hoặc bảng hồ sơ sang module mới.

Ví dụ request minh họa; tất cả ID/version do ngữ cảnh API hoặc lựa chọn có nhãn cung cấp, không phải trường nhập thủ công:

```json
{
  "patientId": 101,
  "expectedVersion": 3,
  "requestKey": "c01e7269-3ba2-49f1-b87d-354c223e6b5a",
  "identityConfirmed": true,
  "arrivalAt": null,
  "arrivalReason": null,
  "purpose": "TreatmentContinuation",
  "priorCourseId": null,
  "note": "Bệnh nhân tiếp tục điều trị, nhờ bác sĩ xác nhận hạng mục."
}
```

`arrivalAt: null` dùng giờ server. Khi hồi cứu, gửi giờ có offset và lý do, server kiểm quyền; mặc định thông thường không đòi lý do. `priorCourseId: null` không chặn check-in; nguồn có sẵn trên Appointment vẫn được server mang theo. Kiểu/định danh đợt và cấu trúc nguồn phải dùng chung với module điều trị, không tạo một định danh đợt khác chỉ cho tiếp nhận.

Ví dụ response lần tạo đầu; dữ liệu giả dành cho tài liệu:

```json
{
  "visitId": "17dff34d-3af6-47d6-baa5-2bbb4de76146",
  "patientId": 101,
  "appointmentId": "c5925bf0-80dc-4c2c-bcc8-3342063d3722",
  "branchId": 1,
  "dentistId": 12,
  "chairId": 2,
  "arrivalAt": "2026-10-25T09:00:00+07:00",
  "recordedAt": "2026-10-25T09:00:00+07:00",
  "status": "Queued",
  "appointmentStatus": "CheckedIn",
  "visitVersion": 1,
  "appointmentVersion": 4,
  "purpose": "TreatmentContinuation",
  "source": null,
  "requiresClinicalSourceReview": true,
  "replayed": false,
  "openVisitPath": "/visits/17dff34d-3af6-47d6-baa5-2bbb4de76146"
}
```

Khi có nguồn, `source` trả liên kết đợt/item/step/hoạt động gốc và phiên bản **đã được server xác minh** theo contract chung; không bắt các thành phần tùy chọn phải đủ. Không mang tên/ID nguồn của Patient khác. `openVisitPath` là route staff đề xuất cùng DCMS-112; truy cập trực tiếp vẫn kiểm quyền. Nếu màn khám chưa được tích hợp, hiển thị trạng thái chưa khả dụng; không báo đã nghiệm thu đường mở màn khám chỉ bằng một trang giả.

Command trả HTTP 200 với Visit có thẩm quyền cho cả lần tạo và đọc lại, dùng `replayed` phân biệt. Lỗi theo envelope `SystemMessage` chung (`code`, `message`, `field` khi cần). Danh mục đã bổ sung MSG42/MSG47 cho lịch không đủ điều kiện, MSG43 cho sai người khám, MSG44 cho nghi trùng, MSG111 cho stale/key-payload xung đột, MSG114 cho lượt đã check-in và MSG144 cho lỗi lưu. UI biểu đạt cảnh báo nguồn tùy chọn qua `requiresClinicalSourceReview`, và kết quả có sẵn qua `replayed`; các nhánh thành công này không trả lỗi chặn. 409 nghiệp vụ không đồng nghĩa mọi trường hợp đều cần reload do stale.

## Ma trận nghiệm thu cần chạy

Ma trận dưới đây là tiêu chí và bằng chứng cần có. Các ca API/UI DCMS-111 đã chạy lại trên nhánh mới được ghi tại [runbook](../implementation/DCMS-111-RECEPTION.md); toàn bộ bước bắt đầu/lưu khám của DCMS-112 vẫn chưa nghiệm thu. Không dùng kết quả của nhánh S17/S18 đã hoàn tác để đánh dấu pass trên `main`.

| Ca | Bằng chứng phải có |
| --- | --- |
| Có lịch hợp lệ | UI xác nhận đúng Patient → API commit → DB một Visit → queue một dòng → bác sĩ mở đúng `visitId` tại DCMS-112 |
| Walk-in | S11 đặt được lịch hợp lệ rồi quay lại S18; race/xung đột ghế hoặc bất kỳ nhân sự nào bị chặn trước khi tạo Visit |
| Bấm đôi/retry/hai nhân viên | Gửi cùng key và khác key đồng thời; cùng Appointment/Patient chỉ một Visit/dòng chờ; đọc lại trả ID chuẩn |
| Retry sau khi bác sĩ bắt đầu | Trả cùng Visit `InProgress`, không reset trạng thái hoặc thêm lượt |
| Danh tính | Booker khác attendee vẫn xử lý đúng; dùng chung điện thoại không tự ghép; chọn sai Patient bị chặn; không tạo Patient thứ hai để vượt lỗi |
| Ngày/trạng thái | Kiểm ranh giới ngày +07, lịch tương lai/khác ngày/hủy/No-show/hết hiệu lực; không tạo Visit |
| Đồng thời sửa/hủy lịch | Race với check-in không để Appointment/Visit trái trạng thái hoặc ghi đè phiên bản |
| Giờ đến | Mặc định server; hồi cứu có quyền/lý do giữ arrival và recorded riêng; thiếu quyền/lý do hoặc giờ tương lai bị chặn |
| Lý do và nguồn cũ | Không cần item/step; thiếu nguồn tùy chọn vẫn Queued/cần review; nguồn Patient khác bị chặn; giữ blocker điều trị |
| Phân quyền | API trực tiếp và deep link sai chi nhánh/phân công/quyền bị chặn; quyền đã thu hồi không được replay kết quả riêng tư |
| Không tác dụng phụ | So sánh DB trước/sau: không thêm plan/diagnosis/Actual/charge/collection/app self-link; reservation không tự nhả hoặc tạo lại |
| Lỗi commit và UI | Lỗi lưu rollback toàn bộ command; form giữ dữ liệu, thoát loading, không hiện thành công hoặc queue giả |

Bằng chứng bàn giao gồm PR, commit, contract đã thống nhất với DCMS-112, ví dụ dữ liệu và kết quả backend/API/PostgreSQL/UI thật trong môi trường test tách biệt. Mock chỉ dùng cho kiểm riêng giao diện. Chưa tích hợp DCMS-112 thì ghi rõ phần handoff chưa đạt, không gộp thành pass toàn luồng.

## Đối chiếu code hiện tại

Kiểm trên `main` tại `7da445f`: đã có Appointment/booking/availability, Patient master và các pattern transaction/receipt/concurrency. Các đầu mối là `backend/src/DCMS.Application/Features/Appointments/Contracts.cs`, `backend/src/DCMS.Infrastructure/Features/Appointments/AppointmentService.cs`, `backend/src/DCMS.Infrastructure/Persistence/DcmsDbContext.cs` và `web/src/app/App.tsx`.

Baseline sau hoàn tác chưa có Visit domain/DbSet, Reception endpoints hoặc route tiếp nhận. Nhánh DCMS-111 đã bổ sung các phần này vào cùng codebase/context, cùng migration nâng cấp dữ liệu legacy. Không cherry-pick toàn bộ engine nguồn và chức năng xếp ưu tiên của nhánh cũ. Trang Visit hiện đọc dữ liệu bàn giao thật; phần bắt đầu/lưu khám và nguồn điều trị còn chờ module tương ứng.
