# Sao chép hạng mục kế hoạch trên phiếu điều trị

## Hành vi hiện hành
Bấm Sao chép trong menu ⋯ lưu ngay một hạng mục mới giống dòng nguồn, không mở hàng chỉnh sửa, không Modal và không cần ✓. Sau thành công bản sao hiển thị theo thứ tự ngày hiện có và được cộng vào dự toán. Dòng nguồn không bị sửa.

Giữ ngày/răng/dịch vụ/phút/ghi chú/bác sĩ/phụ tá/chi phí/loại-giá trị-lý do giảm giá và quantity kỹ thuật/đơn giá. Không lấy lại giá catalog. Bản sao có ID riêng, chưa thực hiện/chưa hoàn tất, không sao chép quyết định bệnh nhân, actual, ảnh hoặc tiền đã trả. “Hoàn thành thao tác sao chép” không phải đánh dấu hạng mục hoàn tất.

## Tích hợp
Tái sử dụng luồng thêm hàng hiện có: lưu draft Plan rồi confirm-plan ngay trong cùng thao tác người dùng. Không thêm API/schema. Khóa gửi lặp trong khi đang sao chép. Giữ ID, snapshot và requestKey của từng bước khi lỗi; nút Thử lại sao chép tiếp tục thao tác cũ, không tạo thêm dòng. Khi MSG111, cho đọc phiên bản mới trước thử lại; nếu dòng cùng ID đã tồn tại thì coi thao tác đã lưu, không thêm lần nữa. Không hiện thành công khi mới chỉ lưu draft. Bác sĩ vẫn dùng Sửa/Xóa hiện có sau khi bản sao đã được lưu. Menu bị khóa nếu đang thêm/sửa hoặc gửi command khác. Các kiểm quyền/chi nhánh/dịch vụ/nhân sự/giảm trừ giữ ở server như luồng nhập tay.

## Kiểm thử
API mock: bấm một lần tạo ngay bản sao đầy đủ, không hiện editor/xác nhận; nguồn không đổi, bản mới không gạch ngang; giữ giá khác catalog, quantity và giảm trừ; tổng/ngày/reload đúng; lỗi mạng trước/sau lưu draft và retry cùng key; MSG111 đọc lại version rồi thử lại; khóa bấm lặp và không tạo actual. Web build và hồi quy phiếu. Không ghi dữ liệu thử vào database development.
