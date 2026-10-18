# UC-06 — triển khai API đổi/hủy lịch

Hợp đồng: [UC06-APPOINTMENT-CHANGES.md](../specs/UC06-APPOINTMENT-CHANGES.md).

Mục tiêu: hai POST command dùng Appointment/ResourceReservation hiện hữu, history và receipt riêng, outbox hiện có. Migration bổ sung APPOINTMENT_CHANGE, APPOINTMENT_CHANGE_RECEIPT và metadata hủy. Không xóa dữ liệu cũ. Read API S16 đọc lịch sử thật.

## Kết quả ngày 18/10/2026

- Endpoint đã đăng ký: `POST /api/appointments/{code}/reschedule`, `POST /api/appointments/{code}/cancel`, `GET /api/appointments/{code}/availability?date=yyyy-MM-dd`.
- Migration `20261018074836_AppointmentChanges`: metadata hủy, hai bảng history/receipt và FK/index; đã áp dụng database local. Snapshot giữ cả model lịch làm việc định kỳ có sẵn trong working tree.
- `AppointmentChangeService` kiểm quyền mỗi lần, kể cả replay; dùng transaction/lock S09 cho cả hai mutation. Read API S16 trả lịch sử thật và assistantId. HTTP adapter S16 tra slot có xác thực theo mã lịch, giữ thời lượng gốc kể cả khi catalog thay đổi; xử lý 401 bằng shared session.
- `dotnet restore backend/DCMS.sln` và `build --no-restore`: đạt; package Pkcs 9.0.0 có warning NU1902/NU1903 hiện hữu.
- 17 ca mới trong `AppointmentChangeApiTests`; lượt kiểm thử nhóm Appointment/Scheduling cuối cùng **48/48 đạt** trên PostgreSQL, schema riêng.
- Lượt full solution trước khi thêm 2 ca cuối: unit **35/35**, integration **140/142**. Hai test chưa đạt: `GoogleApiTests.ExistingLocalEmailCreatesSeparateGoogleAccount` (kỳ vọng tạo tài khoản Google cùng email nhưng user index đang unique) và `RegistrationApiTests.RepeatedAndConcurrentRequestsCreateOneAccount` (request cạnh tranh đổi phone nhưng giữ email request đã tạo). Không thay đổi code authentication/registration trong task này.
- `web`: npm install + build đạt. Lockfile phát sinh do npm được hoàn nguyên về HEAD vì trước task file này không có thay đổi local. Mobile `npm run typecheck`: đạt.
- Lượt HTTP thật trên server local: tạo account/lịch synthetic, đặt Confirmed → đổi MSG35 → hủy MSG37 → replay hủy; đọc lại `Cancelled`, version 3, history 2. SQL xác nhận Appointment/Reservation đều Cancelled, history 2, receipts 2, outbox 3 (1 booking + 2 thay đổi); không có event/lịch sử thừa khi retry. Không dùng lịch của bệnh nhân thật.
- Server sau bản build cuối: OpenAPI có đủ hai POST và GET availability theo code; `/health` trả API/database healthy qua 127.0.0.1 và LAN 192.168.1.15. Metro 8081 chạy lại với HTTP mode; manifest SDK 57 và iOS bundle đều HTTP 200.

## Giới hạn tích hợp

Module follow-up và notification dispatcher chưa có: adapter hủy follow-up mặc định trả 503/MSG144 và rollback để không báo chuyển NeedsBooking sai; đổi lịch giữ liên kết follow-up. Outbox chứa event cho TV5, chưa chứng minh đã hủy reminder/gửi thông báo bên ngoài. API support-request và staff UI S12 chưa có. Source mobile giữ mặc định mock; cấu hình `.env` local (ngoài Git) đã đặt `EXPO_PUBLIC_APPOINTMENTS_MOCK=false` để thử phần HTTP đọc/đổi/hủy. Danh sách S14 trong chế độ này là dữ liệu tài khoản thật, không còn các mã LH-DEMO của mock.
