# S58 W08/W11/W12 — triển khai trên nền main

Ngày 28/10/2026. Trạng thái: In Progress. Nhánh `Trien` đã được đặt lại từ
`origin/main` tại `0e6c33f`; không tái sử dụng ledger/catalog/settings riêng
của code Trien cũ.

## Nguồn dùng chung

- Thuốc: `CatalogMedicine` và `MedicineCatalog` là nguồn duy nhất. Consumer
  đơn thuốc chỉ chọn mục active + `PrescriptionEligible`; lời dặn được snapshot
  trên dòng đơn. Đơn mua ngoài không yêu cầu tồn và không tạo stock event.
- Idle: `GeneralSettings.StaffIdleTimeoutMinutes` là policy duy nhất. Mở rộng
  `AuthSession` bằng client kind, interactive time/version; polling, refresh,
  SignalR và tải nền không touch interactive time.
- Phiếu thu: `FinanceCharge`, `PaymentReceipt`, `PaymentAllocation` và
  `BranchReceiptSequence` là ledger/sequence duy nhất. Mẫu in đọc
  `GeneralSettings.ReceiptPrint`; preview giả không cấp số, phiếu phát hành giữ
  snapshot lịch sử và in lại chỉ đổi presentation hiện hành.

## API/runtime

- `GET /api/auth/session/idle`, `POST /api/auth/session/activity` cho staff web;
  activity nhận `expectedVersion/requestKey`, dùng giờ server và throttle 30s.
  Validation/refresh kiểm deadline trước khi gia hạn, hết hạn trả 401 MSG07 và
  revoke refresh của session. Patient/Public giữ policy hiện hành.
- `POST /api/system-settings/settings/general/preview` tiếp tục preview policy;
  cảnh báo số phiên bị ảnh hưởng được tính từ interactive timestamp, không reset
  hoạt động khi giảm/tăng timeout.
- `POST /api/finance/receipt-print/preview` dùng dữ liệu giả, không write.
  `GET /api/finance/payments/{id}/print` đọc snapshot; `POST .../print-events`
  ghi audit idempotent. Sequence start theo branch là command versioned riêng,
  không giảm dưới số đã cấp.
- `GET /api/reference-options?kind=medicines&context=prescription&branchId=...`
  trả projection thuốc active/eligible và lời dặn, không trả giá/tồn.

## Nghiệm thu

- SET-AT-18: chọn thuốc mua ngoài không tồn, sửa lời dặn dòng đơn không đổi
  catalog và không tạo movement.
- SET-AT-23/24/25: polling/refresh/hai tab không kéo dài idle; activity thật dùng
  giờ server; draft không tự commit hoặc lộ sang actor khác.
- SET-AT-26/27: preview không cấp số; in lại giữ snapshot; số unique theo branch,
  retry không cấp lại, tắt hiển thị vẫn sinh số.

Bằng chứng phải tách integration PostgreSQL, browser thật và browser mock.
