# Chỉnh thời lượng khi đặt lịch điều trị — 09/11/2026

Đặc tả: `../specs/TREATMENT-NEXT-VISIT-BOOKING.md`, mục sửa thời lượng khi đặt hẹn cấp hạng mục.

- `NextVisitBookingDrawer`: số phút mặc định theo candidate; nhập số nguyên 1–480, hủy slot đã chọn khi đổi, tìm lại availability theo số phút mới. Chặn xác nhận khi thời lượng sai/đang tải, loại phản hồi cũ bằng generation, giữ liên hệ và checkbox đồng ý. Mở lại khôi phục mặc định.
- `AppointmentService` truyền cờ nội bộ cho port dựa trên tài khoản Staff đã xác thực. `FollowUpAppointmentPort` chỉ cho lệch duration chỉ định đối với nguồn dossier/item không gắn step. Không nhận cờ này từ JSON. Public và chỉ định cấp bước vẫn giữ kiểm tra thời lượng cũ.
- Appointment và reservation dùng thời lượng mới. Không sửa kế hoạch, chỉ định hoặc danh mục. Dùng lại chỉ định đã có; chỉ định mới mang thời lượng gợi ý ban đầu. Không có migration hoặc thay JSON API.
- Retry cùng nội dung giữ requestKey; chỉnh thời lượng làm mới key đặt lịch. Receipt/fingerprint và kiểm một lịch đang hoạt động của chỉ định tiếp tục bảo vệ ghi trùng.

## Kết quả kiểm chứng

- .NET 10 restore/build solution: đạt, 0 lỗi sau khi dừng đúng API đang khóa DLL.
- API/PostgreSQL schema cách ly: **27/27 đạt**, gồm 1, 37, 480 phút, vượt giới hạn không ghi dở, thời lượng lưu trên reservation, không đổi kế hoạch/chỉ định, replay/key khác nội dung, Public không được override và chỉ định cấp bước vẫn khóa thời lượng. Các kiểm trạng thái hạng mục, consent, lịch bác sĩ và đặt lịch thường cùng bộ hồi quy đều đạt.
- Playwright mock API: **14/14 đạt**; gồm sửa và chọn lại giờ, dùng lại chỉ định, giữ dữ liệu, rỗng/reset mặc định, phản hồi cũ về chậm và đổi requestKey sau lỗi mạng khi thay thời lượng. Đây là kiểm thử giao diện với API giả lập; API/database được kiểm độc lập bằng bộ trên.
- Web production build đạt. Còn cảnh báo Pkcs, annotation SignalR và kích thước bundle đã có.
- Dữ liệu kiểm thử synthetic. Các ca đặt 480 phút dùng ngày riêng để không chiếm bác sĩ chung của các ca hồi quy.
- Bằng chứng local: `.artifacts/runtime-20261109/edited-duration-{restore,build,web-build,api,browser}.log`, `edited-duration-api.trx`.
