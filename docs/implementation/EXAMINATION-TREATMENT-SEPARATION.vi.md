# Tách khám và điều trị nhiều buổi — 27/10/2026

Áp dụng [đặc tả S19/S20 đã cập nhật](../specs/S19-S20-DCMS-112.md) và [tiến trình điều trị](../specs/TREATMENT-PROGRESSION.vi.md).

Lỗi gốc: Complete kiểm ProceedToTreatment phải có ClinicalActivity Posted; đồng thời UI truyền Visit khám vào điều trị như buổi thực hiện. Điều này đảo thứ tự nghiệp vụ và buộc một lượt khám gánh toàn bộ đợt điều trị.

Đã thay đổi:

- Hoàn tất phiếu → kết thúc lượt khám, không yêu cầu điều trị đã làm. Giữ điều kiện xác nhận khám, công việc nháp/CLS, quyền và concurrency. Không bắt trả hết tiền.
- Sau kết thúc khám có **Lập kế hoạch điều trị**. Handoff truyền sourceExaminationVisitId, không truyền visitId cho actual. Màn kế hoạch dùng kết quả khám làm nguồn; không dùng lượt khám cũ làm buổi điều trị.
- Tạo hồ sơ từ khám kiểm Completed/Confirmed, Patient/branch/version; lưu tham chiếu và snapshot khám bất biến. Đính chính khám về sau không ghi đè nguồn đã chốt. Hồ sơ cũ không nguồn vẫn hợp lệ.
- Hồ sơ có **Các buổi điều trị** từ Visit thật liên kết qua tiếp nhận/hoạt động cũ. Mỗi buổi có trạng thái và bắt đầu/kết thúc riêng. Các buổi chưa tiếp nhận không được tạo giả; dùng đặt lịch + check-in với mục đích Điều trị tiếp và chọn hồ sơ hiện có.
- Bắt đầu/kết thúc cập nhật Visit/Appointment/audit/receipt trong transaction có khóa chung, kiểm đúng người phụ trách/branch/patient/course/version. Chặn đóng buổi có actual nháp hoặc CLS chưa xử lý. Không đổi state/plan của hồ sơ hoặc tự sinh phí.
- Actual API từ chối Visit khám ban đầu/khám vấn đề mới. Điều trị thực hiện dùng buổi điều trị đã tiếp nhận. Nguồn dữ liệu lâm sàng/phiếu khám/hoạt động cũ không bị di chuyển/xóa.
- Link tiếp nhận của Visit Điều trị tiếp mở hồ sơ và danh sách buổi, không đưa vào màn Khám để nhập lại phiếu khám mới.

Migration TreatmentExaminationSource chỉ thêm ba cột nullable + index/FK Restrict cho nguồn khám; không backfill giả, không thay trạng thái/tiền/tệp cũ.

Kiểm chứng: **112/112 API** trên PostgreSQL riêng, bao gồm khám, tài chính, hồ sơ/điều chỉnh/tiến trình điều trị và các ca tách buổi. **11/11 browser** thật đạt: kết thúc khám → tạo đợt từ nguồn khám → đặt lịch/check-in hai buổi → kết thúc buổi đầu, buổi hai vẫn mở; hồi quy các luồng khám còn lại. Backend build và web TypeScript/Vite build đạt. Giá/nguồn kiểm thử ở schema riêng, không seed vào dữ liệu manual. Kiểm tra nguồn và việc tách danh sách Khám/Buổi điều trị được chạy lại sau rà soát cuối.

Bằng chứng local: `.artifacts/separate-exam-final-tests.log`, `.artifacts/separate-exam-browser.log`, `.artifacts/separate-exam-web-build.log`; ảnh `separate-exam-two-treatment-sessions.png` dưới `.artifacts/separate-exam-browser/`. Các cảnh báo package Pkcs/bundle là cảnh báo nền chưa sửa trong task này.

Giới hạn giữ nguyên: cổng đồng ý điều trị/thông tin sẵn sàng một số phân hệ chưa được triển khai đầy đủ; không bỏ các điều kiện này để tạo bằng chứng điều trị giả. Kiểm chứng buổi điều trị ở đây là lifecycle các Visit thật và nguồn hồ sơ, không phải nghiệm thu toàn bộ consent/actual/thu tiền/đặt lịch từ bước điều trị. Booking buổi điều trị vẫn dùng các cơ chế lịch/tiếp nhận hiện có.

Bản chạy manual đã áp dụng migration `20261027153004_TreatmentExaminationSource` trên schema public đang dùng; số Visit/VisitExamination/ClinicalDocument trước-sau không đổi. API 5084 và database healthy, web 8084 HTTP 200; dữ liệu/tệp cũ được giữ. Bằng chứng `.artifacts/separate-exam-manual.log`.
