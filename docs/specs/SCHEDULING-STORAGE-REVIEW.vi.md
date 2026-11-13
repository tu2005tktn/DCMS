# Rà soát lưu trữ chi nhánh, lịch làm việc và lịch hẹn

Ngày 12/11/2026. Migration `20261112132059_ConsolidateSchedulingStorage` đã áp dụng vào DB phát triển. `public` giảm từ 82 xuống 72 bảng, model EF giảm từ 80 xuống 70 bảng. Kiểm thử hồi quy đã hoàn tất.

Bước gộp khám sau đó đưa tổng DB về 68 bảng vật lý và 67 bảng model EF. Xem [lưu trữ khám trong hai bảng](EXAMINATION-TWO-TABLES.vi.md) và [danh sách bảng hiện tại](../architecture/DATABASE-TABLE-INVENTORY.vi.md).

Luồng cần phục vụ: cấu hình chi nhánh và nhân sự → công bố lịch làm việc → tính giờ trống → đặt lịch theo dịch vụ điều trị hiện có → xử lý đổi/hủy và nhắc lịch.

## Vì sao hiện có nhiều bảng

Tám bảng thuộc nhóm chi nhánh/lịch làm việc chứa cả danh mục, dữ liệu vận hành và cấu trúc lịch sử:

| Bảng hiện có | Vai trò | Hướng rút gọn |
| --- | --- | --- |
| `SCHEDULING_BRANCH` | Thông tin chi nhánh, cấu hình đặt lịch | Bản ghi chính của chi nhánh |
| `SCHEDULING_MEMBER` | Nhân viên thuộc chi nhánh, khả năng làm nha sĩ và quyền quản lý lịch | Danh sách liên kết nhân sự của chi nhánh; tham chiếu ID nhân viên, không chép thông tin cá nhân |
| `SCHEDULING_CHAIR` | Ghế đang hoạt động và sức chứa đặt lịch | Danh sách ghế thuộc chi nhánh, giữ ID ghế |
| `SCHEDULING_SERVICE` | Danh mục dịch vụ điều trị hiện có, gồm thời lượng, giá tham chiếu và cấu hình đặt lịch | Dùng chung danh mục điều trị cho đặt lịch |
| `WORKING_WEEK` | Mô hình công bố lịch theo tuần cũ | Gộp với kho lịch làm việc thống nhất, giữ loại lịch tuần có ngày hiệu lực |
| `WORKING_SCHEDULE_REVISION` | Lịch lặp, ngoại lệ theo ngày và các lần công bố | Cùng kho lịch làm việc, giữ loại lịch và quy tắc ngày hiệu lực |
| `RESOURCE_RESERVATION` | Giữ chỗ nhân viên, ghế và người khám theo khoảng thời gian | Gộp với lịch hẹn; giữ bản ghi giữ chỗ độc lập nếu đang có |
| `BRANCH_ASSET` | Nội dung/metadata logo chi nhánh | Thuộc bản ghi chi nhánh, giữ API đọc ảnh và nội dung |

`AppointmentService` hiện tạo đồng thời `Appointment` và `ResourceReservation` với cùng ID, mã, chi nhánh, bác sĩ, trợ lý, ghế, dịch vụ, giờ bắt đầu/kết thúc và trạng thái. Hai bản ghi có dữ liệu trùng và phải được cập nhật cùng nhau khi đổi/hủy lịch.

Lịch tuần và lịch lặp được giữ song song do quá trình thay mô hình: lịch tuần chỉ được dùng khi không có lịch lặp/ngoại lệ phù hợp. Chúng có thể dùng một kho với trường phân loại; không được bỏ lịch tuần hoặc biến lịch một tuần thành lịch lặp vô thời hạn.

Nhóm lịch hẹn còn có lịch sử đổi/hủy, yêu cầu hỗ trợ, lịch sử xử lý yêu cầu, sự kiện gửi thông báo và nhắc lịch. Chúng phục vụ quy trình sau khi đặt lịch, không phải các bước riêng để tính giờ trống.

## Phương án ba bảng cho chi nhánh, lịch làm việc và lịch hẹn

Ba bảng vật lý đã áp dụng là `SCHEDULING_BRANCH`, `WORKING_SCHEDULE` và `APPOINTMENT`.

| Bảng logic | Nội dung |
| --- | --- |
| Chi nhánh | Thông tin, cấu hình đặt lịch, liên kết nhân sự, ghế và logo |
| Lịch làm việc | ID nhân viên, chi nhánh trong từng khoảng lịch, loại lịch tuần/lặp/ngoại lệ, ngày hiệu lực, phiên bản và các khoảng làm/nghỉ |
| Lịch hẹn | Người đặt/người khám, bác sĩ, trợ lý, ghế, ID dịch vụ điều trị, thời gian và trạng thái; cùng bản ghi dùng để kiểm tra chiếm chỗ |

Ba bảng trên dùng chung danh mục dịch vụ điều trị hiện có. Trong code hiện tại, danh mục này được lưu bằng entity `SchedulingService` tại bảng `SCHEDULING_SERVICE`; seeder điều trị và các dòng kế hoạch điều trị cũng dùng các ID dịch vụ này. Tên bảng là tên kỹ thuật hiện có, không biểu thị một danh mục đặt lịch độc lập.

`ServiceId` của lịch hẹn tham chiếu trực tiếp dịch vụ điều trị. Thời lượng, giá tham chiếu, phạm vi chi nhánh, trạng thái hoạt động và điều kiện cho phép khách tự đặt (`PublicBookable`) lấy từ cùng danh mục. Các kế hoạch điều trị tiếp tục tham chiếu cùng ID dịch vụ. Xem [danh mục dịch vụ điều trị](TREATMENT-CATALOG-SEED.md).

Thông tin cá nhân nhân viên tiếp tục lấy từ `USER_ACCOUNT`. Lịch làm việc có ngày hiệu lực và có thể qua nhiều chi nhánh; thay đổi danh sách nhân viên không được làm mất lịch đã công bố hoặc lịch hẹn đã đặt.

Lịch sử đổi/hủy và lịch sử xử lý hỗ trợ có thể dùng `AUDIT_LOG` hiện có. Yêu cầu hỗ trợ và kế hoạch nhắc lịch có thể thành collection của lịch hẹn, được đọc/ghi qua bản ghi cha. Các trường phiên bản của từng quy trình phải giữ ý nghĩa hiện có.

Thông báo và công việc gửi nền vẫn cần dữ liệu lưu để gửi lại khi thất bại. Hướng gộp là dùng hạ tầng thông báo chung, gồm trạng thái sự kiện chờ xử lý, thay cho outbox riêng của lịch hẹn. Ba bảng trên là các bảng nghiệp vụ chính của phần chi nhánh/lịch làm việc/lịch hẹn. Danh mục dịch vụ điều trị, kho thông báo, audit và chống gửi trùng dùng chung vẫn được tính trong tổng số bảng toàn hệ thống.

## Quy tắc khi chuyển dữ liệu và code

Giờ trống được tính từ thời gian nhân viên đang làm việc tại chi nhánh, trừ nghỉ/chặn và lịch hẹn còn chiếm chỗ; phải có ghế trống và đủ thời lượng dịch vụ. Trợ lý được chỉ định cũng phải rảnh. Nhân viên và người khám không được trùng giờ giữa các chi nhánh.

Xem giờ trống không giữ chỗ. Khi ghi lịch, phải kiểm tra lại trong cùng giao dịch giữ chỗ, để hai người đặt cùng lúc không cùng chiếm một bác sĩ hoặc ghế. Chuyển `RESOURCE_RESERVATION` vào lịch hẹn phải đổi cả nguồn truy vấn giờ trống và các đường ghi đặt lịch, đổi/hủy, tiếp nhận và điều trị.

Migration phải giữ ID nhân viên, ghế, dịch vụ, lịch hẹn và giữ chỗ; giữ lịch sử và yêu cầu hỗ trợ đang xử lý. Trước khi bỏ bảng nguồn, đối chiếu dữ liệu trên bản sao và kiểm tra đặt đồng thời, đổi/hủy, nghỉ đột xuất và nhân viên làm nhiều chi nhánh.

## Hợp đồng lưu trữ triển khai

- `SCHEDULING_BRANCH` lưu nhân sự, ghế và metadata logo trong các collection JSONB. ID nhân viên tiếp tục tham chiếu tài khoản; ID ghế giữ nguyên. Cấp ID ghế qua sequence chung. Các thao tác ghi thay đổi collection đều nạp chi nhánh có tracking và kiểm soát ghi đồng thời trên dòng chi nhánh. Route và DTO của chi nhánh/ghế/logo giữ nguyên.
- Ghế cũ chưa có mã hồ sơ có thể mang giá trị lưu trữ `Version = 0`; API tiếp tục trả phiên bản hiệu lực 1 như trước. Lần sửa đầu tiên hoặc đổi trạng thái dùng phiên bản hiệu lực này và lưu phiên bản 2. Migration giữ nguyên giá trị lịch sử để rollback đối chiếu khớp.
- `WORKING_SCHEDULE` lưu cả lịch tuần và các lần công bố lịch lặp/ngoại lệ, phân biệt bằng loại bản ghi. Không biến lịch tuần cũ thành lịch lặp. ID, phiên bản, ngày hiệu lực, người công bố và các khoảng giờ giữ nguyên.
- `APPOINTMENT` dùng chung một dòng vật lý cho thông tin lịch hẹn và giữ chỗ. Giữ cả ID lịch hẹn và ID giữ chỗ lịch sử khi chúng khác nhau; bản ghi chỉ giữ chỗ vẫn được giữ lại. Chi nhánh, bác sĩ, trợ lý, ghế, dịch vụ và khoảng thời gian dùng chung cột. Trạng thái và mã giữ chỗ lịch sử được bảo toàn khi chúng khác trạng thái/mã lịch hẹn. Các khóa ngoại lịch hẹn tiếp tục trỏ đúng ID lịch hẹn.
- Yêu cầu hỗ trợ và nhắc lịch là collection JSONB của lịch hẹn; cập nhật qua đối tượng cha có tracking. Mỗi yêu cầu và nhắc lịch giữ ID và phiên bản nghiệp vụ hiện có. Một concurrency stamp lưu trữ riêng bảo vệ toàn dòng mà không làm tăng phiên bản lịch hẹn chỉ vì gửi nhắc lịch.
- Từ 10/11/2026 (S26, nhắc cho nhiều người nhận) mỗi lịch nhắc thuộc một tài khoản nhận. Mỗi lịch hẹn có tối đa một lịch nhắc cho mỗi (phiên bản lịch hẹn, số phút nhắc trước, tài khoản); vì nằm trong JSONB, quy tắc này được kiểm khi lập lịch nhắc thay cho index duy nhất của bảng `APPOINTMENT_REMINDER` cũ.
- Tạo yêu cầu hỗ trợ lấy khóa giao dịch dùng chung, kiểm tra lại receipt sau khi lấy khóa và giữ tính duy nhất của mã/request key trước khi thêm vào JSON. Yêu cầu, lịch sử, sự kiện thông báo và receipt được commit cùng giao dịch.
- `APPOINTMENT_CHANGE` và `APPOINTMENT_SUPPORT_HISTORY` chuyển sang `AUDIT_LOG` dùng chung, giữ loại, ID và thứ tự thời gian. Outbox lịch hẹn chuyển vào kho thông báo dùng chung, vẫn được ghi cùng giao dịch đặt/đổi/hủy lịch và chỉ xử lý sau commit.
- Không đổi danh mục `SCHEDULING_SERVICE`: đây là danh mục dịch vụ điều trị đang dùng chung. Giữ khóa giao dịch `90403`, kiểm tra quyền server, tính giờ trống từ dữ liệu đã công bố, chống gửi trùng, phiên bản và kiểm tra lại trước commit.

Migration phải có Up/Down bảo toàn dữ liệu, chạy thử trên bản sao trước khi áp dụng DB phát triển. Mục tiêu giảm 10 bảng vật lý, từ 82 xuống 72 bảng (`public`), còn 70 bảng trong model EF.

## Kết quả chuyển dữ liệu

Bản sao lưu schema `public` trước chuyển đổi nằm tại `.tools/scheduling-collapse/before-scheduling-collapse.dump`, được Git ignore (737.230 byte; SHA-256 `7c1c130b3ff996e181d1e4a149e4a61609b4e381b498033772559f7399cd78b9`).

Đã chạy Up/Down trên bản sao DB và đối chiếu 1.689 bản ghi của 15 bảng nguồn: số lượng và digest khớp hoàn toàn. Sau khi áp dụng vào DB phát triển, đối chiếu lại trực tiếp từ cấu trúc mới cũng khớp cả 15 nguồn. Giữ 83 lịch hẹn và 83 bản ghi giữ chỗ trong cùng 83 dòng, giữ nguyên các mã và trạng thái khác nhau có sẵn giữa lịch hẹn/giữ chỗ. Không thay danh mục dịch vụ điều trị.

Các kiểm thử bổ sung bao phủ ID lịch hẹn khác ID giữ chỗ, giữ chỗ độc lập không xuất hiện như lịch hẹn, bảo toàn dữ liệu nhánh/lịch/hỗ trợ/nhắc lịch qua rollback, chống ghi đè collection chi nhánh, lệnh sửa/ngừng ghế có phiên bản và replay, cùng gửi lại yêu cầu hỗ trợ đồng thời.

Dữ liệu kiểm thử chuyển lịch hẹn sang chi nhánh khác phải đồng thời chọn ghế và dịch vụ thuộc chi nhánh đích. Luồng khám tiếp theo của cùng bệnh nhân tiếp tục kiểm tra khả năng đọc kết quả khám trước và mở hồ sơ điều trị có sẵn.

## Kết quả kiểm tra triển khai

Backend restore/build bằng .NET 10 và web install/build thành công. Model EF khớp migration hiện tại. Build backend còn các cảnh báo dependency có sẵn của `System.Security.Cryptography.Pkcs` 10.0.0.

Lượt hồi quy toàn bộ đạt 102 unit test và 560 integration test, có một integration test lỗi ở dữ liệu chuẩn bị chuyển chi nhánh nêu trên và hai NFR tùy chọn skip theo cấu hình. Sau khi sửa dữ liệu chuẩn bị, chạy lại toàn bộ lớp `ExaminationApiTests` đạt cả 73 test. Tổng hợp theo từng test case đạt 102 unit test và 561 integration test.

Test `ClinicalWorkApiTests.ReviewSaveDoesNotPostActivityAndActivityRetryUsesOneId` được loại khỏi lượt hồi quy. Lỗi 503 của test này đã tái hiện trên checkout HEAD `1febcd7` trước thay đổi lưu trữ. Fixture giả lập Visit trong cổng clinical, còn cổng ghi phí kiểm tra Visit trong PostgreSQL.

API trên DB phát triển đã kiểm tra thành công đăng nhập, đọc chi nhánh, đọc ghế với phiên bản hiệu lực hợp lệ và đọc tùy chọn giờ trống. `GET /health` trả cả API và database healthy. Schema `public` có 72 bảng. Các database bản sao dùng cho đợt kiểm chứng này đã được thu hồi.

## Nguồn code đã rà

- `backend/src/DCMS.Domain/Features/Scheduling/SchedulingModels.cs`
- `backend/src/DCMS.Domain/Features/Appointments/Appointment.cs`
- `backend/src/DCMS.Infrastructure/Persistence/SchedulingModel.cs`
- `backend/src/DCMS.Infrastructure/Persistence/AppointmentModel.cs`
- `backend/src/DCMS.Infrastructure/Features/Scheduling/RecurringSchedules.cs`
- `backend/src/DCMS.Infrastructure/Features/Scheduling/WorkingScheduleService.cs`
- `backend/src/DCMS.Infrastructure/Features/Appointments/AppointmentService.cs`
- `backend/src/DCMS.Infrastructure/Features/Treatment/DevelopmentTreatmentCatalogSeeder.cs`
- `backend/src/DCMS.Infrastructure/Features/Notifications/AppointmentNotificationDispatcher.cs`
- `docs/specs/S09-UC03.md`
- `docs/specs/TREATMENT-CATALOG-SEED.md`
