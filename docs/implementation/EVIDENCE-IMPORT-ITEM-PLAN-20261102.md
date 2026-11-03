# Áp kế hoạch vào phiếu — 02/11/2026

Đã gỡ TemplatePage/link legacy, redirect URL cũ về Kế hoạch điều trị; PlanStepsPanel không còn đọc/picker/apply mẫu cũ, giữ quản lý bước. TreatmentSheet bỏ nút/Modal Lịch sử phiếu, giữ audit/timeline API. Thêm Modal tìm/phân trang/đọc hạng mục và tổng, Áp dụng thêm cuối bảng, chặn khi đang sửa hàng. Ghi chú cuối phiếu chưa lưu được giữ khi refresh; lỗi giữ nguồn, MSG111/404 chặn áp lại trước đọc nguồn và version dossier mới.

POST `/api/dossiers/{id}/sheet-items/from-item-plan` dùng chung SheetCommand transaction/lock/receipt. AddSheetItemAsync tách finalize nội bộ để bulk tăng version một lần và carry-forward một lần. Server đọc nguồn ItemPlan cùng branch, kiểm versions và các giới hạn phiếu/danh mục, copy cost/name snapshot/teeth/date/minutes/note/assistant, bác sĩ lấy dossier; quantity1, discount0, ClinicianEntry/Proposed. Snapshot và ánh xạ source-item→patient-item ghi event. Không đổi SourceTemplateId, không tạo actual/booking/phí/tiền. Sai một hàng rollback cả đợt; retry cùng ID; không migration.

Kiểm chứng: backend solution build đạt 0 warning/error; Web build đạt (warning SignalR/chunk có sẵn). 18 integration test (TreatmentSheetV25Tests và ItemPlanApiTests) đạt trên database riêng `dcms_importplans_20261102_4c0d79a1`, đã dọn database. Ca mới kiểm hai hàng, phiên bản tăng một lần, doctor/quantity/ClinicianEntry, concurrent retry, stale nguồn, rollback khi hàng thứ hai invalid, không actual và HasPendingModelChanges=false; hồi quy sheet/catalog đạt. 79 unit test đạt.

24 UI mock đạt (2 phút) cho CRUD catalog, phiếu/material/financial mock và Steps không còn picker cũ. Ca áp dụng mới kiểm thêm cuối, tên/giá, retry cùng key và không có Lịch sử phiếu. Đã chạy lại riêng ca này sau khi thêm kiểm ghi chú chưa lưu và responsive390/768/1440: 1/1 đạt (8s). Ảnh synthetic `.artifacts/import-item-plan-{width}.png`; chưa tạo dữ liệu thử dcms_dev hoặc nghiệm thu browser với API development thật.

API development đã chạy lại từ bản build, health api/database healthy; Web HTTP200. Không seed hoặc migration development; không chạy Git/commit/push. Các sửa đổi chưa commit khác trong workspace được giữ nguyên.
