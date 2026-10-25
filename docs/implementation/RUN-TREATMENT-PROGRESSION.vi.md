# S58/S21/S24 backend — Cách kiểm tra và trạng thái bàn giao

## Bổ sung v2.3 ngày 25/10/2026

- Migration `20261025120000_TreatmentStepDurationRange` thêm cột nullable cho khoảng ước tính và dấu xác nhận giữ ghế ngắn; thêm unique index cho một instruction `NeedsBooking`/`Booked` trên mỗi step. Trước khi áp dụng trên database có dữ liệu, kiểm tra thủ công step nào đã có nhiều instruction còn hiệu lực; không tự xóa/gộp chỉ định. Không chạy migration trên database dự án chỉ để thử nghiệm.
- S58 cho chọn khoảng min/max tùy chọn, estimate cụ thể và reserved. S21 khi sao chép hoặc thêm bước với reserved < max phải xác nhận cảnh báo; API lưu actor/thời điểm. Step cũ không có khoảng tiếp tục đọc được. Lệnh thêm bước thuộc item Agreed: `POST /api/dossiers/{id}/steps`; retry cùng `requestKey` giữ ID, payload khác trả MSG111.
- Đánh giá `AddVisit` ghi tiến độ rồi bác sĩ tạo instruction `NeedsBooking` qua API S24 với cửa sổ ngày rõ ràng; không tự tạo Appointment. `ITreatmentActualEvidencePort`, `ITreatmentLabReadinessPort`, `ITreatmentVisitActivitySourcePort` và `ITreatmentDentistCapabilityPort` hiện chưa có nguồn production: complete/lab/source Visit/capability booking bị chặn an toàn. Chỉ đánh dấu nghiệm thu end-to-end sau khi chủ nguồn nối adapter thật.
- First instruction có thể gửi `source: { dossierId, itemId, stepId: null, visitId: null, activityId: null }` nếu item đã Agreed trong plan Committed. Một item-only source và một step source chỉ có tối đa một instruction còn hiệu lực cho mỗi khóa tương ứng; booking kiểm lại item/plan trước khi giữ slot.
- Fixture tích hợp cấu hình ba ghế trong schema PostgreSQL `dcms_test_*`; đây không phải dữ liệu ghế của phòng khám. Due scan dùng khóa instruction + cửa sổ để không nhân sự kiện do thay đổi version mà cửa sổ không đổi; consumer care task thuộc DCMS-99.

Nguồn hợp đồng: [TREATMENT-PROGRESSION.vi.md](../specs/TREATMENT-PROGRESSION.vi.md). Migration `20261019143000_TreatmentProgression` tạo template/version, patient step, follow-up instruction, receipt và event; thêm provenance vào dossier. Migration đã được áp dụng lên database Development `dcms_dev` ngày 19/10/2026 để người dùng thử trên Web; không seed dữ liệu.

## Kiểm tra không sửa database dự án

Từ thư mục gốc: `dotnet build backend/DCMS.sln --no-restore`; `dotnet test backend/tests/DCMS.UnitTests/DCMS.UnitTests.csproj --no-build`; `dotnet test backend/tests/DCMS.IntegrationTests/DCMS.IntegrationTests.csproj --no-build --filter FullyQualifiedName~TreatmentModelTests`. Trong `web`: `npm run build`. Playwright dùng Chrome với API mock: chạy Vite cục bộ rồi `PLAYWRIGHT_USE_SYSTEM_CHROME=1 npx playwright test tests/treatment-progression.spec.ts tests/treatment-dossier.spec.ts` (PowerShell đặt biến qua `$env:PLAYWRIGHT_USE_SYSTEM_CHROME='1'`).

Integration test HTTP/PostgreSQL ở `TreatmentProgressionApiTests` dùng `ApiFixture`: mỗi fixture tạo schema `dcms_test_*` riêng và xóa khi kết thúc. Chỉ chạy khi PostgreSQL test sẵn sàng và `ConnectionStrings__DefaultConnection` trỏ tới database development dành cho test; fixture không sửa schema public hay seed dữ liệu dự án. Test có adapter actual **synthetic** để chứng minh completion/successor theo hợp đồng, không chứng minh actual thật từ DCMS-99. Trước khi chuyển môi trường, xem và áp migration theo quy trình nhóm, không chạy trên production bằng lệnh test.

## Kết quả đã quan sát trong lượt triển khai

- Backend build: đạt, 0 warning/0 error. Web build: đạt; Vite vẫn báo chunk lớn và annotation của dependency SignalR.
- Unit tests backend: 43/43 đạt. EF migration snapshot test không cần DB: đạt. Playwright mock: `treatment-progression.spec.ts` 1/1 và `treatment-dossier.spec.ts` 1/1 đạt.
- Sau khi Docker Desktop được khởi động, migration runtime trên `dcms_dev` thành công. Integration test `TreatmentProgressionApiTests` chạy trên schema PostgreSQL cách ly: 2/2 đạt, gồm danh sách mẫu rỗng/có mẫu, phiên bản mẫu, bản sao bệnh nhân và kiểm tra đầu vào. Lỗi EF không dịch được truy vấn danh sách mẫu và lỗi EF đánh dấu phiên bản mới là `Modified` đã được sửa; API/Web được khởi động lại và health trả healthy. Chưa ghi các ca cạnh tranh booking, no-show, due scan là đã nghiệm thu.

## Điểm tích hợp còn phụ thuộc

- Production `ITreatmentActualEvidencePort` trả false và `ITreatmentLabReadinessPort` trả unavailable cho tới khi DCMS-99/100 cung cấp adapter xác minh nguồn. API complete step/instruction không đánh dấu hoàn tất khi thiếu actual; lab-required step là Blocked. Nguồn Visit/activity của S24 bị từ chối 503 cho tới khi có cổng xác thực Patient/branch.
- `TreatmentDueScanner` ghi event outbox với stable key; dispatcher/care-task consumer thuộc DCMS-99. S24 UI dùng hợp đồng trên do Tuấn thực hiện. Khi nối adapter thật cần test theo Patient/branch, plan version, lab specification và lặp scan/cạnh tranh transaction trước khi xác nhận nghiệm thu end-to-end.
