# S23 — Kê đơn trực tiếp từ hồ sơ bệnh nhân

## Mục tiêu và luồng chính

- Bác sĩ mở hồ sơ bệnh nhân, chọn tab **Đơn thuốc**, bấm **Kê đơn thuốc**, chọn thuốc từ danh mục, nhập số lượng và hướng dẫn dùng, rồi lưu đơn. Không yêu cầu tạo lượt khám mới, chọn mẫu đơn, hoàn tất một quy trình nhiều bước hoặc mở một trang riêng.
- Hồ sơ bệnh nhân là nơi duy nhất staff bắt đầu và xem lại đơn. Bỏ menu/trang quản lý mẫu đơn thuốc và không dùng mẫu đơn trong luồng kê.
- Form tập trung vào thuốc, số lượng và hướng dẫn từng thuốc; lời dặn chung và chẩn đoán là tùy chọn. Ngày kê, bác sĩ và chi nhánh mặc định tự lấy theo tài khoản/phân công; thông tin bệnh nhân chỉ đọc.
- Thuốc được chọn từ danh mục active và prescription-eligible. Không tạo thuốc mới trong form kê đơn; việc quản lý danh mục là chức năng riêng. Có thể thêm/xóa dòng thuốc ngay trên form.
- Lưu đơn một lần; ghi snapshot tên/mã/hàm lượng/dạng/đơn vị để đơn cũ không đổi khi danh mục thay đổi. Ghi audit, chống gửi trùng bằng requestKey và kiểm tra version khi sửa. Đơn lưu thành công được hiển thị trên mobile của tài khoản đã liên kết VERIFIED với bệnh nhân. Không cần bước phát hành riêng.
- Bệnh nhân chỉ xem; không sửa đơn. API mobile không nhận PatientId từ client, chỉ trả đơn thuộc liên kết VERIFIED hiện tại. Nếu liên kết không còn hợp lệ thì từ chối và không trả dữ liệu.
- Đây là dữ liệu kê đơn ngoại trú; không ghi tồn kho, xuất kho, bán thuốc hoặc chi phí. Không khẳng định đã gửi tới donthuocquocgia.vn.

## UI và quyền

- Tab đơn thuốc nằm trong hồ sơ bệnh nhân. `prescriptions.read` xem danh sách/chi tiết; `prescriptions.manage` tạo và sửa.
- CTA **Kê đơn thuốc** hiển thị khi người dùng có quyền quản lý. Danh sách thể hiện ngày kê, chẩn đoán, bác sĩ, số thuốc; xem đơn mở chi tiết gọn trong tab.
- Trường tối thiểu khi lưu: ít nhất một thuốc và số lượng nguyên dương. Chi nhánh/ngày kê/bác sĩ tự điền; chẩn đoán, hướng dẫn và lời dặn chung tùy chọn.
- Không hiển thị mẫu đơn, modal tạo thuốc từ form, in/QR, bước finalize, hiệu chỉnh phiên bản hay cảnh báo nhiều tầng trong luồng thường dùng.

## API staff

- `GET /api/prescriptions/patients/{patientId}`: danh sách trong phạm vi staff.
- `GET /api/prescriptions/patients/{patientId}/context`: chi nhánh được phân công và tên bác sĩ hiện tại.
- `GET /api/prescriptions/patients/{patientId}/{id}`: chi tiết đơn và dòng thuốc.
- `POST /api/prescriptions/patients/{patientId}` và `PUT /api/prescriptions/patients/{patientId}/{id}` nhận `{ requestKey, expectedVersion, branchId, prescribedOn, diagnosis, otherDiagnosis, advice, items: [{ medicineId, quantity, instructions }] }`.
- `GET /api/prescriptions/medicine-options?branchId=...&search=...`: danh mục thuốc hợp lệ trong phạm vi chi nhánh. Server xác thực quyền, vai trò bác sĩ và phạm vi chi nhánh.

## API mobile

- `GET /api/patients/me/prescriptions`: danh sách đơn đã lưu của hồ sơ được liên kết VERIFIED; lấy PatientId từ liên kết phía server.
- `GET /api/patients/me/prescriptions/{prescriptionId}`: chi tiết đơn và thuốc. Không trả đơn của hồ sơ khác; không cần visitId.
- API mobile dùng session public và policy liên kết bệnh nhân hiện có. Không đưa bản nháp client hoặc dữ liệu giả vào danh sách.

## Demo data

- Development có thể seed danh mục thuốc tổng hợp, nhưng không seed đơn bệnh nhân hoặc đơn đã kê sẵn. Luồng demo tạo đơn qua UI hồ sơ bệnh nhân để mobile đọc cùng dữ liệu thật trong database.
