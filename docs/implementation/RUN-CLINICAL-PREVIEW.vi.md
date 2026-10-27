# Xem thử S22/S63 bằng dữ liệu giả

## Mục đích và phạm vi

Đường dẫn `/__preview/clinical` chỉ xuất hiện khi chạy Vite ở chế độ development. Nó dùng chính form Web S22 và S63, với Patient, Visit InProgress, dossier, item đã đồng ý, step và activity nguồn hoàn toàn giả lập trong bộ nhớ trình duyệt. Trang luôn ghi rõ đây là dữ liệu giả. Không cần đăng nhập, không chạy migration, không seed Patient/Visit và không gửi bất kỳ request API nào tới backend từ đường dẫn này.

Các thao tác Lưu nháp, Xác nhận đã làm và Lưu đánh giá cập nhật bộ dữ liệu giả của tab hiện tại. Chuyển giữa S22/S63 giữ trạng thái; tải lại trang bắt đầu lại kịch bản. Chế độ này không chứng minh phân quyền, transaction, consent, ledger, lab hay tính đúng của API thật. Chạy API thật và PostgreSQL cách ly qua integration test để kiểm các phần đó; chỉ nghiệm thu đầu cuối khi chủ Visit/S20/DCMS-101 nối adapter thật.

## Cách dùng

Trong `web/`, chạy `npm run dev`, sau đó mở `http://127.0.0.1:5173/__preview/clinical`. Thử lưu một hoạt động trám răng 25 ở S22, chọn hoàn tất bước, chuyển sang S63 để đánh giá hoạt động đó. Đánh giá chỉ lưu review; muốn ghi chăm sóc thực tế thì quay lại S22 và xác nhận riêng. Có thể kiểm thử hạng mục chữa tủy 46 ở trạng thái mới làm một phần. Chọn “Tính phí riêng” trong bản mock sẽ báo nguồn tài chính chưa kết nối, giống ranh giới fail-closed của API hiện tại.

## Hợp đồng an toàn

Khi URL bắt đầu bằng `/__preview/clinical` và `import.meta.env.DEV` là true, HTTP client Web chuyển mọi lời gọi `/api/` vào adapter giả lập nội bộ; đường dẫn không được adapter nhận diện trả lỗi cục bộ, không fallback sang API thật. Build production không đăng ký route này và không kích hoạt adapter giả lập. Không dùng dữ liệu giả này làm hồ sơ bệnh nhân hay bằng chứng nghiệm thu nghiệp vụ.
