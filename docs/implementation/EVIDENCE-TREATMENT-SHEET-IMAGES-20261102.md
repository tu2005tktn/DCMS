# Bằng chứng sửa tải ảnh tại phiếu — 02/11/2026

Đã cập nhật đặc tả TREATMENT-SHEET-IMAGES.md trước code. Thay Link trong TreatmentSheet bằng PatientImageGallery: nút viền nét đứt mở native file picker ngay tại phiếu, JPEG/PNG, tự gửi từng ảnh sau chọn với patientId/dossierId cố định, requestKey ổn định khi retry, không navigate. Cùng gallery ở tab Hình ảnh đọc ClinicalDocument và imagesOnly trước phân trang. Nội dung Ready được fetch có xác thực, blob URL được thu hồi; PendingScan/Rejected chỉ hiển thị trạng thái. Upload và reload gallery không tải lại dossier hoặc ghi chú/hàng đang nhập.

API/document model cho VisitId nullable khi có dossier hợp lệ; kiểm Patient/branch/context trước receipt, giữ kiểm nguồn Visit nếu có. Review nhận nguồn chi nhánh từ dossier khi không có Visit. Migration 20261102082650_DossierImages chỉ đổi nullable VisitId và thêm check Visit hoặc dossier. Down từ chối khi có tài liệu không Visit, không gán Visit giả.

Backend solution build đạt, không warning/error. Web build đạt, còn cảnh báo chunk size/SignalR hiện có. 73/73 integration test ExaminationApiTests đạt trên database riêng dcms_images_regression_20261102_b72d19; database đã drop. Ca mới kiểm upload không Visit, scope nguồn, replay cùng ID, list phiếu/list patient, content xác thực, sai patient, thiếu nguồn và key khác payload. Ca này trước đó cũng đạt riêng trên dcms_images_verify_20261102_a17c45 rồi database đã drop.

5/5 test mock mới về native picker/upload/cùng tab, retry cùng key, không đọc ảnh PendingScan, bàn phím và responsive 390/768/1440 đạt. Các test dùng API mock, không tạo tài liệu/bệnh nhân vào dcms_dev.

Chưa áp migration development, chưa seed, chưa chạy Git. API đã tạm dừng để bỏ khóa DLL khi build. Localhost chưa cấu hình ClamAV và chưa có Docker image clamav/clamav:stable; muốn ảnh chuyển Ready cần chạy dịch vụ quét hiện có, không bỏ qua quét. Cần duyệt riêng sao lưu/migration trước chạy API mới để người dùng test tích hợp.

Hồi quy toàn bộ patient-treatment-ui.spec.ts: 22/22 ca đạt trong một lượt (2.4 phút), bao gồm 5 ca ảnh mới và các thao tác nhập/sửa/xóa/chuyển thực hiện, ghi chú và vật liệu hiện có.

Sau phê duyệt riêng của người dùng: đã tạo backup dcms-before-dossier-images-029d3c8862b94789b53ac40d5c0d3887.dump tại thư mục LocalAppData/DCMS/backups, 12176466 bytes, SHA256 2D7DC95E437E3DB59CC3399F390C1AB2A386331696AE25927E5EA6F097BAFAD0. Đã áp đúng 20261102082650_DossierImages vào dcms_dev; truy vấn xác nhận VisitId nullable YES. Đã chạy ClamAV official Docker (healthy), cấu hình .env cục bộ host localhost/3310 và chạy API session 36388. /health trả api/database healthy, Web HTTP200. Không upload tệp thử hoặc tạo bệnh nhân development. Không chạy Git. Công cụ kết nối tab IAB báo không khả dụng, chưa tự reload được tab của người dùng; hướng dẫn Ctrl+F5 để xem bản mới.
