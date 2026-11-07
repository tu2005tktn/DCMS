# S23 — Đơn thuốc trong hồ sơ khách hàng

## Luồng

- Tab Đơn thuốc hiển thị các đơn đã lưu của bệnh nhân, ngày kê, chẩn đoán và người kê. Người có quyền `prescriptions.manage` có thể tạo và sửa đơn; người có `prescriptions.read` có thể xem.
- Form kê đơn hiển thị ngay trong tab hồ sơ theo bố cục: thông tin khách hàng chỉ đọc; chẩn đoán có gợi ý từ danh mục chẩn đoán và vẫn nhận văn bản tự nhập; chẩn đoán khác; khối **CHỈ ĐỊNH**; lời dặn chung; ngày và bác sĩ kê. Tạo/sửa đơn không mở modal toàn màn hình.
- Mỗi dòng chỉ định cho phép chọn thuốc đang hoạt động và được phép kê từ danh mục, xem/nhập số lượng nguyên dương, đơn vị tự lấy từ danh mục, cùng hướng dẫn sử dụng riêng. Nút `+` cạnh tên thuốc mở popup tạo thuốc vào danh mục nếu tài khoản có `catalog.medicines.manage`; lưu thành công sẽ đưa thuốc vào danh sách lựa chọn và chọn ngay. Nút **+ THÊM** thêm dòng kê mới; thao tác này không lưu đơn cho tới khi bác sĩ bấm Lưu.
- Người không có quyền quản lý danh mục có thể chọn thuốc sẵn có; luồng thuốc tùy chỉnh nếu bật vẫn chỉ lưu snapshot trong đơn và không tạo dữ liệu danh mục.
- Lưu toàn bộ đơn trong một giao dịch. Ghi snapshot tên, mã, hàm lượng, dạng và đơn vị thuốc để đơn cũ không đổi khi danh mục được sửa. Sửa đơn yêu cầu version hiện tại; requestKey chống lưu trùng. Người kê là nhân viên gắn với tài khoản đang thao tác, không nhận từ payload.
- Đơn theo lượt khám S19 vẫn giữ nguyên; danh sách hồ sơ có thể dẫn tới đơn theo lượt khám. Việc gửi lên donthuocquocgia.vn chưa có tích hợp và không được báo thành công giả.

## API

- `GET /api/prescriptions/patients/{patientId}`: danh sách đơn S23 trong các chi nhánh người dùng được xem.
- `GET /api/prescriptions/patients/{patientId}/context`: các chi nhánh đang hoạt động nơi tài khoản hiện tại được phân công kê đơn và tên người kê.
- `GET /api/prescriptions/patients/{patientId}/{id}`: chi tiết đơn, gồm các dòng thuốc.
- `POST /api/prescriptions/patients/{patientId}` và `PUT /api/prescriptions/patients/{patientId}/{id}`: `{ requestKey, expectedVersion, branchId, prescribedOn, diagnosis, otherDiagnosis, advice, items: [{ medicineId, quantity, instructions }] }`. Create dùng expectedVersion 0; update dùng version đã đọc. Trả chi tiết đơn mới.
- Dùng `GET /api/prescriptions/medicine-options?branchId=...&search=...` để chọn thuốc. Quyền đọc/quản lý và phạm vi chi nhánh được kiểm tra phía server. Không nhận bản ghi bệnh nhân đã lưu trữ khi tạo mới.

## Dữ liệu khởi tạo

- Có thể seed một bộ danh mục thuốc khởi tạo trong Development để bác sĩ có dữ liệu chọn khi kê; chỉ tạo `CatalogMedicine`, không tạo bệnh nhân, đơn đã kê, liều, hướng dẫn mặc định, giá hoặc tồn kho.
- Dữ liệu seed được đánh dấu bằng mã cố định, chạy lặp không nhân đôi, và chỉ chạy qua `--seed-clinic-medicines` trong Development. Danh mục production vẫn do cơ sở khám chữa bệnh xác nhận/quản lý; bác sĩ chịu trách nhiệm chọn thuốc, liều và hướng dẫn phù hợp từng bệnh nhân.
