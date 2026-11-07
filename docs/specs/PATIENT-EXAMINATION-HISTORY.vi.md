# Lịch sử khám tự động trong hồ sơ bệnh nhân

Cập nhật 02/11/2026; liên quan S06/UC-08, S19/S20 và kết thúc lượt khám.

## Hành vi

- Khi `POST /api/visits/{visitId}/complete` thành công, phiếu khám đã xác nhận của lượt đó phải xuất hiện ngay trong tab Khám của đúng hồ sơ bệnh nhân, với trạng thái lượt Đã kết thúc và thời điểm kết thúc thực tế. Không cần thao tác đính kèm/lưu hồ sơ riêng.
- Nguồn bệnh nhân là `Visit.PatientId`; phiếu `VisitExamination.VisitId` đã liên kết bền vững với lượt. Giữ cơ chế cập nhật Visit/Appointment, audit và request receipt trong transaction hiện có; không tạo bản sao phiếu hay hồ sơ điều trị. Gửi lại cùng request không sinh lịch sử trùng.
- Hoàn tất phiếu chưa đồng nghĩa kết thúc lượt. Phiếu xác nhận khi lượt còn InProgress vẫn hiện trong hồ sơ nhưng không được gắn nhãn đã kết thúc hay tự đặt thời điểm kết thúc.
- Sau khi kết thúc, màn khám xác nhận kết quả đã nằm trong hồ sơ và có liên kết trực tiếp tới tab Khám. Giữ các thao tác tiếp theo như thu ngân và lập kế hoạch điều trị.
- Tab Khám hiển thị tên/mã bệnh nhân ở đầu hồ sơ, ngày đến, bác sĩ, chi nhánh, trạng thái phiếu, trạng thái lượt, thời điểm kết thúc, chẩn đoán và hướng xử trí. Chọn Xem nội dung để mở bảng chi tiết thích ứng với màn hình, gồm triệu chứng, tiền sử/dị ứng/thuốc đang dùng, kết quả theo răng/vùng, kết luận, dặn dò, nhu cầu quay lại/chuyển khám và ghi chú. Liên kết mở phiếu gốc để xem toàn bộ tài liệu, CLS và đơn thuốc theo quyền hiện có.
- Có bộ lọc lượt đã kết thúc riêng với phiếu đã xác nhận. Nạp lại khi mở tab Khám, tải lại hồ sơ hoặc quay lại cửa sổ/tab trình duyệt để thấy lượt vừa kết thúc ở màn khác. Lỗi tải phải báo rõ và cho thử lại.
- Đính chính cập nhật phiên bản hiện hành của cùng phiếu trong lịch sử; các phiên bản trước tiếp tục truy cập ở phiếu gốc. Không thay đổi thời điểm kết thúc hoặc tạo dòng lượt mới.
- Giữ kiểm tra quyền đọc khám và phạm vi chi nhánh hiện có. Không lẫn dữ liệu bệnh nhân khác, không thêm các buổi điều trị không có phiếu khám vào lịch sử khám.

## Hợp đồng API

`GET /api/patients/{patientId}/examinations` giữ các trường hiện có và bổ sung `completedAt: string | null` cho từng dòng. Giá trị lấy từ thời gian audit `CompleteVisit` đã lưu của lượt Completed; null khi chưa kết thúc hoặc dữ liệu lịch sử không có audit, không suy diễn từ ngày đến/thời điểm sửa phiếu. Danh sách vẫn tối đa 100 lượt theo phạm vi được cấp quyền, có thông báo giới hạn trong giao diện.

## Kiểm chứng

- API/PostgreSQL: kết thúc -> truy vấn hồ sơ đúng bệnh nhân có một dòng Completed, nội dung/phiên bản chính xác và completedAt; trước kết thúc không có completedAt; retry không trùng; đính chính không đổi completedAt; bệnh nhân khác không chứa lượt này.
- Trình duyệt API thật: hoàn tất/kết thúc -> mở lịch sử đúng hồ sơ -> xem nội dung -> tải lại vẫn còn, mở lại phiếu gốc. Kiểm tra làm mới hồ sơ đang mở sau khi lượt kết thúc từ cửa sổ khác.
- UI: phân biệt phiếu xác nhận và lượt kết thúc, lọc đúng, hiển thị thông tin khám và xử lý lỗi tải.
