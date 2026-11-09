# Xác nhận bệnh nhân đồng ý đặt lịch và lịch hẹn trong lịch bác sĩ — 07/11/2026

## Tích hợp main ngày 08/11/2026

- Khi khôi phục thay đổi consent sau khi merge `origin/main` tại `3566056`, snapshot EF Core phải giữ cả metadata consent của `APPOINTMENT` và các trường/trạng thái, nhật ký labo cùng schema đơn thuốc/chi nhánh từ main. Không thay đổi hợp đồng API hoặc nội dung các migration hiện có trong lượt đồng bộ này.

## Hành vi

- Drawer **Đặt lịch theo kế hoạch điều trị** thêm checkbox **Đã có sự đồng ý của bệnh nhân**. Mỗi lần mở hoặc chuyển hạng mục, checkbox được bỏ chọn; chỉ cho xác nhận khi đã tích cùng các điều kiện đặt lịch hiện có. Lỗi gửi yêu cầu giữ lựa chọn và request key để thử lại.
- API `POST /api/appointments` nhận thêm `patientConsentConfirmed` mặc định false. Với chỉ định có nguồn dossier/item, server bắt buộc true và ghi thời điểm UTC cùng account thực hiện. Thiếu đồng ý trả `400 APPOINTMENT_PATIENT_CONSENT_REQUIRED`, field `patientConsentConfirmed`, trước khi ghi lịch, giữ chỗ, receipt hoặc outbox. Lịch khám thường và chỉ định không có nguồn hạng mục giữ hành vi hiện có.
- Idempotency phân biệt giá trị đồng ý nhưng giữ fingerprint cũ khi trường mới thiếu/false. Replay không thay thời điểm ghi nhận; thay giá trị đồng ý trên cùng request key trả `409 MSG111`. Đổi lịch giữ thông tin ghi nhận lúc tạo.
- Trang **Lịch làm việc** thêm thẻ **Lịch hẹn của bác sĩ trong tuần**: chọn tuần, tải lại, tự đọc lại khi quay về cửa sổ; hiển thị thời gian UTC+7, mã lịch, bệnh nhân, dịch vụ, chi nhánh, trạng thái và thông tin đồng ý. Lịch cũ không có metadata ghi **Chưa ghi nhận**, không tự xem là đã đồng ý.
- Endpoint `GET /api/working-schedules/appointments?dentistId&from&to` giới hạn 31 ngày và dùng lại quyền xem nhân viên/chi nhánh của schedule options. Không trả liên hệ, ghi chú lâm sàng hoặc account ghi nhận. Link chi tiết chỉ hiện khi có `appointments.read`; tuần của thẻ độc lập với bản nháp lịch làm việc.
- Xác nhận này ghi nhận đồng ý đặt lịch; không thay đồng thuận điều trị, chữ ký hay xác nhận đến khám. Giữ bản sửa kiểm trạng thái hạng mục trước đó; không bổ sung ràng buộc lựa chọn dịch vụ/hạng mục khác.

## Tệp chính

- Đặc tả: `docs/specs/TREATMENT-NEXT-VISIT-BOOKING.md`, `docs/specs/S09-UC03.md`.
- Backend: appointment contracts/entity/model/service; scheduling contracts/endpoints; `WorkingScheduleAppointments.cs`.
- Web: `NextVisitBookingDrawer.tsx`, `WorkingSchedulesPage.tsx`, `DoctorAppointmentsPanel.tsx`.
- Kiểm thử: `TreatmentPlanBookingApiTests.cs`, `patient-appointments.spec.ts`, `working-schedules.spec.ts`, `doctor-appointments.spec.ts`.
- Migration: `20261107101731_TreatmentPlanBookingConsent`, designer và `DcmsDbContextModelSnapshot.cs`. Chỉ thêm hai cột nullable trên `APPOINTMENT`, index và FK account ghi nhận; dữ liệu lịch cũ không bị điền đồng ý.

## Kết quả kiểm chứng

- .NET solution build: thành công, 0 lỗi; còn hai cảnh báo NuGet đã có của `System.Security.Cryptography.Pkcs` 9.0.0.
- Web production build: thành công; còn cảnh báo annotation SignalR và kích thước bundle.
- Unit: **87/87**.
- API/database PostgreSQL cách ly: **88/88**, gồm kiểm trạng thái kế hoạch, đồng ý bắt buộc, không ghi dở khi từ chối, actor/time, lịch khám thường, phân quyền nhân viên/chi nhánh, dữ liệu cũ và trạng thái hủy. Fixture chạy migration thật trên schema thử nghiệm riêng.
- Bổ sung assertion đổi giờ hiện đúng trên lịch bác sĩ, giữ metadata và đổi giá trị đồng ý trên request key cũ: chạy lại **2/2** trường hợp đạt.
- Playwright với API giả lập: **26 đạt, 1 bỏ qua** (bài dùng tài khoản môi trường thật). Kiểm tra checkbox bắt buộc/reset/retry, lịch tuần và múi giờ, quyền link, tải lại/lỗi, các luồng recurring hiện có. Đã xem ảnh thẻ lịch ở viewport 390px; bảng cuộn ngang trong thẻ.
- Bằng chứng tại `.artifacts/consent-schedule-20261107/`: các tệp TRX, `api.log`, `unit.log`, `replay-reschedule.log`, `browser.log`, `doctor-appointments-mobile.png`.

## Trạng thái môi trường

- **Chưa áp dụng migration lên database phát triển chính.** Danh sách migration hiện có hai migration trước đó còn pending (`20261106153353_TreatmentPayOsOrders`, `20261106160156_PatientPrescriptions`) cùng migration consent mới. Cần triển khai chuỗi migration trước khi chạy bản API này trên database đó.
- API cổng 5080 không chạy khi kiểm tra health; không tuyên bố đã kiểm chứng giao diện với API thật hoặc đã triển khai ứng dụng. Dữ liệu dùng trong kiểm thử là dữ liệu synthetic trong schema cách ly.
- Không chạy thao tác Git/GitHub, commit hoặc push cho các thay đổi này. Không thay `.env` hay lưu thông tin xác thực trong mã nguồn/tài liệu.
