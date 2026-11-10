# S19 — Kê đơn mua ngoài tùy chọn trong Tổng kết khám

Ngày 02/11/2026. Theo yêu cầu tích hợp luồng kê đơn của Triển vào phần kết luận khám, **không bắt buộc kê đơn**. Mở rộng [S19/S20](S19-S20-DCMS-112.md), dùng danh mục thuốc S58 và API `/api/prescriptions` đã có; không tạo nguồn đơn thuốc thứ hai.

## Luồng sử dụng

- Trong tab **Tổng kết khám**, dưới chẩn đoán/dặn dò/hướng xử trí có khối **Đơn thuốc (không bắt buộc)**. Chỉ tài khoản có `examinations.manage` thấy khối này; Assistant đọc phiếu khám không phát sinh request prescription bị cấm.
- Mặc định không mở form khi chưa có thuốc; bấm **Kê đơn thuốc** khi cần. Đơn đã có dòng tự hiển thị để bác sĩ kiểm tra. Thu gọn/mở lại không xóa đơn hoặc phần nhập.
- Thêm/sửa thuốc ngay trong trang khám, không điều hướng ra ngoài và không tải lại/reset bản nháp kết luận. Chọn thuốc active + prescription eligible; tìm thuốc qua API có tham số `q`, không giới hạn việc chọn vào 50 dòng đầu tiên.
- Thuốc có đơn vị/hàm lượng/dạng dùng khi nguồn cung cấp. Số lượng là số nguyên 1–10.000, lời dặn tối đa 1.000 ký tự. Chọn thuốc mới lấy lời dặn mặc định; bác sĩ sửa lời dặn theo bệnh nhân. Dữ liệu này không thay trường “Thuốc đang dùng” của tiền sử.
- **Thêm vào đơn/Lưu thay đổi** lưu từng dòng bằng API Triển. Không tự lưu khi chọn thuốc, mở khối hoặc hoàn tất khám. Bỏ phần nhập chỉ bỏ form chưa lưu, không xóa dòng đã kê. API hiện chưa có xóa dòng đơn; bản tích hợp không giả lập xóa.
- Kê được khi Visit đang `InProgress`, bác sĩ có quyền và workspace cho `save` hoặc `complete` (kể cả sau xác nhận kết luận, trước khi kết thúc lượt). Visit chưa bắt đầu/đã kết thúc hoặc workspace chỉ đọc hiển thị đơn đã lưu, không mở form chỉnh sửa. Route đơn thuốc riêng dùng cùng component và quyền UI, lấy branch từ workspace khám thay vì phụ thuộc `finance.read`.

### Mở kê đơn từ khám và điều trị

- Bác sĩ có thể mở cùng màn hình kê đơn từ phần Tổng kết khám hoặc phiếu điều trị của bệnh nhân; mọi đơn vẫn gắn với một `Visit`, không tạo nguồn đơn thuốc thứ hai và không gắn trực tiếp với dossier/clinical activity.
- Phiếu điều trị chỉ hiện liên kết kê đơn khi ngữ cảnh trả về một Visit `InProgress` của đúng bệnh nhân, chi nhánh và bác sĩ hiện tại. Nút mở `/visits/{visitId}/prescription`; nếu không có Visit phù hợp, hướng dẫn bắt đầu lượt khám thay vì tạo/fake Visit.
- Màn hình riêng trình bày thông tin bệnh nhân (mã, họ tên, ngày sinh, giới tính), chẩn đoán từ dossier khi mở ở ngữ cảnh điều trị (nếu không thì lấy phiếu khám), ngày kê đơn và bác sĩ phụ trách phía trên danh sách thuốc/lời dặn. Thông tin bệnh nhân, chẩn đoán, ngày và bác sĩ là ngữ cảnh đọc; không cho sửa/nhập trùng với hồ sơ khám/điều trị. Giờ ký/verification trong bản in vẫn do API quyết định.
- Việc hoàn tất một hạng mục điều trị không tự ký đơn, không đóng Visit và không lưu nội dung thuốc chưa gửi. Bác sĩ chủ động mở màn hình, rà soát/liệt kê thuốc, lưu từng dòng rồi hoàn tất đơn theo API hiện có. Dossier không có Visit đang mở thì không thể tạo đơn theo đặc tả hiện tại.

## Optional và an toàn bản nhập

- Chưa mở đơn, không chọn thuốc, danh mục rỗng hoặc lỗi tải prescription **không** trở thành điều kiện validation của kết luận/hướng xử trí. Có thể hoàn tất khám và kết thúc lượt mà không có dòng thuốc nào; không ghi đơn rỗng, không tạo tồn kho/phí/phiếu thu.
- Xác nhận kết luận không tự lưu hoặc xóa form kê đơn còn nhập. Khi đang lưu thuốc thì khóa tạm thao tác ghi phiếu khám để tránh cạnh tranh trên UI; lỗi lưu trả lại khả năng thao tác, giữ form thuốc và kết luận.
- Rời trang có bản nhập thuốc dùng cùng cảnh báo mất bản nhập của màn khám. Khi **kết thúc lượt** mà thuốc đang nhập chưa lưu, hỏi bác sĩ quay lại kê hoặc tiếp tục kết thúc và bỏ phần thuốc chưa lưu. Có thể tiếp tục dù form thuốc chưa hợp lệ; không ép lưu và không tự ghi thuốc. Nếu kết thúc lượt thất bại, form thuốc vẫn giữ.
- Retry cùng payload sau lỗi mạng giữ `requestKey`, chặn double submit. MSG111 giữ form, cho đọc lại version dòng thuốc trước khi lưu tiếp. Lỗi prescription hiển thị trong khối kê đơn, không che kết luận hoặc biến thành lỗi bắt buộc của khám.
- Khi chuyển Visit hoặc đổi actor/mất phiên, bỏ state của panel cũ, không gắn thuốc/bản nhập sang Visit khác. Response cũ không cập nhật panel mới.

## API dùng lại

| Route | Mục đích |
| --- | --- |
| `GET /api/prescriptions/medicine-options?branchId=&q=` | Thuốc active/eligible, tối đa 50 kết quả mỗi từ khóa; metadata và lời dặn mặc định |
| `GET /api/prescriptions/visits/{visitId}/lines` | Dòng đơn đã lưu theo đúng Visit |
| `POST /api/prescriptions/visits/{visitId}/lines` | `{requestKey,expectedVersion:0,medicineId,quantity,instructions}` |
| `PUT /api/prescriptions/visits/{visitId}/lines/{lineId}` | Cùng body với version hiện hành của dòng |

Không đổi contract lưu/xác nhận/kết thúc phiếu khám, không nhét đơn thuốc vào `ExaminationContent` hoặc snapshot lịch sử khám. Backend vẫn kiểm quyền `examinations.manage` và scope chi nhánh theo service Triển hiện tại; trạng thái/doctor phụ trách dùng `allowedActions` của workspace cho việc mở form UI, không tuyên bố thêm guard backend mới. Không migration/seed dữ liệu development.

## Nghiệm thu

Browser mock: hoàn tất không kê đơn và không POST prescription; kê/sửa tại chỗ giữ kết luận chưa lưu; tìm thuốc; retry cùng key/MSG111 giữ dữ liệu; đơn đã lưu hiển thị khi mở lại; chỉ đọc khi đóng lượt; lỗi prescription không chặn kết luận; cảnh báo bản nhập khi kết thúc; không gọi API prescription cho người không có quyền. Hồi quy route riêng và phần in phiếu thu đang sửa trong workspace.

Browser/API/PostgreSQL thật trên schema riêng: tạo Visit qua đặt lịch/check-in/start; hoàn tất một ca không kê và một ca có kê; đọc lại dòng/đúng Visit, sửa lời dặn không đổi catalog và không làm đổi bản nháp khám. Báo riêng mock/thật; chỉ ghi kết quả sau exit code cuối.
