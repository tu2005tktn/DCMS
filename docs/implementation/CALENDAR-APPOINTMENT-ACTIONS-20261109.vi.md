# Thao tác tại lưới Lịch hẹn — 09/11/2026

Trên `/appointments`, bấm thẻ trong chế độ Ngày hoặc Tuần để mở drawer **Thông tin lịch hẹn**. Ngay trong drawer có Chỉnh sửa lịch, Hủy lịch, Khách không tới hoặc Đặt lịch thay thế theo quyền/trạng thái/thời gian. Drawer giữ mở sau commit, cập nhật chi tiết và lưới từ server, giữ ngày/bộ lọc; có liên kết tới trang chi tiết đầy đủ.

API options thêm `currentDentistId` do server suy từ Employee của actor trong tập nha sĩ được phép. Khi mở màn, ưu tiên chi nhánh của người đó và tự chọn đúng nha sĩ. Không so tên hoặc dùng AccountId làm EmployeeId. Trợ lý không phải nha sĩ giữ lọc Tất cả ở chế độ Ngày. Đổi lựa chọn thủ công không bị ghi đè khi tải lại/đổi ngày. Đổi chi nhánh áp dụng lại mặc định nếu có nha sĩ đó.

Đặc tả được cập nhật trước code tại `UC04-05-S10-S11.md` mục 4 và `UC06-APPOINTMENT-CHANGES.md`. Dùng chung chi tiết và `StaffAppointmentActions` với hồ sơ bệnh nhân/trang chi tiết; không thêm API ghi hoặc migration.

Kiểm tra:

- .NET restore/build thành công, 0 lỗi; 5 AppointmentQueryApiTests qua, gồm danh tính nha sĩ/owner/trợ lý và giới hạn phạm vi. Các warning Pkcs/tham số chưa dùng vốn có vẫn còn.
- Web build thành công; còn warning bundle vốn có.
- 18 UI mock tests qua trong appointment-calendar-actions, appointment-calendar, staff-appointment-actions và no-show-replacement. Kiểm tra sửa/hủy trên lưới, NoShow/đặt thay ở chế độ Tuần dù bộ lọc ẩn thẻ cũ, giữ URL/bộ lọc, tự chọn đúng EmployeeId, đổi chọn thủ công/chi nhánh, lỗi đọc/thử lại, quyền chỉ đọc và các luồng dùng chung.
- Ảnh drawer 1440/390px tại `.artifacts/runtime-20261109/calendar-actions-{1440,390}.png`; kiểm tra cả hai cạnh drawer trong viewport trước khi chụp.
- `git diff --check` qua. Main API/database healthy ở 5080, web 5173 trả HTTP 200. Không tạo/sửa lịch hay hồ sơ thực để kiểm tra; browser tests mock network, API tests dùng PostgreSQL schema riêng.

Log: `.artifacts/runtime-20261109/calendar-actions-{restore,build,api,web-build,browser,layout}.log`, TRX `calendar-actions-api.trx`.
