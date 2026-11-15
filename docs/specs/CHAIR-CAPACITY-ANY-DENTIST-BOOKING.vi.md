# Đặt lịch theo ghế trống — "Bất kỳ nha sĩ"

Cập nhật 14/11/2026. Bổ sung cho [S58-BRANCHES-AND-CHAIRS.md](S58-BRANCHES-AND-CHAIRS.md) §1 (ghế là sức chứa) và
[CLINIC-SLOTS-AND-SERVICE-DURATIONS.vi.md](CLINIC-SLOTS-AND-SERVICE-DURATIONS.vi.md).

## 1. Mục tiêu

Phòng khám có nhiều nha sĩ làm cùng một ca. Ghế **không gắn cố định** với nha sĩ: mỗi chi nhánh có N ghế hoạt động, nên trong
cùng một khoảng thời gian chi nhánh nhận tối đa N lịch hẹn (mỗi lịch giữ một ghế và một nha sĩ). Người đặt có thể:

- chọn một nha sĩ cụ thể (giữ nguyên hành vi cũ), hoặc
- chọn **"Bất kỳ nha sĩ"**: chỉ chọn chi nhánh, dịch vụ, ngày; hệ thống liệt kê các giờ còn **ít nhất một ghế trống và một nha sĩ
  đang trong ca, rảnh** và tự gán nha sĩ + ghế cho khung giờ đó.

## 2. Quy tắc

1. Một ghế chỉ giữ một lịch tại một thời điểm (`Confirmed` hoặc `Checked-in` chồng thời gian ⇒ ghế bận).
2. Một nha sĩ chỉ có một lịch tại một thời điểm; giờ làm chỉ lấy từ lịch làm việc đã công bố của nha sĩ.
3. Ghế được chọn là ghế hoạt động có Id nhỏ nhất còn trống (không thay đổi).
4. Chế độ "Bất kỳ nha sĩ": với mỗi giờ bắt đầu hợp lệ (theo bước slot của chi nhánh và ca của từng nha sĩ), xét mọi nha sĩ hoạt
   động của chi nhánh. Nếu nhiều nha sĩ cùng rảnh, chọn nha sĩ **có ít lịch còn hiệu lực nhất trong ngày** tại chi nhánh
   (cân bằng tải), hoà thì chọn Id nhỏ hơn. Mỗi giờ bắt đầu trả về tối đa một slot.
5. Nha sĩ có lịch không hợp lệ cho truy vấn (ví dụ không thuộc chi nhánh) bị bỏ qua, không làm hỏng cả truy vấn.
6. Khi xác nhận đặt lịch, client gửi `dentistId` và `chairId` (nhân viên) lấy từ slot đã chọn; server kiểm tra lại toàn bộ trong
   transaction như cũ. Nếu nha sĩ/ghế vừa bị chiếm ⇒ `409 MSG27/MSG28`, client tải lại slot.
7. Chế độ "Bất kỳ nha sĩ" không dùng phụ tá (`assistantId` phải trống).

## 3. API

`GET /api/availability?branchId=&dentistId=&serviceId=&date=&durationMinutes=&assistantId=`

- `dentistId` trở thành **tuỳ chọn**. Bỏ trống hoặc `0` ⇒ chế độ "Bất kỳ nha sĩ".
- Phản hồi không đổi: `{ timeZone, date, slots: [{ start, end, chairId, dentistId, assistantId, scheduleVersion,
  assistantScheduleVersion, bookingSettingsVersion }] }`. Ở chế độ "Bất kỳ nha sĩ", `dentistId`/`scheduleVersion` là của nha sĩ
  được gán cho slot.
- `0` kèm `assistantId` ⇒ `400 MSG110`.
- Quyền truy cập, kiểm tra dịch vụ công khai và thời hạn đặt trước 60 phút giữ nguyên.

`POST /api/appointments` không đổi: `dentistId` vẫn bắt buộc (lấy từ slot).

## 4. Giao diện

- Web khách hàng (đặt lịch): ô "Bác sĩ" có lựa chọn đầu tiên **"Bất kỳ bác sĩ (ghế trống)"**. Khi chọn, danh sách khung giờ gộp
  của mọi bác sĩ; tóm tắt và bước xác nhận hiển thị tên bác sĩ được gán theo slot đã chọn.
- Web nhân viên (BookingDrawer): ô "Nha sĩ" có lựa chọn **"Bất kỳ nha sĩ (ghế trống)"**; bước xem lại hiển thị nha sĩ và ghế
  theo slot. Bộ lọc ghế vẫn áp dụng trên các slot trả về.
