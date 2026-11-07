# S23 — Đơn thuốc trong hồ sơ khách hàng

## Luồng

- Tab Đơn thuốc hiển thị các đơn đã lưu của bệnh nhân, ngày kê, chẩn đoán và người kê. Người có quyền `prescriptions.manage` có thể tạo và sửa đơn; người có `prescriptions.read` có thể xem.
- Mẫu đơn hiển thị thông tin bệnh nhân từ hồ sơ dưới dạng chỉ đọc; nhập chẩn đoán, chẩn đoán khác, lời dặn, ngày kê và ít nhất một dòng thuốc. Mỗi dòng chọn thuốc đang hoạt động và được phép kê đơn từ danh mục, số lượng nguyên dương, hướng dẫn sử dụng. Đơn vị hiển thị từ danh mục.
- Lưu toàn bộ đơn trong một giao dịch. Ghi snapshot tên, mã, hàm lượng, dạng và đơn vị thuốc để đơn cũ không đổi khi danh mục được sửa. Sửa đơn yêu cầu version hiện tại; requestKey chống lưu trùng. Người kê là nhân viên gắn với tài khoản đang thao tác, không nhận từ payload.
- Đơn theo lượt khám S19 vẫn giữ nguyên; danh sách hồ sơ có thể dẫn tới đơn theo lượt khám. Việc gửi lên donthuocquocgia.vn chưa có tích hợp và không được báo thành công giả.

## API

- `GET /api/prescriptions/patients/{patientId}`: danh sách đơn S23 trong các chi nhánh người dùng được xem.
- `GET /api/prescriptions/patients/{patientId}/context`: các chi nhánh đang hoạt động nơi tài khoản hiện tại được phân công kê đơn và tên người kê.
- `GET /api/prescriptions/patients/{patientId}/{id}`: chi tiết đơn, gồm các dòng thuốc.
- `POST /api/prescriptions/patients/{patientId}` và `PUT /api/prescriptions/patients/{patientId}/{id}`: `{ requestKey, expectedVersion, branchId, prescribedOn, diagnosis, otherDiagnosis, advice, items: [{ medicineId, quantity, instructions }] }`. Create dùng expectedVersion 0; update dùng version đã đọc. Trả chi tiết đơn mới.
- Dùng `GET /api/prescriptions/medicine-options?branchId=...&search=...` để chọn thuốc. Quyền đọc/quản lý và phạm vi chi nhánh được kiểm tra phía server. Không nhận bản ghi bệnh nhân đã lưu trữ khi tạo mới.
