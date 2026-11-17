# Người thân trên ứng dụng mobile — 16/11/2026

Ứng dụng mobile của bệnh nhân có thêm mục **Người thân**, dùng nguyên các API đã có cho web khách hàng. **Không đổi backend, không thêm bảng, không thêm migration.** Quy tắc nghiệp vụ giữ theo [Người thân và đặt lịch hộ](CUSTOMER-FAMILY-MEMBERS.md) và [Đặt lịch và xem hồ sơ người thân](FAMILY-DELEGATED-RECORD-ACCESS.vi.md); tài liệu này chỉ mô tả phần màn hình mobile.

Chỉ tài khoản bệnh nhân (`Public`) thấy mục này. Việc nhân viên duyệt yêu cầu xem hồ sơ vẫn làm trên web (`/link-verification`), không có trên mobile.

## Bước 1 — Danh sách người thân và đặt lịch hộ

### Lối vào và đường dẫn

- Màn **Tài khoản** thêm mục **Người thân**, nằm sau "Lịch hẹn".
- `/(account)/family-members` — danh sách.
- `/(account)/family-members/form` — thêm mới; kèm `?id={uuid}` là sửa.
- `/(account)/family-members/{id}` — chi tiết một người thân.

### Danh sách

- Gọi `GET /api/me/family-members?keyword=&page=&pageSize=20`. Ô **Tìm theo họ tên** (tối đa 100 ký tự) gửi `keyword` sau khi ngừng gõ 300 ms. Còn dữ liệu thì có nút **Xem thêm** tải trang kế và nối vào danh sách; kéo xuống để tải lại từ đầu.
- Mỗi người thân là một thẻ: họ tên, quan hệ, ngày sinh, giới tính, số điện thoại. Thiếu thông tin hiện "Chưa cung cấp". Bấm thẻ mở chi tiết.
- Trạng thái: đang tải, trống ("Chưa có người thân phù hợp. Bạn có thể thêm người thân mới."), lỗi kèm **Thử lại**. Nút **Thêm người thân** luôn hiện.
- Quay lại màn danh sách sau khi thêm, sửa hoặc xóa thì danh sách tự tải lại.

### Thêm và sửa

- Trường: Họ tên (bắt buộc, 1–100 ký tự), Quan hệ (bắt buộc, chọn một trong Bố, Mẹ, Vợ/Chồng, Con, Anh/Chị/Em, Ông/Bà, Khác), Ngày sinh (không bắt buộc, nhập `DD-MM-YYYY`, không sau hôm nay theo giờ Việt Nam), Giới tính (bắt buộc: Nam, Nữ, Khác), Số điện thoại liên hệ (bắt buộc, di động Việt Nam 10 số bắt đầu 03/05/07/08/09).
- Kiểm tra tại ứng dụng trước khi gửi và báo lỗi ngay dưới trường. Lỗi `FAMILY_MEMBER_VALIDATION` từ máy chủ được gắn vào đúng trường theo `field`.
- Thêm gọi `POST /api/me/family-members`, sửa gọi `PUT /api/me/family-members/{id}` kèm `expectedVersion`. Mỗi lần gửi có `requestKey`; gửi lại đúng nội dung cũ sau lỗi mạng dùng lại khóa cũ, đổi nội dung thì sinh khóa mới. Nút Lưu bị khóa trong lúc đang gửi.
- Lỗi 409 `MSG111` (dữ liệu đã đổi ở nơi khác): giữ nguyên nội dung đang nhập, hiện thông báo và nút **Tải bản mới nhất** để nạp lại `version` rồi lưu lại.
- Sửa họ tên hoặc ngày sinh sẽ chấm dứt yêu cầu/quyền xem hồ sơ đang có của người thân đó (quy tắc của máy chủ). Form sửa nhắc điều này bằng một dòng chú thích.
- Lưu thành công thì quay về màn trước.

### Chi tiết và xóa

- Hiện thông tin người thân và ba hành động: **Đặt lịch khám**, **Sửa**, **Xóa khỏi danh sách**.
- Xóa hỏi xác nhận ngay trên màn hình ("Bạn có muốn xóa người thân này khỏi danh sách không?" — Đồng ý / Không đồng ý), không dùng hộp thoại hệ thống để chạy được cả trên bản web của Expo. Gọi `DELETE /api/me/family-members/{id}` với `{expectedVersion, requestKey}`; xong thì quay về danh sách.
- Người thân không còn tồn tại (404) hiện thông báo và nút quay về danh sách.

### Đặt lịch cho người thân

- Trong màn Đặt lịch, khi đã đăng nhập bằng tài khoản bệnh nhân và chọn **Người thân**: người khám **chỉ được chọn từ danh sách người thân đã lưu** (như web). Chưa chọn thì không bấm được Tiếp tục. Chưa có người thân nào thì hiện hướng dẫn và nút **Thêm người thân**.
- Yêu cầu đặt lịch gửi thêm `familyMemberId`; họ tên và ngày sinh người khám lấy từ người thân đã chọn và **chỉ đọc** ở bước Thông tin. Người đặt và liên hệ vẫn là chủ tài khoản.
- Mở màn Đặt lịch từ chi tiết người thân (`/book-appointment?familyMemberId={uuid}`) thì chọn sẵn người đó; ứng dụng đọc lại người thân từ API trước khi chọn, không tin dữ liệu trên đường dẫn. Không tìm thấy thì báo và cho chọn lại.
- Khách chưa đăng nhập vẫn nhập tay họ tên người khám như trước, không có `familyMemberId`.

## Bước 2 — Lịch đặt hộ và hồ sơ của người thân

Nằm trong màn chi tiết người thân, bên dưới phần thông tin.

### Lịch đặt hộ

- `GET /api/me/family-members/{id}/appointments?page=&pageSize=10`: mã lịch, thời gian, trạng thái, chi nhánh, nha sĩ. Bấm một lịch mở chi tiết lịch hẹn sẵn có (`/(account)/appointments/{code}`). Không cần quyền xem hồ sơ.

### Quyền xem hồ sơ

- `GET /api/me/family-members/{id}/record-access` cho trạng thái hiện tại: Chưa có hồ sơ liên kết, Chờ phòng khám xác minh, Đã được cấp quyền, Bị từ chối, Đã thu hồi, Đã rút yêu cầu, Đã hết hạn. Có nút **Tải lại trạng thái**; ghi chú công khai của phòng khám (nếu có) hiện ngay dưới trạng thái.
- Giải thích cho người dùng: hồ sơ tự hiển thị sau khi phòng khám tiếp nhận đúng người thân từ lịch đặt hộ; không cần gửi yêu cầu cho trường hợp đó.
- Khi `canRequest = true` có mục **Liên kết hồ sơ đã có tại phòng khám**: Mã hồ sơ người thân (không bắt buộc, tối đa 40 ký tự) và Thông tin giúp phòng khám đối chiếu (không bắt buộc, tối đa 1000 ký tự); gửi `POST .../record-access/requests`.
- Đang Chờ xác minh hoặc Đã được cấp quyền thì có nút **Rút yêu cầu** / **Chấm dứt quyền truy cập**, hỏi xác nhận trên màn hình rồi gọi `POST .../requests/{requestId}/withdraw` với `expectedVersion`.
- Mỗi thao tác ghi có `requestKey` theo quy tắc như form người thân. Có **Lịch sử yêu cầu** khi máy chủ trả về.

### Nội dung hồ sơ

- Chỉ khi trạng thái là Đã được cấp quyền: gọi `GET .../record` lấy bệnh nhân, chi nhánh, thời hạn và các phạm vi được cấp. Mỗi phạm vi là một tab; phạm vi không được cấp thì không có tab.
- Tab gọi `GET .../record/{section}?page=&pageSize=10` với `section` là `examinations`, `treatment`, `prescriptions`, `documents`; có **Xem thêm** khi còn trang.
  - Lịch sử khám: thời gian, nha sĩ, chi nhánh, triệu chứng, chẩn đoán, kết luận, lời dặn.
  - Điều trị: mã phiếu, ngày, chẩn đoán, từng dịch vụ với vùng răng, tiến độ và ngày dự kiến. Không có giá tiền.
  - Đơn thuốc: ngày, chẩn đoán, từng thuốc với liều dùng, lời dặn.
  - Ảnh và tài liệu: tên, loại, ngày, dung lượng. Tệp ảnh mở xem ngay trong ứng dụng; nội dung tải bằng token của phiên qua `GET .../record/documents/{documentId}/content` và chỉ giữ trong bộ nhớ. Tệp không phải ảnh (ví dụ PDF) chưa mở được trên mobile, ứng dụng nói rõ là xem trên web khách hàng.
- Luôn phân biệt **chưa được cấp quyền** với **hồ sơ rỗng** ("Chưa có nội dung chính thức trong phạm vi đã cấp.").
- Máy chủ trả 403 hoặc 404 ở bất kỳ lần gọi nào thì ứng dụng xóa nội dung hồ sơ đang hiện và tải lại trạng thái quyền. Trả 401 thì đăng xuất như các màn khác. Đăng xuất hoặc rời màn hình là nội dung hồ sơ mất theo trạng thái màn hình; không lưu xuống máy.

## Kiểm chứng

- API: đã có test tích hợp của backend cho toàn bộ các route trên; lần này không đổi backend.
- Mobile: kiểm tra kiểu (`npm run typecheck`), chạy bản web của Expo với **API thật và PostgreSQL thật** bằng tài khoản demo, đi qua thêm/sửa/xóa người thân, đặt lịch hộ và xem trạng thái quyền. Phần xem nội dung hồ sơ cần một quyền đã được nhân viên cấp; ghi rõ phần nào đã kiểm bằng dữ liệu thật và phần nào chưa.
- Chưa kiểm trên thiết bị Android/iOS thật trong lượt này trừ khi có ghi chú khác.
