# Đơn thuốc mua ngoài + Chi nhánh/Ghế (DCMS-124) — 07/11/2026

Đặc tả: [PRESCRIPTION-MANAGEMENT.md](../specs/PRESCRIPTION-MANAGEMENT.md), [S58-BRANCHES-AND-CHAIRS.md](../specs/S58-BRANCHES-AND-CHAIRS.md) §1.5.

## Hiện trạng trước khi sửa

- Backend không build (8 lỗi C#) và web không build (22 lỗi TypeScript) sau commit đưa vào code đơn thuốc/chi nhánh.
- Không có migration cho `PRESCRIPTION_TEMPLATE(_LINE)`, `PRESCRIPTION_AUDIT`, cột liều của `EXTERNAL_PRESCRIPTION_LINE`, cột `Route/Contraindications/AllergyWarnings` của `CATALOG_MEDICINE`, `BRANCH_PROFILE`, `BRANCH_CHAIR`.
- Route `/api/settings/branches` và `/api/system-settings/branches` không có rule trong `ScreenEndpointPermissions` → nhân viên luôn nhận 403.
- Đơn đã ký vẫn thêm được dòng mới/áp dụng mẫu; `finalize` bỏ qua `expectedVersion`; không có bản điều chỉnh; bản in dùng số CCHN và địa chỉ giả.
- Tạo chi nhánh/ghế đổi khóa chính của entity đang track (lỗi runtime); sửa chi nhánh chưa có profile không lưu; 409 MSG205 không trả `blockers`; upload logo không strip metadata, lưu trong thư mục bin, không có bản ghi asset; web gọi upload sai base URL/token.
- Danh mục cung ứng (thuốc) chỉ kiểm quyền `read` ở service cho lệnh ghi.

## Thay đổi chính

- Backend: header `EXTERNAL_PRESCRIPTION` (Revision, Draft/Finalized/Superseded, lời dặn chung, chẩn đoán snapshot, mã xác thực); lệnh lời dặn, bản điều chỉnh; khóa ghi theo trạng thái Visit + vai trò Dentist; idempotency cho mọi lệnh (kể cả xóa); API bệnh nhân hỗ trợ nhiều liên kết Verified và trả dữ liệu bản in.
- Chi nhánh: khóa chung với `SCHEDULING_*`, validation đầy đủ, blockers (lịch hẹn, lượt khám, giữ chỗ, tài chính; Labo qua port chưa có adapter → 503 MSG144), `BRANCH_ASSET` + strip metadata PNG/JPEG/WebP, đọc logo qua API có quyền.
- `SystemMessage` thêm `currentVersion`, `blockers` (bỏ qua khi null).
- Migration `20261107050902_PrescriptionsAndBranchProfiles` (Up/Down viết tay, có backfill header cho dòng thuốc cũ; Designer và snapshot sinh bằng `dotnet ef`).
- Web: panel kê đơn (lời dặn chung, hoàn tất theo version, bản điều chỉnh, lịch sử, cảnh báo chống chỉ định), bản in A4/A5 chỉ in vùng đơn, trang mẫu đơn (lọc trạng thái, phân trang 20/50/100, ngừng dùng có lý do, đổi thứ tự), BranchesPanel viết lại.
- Mobile: `/(account)/prescriptions` và chi tiết đơn + màn bản in (mã xác thực dạng chữ).

## Kiểm tra đã chạy

- `dotnet build backend/DCMS.sln` — thành công.
- `dotnet test backend/tests/DCMS.UnitTests` — 99/99 pass (thêm `PrescriptionAndBranchAssetRulesTests`).
- `dotnet ef migrations script 20261106120000_MaterialPurchaseLot 20261107050902_PrescriptionsAndBranchProfiles` — sinh SQL thành công.
- `web`: `npm install`, `npm run build` — thành công.
- `mobile`: `npm ci`, `npm run typecheck` — thành công.

## Chưa kiểm tra / giới hạn

- Chưa chạy migration trên PostgreSQL thật và chưa chạy integration test/E2E (Docker không chạy trong phiên này).
- Cần đặt `BRANCH_ASSETS_STORAGE_PATH` (ngoài repository) trước khi upload logo; thiếu cấu hình → 503 MSG144.
- Ngừng chi nhánh luôn trả 503 MSG144 cho tới khi có adapter phiếu Labo (đúng đặc tả). Ngừng/khôi phục ghế không phụ thuộc Labo.
- Hồ sơ nhân sự chưa có số CCHN; bản in để trống dòng CCHN.
- Chưa có `ICurrentBranchContext`/header `X-Branch-Id`/BranchSelector toàn cục; các API vẫn kiểm phạm vi chi nhánh theo từng service.
