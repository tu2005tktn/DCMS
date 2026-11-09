# Khách không tới và đặt lịch thay thế — 09/11/2026

## Sử dụng

1. Mở hồ sơ bệnh nhân → **Lịch hẹn**, hoặc mở chi tiết lịch tại `/appointments/{id}`.
2. Với lịch **Đã xác nhận** đã tới giờ và chưa tiếp nhận, chọn **Khách không tới**. Kiểm tra lịch, nhập lý do và xác nhận.
3. Lịch cũ chuyển thành **Khách không tới**, giữ nguyên người khám, mã lịch và thời gian. Bác sĩ/ghế được giải phóng; chỉ định điều trị chưa hoàn tất trở về NeedsBooking.
4. Nếu lịch còn giờ, chọn **Đặt lịch thay thế**. Nhập/chọn khách mới, điều chỉnh số phút nếu cần, chọn khung giờ hợp lệ rồi **Xem lại → Xác nhận đặt lịch**.
5. Lịch mới có mã riêng, loại Vãng lai, nguồn Trực tiếp, cùng chi nhánh/bác sĩ/ngày. Không sao chép người đặt, liên hệ, Patient hoặc chỉ định của khách cũ. Sau khi đặt, lịch cũ vẫn là Khách không tới.

Không đặt lùi giờ. Lịch mới chỉ dùng khoảng từ thời điểm bắt đầu còn hợp lệ tới hết khung nguồn; engine kiểm toàn bộ thời lượng và tài nguyên. Hết giờ/không đủ slot thì đặt lịch ở lịch chung. Dưới 60 phút vẫn có lý do ngoại lệ theo quy tắc hiện hành.

## Contract và quyền

Đặc tả cập nhật trước code tại `docs/specs/UC06-APPOINTMENT-CHANGES.md` và `PATIENT-APPOINTMENTS-TAB.md`.

- `appointments.no-show`: command hiện có kiểm grant động, chi nhánh, phạm vi bác sĩ, Confirmed, giờ server, expectedVersion, chưa có Visit, reservation nhất quán. Receipt/history/outbox giao dịch cùng khóa 90403.
- `appointments.create`: đặt thay dùng trường tùy chọn `replacesAppointmentId`. Service kiểm lại grant và quyền đọc nguồn trước replay; nguồn phải NoShow, chưa có Visit, còn giờ, đúng chi nhánh/bác sĩ, WalkIn/InPerson, không follow-up/assistant. Lịch thông thường giữ hợp đồng cũ.
- Fingerprint cũ không đổi khi thiếu trường mới. Retry cùng nội dung trả đúng lịch đã lưu; đổi nội dung sau lỗi mạng dùng key mới. Không thêm migration.
- Event của lịch mới lưu reference nguồn; không đưa thông tin khách mới vào history của khách cũ. Dispatcher phát loại NoShow, ngừng reminder lịch cũ, không phát nhầm lời xác nhận đặt lịch.

## Bằng chứng

- .NET restore/build: thành công, 0 lỗi. Còn cảnh báo NuGet Pkcs và hai tham số chưa dùng vốn có.
- Web npm install/build: thành công; còn cảnh báo kích thước bundle vốn có.
- 70 kiểm thử HTTP/PostgreSQL qua AppointmentChangeApiTests, AppointmentApiTests, TreatmentPlanBookingApiTests, SupportNotificationApiTests. Bao gồm staff/own dentist/owner, grant bị thu hồi ngay với cùng JWT, direct service/replay, từ chối khách public/bác sĩ khác/lịch tương lai/đã tiếp nhận/có Visit, nguồn hết giờ/sai bác sĩ/vượt khoảng, xung đột tài nguyên, giữ lịch sử và notification NoShow.
- 26 kiểm thử UI mock qua no-show-replacement, staff-appointment-actions, patient-appointments. Bao gồm xác nhận, không tự lưu khi đóng, quyền riêng, lịch hết giờ, không sao chép khách cũ, khóa ngữ cảnh, chỉnh thời lượng, lọc slot và retry mạng cùng key.
- 1 kiểm thử trình duyệt thật qua no-show-replacement.integration.spec.ts: PostgreSQL schema riêng, API 5087/web 5187, tạo hồ sơ tổng hợp, cho lịch thử tới giờ bằng SQL chỉ trong schema đó, thực hiện toàn bộ luồng bằng UI, đọc lại hai lịch bằng API, không pageerror. Kiểm kích thước drawer 1440/390px; ảnh đã xem tại `.artifacts/runtime-20261109/no-show-replacement-{1440,390}.png`.
- Test notification dùng fixture không chạy background polling để tránh race với lần gọi dispatcher chủ động trong test.
- Lần chạy bổ sung booking-prefill có 2 case tự thân qua; case Relative cũ thiếu mock danh sách người thân/chọn người thân theo contract mới nên không thể tiếp tục. Không liên quan tới form đặt tại quầy/đặt thay; không đổi hành vi sản phẩm theo fixture cũ.

Log và TRX: `.artifacts/runtime-20261109/no-show-{restore,build,web-build,api,browser,live}.log`, `no-show-api.trx`. Không dùng dữ liệu bệnh nhân thực. Schema/API/web thử được dọn sau kiểm tra; ứng dụng chính được khởi động lại.
