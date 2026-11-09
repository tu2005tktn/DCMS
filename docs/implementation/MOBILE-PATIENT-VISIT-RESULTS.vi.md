# Mobile bệnh nhân — Xem hình ảnh, kết quả khám và tài liệu theo lượt: kết quả code và bằng chứng

Người thực hiện: Ngô Minh Trí. Ngày 08/11/2026, nhánh `TriNM`, đã merge `main` `e5a81e2`. Đặc tả: [MOBILE-PATIENT-VISIT-RESULTS.vi.md](../specs/MOBILE-PATIENT-VISIT-RESULTS.vi.md).

## Thay đổi

| Phần | Nội dung |
| --- | --- |
| API bệnh nhân | `GET /api/patients/me/visits`, `/visits/{visitId}`, `/documents?visitId=`, `/documents/{id}/content` (`IOwnClinicalRecordService`, `OwnClinicalRecordService`)<br>Patient lấy từ liên kết tự thân VERIFIED, kiểm lại ở mọi request<br>Chỉ trả phiếu đã xác nhận, CLS chưa hủy (kết quả chỉ khi đã đọc), tài liệu Ready<br>Nội dung tệp kiểm SHA-256, trả `Cache-Control: no-store`, ghi audit `PatientViewed` |
| Mobile | Màn **Kết quả khám theo lượt** `(account)/visit-results`: danh sách phân trang, kéo làm mới, tải thêm<br>Màn **Chi tiết lượt** `(account)/visit-results/[visitId]`: kết quả theo răng/vùng, chẩn đoán, dặn dò, hướng xử trí, nhu cầu quay lại, CLS, link đơn thuốc, lưới ảnh và danh sách tài liệu<br>**Trình xem ảnh**: chuyển ảnh, phóng to/thu nhỏ, kéo khi phóng to, không thêm gói native<br>Thêm lối vào từ "Hồ sơ y tế của tôi"; thẻ "Nguồn chưa kết nối" của S41 cập nhật theo hiện trạng |
| Tải ảnh | `fetch` có token, hiển thị bằng data URI trong bộ nhớ, vì `<Image>` của Expo Go Android không gửi header `Authorization` (đã kiểm trên emulator) |

## Bằng chứng

| Loại | Kết quả |
| --- | --- |
| API + PostgreSQL thật | `OwnClinicalRecordApiTests` 1/1 (test chỉ để local), kiểm:<br>• chỉ phiếu đã xác nhận; ẩn ghi chú nội bộ và tiền sử<br>• tài liệu Ready, đọc đúng byte ảnh, có `no-store`<br>• PendingScan trả 409 MSG42; phiếu nháp 404<br>• bệnh nhân khác 404 ở mọi ID<br>• không liên kết / liên kết chờ / liên kết thu hồi: 403 MSG06<br>• nhân viên: 403<br>• `pageSize` > 50: 400 MSG04 |
| Emulator Android 12 AOSP (Expo Go 55.0.7), API thật trên schema `dcms_test_demo` | Dữ liệu giả tạo qua API thật:<br>• đăng ký → gửi liên kết → lễ tân xác minh<br>• check-in → khám, xác nhận → kết thúc lượt<br>• 3 ảnh PNG tự sinh và 1 PDF; trạng thái Ready đặt bằng SQL vì máy dev không có ClamAV<br><br>Kết quả trên app:<br>• danh sách hiện 1 lượt, "4 ảnh/tài liệu"<br>• chi tiết đủ mục và không có ghi chú nội bộ; lưới hiện 3 thumbnail; PDF hiện thông báo không xem được trong app<br>• trình xem "1 / 3" → phóng 200% → ảnh sau "2 / 3" (X-quang)<br>• thu hồi liên kết → danh sách chỉ còn banner liên kết, chi tiết không còn kết quả<br>• liên kết lại → hiện lại |
| Typecheck mobile | `npm run typecheck` đạt; Metro bundle Android 1349 module |

## Còn mở

- **Thumbnail phía server và trình xem PDF:** DCMS-104 / mobile. Hiện thumbnail là ảnh gốc thu nhỏ, tải lười theo trang 20.
- **Chưa kiểm trên emulator:** CLS đã đọc, có đơn thuốc, danh sách trên 20 lượt. API và UI đã có; dữ liệu giả lần này chưa gồm các trường hợp này.
- **Review contract:** người phụ trách DCMS-95 (own-record) và DCMS-104 (tài liệu) rà contract mục 3 của đặc tả.
