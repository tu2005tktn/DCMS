# S23 — Kê đơn trực tiếp từ hồ sơ bệnh nhân

Cập nhật lưu trữ 12/11/2026 theo [hợp đồng rút gọn database](DATABASE-SIMPLIFICATION.vi.md). Đơn lưu một dòng trong `PRESCRIPTION`; `Items` JSONB chứa ID dòng, ID thuốc, snapshot và thứ tự. Danh mục thuốc dùng chung `CATALOG_MEDICINE`. Các route và DTO bên dưới tiếp tục áp dụng.

Cập nhật 09/11/2026: form kê đơn dạng "tờ đơn thuốc", chọn bác sĩ kê đơn, context phòng khám, mobile hiển thị cùng bố cục và dữ liệu demo đơn thuốc.

## Mục tiêu và luồng chính

- Bác sĩ mở hồ sơ bệnh nhân, chọn tab **Đơn thuốc**, bấm **Kê đơn thuốc**, điền form đơn thuốc rồi lưu. Không yêu cầu tạo lượt khám mới, chọn mẫu đơn, hoàn tất một quy trình nhiều bước hoặc mở một trang riêng.
- Hồ sơ bệnh nhân là nơi duy nhất staff bắt đầu và xem lại đơn. Không dùng mẫu đơn trong luồng kê này.
- Thông tin bệnh nhân (họ tên, mã BN, ngày sinh, giới tính) lấy từ hồ sơ bệnh nhân ở server, chỉ đọc trên form. Thuật ngữ trên form là **Bệnh nhân** (không dùng "Khách hàng").
- Thuốc được chọn từ danh mục active và prescription-eligible. Không tạo thuốc mới trong form kê đơn. Có thể thêm/xóa dòng thuốc ngay trên form.
- Lưu đơn một lần; ghi snapshot tên/mã/hàm lượng/dạng/đơn vị để đơn cũ không đổi khi danh mục thay đổi. Ghi audit, chống gửi trùng bằng requestKey và kiểm tra version khi sửa. Đơn lưu thành công được hiển thị trên mobile của tài khoản đã liên kết VERIFIED với bệnh nhân. Không cần bước phát hành riêng.
- Bệnh nhân chỉ xem; không sửa đơn. API mobile không nhận PatientId từ client, chỉ trả đơn thuộc liên kết VERIFIED hiện tại. Nếu liên kết không còn hợp lệ thì từ chối và không trả dữ liệu.
- Đây là dữ liệu kê đơn ngoại trú; không ghi tồn kho, xuất kho, bán thuốc hoặc chi phí. Không khẳng định đã gửi tới donthuocquocgia.vn.

## Form "Đơn thuốc" (web staff)

Form mở trong hộp thoại dạng tờ đơn, theo thứ tự:

1. **Tiêu đề**: chữ "ĐƠN THUỐC"; bên phải là tên phòng khám (chi nhánh kê đơn), số điện thoại và email của chi nhánh nếu có trong hồ sơ chi nhánh. Không có chức năng gửi đơn lên donthuocquocgia.vn. Khi bác sĩ thuộc nhiều chi nhánh, có ô chọn chi nhánh kê đơn; một chi nhánh thì tự chọn.
2. **BỆNH NHÂN**: Họ và tên, Mã BN, Ngày sinh, Giới tính (chỉ đọc). Chẩn đoán: chọn một hoặc nhiều mục từ danh mục Chẩn đoán đang hoạt động (`/api/system-settings/reference-options?kind=diagnoses`) và được gõ thêm nội dung riêng; lưu thành chuỗi nối bằng `; `. Chẩn đoán khác: ô nhập tự do.
3. **CHỈ ĐỊNH**: mỗi dòng gồm Tên thuốc (chọn từ danh mục, tìm theo tên/mã), Số lượng (nguyên 1–10.000), Đơn vị thuốc bắt buộc do bác sĩ tự nhập (đơn vị danh mục chỉ hiện làm gợi ý, không tự điền), và Hướng dẫn sử dụng (tự điền hướng dẫn mặc định của thuốc, sửa được, ≤1000 ký tự); nút xóa dòng khi có hơn một dòng. Nút xanh **+ THÊM** thêm dòng. Tối đa 30 dòng.
4. **GHI CHÚ/LỜI DẶN**: ô nhiều dòng, tùy chọn (≤4000 ký tự).
5. **NGÀY & BÁC SĨ KÊ ĐƠN**: Ngày kê (mặc định hôm nay, không cho chọn ngày tương lai) và Bác sĩ (mặc định bác sĩ đang đăng nhập; danh sách gồm nhân sự có vai trò bác sĩ đang hoạt động tại chi nhánh kê đơn).

Nút **Lưu đơn thuốc** / **Hủy** ở chân form. Danh sách đơn trong tab hiển thị ngày kê, chẩn đoán, thuốc, bác sĩ; **Xem** mở cùng bố cục ở chế độ chỉ đọc (người không có quyền quản lý chỉ có chế độ này), **Sửa** dành cho người có `prescriptions.manage`.

## UI và quyền

- Tab đơn thuốc nằm trong hồ sơ bệnh nhân. `prescriptions.read` xem danh sách/chi tiết; `prescriptions.manage` + vai trò Dentist tạo và sửa.
- Trường tối thiểu khi lưu: ít nhất một thuốc, đơn vị thuốc bác sĩ nhập và số lượng nguyên dương. Chi nhánh/ngày kê/bác sĩ tự điền; chẩn đoán, chẩn đoán khác, hướng dẫn và lời dặn tùy chọn.
- **Bác sĩ kê đơn** được chọn trên form (`prescriberEmployeeId`). Server chỉ nhận bác sĩ là thành viên đang hoạt động, có quyền khám (`CanDentist`) của chi nhánh kê đơn; bỏ trống thì dùng nhân sự của tài khoản đang thao tác. Audit luôn ghi tài khoản thực hiện thao tác, kể cả khi ghi đơn dưới tên bác sĩ khác.
- Không hiển thị mẫu đơn, modal tạo thuốc từ form, in/QR, bước finalize, hiệu chỉnh phiên bản hay cảnh báo nhiều tầng trong luồng thường dùng.

## API staff

- `GET /api/prescriptions/patients/{patientId}`: danh sách trong phạm vi chi nhánh của staff.
- `GET /api/prescriptions/patients/{patientId}/context` → `{ branchIds, prescriberName, prescriberEmployeeId, branches: [{ id, name, address, phone, email }], doctors: [{ employeeId, name, branchId }] }`. `branches` là chi nhánh trong phạm vi của bác sĩ (tên/điện thoại/email ưu tiên hồ sơ chi nhánh, nếu chưa có thì tên chi nhánh lịch); `doctors` là thành viên `CanDentist` đang hoạt động của các chi nhánh đó.
- `GET /api/prescriptions/patients/{patientId}/{id}`: chi tiết đơn và dòng thuốc.
- `POST /api/prescriptions/patients/{patientId}` và `PUT /api/prescriptions/patients/{patientId}/{id}` nhận `{ requestKey, expectedVersion, branchId, prescribedOn, diagnosis, otherDiagnosis, advice, prescriberEmployeeId?, items: [{ medicineId, unit, quantity, instructions }] }`. `unit` là đơn vị thuốc bắt buộc do bác sĩ tự nhập, tối đa 40 ký tự; giá trị danh mục chỉ dùng làm gợi ý, không tự lưu. `prescribedOn` không được sau ngày hiện tại (giờ Việt Nam) → `400 MSG04 field=prescribedOn`. Bác sĩ không hợp lệ → `400 MSG04 field=prescriberEmployeeId`.
- DTO đơn (`PatientProfilePrescriptionDto`) gồm thêm `prescriberEmployeeId`, thông tin phòng khám (`branchAddress`, `branchPhone`, `branchEmail`) và bệnh nhân (`patientCode`, `patientName`, `patientBirthDate`, `patientGender`) để web/mobile hiển thị cùng bố cục tờ đơn.
- `GET /api/prescriptions/medicine-options?branchId=...&q=...`: danh mục thuốc hợp lệ trong phạm vi chi nhánh. Server xác thực quyền, vai trò bác sĩ và phạm vi chi nhánh.

## API và màn hình mobile

- `GET /api/patients/me/prescriptions`: danh sách đơn đã lưu của hồ sơ được liên kết VERIFIED; lấy PatientId từ liên kết phía server.
- `GET /api/patients/me/prescriptions/{prescriptionId}`: chi tiết đơn và thuốc (cùng DTO staff). Không trả đơn của hồ sơ khác; không cần visitId.
- Màn danh sách (`/(account)/prescriptions`): ngày kê, phòng khám, bác sĩ, chẩn đoán, số thuốc; rỗng thì hiển thị "Chưa có đơn thuốc nào."
- Màn chi tiết hiển thị cùng bố cục tờ đơn: tiêu đề + phòng khám (điện thoại/email), Bệnh nhân (họ tên, mã BN, ngày sinh, giới tính), chẩn đoán/chẩn đoán khác, danh sách chỉ định (tên thuốc, số lượng + đơn vị, hướng dẫn), lời dặn nổi bật, ngày kê và bác sĩ kê đơn, kèm ghi chú "Đơn thuốc mua ngoài. Phòng khám không bán thuốc tại chỗ."
- API mobile dùng session public và policy liên kết bệnh nhân hiện có. Không đưa bản nháp client hoặc dữ liệu giả lập phía client vào danh sách.

## Cổng bệnh nhân web (`/customer`)

- Tài khoản Public đăng nhập cổng bệnh nhân web thấy mục điều hướng **Đơn thuốc của tôi** (`/customer/prescriptions`) và một thẻ truy cập nhanh cùng tên ở trang chủ cổng.
- Danh sách dùng `GET /api/patients/me/prescriptions` với phiên khách hàng: ngày kê, phòng khám, bác sĩ, chẩn đoán, số thuốc; mới nhất trước. Rỗng thì hiển thị "Chưa có đơn thuốc nào." kèm liên kết tới **Liên kết hồ sơ** (không phân biệt chưa liên kết hay chưa có đơn để không lộ thông tin).
- Chi tiết `/customer/prescriptions/{prescriptionId}` dùng `GET /api/patients/me/prescriptions/{prescriptionId}` và hiển thị cùng bố cục tờ đơn chỉ đọc như web staff/mobile (phòng khám, bệnh nhân, chẩn đoán, chỉ định, lời dặn, ngày & bác sĩ kê đơn) cùng ghi chú "Đơn thuốc mua ngoài. Phòng khám không bán thuốc tại chỗ." Không có nút sửa, gửi đơn hay thanh toán.
- `401` → hết phiên, chuyển về đăng nhập cổng; `404` → "Không tìm thấy đơn thuốc trong hồ sơ đã liên kết của bạn."

## Demo data (chỉ Development)

- `--seed-demo` (script `start-api.ps1 -SeedDemo`) chạy thêm seeder đơn thuốc demo sau các seeder tài khoản, danh mục thuốc, bệnh nhân demo và chi nhánh thử nghiệm. Production không chạy seeder này.
- Danh mục thuốc demo bổ sung vài thuốc nha khoa thường gặp (mã `MED-STARTER-*`) để bác sĩ có thể kê ngay.
- Liên kết demo: tài khoản Public tổng hợp `patient.demo` → `BN-DEMO-0001` (Nguyễn Văn An) và `patient2.demo` → `BN-DEMO-0002` (Trần Thị Bình) được tạo trạng thái `VERIFIED` nếu tài khoản/hồ sơ đó chưa có liên kết đang xử lý hoặc đã xác minh; ghi `PATIENT_LINK_AUDIT` hành động `DevelopmentSeed`. Không bao giờ tự liên kết tài khoản/hồ sơ khác.
- Đơn demo: 2 đơn cho `BN-DEMO-0001` và 1 đơn cho `BN-DEMO-0002`, kê bởi `dentist.demo` tại "S09 - Chi nhánh thử nghiệm", dùng thuốc danh mục demo. Mỗi đơn có Id cố định để chạy lặp an toàn (đã có thì bỏ qua) và lời dặn ghi rõ "Dữ liệu minh họa". Không phải hướng dẫn điều trị thực tế.
- Kịch bản demo: đăng nhập web `dentist.demo` → hồ sơ `BN-DEMO-0001` → tab Đơn thuốc → xem đơn demo hoặc **Kê đơn thuốc** mới; đăng nhập mobile hoặc cổng bệnh nhân web (`/customer/sign-in`) bằng `patient.demo` → Đơn thuốc của tôi → thấy cùng đơn vừa lưu.
- Danh sách đơn thuốc trong hồ sơ bệnh nhân mở chi tiết bằng ID của `PatientPrescription` qua `GET /api/patients/me/prescriptions/{id}`. Route mobile không được khai báo hai tham số động ở cùng một cấp; liên kết từ lượt khám phải dùng API đơn theo lượt khám riêng. Chi tiết hiển thị đúng thuốc và lời dặn của đơn được chọn, kể cả khi có nhiều đơn.
