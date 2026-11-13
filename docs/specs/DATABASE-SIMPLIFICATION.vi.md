# Rút gọn lưu trữ dữ liệu DCMS

Ngày cập nhật 13/11/2026. Yêu cầu của chủ dự án là giảm số bảng và gom các dòng chi tiết vào bản ghi chính, bắt đầu từ đơn thuốc và áp dụng cho các màn hình khác.

## Thiết kế lưu trữ

- `COMMAND_RECEIPT` dùng chung cho kết quả chống gửi trùng của các module. `ReceiptType` phân biệt loại lệnh. Mỗi loại giữ phạm vi khóa duy nhất hiện có, fingerprint, kết quả và các liên kết đang sử dụng. Quyền được kiểm trước khi replay. Việc lưu receipt và thay đổi nghiệp vụ nằm trong cùng giao dịch.
- `AUDIT_LOG` dùng chung cho lịch sử tài khoản, bệnh nhân, liên kết hồ sơ, khám, gia đình, tài chính, đơn thuốc, thiết lập và lượt khám. `AuditType` phân biệt nguồn. Giữ ID lịch sử của từng nguồn, người thực hiện, thời điểm và nội dung trước/sau. API đọc lịch sử tiếp tục lọc đúng nguồn và phạm vi quyền.
- `PRESCRIPTION` lưu đơn trực tiếp từ hồ sơ, các phiên bản đơn theo lượt khám và mẫu đơn cũ bằng `PrescriptionKind`. Danh sách thuốc của từng bản ghi nằm trong JSONB. `CATALOG_MEDICINE` tiếp tục là danh mục dùng chung. Hai bảng kỹ thuật dùng chung lưu audit và receipt của chức năng. Các API cũ đọc/ghi cùng kho lưu trữ này.
- Chi nhánh lưu trong `SCHEDULING_BRANCH`, gồm nhân sự, ghế và metadata logo trong JSONB. Phần thông tin hồ sơ dùng chung khóa và cột với phần đặt lịch. Tên, trạng thái, cấu hình đặt lịch và version có cùng nguồn dữ liệu.
- Lịch tuần và phiên công bố lịch lặp/ngoại lệ lưu trong `WORKING_SCHEDULE`, gồm các khoảng thời gian JSONB. Kiểm tra khoảng thời gian, xung đột lịch và phiên bản thực hiện khi ghi.
- `VISIT` lưu tiếp nhận, phiếu khám và các chỉ định JSONB. `CLINICAL_DOCUMENT` lưu metadata tệp cùng các lần bác sĩ đọc tài liệu trong JSONB. Bảng tham chiếu cũ có snapshot phục hồi trong audit chuyển đổi dùng chung.
- Grant của nhóm quyền nằm trong JSONB của `ACCESS_GROUP`. Các quyền hiệu lực và phạm vi chi nhánh tiếp tục được tính ở server.
- Các dòng lựa chọn của đơn PayOS nằm trong JSONB của `TREATMENT_PAYOS_ORDER`. Giữ nguyên ID hoạt động, số tiền, thứ tự và nội dung dùng đối chiếu webhook.

Các dòng tiền, phân bổ thanh toán, dòng hóa đơn và hoạt động điều trị đang được chứng từ khác tham chiếu tiếp tục có khóa quan hệ riêng. Thiết lập legacy vẫn được bảo toàn theo hợp đồng chuyển dữ liệu S58 hiện có.

## Hợp đồng dữ liệu và API

Route, DTO, quyền, phân trang, lỗi version và quy tắc retry giữ hành vi hiện hành. Mỗi dòng thuốc JSONB giữ ID, ID thuốc danh mục và snapshot tên, hàm lượng, đơn vị, số lượng, cách dùng. Đơn cũ tiếp tục đọc đúng snapshot sau khi sửa danh mục. Thứ tự dòng được lưu rõ ràng.

Các collection JSONB thuộc bản ghi cha được thay đổi qua bản ghi cha. Kiểm tra và cập nhật version của cha áp dụng cho thao tác sửa dòng. Các truy vấn thống kê và kiểm tra tác động phải đi qua bản ghi cha hoặc truy vấn JSONB có thể dịch sang SQL.

## Chuyển dữ liệu

Migration chuyển từng dòng nguồn sang cấu trúc mới trước khi thu hồi bảng nguồn, trong cùng giao dịch PostgreSQL. Giữ nguyên ID nghiệp vụ, thứ tự dòng, snapshot, fingerprint, kết quả replay và nội dung audit. Các kiểu audit dùng ID số giữ giá trị ID cũ và có bộ sinh ID tiếp nối giá trị lớn nhất. Mỗi receipt có khóa lưu trữ mới và giữ khóa duy nhất theo nguồn cũ.

Giữ hai cờ legacy `RequiresExternalPurchase` và `IsExternalPurchase` để hỗ trợ rollback dữ liệu. Lịch sử tiếp nhận giữ cả `Reason` và `DecidingDentistId`, gồm quan hệ đến nhân sự nha sĩ. Migration đọc được các schema lịch sử có hoặc thiếu hai cột lịch sử tiếp nhận này. Bộ kiểm chứng digest tài khoản đọc được cả `ACCOUNT_AUDIT` và `AUDIT_LOG`.

Phát hiện ID đơn hoặc mẫu trùng giữa các nguồn phải làm migration thất bại và rollback. Dữ liệu phụ không có bản ghi cha cũng phải làm migration thất bại. Bản Down chỉ thực hiện khi có thể khôi phục đầy đủ các cấu trúc nguồn.

## Kiểm chứng

- So sánh số bảng từ relational model trước/sau và kiểm tra snapshot khớp model.
- Kiểm tra migration trên schema riêng có dữ liệu tổng hợp thuộc cấu trúc cũ, đối chiếu ID, nội dung, thứ tự và số lượng sau chuyển đổi.
- Kiểm tra đăng nhập, phân quyền, lưu/sửa/replay/version đơn thuốc, lịch làm việc, nhóm quyền, chi nhánh, ghế, PayOS và lịch sử audit qua API.
- Chạy build backend bằng .NET 10, các bộ kiểm thử liên quan và build web.

## Kết quả triển khai ngày 12-13/11/2026

| Hạng mục | Trước | Sau |
| --- | --- | --- |
| Bảng trong relational model EF | 130 | 57 |
| Base table trong schema `public` của DB phát triển | 132 | 58 |
| Tài khoản và phân quyền | 15 | 2 |
| Điều trị (phiếu, kế hoạch, quá trình, mẫu, sự kiện) | 14 | 4, sự kiện nằm trong kho audit chung |
| Kho receipt theo module | 18 | 1 |
| Kho audit theo module | 10 | 1 |
| Kho đơn và mẫu đơn | 3 | 1 |
| Bảng chi tiết thuốc, khoảng lịch, grant và lựa chọn PayOS | 7 | 0, dữ liệu nằm trong JSONB của cha |
| Kho hồ sơ chi nhánh và ghế bổ sung | 2 | 0, dữ liệu nằm trong dòng Scheduling |

Migration đầu tiên `20261112083713_SimplifyScreenStorage` đưa DB về 95 bảng vật lý và 93 bảng model. Các bước tiếp theo gộp tài khoản, chi nhánh/lịch hẹn rồi phần khám. Sau bước gộp điều trị, `public` có 58 bảng, gồm 57 bảng model và `__EFMigrationsHistory`. Bản sao lưu trước bước đầu nằm tại `.tools/database-simplification/backups/before-simplification.dump`, được Git ignore.

Đã phục hồi bản sao lưu vào database kiểm chứng, chạy Up rồi Down trong giao dịch và đối chiếu 1.783 dòng của cả 40 bảng nguồn. Số lượng và digest khớp ở tất cả các bảng. Các cột chứa JSON được chuẩn hóa sang JSONB trước khi tính digest để so sánh nội dung JSON. Các database kiểm chứng đã được thu hồi sau kiểm tra.

Backend restore/build bằng .NET 10 và web install/build thành công. Tổng hợp theo từng test case từ lượt toàn bộ và các lượt chạy lại đạt 102 unit test và 552 integration test. Hai NFR tùy chọn được skip theo cấu hình hiện có. Một test `ClinicalWorkApiTests.ReviewSaveDoesNotPostActivityAndActivityRetryUsesOneId` trả 503; cùng lỗi đã được tái hiện trên checkout HEAD `1febcd7` trước thay đổi. Fixture giả lập Visit trong cổng clinical, còn cổng ghi phí kiểm tra Visit trong PostgreSQL.

`GET http://localhost:5080/health` đã trả cả `api` và `database` là `healthy` sau migration. Script đọc digest tài khoản đã được kiểm tra với kho audit mới.

Thiết kế JSONB và bảng phân loại dùng các cơ chế của [Npgsql](https://www.npgsql.org/efcore/mapping/json.html) và [EF Core](https://learn.microsoft.com/en-us/ef/core/modeling/inheritance).

Migration tiếp theo `20261112095901_ConsolidateAccountStorage` giảm phần tài khoản/phân quyền từ 15 xuống hai bảng, đưa `public` từ 95 xuống 82 bảng. Xem [thiết kế tài khoản](ACCOUNT-TWO-TABLES.vi.md).

Sau bước gộp tài khoản: 102 unit test và 556 integration test đạt khi tổng hợp lượt hồi quy và lượt chạy lại lớp kiểm thử danh mục. Hai NFR tùy chọn skip; test ClinicalWork lỗi cũ nêu trên được loại khỏi lượt hồi quy. Backend/web build thành công, model/migration khớp và API/DB healthy; đăng nhập rồi đăng xuất tài khoản demo trên DB phát triển đã được kiểm tra.

Migration `20261112132059_ConsolidateSchedulingStorage` đã gộp chi nhánh, lịch làm việc và lịch hẹn về ba bảng nghiệp vụ, dùng chung danh mục dịch vụ điều trị. `public` giảm tiếp từ 82 xuống 72 bảng; model EF từ 80 xuống 70. Đã kiểm chứng Up/Down trên bản sao và đối chiếu trực tiếp sau khi áp dụng vào DB phát triển: 1.689 bản ghi của 15 nguồn đều khớp số lượng và digest. Xem [hợp đồng gộp lịch](SCHEDULING-STORAGE-REVIEW.vi.md) và [danh sách bảng hiện tại](../architecture/DATABASE-TABLE-INVENTORY.vi.md).

Sau bước gộp lịch, tổng hợp lượt hồi quy và lượt chạy lại lớp khám đạt 102 unit test và 561 integration test. Hai NFR tùy chọn skip, test ClinicalWork lỗi cũ nêu trên được loại khỏi lượt hồi quy. Backend/web build thành công, model/migration khớp, API và DB healthy. Các API đăng nhập, đọc chi nhánh, ghế và tùy chọn giờ trống đã được kiểm tra trên DB phát triển sau migration.

Migration `20261112143714_ConsolidateExaminationStorage` đã gộp sáu bảng khám về `VISIT` và `CLINICAL_DOCUMENT`. DB phát triển giảm từ 72 xuống 68 bảng, model EF còn 67 bảng. Đã đối chiếu 1.294 bản ghi của 9 nguồn cả trên bản sao Up/Down và trực tiếp sau áp dụng. Tổng hợp kiểm thử liên quan đạt 102 unit test và 201 integration test. Backend/web build thành công, model/migration khớp, API và DB healthy. Xem [thiết kế và bằng chứng hai bảng khám](EXAMINATION-TWO-TABLES.vi.md).

Migration `20261113013443_ConsolidateTreatmentStorage` đã gộp mười bảng điều trị về `TREATMENT_DOSSIER`, `TREATMENT_PLAN_ITEM`, `CLINICAL_ACTIVITY` và `TREATMENT_TEMPLATE`; sự kiện phiếu và sự kiện tiến độ chuyển vào `AUDIT_LOG` với cột `EventActorId` riêng vì tác vụ hệ thống ghi actor `0`. DB phát triển giảm từ 68 xuống 58 bảng, model EF còn 57 bảng. Đã đối chiếu 2.160 dòng của 24 nguồn cả trên bản sao (Up, Up rồi Down, Down rồi Up) và trực tiếp sau áp dụng. Tổng hợp kiểm thử đạt 102 unit test và 568 integration test; hai NFR tùy chọn skip và test ClinicalWork lỗi cũ nêu trên vẫn trả 503. Backend/web build thành công, model/migration khớp, API và DB healthy. Xem [thiết kế và bằng chứng bốn bảng điều trị](TREATMENT-FOUR-TABLES.vi.md).
