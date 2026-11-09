# Sửa kiểm tra đặt lịch theo kế hoạch điều trị — 07/11/2026

Đặc tả: [TREATMENT-NEXT-VISIT-BOOKING.md](../specs/TREATMENT-NEXT-VISIT-BOOKING.md#kiểm-tra-lại-khi-ghi-lịch--sửa-lỗi-07112026).

## Hành vi sau sửa

- Chỉ định cấp hạng mục không thể tạo hoặc đổi lịch khi hạng mục đã xóa/ẩn, đánh dấu hoàn tất, hoàn tất lâm sàng hoặc đã có hoạt động Posted còn hiệu lực. Trả `409 MSG230`, kể cả hộp đặt lịch đã được mở trước đó.
- Theo yêu cầu thu hẹp phạm vi của người dùng, đã bỏ phần bổ sung kiểm tra dịch vụ/hạng mục khác; giữ contract port lịch hẹn hiện có. Bản sửa chỉ giữ kiểm tra trạng thái hạng mục.
- Các kiểm tra chạy trong transaction và khóa lịch hiện có. Từ chối không tạo lịch/đặt chỗ/receipt/outbox, không chuyển chỉ định sang Booked; đổi lịch thất bại giữ giờ và trạng thái cũ.
- `TreatmentItemRules.IsPendingForBooking` và `TreatmentBookingQueries` dùng chung điều kiện hạng mục và snapshot hoạt động giữa danh sách cần hẹn và bước lưu. Xóa hoạt động trên phiếu được tính theo sự kiện cuối; cờ hoàn tất hiển thị vẫn là điều kiện riêng, cần được bỏ nếu muốn hẹn lại.
- Chỉ định cấp bước vẫn cho hẹn bước sau khi bước trước của cùng hạng mục đã được thực hiện. Giữ kiểm tiền nhiệm/labo hiện có. Retry cùng request key của lịch đã tạo vẫn trả receipt cũ, không nhân lịch.
- Không đổi contract port tạo/đổi lịch, JSON API công khai, bảng hoặc migration; không đổi quyền chuyên môn và quyền thao tác.

## Kiểm chứng

| Kiểm tra | Kết quả |
| --- | --- |
| .NET 10 restore/build solution | Qua, 0 lỗi |
| Unit tests | 87/87 qua |
| API tạo/đổi lịch sau khi thu hẹp phạm vi | 35/35 qua |
| TreatmentPlanBookingApiTests mới (nằm trong 35 ca trên) | 10/10 qua |
| Browser patient-appointments/treatment-progression | 8/8 qua với mock API |
| Web npm install/build | Qua |
| PostgreSQL pg_isready | Nhận kết nối |

Các ca đã xóa, đánh dấu hoàn tất kiểm chính xác `409 MSG230` và không có ghi dở. Các ca bổ sung bao phủ ẩn/hoàn tất lâm sàng, Posted rồi xóa hoạt động, đặt/đổi lịch hợp lệ, từ chối đổi khi nguồn không còn hợp lệ, replay và nhiều bước của cùng hạng mục. Đã bỏ các ca yêu cầu từ chối dịch vụ khác/thiếu dịch vụ trong phạm vi này.

Phạm vi hiện tại đã biên dịch và chạy lại 35/35 ca API: 10 ca kiểm trạng thái hạng mục và 25 ca tạo/đổi lịch hiện có. 87 unit test và 8 ca browser được chạy ở lượt sửa trước khi thu hẹp phạm vi; không cộng số kiểm chứng 95 ca của phạm vi cũ vào kết quả mới.

Bằng chứng hiện tại: `.artifacts/treatment-booking-fix-20261107/state-only-final.trx` và `verification.json`. Các TRX `booking-fix-api.trx`, `booking-regressions-final.trx`, `unit.trx` thuộc lượt trước. API integration dùng PostgreSQL/auth/EF thật trên schema `dcms_test_*` cách ly; browser dùng mock API, không coi là browser với API thật. Không dùng khóa PayOS trong tiến trình kiểm thử hoặc tạo giao dịch thanh toán thật.

Build còn cảnh báo Pkcs NU1902/NU1903, hai tham số chưa dùng và chunk/annotation SignalR có sẵn. Không thay dependency trong phạm vi sửa lỗi. Chưa thực hiện Git trong lượt sửa này.
