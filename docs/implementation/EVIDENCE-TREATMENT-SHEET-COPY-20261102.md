# Sao chép hạng mục kế hoạch — 02/11/2026

Đặc tả: docs/specs/TREATMENT-SHEET-COPY.md, cập nhật trước implementation.

Thay đổi Web: TreatmentSheet thêm menu Sao chép, tái sử dụng ánh xạ hàng Sửa với ID mới và không đặt itemId. Hàng mở cuối bảng ở trạng thái nhập, giữ ngày/răng/dịch vụ/phút/ghi chú/bác sĩ/phụ tá/chi phí/loại-giá trị-lý do giảm giá cùng quantity kỹ thuật. Không mang trạng thái gạch ngang hoặc nguồn actual. Chỉ ✓ dùng luồng thêm draft/confirm-plan hiện có; × trước lưu không gọi command. Menu khóa khi đang nhập/gửi theo quy tắc hiện tại. Không đổi API/schema/backend.

Hai ca UI API mock mới đạt: copy đầy đủ và hủy không ghi/giữ nguồn; copy nguồn gạch ngang, sửa ngày/note, MSG111 giữ form, retry cùng key, tạo ID riêng, quantity cũ 2/đơn giá/chi phí/giảm giá không đổi, nguồn không đổi, tổng và sắp ngày đúng, reload giữ trạng thái mới, không tạo actual. Mock confirm-plan cập nhật projection đủ trường trả về để kiểm lại dữ liệu sau lưu.

API/database health healthy, Web HTTP200. Không tạo dữ liệu thử trong development, không migration, chưa chạy Git. Đang chạy build và hồi quy toàn bộ phiếu; kết quả được bổ sung sau khi hoàn tất.

Web build đạt (còn cảnh báo chunk size/annotation SignalR hiện có). Lượt hồi quy 24 ca: 23 đạt, 1 ca cũ thất bại vì kỳ vọng 4 mục menu thay vì 5 sau bổ sung Sao chép; đã cập nhật assertion. Chạy lại riêng ca đó và hai ca sao chép: 3/3 đạt (21.5s), trong đó nguồn có giá khác catalog vẫn giữ đúng chi phí. Không tuyên bố một lượt 24/24. Không thay implementation sau build, chỉ cập nhật test và bằng chứng. Không sửa backend, áp migration hoặc chạy Git. Localhost vẫn phục vụ Web/API như kiểm health ở trên.

## Điều chỉnh lưu ngay sau yêu cầu mới
Sao chép nay gọi lưu Plan draft và confirm-plan ngay khi bấm menu, không mở editor/Modal và không cần ✓. Dùng planRow chung với Sửa để giữ dữ liệu/quantity/đơn giá. Khóa thao tác bằng actionGuard/busy, giữ snapshot/ID/requestKey của từng bước khi lỗi; Thử lại tiếp tục cùng thao tác. MSG111 có đọc phiên bản mới rồi thử lại; nếu ID đã có trong phiếu thì kết thúc thay vì tạo lại. Response cập nhật dossier/dự toán ngay, sau đó tải lại. Dòng mới chưa thực hiện/chưa hoàn tất, nguồn không đổi.

Web build đạt, còn cảnh báo SignalR/chunk size hiện có. 4/4 ca mock Sao chép mới đạt (25.8s): lưu ngay và reload/giá nguồn khác catalog/quantity 2/giảm %, retry lỗi mạng cùng ID-key, MSG111 đọc version, khóa bấm lặp và lần sao chép chủ động kế tiếp tạo ID mới. Không có editor hoặc bước xác nhận. API/database health healthy, Web HTTP200. Không đổi backend, migration, dữ liệu development hoặc Git.

Hồi quy nhập/sửa/xóa/thực hiện/đánh dấu hạng mục: 4/4 ca đạt (34.3s). Tổng 8 ca liên quan đạt qua hai lượt; không chạy lại toàn bộ suite trong lần điều chỉnh này.
