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

## Bổ sung thuốc phòng khám và thuốc mua ngoài — 07/11/2026

- Danh mục thuốc S58 là nguồn duy nhất. `internalStockTracked=true` đánh dấu thuốc dùng tại phòng khám; `false` là thuốc bệnh nhân mua ngoài. Trang quản lý thuốc trong Thiết lập cho phép lọc hai nhóm, tìm kiếm, xem chi tiết, thêm, sửa, ngừng dùng/khôi phục và phân trang mặc định 20 thuốc. Thuốc đã dùng trong đơn không bị xóa cứng; đơn cũ giữ snapshot.
- `GET /api/system-settings/settings/catalogs/medicines?type=Clinic|External&page=1&pageSize=20` lọc theo nhóm; bỏ `type` trả cả hai. Các API CRUD/version/audit hiện hữu giữ nguyên. Chỉ Chủ phòng khám quản lý danh mục; nha sĩ kê đơn được đọc lựa chọn thuốc đang hoạt động.
- Mẫu đơn bệnh nhân có ô `Yêu cầu mua thêm thuốc ngoài`. Khi không tích, mọi dòng chỉ chọn thuốc phòng khám. Khi tích, có thể thêm dòng thuốc mua ngoài bên cạnh các dòng thuốc phòng khám. Tối thiểu một dòng thuốc trong toàn đơn. Thuốc ngoài không trừ kho và không tự ghi thanh toán.
- `GET /api/prescriptions/medicine-options?branchId=...&source=Clinic|External` trả tối đa 50 thuốc phù hợp và `internalStockTracked` cho giao diện. `POST/PUT /api/prescriptions/patients/{patientId}[/{id}]` nhận thêm `requiresExternalPurchase` và `items[].isExternalPurchase`. Server kiểm tra thuốc thuộc đúng nhóm, không nhận dòng ngoài nếu ô tổng không tích, và yêu cầu ít nhất một dòng ngoài khi ô tổng tích. DTO đọc trả hai cờ và snapshot nhóm của từng dòng. Đơn cũ mặc định `false` để tiếp tục đọc được; sửa đơn cũ có thể chọn lại nhóm.
- Khi sửa đơn với version hiện tại, server thay toàn bộ dòng thuốc trong cùng giao dịch và tăng version đúng một lần. Version cũ trả 409, không xóa hoặc sửa một phần dòng cũ.
- Luồng đơn theo lượt khám S19 vẫn là đơn thuốc mua ngoài riêng hiện hữu; thay đổi này ghép thuốc ngoài vào mẫu đơn bệnh nhân S23, không tự chuyển hoặc nhân đôi dữ liệu giữa hai loại đơn.
