# Thông báo trong một bảng

Ngày 13/11/2026. Yêu cầu của chủ dự án: nhóm thông báo đang nhiều bảng. Nhóm bệnh nhân và gia đình giữ nguyên bốn bảng theo quyết định cùng ngày.

`IN_APP_NOTIFICATION` vốn đã lưu hai loại dòng, phân biệt bằng `NotificationRecordKind`: thông báo trong ứng dụng (`InAppNotification`) và sự kiện chờ gửi của lịch hẹn (`AppointmentOutboxEvent`). Bảng nhận thêm hai loại dòng:

| Loại dòng | Trước đây | Dữ liệu |
| --- | --- | --- |
| `NotificationDelivery` | `NOTIFICATION_DELIVERY` | Một lần gửi của một thông báo qua một kênh: kênh, trạng thái, số lần thử, lần thử gần nhất, lần thử lại tiếp theo, lỗi |
| `StaffHandlingItem` | `STAFF_HANDLING_ITEM` | Việc nhân viên cần xử lý khi gửi ra ngoài thất bại: loại, chi nhánh, lịch hẹn, thông báo, số lần thử, lỗi, trạng thái, thời điểm tạo |

Mỗi lượt gửi và mỗi việc cần xử lý vẫn là một dòng riêng với ID cũ, không gộp vào JSONB của thông báo, để worker gửi lại và thao tác đánh dấu đã đọc cập nhật các dòng khác nhau và không chặn nhau.

Các cột cùng nghĩa dùng chung: `Kind`, `BranchId`, `AppointmentId`, `CreatedAt` của thông báo và việc xử lý; `NotificationId`, `Status`, `AttemptCount` của lượt gửi và việc xử lý. Ràng buộc giữ nguyên:

- Mỗi thông báo có tối đa một lượt gửi cho mỗi kênh: index duy nhất (`NotificationId`, `Channel`) chỉ áp dụng cho dòng `NotificationDelivery`.
- `NotificationId` của lượt gửi và việc xử lý là khóa ngoại tới dòng thông báo cùng bảng; `AppointmentId` là khóa ngoại tới `APPOINTMENT`. Khóa ngoại `NotificationId` dùng `RESTRICT` cho cả hai loại dòng (trước đây lượt gửi dùng `CASCADE`); hệ thống không có thao tác xóa thông báo.
- Index tìm lượt gửi đến hạn thử lại (`Status`, `NextRetryAt`) và việc cần xử lý theo trạng thái (`Status`, `CreatedAt`) được giữ, lọc theo loại dòng.

API thông báo, số chưa đọc, đánh dấu đã đọc, realtime, worker gửi lại và việc tạo việc xử lý khi gửi thất bại giữ hành vi hiện hành.

## Chuyển dữ liệu

Một migration nối sau `ConsolidateSettingsStorage`, trong cùng giao dịch PostgreSQL: thêm cột, chép từng dòng của hai bảng nguồn vào `IN_APP_NOTIFICATION` với ID và giá trị cũ, kiểm số dòng và ID không trùng với dòng đã có, rồi bỏ hai bảng. Down dựng lại hai bảng với ràng buộc và index cũ, chép dữ liệu về và xóa các dòng, cột, index đã thêm.

Mục tiêu: model EF từ 33 xuống 31 bảng, `public` từ 34 xuống 32 bảng.

## Kiểm chứng

- Up, Down và Up lại trên bản sao DB phát triển; đối chiếu số lượng và digest từng nguồn và cấu trúc bảng sau rollback.
- Kiểm thử lưu trữ: lượt gửi và việc xử lý giữ qua rollback; kênh trùng của cùng thông báo bị từ chối.
- Backend restore/build bằng .NET 10, toàn bộ integration, model khớp migration, API health, đọc thông báo và số chưa đọc trên DB phát triển, log worker không lỗi.

## Kết quả triển khai ngày 13/11/2026

Migration `20261113084455_ConsolidateNotificationStorage` đã áp dụng vào DB phát triển. Model EF còn 31 bảng; `public` còn 32 bảng gồm `__EFMigrationsHistory`, khớp [danh sách bảng](../architecture/DATABASE-TABLE-INVENTORY.vi.md). Bản sao lưu `public` trước khi áp dụng nằm tại `.tools/notification-collapse/before-notification-collapse-20261113.dump`, được Git ignore, SHA-256 `1b09b4842bc3cf5ff347615240c847304a4f5d6ea11e4cbd4c99549072ed5662`.

Trên bản sao phục hồi từ DB phát triển, migration EF thật chạy Up, Down về `ConsolidateSettingsStorage` rồi Up lại. Cả 33 nguồn với 2.708 dòng khớp số lượng và digest ở cả ba bước, gồm 142 lượt gửi đọc lại từ `IN_APP_NOTIFICATION`; bảng việc xử lý đang trống. Sau Down, cột, ràng buộc và index của `IN_APP_NOTIFICATION` và hai bảng được dựng lại giống hệt bản gốc, không còn dòng đã chuyển. Sau khi áp dụng vào DB phát triển, đối chiếu trực tiếp cả 33 nguồn đều khớp.

Backend restore/build bằng .NET 10 thành công; model EF khớp migration. Có 100 unit test đạt. Lượt integration toàn bộ chạy tuần tự đạt 511/513, 2 NFR tùy chọn skip, không lỗi; gồm hai test lưu trữ mới kiểm lượt gửi và việc xử lý giữ qua rollback, và lượt gửi trùng kênh của cùng thông báo bị từ chối. Web không thay đổi.

API trên DB phát triển: `GET /health` trả API và database healthy. Đăng nhập trợ lý demo, số thông báo chưa đọc khớp DB; danh sách thông báo trả mỗi thông báo kèm đúng lượt gửi của nó. Phiên kiểm chứng đã đăng xuất; log API và worker gửi thông báo không có lỗi.
