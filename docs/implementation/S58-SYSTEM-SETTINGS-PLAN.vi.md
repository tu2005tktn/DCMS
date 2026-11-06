# S58 — Kế hoạch triển khai và nghiệm thu

Ngày 27/10/2026. **Đây là kế hoạch, không phải kết quả chạy thử.** Đã giao việc tại [Epic DCMS-118](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-118), gồm 14 ticket có assignee; xem [bảng phân công và phụ thuộc](S58-JIRA-HANDOFF.vi.md). Đó là trạng thái của lần lập kế hoạch. Cập nhật code local ngày 27/10 cho W01/W03/W10 và bằng chứng được ghi riêng tại [DCMS-119/121/128](S58-119-121-128.vi.md); không tự đánh dấu đạt các task khác hoặc toàn bộ 32 AT.

Đọc trước: [đặc tả S58/UC-51](../specs/S58-SYSTEM-SETTINGS.vi.md), [khảo sát oDental](../research/ODENTAL-SYSTEM-SETTINGS-20261027.vi.md), [bộ v2.5](../specs/README.md). Mỗi đợt triển khai tiếp tục cập nhật docs trước code theo AGENTS.md.

## 1. Khoảng trống đã đối chiếu từ code local

Chỉ xác nhận sự hiện diện/logic của source tại thời điểm đọc; workspace đang có nhiều thay đổi của công việc khác. Không dùng bảng này để khẳng định runtime đã đạt.

| Phần | Code đã đọc | Tái dùng / cần bổ sung |
| --- | --- | --- |
| Menu/route | [App.tsx](../../web/src/app/App.tsx) | Có `/booking-settings`, `/accounts`, `/treatment-templates`, `/commercial/treatment`; chưa có `/settings` và shell 11 nhóm |
| Cấu hình lịch | [BookingSettingsService.cs](../../backend/src/DCMS.Infrastructure/Features/Scheduling/BookingSettingsService.cs), [BookingSettingsPage.tsx](../../web/src/features/working-schedules/BookingSettingsPage.tsx) | Có branch scope, duration, canonical Khám, version/key/transaction; tái dùng, không tạo nguồn thời lượng thứ hai |
| Chi nhánh/dịch vụ/ghế | [SchedulingModels.cs](../../backend/src/DCMS.Domain/Features/Scheduling/SchedulingModels.cs) | Có stable IDs/branch membership/chair/service; chưa đủ BranchProfile/contact/logo/catalog tree. Mở rộng giữ ID |
| Giá dịch vụ | [ServicePriceService.cs](../../backend/src/DCMS.Infrastructure/Features/Treatment/ServicePriceService.cs), [TreatmentEndpoints.cs](../../backend/src/DCMS.Api/Features/Treatment/TreatmentEndpoints.cs) | Có giá/version riêng và API service-prices; dùng lại, bổ sung metadata catalog và quyền mới có lộ trình |
| Bệnh nhân | [PatientProfile.cs](../../backend/src/DCMS.Domain/Features/Patients/PatientProfile.cs), [PatientService.cs](../../backend/src/DCMS.Infrastructure/Features/Patients/PatientService.cs) | Có patient master/code/phone/gender/guardian/audit; chưa đủ cấu hình required/mode và bộ trường mở rộng v2.5. Không chỉ làm toggle mà bỏ consumer validation |
| Chẩn đoán S19 | [odontogram.ts](../../web/src/features/examinations/odontogram.ts), [ExaminationService.cs](../../backend/src/DCMS.Infrastructure/Features/Examinations/ExaminationService.cs) | Options UI hiện là hằng; server giữ chẩn đoán chuỗi trong content và tổng hợp nguồn. Thêm catalog options/snapshot tương thích, giữ nhiều bệnh tại răng/vùng |
| Quyền | [AccountRoles.cs](../../backend/src/DCMS.Domain/Features/Accounts/AccountRoles.cs), [AuthenticationService.cs](../../backend/src/DCMS.Infrastructure/Features/Authentication/AuthenticationService.cs), [S04](../specs/S04-UC31.md) | Owner chỉ kế thừa Dentist; permission projection chủ yếu từ role, chưa có access-group editor/resolver hoàn chỉnh. Không mặc nhiên cấp Admin cho Owner |
| Idle | [AuthSession.cs](../../backend/src/DCMS.Domain/Features/Authentication/AuthSession.cs), [AuthenticationService.cs](../../backend/src/DCMS.Infrastructure/Features/Authentication/AuthenticationService.cs) | Có LastSeenAt/expiry/refresh; LastSeenAt cập nhật mỗi request nên không đủ chứng minh idle theo tương tác. Bổ sung client kind/interactive activity và check refresh |
| Provider/material/medicine/print | S58 shell và các resource mới trong đặc tả chưa có đầy đủ module tương ứng trong các nguồn đã khảo sát | Phải xây model/service/consumer riêng; không đánh dấu xong kho/thuốc/in chỉ vì có CRUD danh mục |
| Thông báo | [SystemMessages.cs](../../backend/src/DCMS.Application/Common/SystemMessages.cs) | Có MSG111/201/144; cần đối chiếu/bổ sung MSG205/257 theo Word khi triển khai. Không đổi mọi business 409 thành stale |

## 2. Công việc theo thứ tự phụ thuộc

Mã SET-W* ánh xạ 14 ticket **DCMS-119 đến DCMS-132** tại [bảng phân công](S58-JIRA-HANDOFF.vi.md). Mỗi ticket gồm scope code, đặc tả trường/API/quyền, nghiệm thu và phụ thuộc; giữ mã SET-W để truy vết.

| Mã / ưu tiên | Công việc và đầu ra | Phụ thuộc | Điều kiện hoàn thành |
| --- | --- | --- | --- |
| SET-W01 / P0 | Chốt schema/projection quyền, scope clinic/branch, typed DTO, error codes và danh sách field; thêm setting version/audit/receipt | Đặc tả | Migration tương thích, read không ghi, concurrency/idempotency có kiểm thử |
| SET-W02 / P0 | Shell `/settings`, 11 nhóm, deep link, loading/error/empty/read-only, cảnh báo unsaved, capabilities | W01 | Chỉ hiện section được phép, reload/deep link đúng scope, không có nút cho chức năng chưa nối |
| SET-W03 / P0 | CRUD chẩn đoán và danh mục phụ + options API; nối S19/S23/S05 theo consumer hiện có | W01–02 | Đổi danh mục xuất hiện ở dòng mới, inactive/historical/legacy và chẩn đoán nhiều nguồn đúng |
| SET-W04 / P0 | General: required fields và ba chế độ mã; bổ sung trường hành chính patient và UI theo v2.5 | W03 | Tất cả đường lưu hành chính kiểm policy server, mã sinh đồng thời unique; hồ sơ cũ đọc được |
| SET-W05 / P0 | Cây dịch vụ/code/unit/warranty/status; nối booking-settings và service-prices giữ cùng ID | W01–03 | Không có nguồn giá/thời lượng song song; canonical Khám và lịch cũ không đổi |
| SET-W06 / P0 | Branch profile/logo/ghế + preview dependencies; adapter kiểm nghĩa vụ | W01–02 | Giữ branch/chair ID, không tự seed thêm ghế, chặn deactivation khi còn blocker/adapter thiếu |
| SET-W07 / P1 | Provider Supplier/Lab/Both, material category/type, lab-work type và consumer projection | W03, W06 | Provider không trùng giữa tab, unit/cost snapshot đúng; không tạo kho/nợ từ catalog |
| SET-W08 / P1 | Medicine catalog và nối lựa chọn đơn mua ngoài | W03 | Mã/đơn vị rõ; prescription không trừ tồn; danh mục giá không bật bán thuốc |
| SET-W09 / P1 | Nhóm khách hàng thuần phân loại; nối Patient group, điều kiện khuyến mãi khi module sẵn sàng | W03–04 | Không điểm/thẻ trả trước, không tự giảm giá khi gán nhóm |
| SET-W10 / P0 | Access groups, assignment và permission resolver cho API + UI; migration đối chiếu quyền cũ | W01–02 | Thu hồi quyền có hiệu lực request sau, không tự nâng quyền; không hard-delete posted record |
| SET-W11 / P1 | Inactivity staff web, activity endpoint, UI warning, refresh/session checks | W01, W10 | Polling/refresh không giữ phiên idle; patient session không bị áp policy staff |
| SET-W12 / P1 | Receipt print options, preview giả, số phiếu branch; nối ledger/in thực tế | W01, W06 và module S39/in | Preview không tiêu thụ số; số concurrent unique; in lại giữ tiền/identity lịch sử |
| SET-W13 / P1 | Seed manifest phiên bản và bảo toàn dữ liệu hiện có; migration staging copy đã ẩn danh | W03–10 | Chạy lại không overwrite, không có data tenant/giá âm/public booking tự bật |
| SET-W14 / P1 | Excel theo filter/quyền, audit/restore và kiểm thử end-to-end các consumer đã nối | Từng resource tương ứng | Export không lộ cột, restore sinh version mới; báo evidence theo từng lớp |

Đợt bàn giao gợi ý: **A** W01–05 (shell + danh mục khám + general hồ sơ + dịch vụ), **B** W06–10 (chi nhánh và danh mục hỗ trợ + quyền), **C** W11–14 (idle/in/export/seed/nghiệm thu tích hợp). Quyền kiểm backend cơ bản phải có ngay từ A; W10 mở rộng editor và resolver nhóm, không trì hoãn authorization đến B. P0 ưu tiên theo phụ thuộc, không hứa toàn bộ có thể làm song song. W12 chỉ hoàn thành khi ledger/in thật đã nối; thiếu module ghi rõ blocked dependency, không giả in đạt.

## 3. Tiêu chí nghiệm thu

Các mã dưới đây là test specification, **chưa chạy**. Nêu thao tác và kết quả để team chuyển thành API/browser tests có ý nghĩa.

| Mã | Kịch bản | Kết quả cần quan sát |
| --- | --- | --- |
| SET-AT-01 | Owner và staff chỉ có một catalog grant mở shell/deep link | Owner/granted scope đúng; staff chỉ thấy nhóm được cấp; gọi API nhóm khác trả 403, không lộ total |
| SET-AT-02 | Tạo/sửa một danh mục có dấu, tìm/lọc/phân trang, refresh | ID/version đọc lại đúng, tên giữ dấu; order ổn định; lỗi tải khác empty |
| SET-AT-03 | Hai client sửa cùng version và retry sau timeout | Một write thắng, client sau MSG111 giữ draft; same key/payload không tạo audit/entry trùng |
| SET-AT-04 | Retry key với payload khác hoặc sau khi mất quyền | Payload khác 409 MSG111; mất quyền 403 trước replay, không trả dữ liệu cũ |
| SET-AT-05 | Bật required ngày sinh/nguồn rồi mở patient cũ thiếu | Đọc được; lưu hành chính thiếu bị MSG02; không tự điền và không chặn riêng read/khám |
| SET-AT-06 | Bật/tắt policy trong khi form patient đang nhập | Lưu stale bị conflict, draft giữ; tải policy mới rồi validate đúng; không âm thầm tạo bệnh nhân thiếu |
| SET-AT-07 | Lần lượt ba code modes và tạo đồng thời | AutoOnly bỏ qua mã client; ManualOnly thiếu bị chặn; generated code unique clinic-wide; manual duplicate MSG201 |
| SET-AT-08 | Đổi mode/prefix/sequence; xem mã bệnh nhân cũ | Mã cũ bất biến, không tái sử dụng; sequence lùi bị từ chối; GET không cấp số |
| SET-AT-09 | Ngừng/đổi tên bệnh đã dùng trong khám cũ, mở khám và đơn mới | Phiếu cũ giữ snapshot/legacy; dòng mới không chọn inactive; bệnh tại răng/Tổng kết không mất hoặc tự đổi |
| SET-AT-10 | Sửa/xóa chẩn đoán nguồn, ca bình thường và ca chưa kết luận | Giữ hợp đồng nhiều chẩn đoán S19; không dùng catalog để tự xác nhận bình thường hoặc ép bệnh giả |
| SET-AT-11 | Di chuyển category vào hậu duệ/cha khác clinic; ngừng cha có con active | Chặn vòng/sai scope; yêu cầu xử lý con; tree search giữ đường dẫn |
| SET-AT-12 | Giá âm, giá NULL, giá 0 và giá thỏa thuận cũ | Âm MSG257; NULL/0 phân biệt; giá mới không sửa plan/fee snapshot |
| SET-AT-13 | Đổi duration qua settings rồi booking với token cũ | Cùng một nguồn duration/version, stale MSG111; appointment cũ giữ thời lượng |
| SET-AT-14 | Gửi ID điều trị hoặc giả cờ public/isInitialExamination | Đặt khám mới vẫn chỉ canonical Khám; không nhân đôi Khám khi seed hoặc rename |
| SET-AT-15 | Provider Both hiện ở hai tab, đổi về Supplier khi còn labo | Một ID; chặn chuyển loại làm mất quan hệ hợp lệ; không sinh payable |
| SET-AT-16 | Đổi unit/conversion/default price có movement cũ | Ledger cũ giữ snapshot; base unit nghĩa khác bị chặn hoặc version mới; số dư không đổi từ catalog |
| SET-AT-17 | Tạo work type labo không default provider và tạo yêu cầu mới | Catalog lưu được; tại yêu cầu chọn provider đủ điều kiện, snapshot giá/đơn vị; không tự bảo hành |
| SET-AT-18 | Chọn thuốc kê mua ngoài không có tồn và sửa lời dặn trên đơn | Chọn được theo prescriptionEligible; chỉ dòng đơn đổi, không sửa catalog hoặc tạo movement |
| SET-AT-19 | Gắn nhóm bệnh nhân rồi xem phí/collection | Chỉ phân nhóm; không tạo tiền thẻ/điểm hoặc giảm giá chưa được xác nhận |
| SET-AT-20 | Thu hồi group grant khi tab đang mở và gán group inactive | Request kế tiếp bị chặn; inactive không gán mới; draft không gửi ngầm; không đổi Identity roles ngoài phạm vi |
| SET-AT-21 | Tự cấp quyền nhóm, grant clinical cho non-dentist, grant ngoài scope | Server chặn cả request trực tiếp; tên nhóm Doctors không đủ tạo quyền nha sĩ |
| SET-AT-22 | Sửa quyền xóa thanh toán/kho cho chứng từ posted | Chỉ mở correction/reversal được phép và phê duyệt độc lập; không DELETE ledger |
| SET-AT-23 | Idle 5 phút với polling, refresh và hai tab | Server từ chối sau deadline, refresh không hồi sinh; Patient session vẫn dùng policy riêng |
| SET-AT-24 | Có tương tác thật trước deadline, sau deadline, giảm/tăng timeout | Chỉ phiên chưa expired được touch bằng giờ server; giảm không reset activity; tăng không phục hồi revoked |
| SET-AT-25 | Idle trong khi nhập phiếu rồi đăng nhập tài khoản khác | Không tự commit, không lộ draft/record cho actor khác; hiển thị đăng nhập lại rõ ràng |
| SET-AT-26 | Toggle in rồi preview và in lại receipt cũ | Preview rõ dữ liệu giả/không cấp số; layout đổi nhưng tiền/số/payer/collector/branch snapshot cũ giữ nguyên |
| SET-AT-27 | Phát hành số phiếu đồng thời hai chi nhánh, retry và đổi start | Unique trong branch, retry giữ ID/số; không reset/tái dùng; tắt show number vẫn cấp số |
| SET-AT-28 | Upload logo giả MIME, file quá giới hạn hoặc URL script | Bị từ chối trước gắn asset; file hợp lệ không nằm trong Git/webroot, chỉ đúng scope truy cập |
| SET-AT-29 | Ngừng branch/chair có lịch, Visit hoặc nghĩa vụ/module thiếu | Chặn với dependency/unavailable cụ thể; không tự hủy/dời và không tăng ghế |
| SET-AT-30 | Export lọc active/search với quyền contact/cost bị hạn chế | Đúng filter/authorized total/columns; không có cost/contact/secret ngoài quyền; công thức Excel vô hiệu |
| SET-AT-31 | Restore cấu hình cũ trong khi có số/mã/mục đã tham chiếu mới | Tạo version mới, validate current constraints; không rollback sequence/ID hoặc thay chứng từ cũ |
| SET-AT-32 | Chạy seed hai lần trên clinic có dữ liệu đã sửa | Không duplicate/overwrite, đúng manifest; Cash/Transfer duy nhất; không tự kích hoạt service/public grant |

Đối chiếu Word: AT81 ↔ SET-AT-05/06; AT82 ↔ 07/08; AT83 ↔ 05/09 và consumer hồ sơ; AT84 ↔ 32 với sửa nghĩa 2 methods theo GBR-180; AT85 ↔ 12; AT89 ↔ 26/27; AT90 ↔ 01/23–25. Các acceptance về ledger/stock dùng contract module tương ứng, không suy đạt từ catalog.

## 4. Cách thu bằng chứng khi triển khai

1. Kiểm schema/domain/API bằng PostgreSQL schema riêng, giờ test điều khiển được cho idle, concurrency cho mã/số/version. Dữ liệu giả, không dùng hồ sơ bệnh nhân thật hoặc sửa danh mục tenant oDental.
2. Browser dùng API mock để kiểm state/form/permission rendering; ghi rõ mock. Sau đó chạy các ca owner/limited staff thật với API và schema riêng để chứng minh enforcement, read-back, consumer và audit.
3. Backend .NET 10 restore/build theo AGENTS; web install/build; chạy integration/browser suites phù hợp phạm vi và kiểm `/health`. Không cần chạy chúng cho lần chỉ soạn Markdown này.
4. Khi handoff, báo từng nhóm: đã code, API/database thật đạt, browser mock đạt, browser thật đạt hoặc chưa nối. Mobile typecheck không phải device proof; không tuyên bố parity mobile từ web settings.

## 5. Các đầu vào cần trước dùng thật

- Manifest seed tên cụ thể/ICD và danh mục dịch vụ, đơn vị, giá thực tế (Q01); rà soát riêng các tên gần trùng quan sát trên oDental.
- Quyền thực tế từng nhóm/nhân viên và scope branch; không dùng bốn nhóm seed để cấp hàng loạt quyền nhạy cảm.
- Điều khoản ưu đãi/bảo hành và ghi phí gói (Q08); không suy từ tên nhóm hoặc ô bảo hành.
- Thông tin chi nhánh, logo và bank/payee công khai đã xác minh. Credential tích hợp ở môi trường, không đưa vào issue/log/docs.

Các đầu vào này là việc chuẩn bị vận hành, không phải yêu cầu dừng viết code CRUD/quyền/version theo đặc tả. Đã tạo và phân công backlog tại DCMS-118; chưa commit/push code hoặc sửa hệ thống oDental trong yêu cầu này.
