# Tích hợp main / PR 30 của Vinh — 27/10/2026

Main 77092e6 (f8b2dcd, c8abd9b) được fast-forward vào feat/dcms-112-examination từ 63f8589. Trước tích hợp đã có backup stash include-untracked và manifest SHA-256 ngoài repo. Không push hoặc xóa stash.

Giữ UI phiếu điều trị inline/direct actual và danh mục vật tư của Vinh. Giữ khám/CLS/settings/finance/session/source snapshot đã có. Giải quyết các xung đột từng phần, không checkout đè nguyên phiên bản local lên phần điều trị mới.

Cổng phí dùng hợp đồng ClinicalFeeRequest/ClinicalFeeResult của main và ghi outbox Pending theo Vinh; không biến Pending thành Paid hoặc tạo charge trùng. Cổng ledger CLS hiện có tiếp tục cho dịch vụ CLS đã xác nhận thực hiện. Overload nội bộ cũ được giữ cho tương thích nguồn cũ/kiểm thử, không thay đổi ngầm direct actual thành một khoản thu. Consent dùng adapter thật từ main.

Nguồn khám: giữ cả hợp đồng SourceVisitId/ExpectedSourceVisitVersion của main và SourceExaminationVisitId/SourceExaminationVersion cho phiếu khám đã kết thúc; không chấp nhận cùng lúc hai ID nguồn. Native main giữ SourceExaminationId và source Visit version; nguồn phiếu khám local giữ SourceExaminationVisitId và examination record version, snapshot JSON. SourceExaminationVersion là cột chung và chỉ có một khai báo; hai kiểu nguồn phân biệt bằng ID nguồn tương ứng. Cột này đã có ở schema manual nên hai migration thêm cột phải có IF NOT EXISTS, không xóa giá trị cũ. Down của migration nguồn khám không xóa cột do migration main sở hữu trước đó.

Kiểm chứng sau hợp nhất: build backend/web; schema PostgreSQL riêng với migration/model snapshot hợp nhất; tests treatment sheet/catalog/khám/CLS/settings/session. Không seed dữ liệu thử vào public. Ghi lại kết quả cuối tại đây.

Fixture các attendance dùng để ghi actual trong TreatmentSheetV25Tests được gắn mục đích TreatmentContinuation thay vì mặc định InitialExamination, để giữ quy tắc tách khám/điều trị đã chốt mà không nới kiểm quyền nguồn.

Hợp đồng main cho phép actual nguồn Dossier không cần Visit (ClinicianEntry) và giữ fee outbox Pending. Quy tắc tách Khám áp dụng cho actual nguồn Visit; danh sách Các buổi điều trị vẫn dùng các Visit đã tiếp nhận thật và không tạo Visit giả cho dòng direct. Cách nhập phiếu/direct của Vinh là luồng mới được giữ, không thay bằng giao diện danh sách hồ sơ cũ.

Đã thử lại test chọn vật liệu trùng tên bằng text và role option; vẫn timeout ở option thứ hai. Giữ nguyên component và test UI của main, không áp dụng thay đổi thử nghiệm không giải quyết được lỗi. Không coi toàn bộ UI đạt.


## Kết quả tích hợp

- HEAD = origin/main = **77092e6**, main đã là ancestor của HEAD; nhánh làm việc vẫn feat/dcms-112-examination. Không còn unmerged path hoặc conflict marker. Không commit/push các thay đổi local.
- Backup giữ nguyên: stash `before-main-vinh-treatment-20261027-225839` (e3b1502f97d2efa1801eb3f696e1d8a043fdc9b3), gồm 543 tệp; manifest ngoài repo xác nhận không có tệp thất lạc. Những thay đổi nội dung thuộc các phần tích hợp/điều chỉnh test để tương thích; các tệp khác giữ nguyên nội dung, có chuẩn hóa LF/CRLF theo Git.
- Backend và TypeScript/Vite build đạt. EF migrations has-pending-model-changes: không có thay đổi model chưa được mô tả bởi migration.
- **141/141 API/PostgreSQL** đạt: treatment sheet/direct source, payment migration, material catalog, khám, CLS, settings, session, hồ sơ và clinical work.
- Bộ browser: 15/16 đạt, gồm 2 luồng tài chính API/PostgreSQL thật và 13 ca UI mock của phiếu điều trị. Còn 1 ca UI mock timeout khi chọn vật liệu trùng tên khác nhóm; đã tái hiện riêng, không xác định là do merge hay baseline nên chưa nhận là lỗi baseline. Giữ ghi nhận này để xử lý riêng, không coi là đạt toàn bộ UI.
- Chạy riêng luồng **khám kết thúc → lập phiếu mới của Vinh từ nguồn khám → hai buổi điều trị riêng** trên API/PostgreSQL thật: 1/1 đạt sau khi cập nhật tên nút tạo phiếu trong test.
- Runtime 5084/8084 đã khởi động lại bằng cấu hình và storage cũ. Migrate-only trên public, không seed. Số Visit/VisitExamination/ClinicalDocument trước/sau không đổi; API/database healthy và web HTTP 200.
- Bằng chứng local: `.artifacts/main-vinh-backend-build.log`, `main-vinh-web-build.log`, `main-vinh-integration-tests.log`, `main-vinh-browser.log`, `main-vinh-last-browser.log`, `main-vinh-material-ui-recheck.log`, `main-vinh-manual.log`. Artifact/credentials không được đưa vào commit.
