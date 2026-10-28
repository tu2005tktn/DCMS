# Dữ liệu giả lập để thử ứng dụng local

Ngày chuẩn bị: 28/10/2026. Mục tiêu là bổ sung dữ liệu gần tình huống phòng khám để thử web hiện tại, không thay đổi quy tắc nghiệp vụ.

- Tạo thêm 20 hồ sơ tổng hợp, tên Việt Nam, tuổi/giới tính/địa chỉ và lý do khám khác nhau, gồm trẻ em có người giám hộ. Mã `DEMO2810-*`, ghi chú rõ giả lập; số liên hệ giả và email `example.invalid`, không dùng thông tin bệnh nhân thật.
- Dự kiến 12 lịch hôm nay và 6 lịch trong các ngày tới; 2 hồ sơ chưa đặt lịch. Chỉ dùng slot do API availability trả về, không sửa giờ mở cửa hoặc lịch đã có.
- Ca hôm nay gồm chưa check-in, hàng chờ, đang khám/lưu nháp, có chỉ định, đã kết thúc khám và chờ thu tiền; chuẩn bị thu một phần, chuyển khoản chờ xác minh và đã thanh toán để thử sổ phí.
- Ghi dữ liệu qua API đang chạy tại 5080 để giữ validation, audit, reservation, snapshot, receipt và version. Check-in là thao tác mô phỏng trên bệnh nhân giả. Không ghi trực tiếp trạng thái hoàn thành vào database hoặc bỏ qua điều kiện consent.
- Danh mục CLS thêm riêng có nhãn DEMO; không sửa giá hay dịch vụ đang có. Phí khám ca giả dùng giá chốt có lý do, không đổi bảng giá chung. Không gửi email/SMS ra bên ngoài.
- Manifest ở `.tools/realistic-demo-20261028.json` (ignored), chỉ chứa dữ liệu tổng hợp/ID/link, không chứa token hoặc mật khẩu. Script checkpoint và request key ổn định để chạy tiếp khi gián đoạn; không tự reset ca sau khi người dùng đã thử.
- Kiểm chứng bằng đọc lại API: số hồ sơ/lịch/lượt, trạng thái khám, chỉ định và tổng phí/đã thu/còn nợ. Chỉ báo cáo số lượng thực sự tạo thành công. Hướng dẫn ca cụ thể được sinh cùng manifest sau khi hoàn tất.

Đây là dữ liệu tổng hợp lưu thật trong PostgreSQL local, không phải mock và không phải dữ liệu người thật. Không tuyên bố các phiếu/thu tiền giả là giao dịch vận hành thực tế.

## Kết quả đã tạo và kiểm tra

- 20 hồ sơ `DEMO2810-01` đến `DEMO2810-20`; 18 lịch: 12 ngày 28/10 và 6 ngày 29–31/10/2026. Hai hồ sơ chưa có lịch.
- 10 Visit: 4 Completed, 3 InProgress với phiếu nháp, 3 Queued. Hai lịch hôm nay còn chưa check-in để thử tiếp nhận.
- Hai dịch vụ DEMO riêng: phim quanh chóp 80.000đ và Panorama 200.000đ. Hai chỉ định: một đã thực hiện/nhận/đọc kết quả, một chờ xử lý. Một hồ sơ điều trị được tạo từ phiếu khám đã hoàn tất, chưa ghi nhận điều trị đã thực hiện.
- Bốn ca thu ngân: phí khám 100.000đ chưa thu; khám + phim tổng 180.000đ đã thu tiền mặt 50.000đ/còn 130.000đ; phí khám 100.000đ có chuyển khoản Pending chưa tăng số đã thu; phí khám 100.000đ đã thu đủ tiền mặt.
- API đọc lại đủ 20 hồ sơ, 18 lịch và trạng thái/sổ phí của 10 lượt. Trình duyệt Playwright dùng API thật xác nhận tìm `DEMO2810` thấy 20 hàng và bill Trần Quốc Bảo hiển thị dư 130.000đ. Ảnh local tại `.artifacts/realistic-demo/`.
- Hướng dẫn từng bệnh nhân và link mở nằm ở `.tools/realistic-demo-guide.md`. Script local `.tools/seed-realistic-demo.mjs` và manifest không chứa thông tin xác thực; mật khẩu chỉ nhận qua environment.
