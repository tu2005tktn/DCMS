# Giá thiết lập sẵn cho khám và cận lâm sàng

> **Đã gỡ ngày 13/11/2026.** Phân hệ tài chính đã được xóa khỏi backend, web và database theo [CARE-LAB-FINANCE-STORAGE.vi.md](CARE-LAB-FINANCE-STORAGE.vi.md). Tài liệu này chỉ còn giá trị tham khảo khi đặc tả lại chức năng; không mô tả hành vi hiện tại.

Quyết định người dùng 02/11/2026: chỉ chỉnh phần khám/CLS, phòng khám thiết lập giá cụ thể từ trước để minh bạch. **Không chỉnh phần điều trị của Vinh**, không thêm khoảng giá điều trị trong thay đổi này.

## Quy tắc giá danh mục

- Khám và mỗi dịch vụ CLS dùng **một đơn giá VND theo đơn vị tính**, không có giá từ–đến. Tạo/sửa danh mục phải nhập giá nguyên 0–1.000.000.000.000; bỏ trống trả 400 MSG02 `field=priceVnd`. `0` là chủ động cấu hình miễn phí theo bảng giá, khác với chưa có giá.
- Phòng khám thiết lập qua `/clinical-services`: khối Dịch vụ và giá khám và danh mục CLS. UI gọi giá là **Giá khám/Đơn giá**, không gọi là khoảng giá hoặc giá tham chiếu để ngỏ. Cột giá và lựa chọn dịch vụ hiển thị rõ đơn giá/đơn vị; 0 ghi rõ miễn phí.
- Không seed giá thực tế, không tự gán 0 cho dữ liệu cũ, không sửa giá/phí/snapshot đã ghi. Danh mục cũ `priceVnd=null` vẫn đọc được với nhãn **Chưa thiết lập giá**, người có quyền phải sửa trước khi sử dụng để ghi khoản phí mới.
- Quyền, requestKey, optimistic version và snapshot giá hiện hành giữ nguyên. Cùng DTO `priceVnd` là nguồn giá của frontend và backend; không tạo bảng giá khác và không sửa API điều trị.

## Áp dụng vào lượt khám/CLS

- Khi chọn dịch vụ, tự điền giá đã thiết lập; nếu có duy nhất một dịch vụ CLS phù hợp thì chọn sẵn dịch vụ/giá. Hiển thị **Giá bảng** riêng với **Đơn giá áp dụng** để biết giá gốc. Chọn dịch vụ khác cập nhật giá theo danh mục.
- Giá đã thiết lập không bắt nhân viên gõ lại. Các ngoại lệ giá theo từng ca đã có trong hệ thống vẫn cần quyền và căn cứ như trước; thay đổi này không tạo chính sách giảm trừ mới hoặc sửa phần điều trị.
- CLS chưa được thiết lập giá không được lập phí bằng cách truyền giá thỏa thuận trực tiếp; báo cần thiết lập danh mục trước. Cả giá bảng 0 và giá khác 0 đều đi từ giá đã thiết lập; miễn phí đúng bảng không cần người dùng nhập thêm căn cứ chỉ để xác nhận lại giá 0. Khác giá bảng vẫn phải có căn cứ, giữ nguyên snapshot khi ghi.
- Khám cũ/chưa cấu hình vẫn có thể kết thúc lâm sàng; phí ở trạng thái chưa xác định, không tạo charge 0 và không coi là hết nợ. Muốn chốt phí sau phải thiết lập giá danh mục trước; không dùng giá nhập tùy ý để bỏ qua bước cấu hình. Không đặt điều kiện kê đơn hoặc đã thu đủ tiền cho kết thúc khám.
- Giá catalog thay đổi không tự tính lại chỉ định/charge/phiếu thu đã chốt. Version cũ khi người dùng ghi lại phải theo cơ chế MSG111 hiện tại.

## Contract giữ và validation mới

`POST/PUT /api/finance/services[/id]` và `POST/PUT /api/finance/examination-services[/id]` giữ shape hiện tại (`priceVnd` nullable trong DTO để đọc dữ liệu lịch sử), nhưng lệnh tạo/sửa mới không nhận null/thiếu giá. Range/giới hạn tiền vẫn do server kiểm.

Không migration hoặc backfill dữ liệu. Khi quote CLS hoặc chốt phí khám từ danh mục cũ null, server yêu cầu thiết lập giá; UI chỉ dẫn sang danh mục. Luồng ExternalSelfPay/ExistingResult không có khoản thu tại phòng khám và không bị ép gắn một dịch vụ tính phí.

## Kiểm tra

- API/PostgreSQL: null/thiếu/âm/vượt giới hạn không ghi; giá 0 hợp lệ; giá cụ thể lưu/đọc lại/retry; giá thiết lập được dùng đúng khi ghi phí; legacy null không được bỏ qua bằng giá truyền từ client; snapshot cũ không đổi; kết thúc khám chưa cấu hình vẫn Pending.
- Browser mock: field giá bắt buộc, hiển thị giá/đơn vị và miễn phí; tự điền giá từ lựa chọn, dịch vụ null không được chọn để lập phí, không nhập lại giá khi kết thúc khám.
- Browser API thật trên schema riêng: thiết lập giá khám/CLS → chọn/thực hiện → ghi phí đúng giá; hồi quy optional prescription và in phiếu thu. Không thay dữ liệu development và phần điều trị.
