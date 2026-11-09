# Đặt lịch và xem hồ sơ bệnh nhân cho người thân — 09/11/2026

## Phạm vi triển khai

Theo yêu cầu triển khai ngày 09/11/2026, thực hiện trên **web khách hàng trước**, gồm API, database và màn nhân viên xác minh; mobile là lượt riêng. Tài liệu này thay thế bản đề xuất ngày 07/11. B có thể không có tài khoản; Account A, FamilyMember do A khai và Patient B vẫn là ba đối tượng riêng.

## Đặt lịch hộ

- Từ Người thân của tôi hoặc bộ chọn trong đặt lịch, A chọn FamilyMember thuộc mình. Lịch giữ A là người đặt/liên hệ và B là người khám.
- `POST /api/appointments` thêm `familyMemberId?: UUID`. Chỉ tài khoản Public đang hoạt động, `attendeeRelation=Relative`, người thân chưa xóa thuộc phiên mới được dùng trường này. Staff/guest/self hoặc ID của người khác bị từ chối. Server chụp họ tên/ngày sinh từ bản ghi đã kiểm sở hữu, không tin `PatientId` do khách hàng gửi và không dùng số điện thoại để ghép hồ sơ. Tên/ngày sinh người thân trên form booking là chỉ đọc; muốn đổi thì sửa người thân trước.
- Appointment lưu `FamilyMemberId` nullable để truy vết lịch đặt hộ. Không tự tạo/ghép Patient, không cấp quyền bệnh án khi lưu người thân hoặc đặt lịch. Tiếp nhận tiếp tục đối chiếu và chọn/tạo Patient theo luồng hiện hành. Booking cũ không có ID được giữ nguyên, không tự ghép theo tên/SĐT.
- Fingerprint thêm FamilyMemberId khi có, giữ nguyên fingerprint cũ khi thiếu. Retry sau khi người thân đã xóa vẫn có thể trả receipt đặt lịch đã commit cho đúng account; không tạo lịch mới. Command mới phải kiểm người thân còn hiệu lực trong transaction trước khi tạo reservation.
- GET `/api/me/family-members/{id}/appointments?page&pageSize` trả lịch có BookerAccountId phiên và đúng FamilyMemberId, không cần quyền bệnh án. Không trả hồ sơ khám/tài chính/ghi chú nội bộ. Link chi tiết dùng route lịch khách hàng hiện có.

## Xin và cấp quyền theo dõi

1. Trong Xem hồ sơ, A xem trạng thái và gửi yêu cầu kèm mã hồ sơ nếu biết/thông tin đối chiếu. Chưa được duyệt chỉ hiện dữ liệu A khai và lịch A đặt hộ.
2. Yêu cầu lưu snapshot họ tên/ngày sinh/quan hệ và owner; PatientId để null. Một người thân chỉ có một Pending/Verified còn hiệu lực; gửi lại khi Pending trả yêu cầu hiện tại. Verified hết hạn có thể xin lại; lưu chuyển trạng thái hết hạn và audit.
3. Nhân viên có role ClinicAssistant và quyền `patient-links.read` xem hàng đợi. Tra cứu/duyệt/từ chối/thu hồi yêu cầu quyền `patient-links.verify`, kiểm lại từ database ở service. Không tự mở quyền cho Dentist/Admin/Owner chỉ vì có menu.
4. Nhân viên chỉ tra cứu Patient có lượt khám/hồ sơ điều trị ở chi nhánh được phân quyền; xác minh họ tên (chuẩn hóa dấu/khoảng trắng) và ngày sinh nếu có. Không chọn hồ sơ đã lưu trữ. Bắt buộc ghi kết quả đối chiếu và căn cứ chia sẻ `PatientConsent` hoặc `LegalRepresentative`; nhân viên xác nhận đã kiểm tra sự đồng ý/căn cứ, hệ thống không suy tư cách đại diện từ quan hệ hoặc tuổi.
5. Khi duyệt, chọn rõ một hay nhiều phạm vi `Examinations`, `Treatment`, `Prescriptions`, `Documents`, cùng ngày hết hạn bắt buộc (sau hiện tại và tối đa 365 ngày). UI gợi ý 90 ngày, các phạm vi mặc định không chọn. Quyền chỉ áp dụng dữ liệu thuộc chi nhánh xác minh đã chọn/được nhân viên quản lý, tránh chia sẻ hồ sơ ở chi nhánh ngoài quyền của người duyệt.
6. Từ chối/thu hồi có lý do nội bộ, nội dung công khai riêng. Chủ yêu cầu có thể rút Pending hoặc chấm dứt quyền Verified. Mọi mutation dùng expectedVersion + requestKey, giao dịch/audit/receipt cùng commit, sai version hoặc key khác nội dung trả 409.
7. Sửa họ tên/ngày sinh người thân hoặc xóa người thân chấm dứt Pending/Verified trong cùng giao dịch, lưu audit; sửa liên hệ/quan hệ không chuyển quyền sang Patient khác. Patient, lịch hẹn và bệnh án không bị xóa. Tài khoản B có self-link sau này không tự thay đổi quyền A.

## Hồ sơ được đọc

Server kiểm account Public active/không khóa, ownership, FamilyMember chưa xóa, trạng thái Verified, thời hạn, PatientId, phạm vi và chi nhánh ở **mỗi request**, kể cả tải file. Không dùng cache quyền hoặc thay đổi `PATIENT_ACCOUNT_LINK` tự thân.

- Examinations: lịch sử và nội dung đã xác nhận (`Confirmed`), thuộc Visit của Patient/chi nhánh cấp. DTO riêng chỉ có ngày, bác sĩ, chi nhánh, triệu chứng, chẩn đoán, kết luận và lời dặn. Không trả `Notes`, dữ liệu intake nội bộ, nháp hoặc audit của nhân viên.
- Treatment: dossier có PlanState=Committed, không Deleted; chỉ hạng mục Agreed không xóa/ẩn, tên dịch vụ/vùng/ngày dự kiến/tiến độ. Không trả giá, công nợ, chi phí nhà cung cấp hoặc ghi chú nội bộ.
- Prescriptions: chỉ phiên bản Finalized của đúng Patient/chi nhánh, các dòng thuốc/liều/số lượng/hướng dẫn và lời dặn. Không trả bản Draft/Superseded hoặc thông tin điều chỉnh nội bộ.
- Documents: chỉ file Ready có loại XRay/IntraoralPhoto/LabResult/Referral/ConsentForm và gắn với phiếu khám Confirmed hoặc dossier Committed của Patient/chi nhánh. Không trả file PendingScan/Rejected, tệp Other, metadata đường dẫn lưu trữ, audit/ghi chú nội bộ. Tải qua endpoint có JWT, kiểm SHA256; không tạo URL công khai.
- Mỗi loại danh sách phân trang server (page>=1, pageSize 1–50). Không có quyền cho loại nào thì không hiện tab đó, direct API vẫn 403. Lỗi ownership/missing ID đồng nhất 404. Mọi lần đọc hồ sơ/file có audit; response dùng Cache-Control: no-store.
- Web luôn phân biệt chưa cấp quyền với hồ sơ rỗng; có loading/lỗi/thử lại. Dọn dữ liệu khi đổi người thân, đóng modal, sign-out, 403/404 và khi quyền thu hồi/hết hạn; tải lại trạng thái khi focus và định kỳ lúc modal mở. Object URL tải file chỉ tồn tại tạm và được revoke.

## API

Tất cả owner lấy AccountId từ phiên, PublicAccount policy. Các URL dưới gắn người thân thuộc phiên:

| Route | Nội dung |
|---|---|
| GET `/api/me/family-members/{id}/appointments` | Lịch do A đặt cho người thân, phân trang |
| GET `/api/me/family-members/{id}/record-access` | Yêu cầu mới nhất, trạng thái/scopes/expiry, lịch sử quyết định công khai |
| POST `/api/me/family-members/{id}/record-access/requests` | `{ patientCode?, matchingInformation?, requestKey }` |
| POST `/api/me/family-members/{id}/record-access/requests/{requestId}/withdraw` | `{ expectedVersion, requestKey }`; rút/chấm dứt quyền |
| GET `/api/me/family-members/{id}/record` | Header Patient và phạm vi đang có hiệu lực |
| GET `/api/me/family-members/{id}/record/{section}` | section=`examinations|treatment|prescriptions|documents`, page/pageSize |
| GET `/api/me/family-members/{id}/record/documents/{documentId}/content` | File đã kiểm quyền; không dùng URL công khai |

Nhân viên: `/api/family-record-reviews` GET danh sách (status/keyword/page/pageSize), GET `/{id}` chi tiết, GET `/{id}/patients?keyword` ứng viên trong chi nhánh được quản lý, GET `/{id}/audit`; POST `/{id}/verify|reject|revoke`. Verify nhận `{patientId, branchId, scopes, expiresAt, sharingBasis, verificationNote, consentConfirmed:true, expectedVersion, requestKey}`. Reject/Revoke nhận `{decisionReason, publicNote?, expectedVersion, requestKey}`. Queue hiển thị tab Người thân ở trang `/link-verification`; route hiện có/self-link tiếp tục hoạt động độc lập.

## Database và kiểm chứng

Bổ sung bảng yêu cầu/quyền đại diện `FAMILY_RECORD_ACCESS`, audit append-only `FAMILY_RECORD_AUDIT`, receipt `FAMILY_RECORD_RECEIPT`, cùng FK nullable Appointment.FamilyMemberId. FK Restrict, unique một request Pending/Verified mỗi FamilyMember, token Version; không backfill quyền, không seed cấp quyền vào database demo. Chuyển trạng thái hết hạn trước khi xin lại để giữ unique.

Kiểm thử có schema PostgreSQL cách ly: sở hữu/IDOR, staff role/grant/branch, Pending không đọc, đúng scope/Patient/chi nhánh, không lộ nháp/internal, hết hạn/thu hồi với token cũ, rút/xóa/sửa tên, idempotent/concurrent/stale, self-link không ảnh hưởng, booking snapshot/owner/replay/không tự ghép Patient; UI thật đi từ gửi yêu cầu đến duyệt và xem hồ sơ, test file/private download và responsive. Tách kết quả API, UI mock, UI+API thật; không tự cấp quyền hay sửa bệnh án trên database demo để thử.

Tài liệu liên quan: [Người thân và đặt lịch hộ](CUSTOMER-FAMILY-MEMBERS.md), [Self-link hiện hành](../implementation/UC36-S07-S08.md), [Kê đơn](PRESCRIPTION-MANAGEMENT.md).