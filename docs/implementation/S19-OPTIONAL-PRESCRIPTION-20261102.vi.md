# Kê đơn tùy chọn trong kết luận khám — 02/11/2026

Đặc tả cập nhật trước implementation: [S19-OPTIONAL-PRESCRIPTION.vi.md](../specs/S19-OPTIONAL-PRESCRIPTION.vi.md).

## Kết quả

- Tab **Tổng kết khám**, dưới hướng xử trí, có **Đơn thuốc (không bắt buộc)**. Chưa kê thì mở bằng nút Kê đơn thuốc; đơn có dữ liệu tự hiển thị.
- `PrescriptionPanel` dùng chung cho màn khám và trang `/visits/:visitId/prescription`, dùng API kê đơn Triển. Không tải lại khám sau khi lưu thuốc, nên bản nhập kết luận/dặn dò không bị thay thế. Trang riêng lấy chi nhánh từ workspace khám, bỏ phụ thuộc gọi bảng thanh toán.
- Tìm thuốc phía server, lấy lời dặn mặc định, lưu/sửa từng dòng, retry giữ request key, MSG111 cho đọc lại version mà giữ form. Thu gọn không xóa form/đơn; bỏ phần nhập không xóa dòng đã lưu.
- Có thể xác nhận/kết thúc khám không kê thuốc. Lỗi tải đơn không chặn phần khám. Xác nhận kết luận giữ phần kê chưa lưu; kết thúc lượt có lựa chọn quay lại kê hoặc bỏ phần chưa lưu. Đơn đã lưu giữ nguyên.
- Chỉ mở form khi có `examinations.manage`, Visit InProgress và workspace cho save/complete. Sau kết thúc chỉ xem. Assistant không gọi API prescription từ phần Tổng kết.
- Không thay backend/API/schema; không đưa prescription vào `ExaminationContent`, không đổi tiền sử thuốc đang dùng hoặc danh mục thuốc, không bổ sung xóa dòng mà API chưa có. Đây là điều kiện UI từ workspace, không phải bổ sung guard trạng thái mới ở backend.

## Bằng chứng đã hoàn tất

| Kiểm tra | Kết quả |
| --- | --- |
| Web production build | Đạt; còn warning SignalR annotation/bundle size đã có |
| Browser mock | **14/14 đạt**: 6 ca integration khám/kê đơn, 3 ca hồi quy in phiếu thu, 5 ca sidebar/permission/responsive |
| Browser với API/PostgreSQL thật | **3/3 đạt**: không kê đơn vẫn xác nhận/kết thúc; kê và sửa tại kết luận rồi kết thúc giữ đúng đơn; hồi quy khám/ghi phí/thu một phần/xác nhận chuyển khoản |
| API health | API/database healthy trên 5084 |
| Git whitespace/local work | `git diff --check` đạt; giữ nguyên file local REALISTIC-DEMO-DATA.vi.md theo SHA-256 trước pull |

Browser mock kiểm tra bản nhập kết luận không đổi khi thêm/sửa thuốc, retry sau mất response không tạo dòng thứ hai, MSG111 giữ input, đơn cũ tự hiển thị, read-only sau kết thúc, không gọi API với Assistant, cảnh báo rời trang và lựa chọn bỏ phần thuốc chưa lưu mà vẫn kết thúc được. Trường kê đơn không được đưa vào validation của kết luận.

Browser thật chạy trên schema synthetic riêng `dcms_reception_e2e_84a40bda31554e048d55d2fba6b2419d`, API 5084/web 5173, dùng quy trình API tạo bệnh nhân → đặt lịch → check-in → bắt đầu khám → lưu nháp. Ca có thuốc xác nhận API dòng đơn gắn đúng Visit, số lượng 2/version 2 sau sửa, lời dặn catalog không đổi, dặn dò đang nhập vẫn còn và chỉ được ghi khi xác nhận khám. Đọc lại sau reload/kết thúc giữ một dòng đã kê. Không migrate/seed dữ liệu development mặc định.

Đã xem ảnh Tổng kết có đơn thuốc ở 1440 và 390px; bảng cuộn trong vùng, không tràn ngang toàn trang. Không dùng dữ liệu bệnh nhân thật. Lượt này không chạy lại toàn bộ backend suite vì backend không đổi; không diễn giải kết quả trên thành toàn bộ dự án đã xanh (hai test auth và các test cũ đã ghi trong báo cáo sau merge vẫn nằm ngoài phạm vi).

Bằng chứng ignored trong `.artifacts/verification-20261102/`:

- `prescription-integration-browser.json` / `.log` và thư mục output tương ứng.
- `prescription-real-browser.json` / `.log`; ảnh `optional-no-prescription.png`, `optional-prescription-1440.png`, `optional-prescription-390.png` trong thư mục output.

Test duy trì trong repo: `web/tests/examination-prescription.spec.ts` và `web/tests/examination-prescription.integration.spec.ts`. Test API thật yêu cầu biến `E2E_API_URL` và `E2E_DEMO_PASSWORD` cùng fixture cách ly.
