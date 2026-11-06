# Tồn kho và lịch sử xuất dùng dụng cụ - vật liệu

Mở rộng `MATERIAL-PURCHASES.md` (nhập hàng) và `TREATMENT-ITEM-PLANS.md` (phiếu điều trị). Màn hình `/materials` có thêm hai thẻ: **Tồn kho** và **Lịch sử xuất dùng**.

## Quy tắc nghiệp vụ

- Tồn kho **chung toàn hệ thống** (không tách theo chi nhánh), tính theo **đơn vị dùng** của danh mục (`usageUnit`).
- Tồn = tổng `UsageQuantity` của các dòng nhập **đã nhận** (`ReceivedOn` khác rỗng, chưa xóa) − tổng số lượng đã xuất dùng.
- Khi ghi nhận hoạt động điều trị có chọn dụng cụ/vật liệu, hệ thống tạo các dòng **xuất dùng** và trừ kho.
- **Chặn khi thiếu tồn**: nếu số lượng dùng vượt tồn, không lưu hoạt động; trả `409` mã `MATERIAL_STOCK_INSUFFICIENT` kèm `field = materials`, và danh sách vật liệu thiếu (tên, tồn, cần).
- Một hoạt động có tối đa 30 dòng vật liệu, mỗi vật liệu một dòng, số lượng > 0, tối đa 3 chữ số thập phân. Vật liệu phải còn hoạt động.
- Sửa hoạt động: danh sách vật liệu gửi lên **thay thế** toàn bộ dòng xuất dùng cũ (tồn được hoàn rồi trừ lại, kiểm tra trên số mới). Không gửi (`null`) = giữ nguyên.
- Xóa hoạt động: xóa các dòng xuất dùng của nó, tồn được hoàn lại.
- Sửa/xóa/đổi ngày nhận của dòng nhập làm tồn của một vật liệu **âm** bị chặn với `MATERIAL_STOCK_NEGATIVE` (409).
- Dòng xuất dùng lưu snapshot tên vật liệu, đơn vị dùng, bệnh nhân, chi nhánh, hồ sơ, người ghi nhận, thời điểm.

## Dữ liệu

`MATERIAL_USAGE`: `Id`, `ActivityId` (FK `CLINICAL_ACTIVITY`, cascade), `MaterialId` (FK catalog, restrict), `MaterialName`, `UsageUnit`, `Quantity` (18,3), `PatientId`, `BranchId`, `DossierId?`, `UsedAt`, `RecordedBy?`, `CreatedAt`. Chỉ mục `MaterialId`, `UsedAt`, `ActivityId`.

## API

Quyền đọc dùng lại `catalog.materials.read`; không thêm quyền mới.

- Các request ghi nhận/chỉnh hoạt động trên phiếu điều trị nhận thêm `materials?: [{ materialId, quantity }]`.
- `GET /api/system-settings/material-stock?q=&page=&pageSize=` → từng **lần nhập đã nhận** (một dòng = một lô): `{ items: [{ purchaseId, materialId, name, categoryName, usageUnit, supplierName, receivedOn, expiresOn, received, used, remaining }], total, page, pageSize }`, sắp theo tên vật liệu rồi hạn dùng gần nhất trước.
- `GET /api/system-settings/material-usage?materialId=&activityId=&from=&to=&q=&page=&pageSize=` → `{ items: [{ id, usedAt, materialId, materialName, quantity, usageUnit, patientName, branchName, dossierCode, recordedByName }], total, ... }`, mới nhất trước.

## Giao diện

- Hộp "Vật liệu dùng" trên phiếu điều trị lưu danh sách vật liệu có cấu trúc (vẫn ghi chú văn bản vào ghi chú như trước) và hiển thị tồn khi chọn.
- Thẻ **Tồn kho**: mỗi lần nhập đã nhận là một dòng riêng (không gộp theo vật liệu): vật liệu, ngày nhận, hạn dùng (gắn nhãn sắp/hết hạn), công ty, đã nhập, đã dùng, còn lại, đơn vị; tìm theo tên.
- Thẻ **Lịch sử xuất dùng**: bảng thời gian, vật liệu, số lượng + đơn vị, bệnh nhân, chi nhánh, hồ sơ; lọc theo vật liệu/khoảng ngày.
- Thiếu tồn: hiển thị lỗi tại hộp vật liệu "X: tồn a, cần b", không đóng hộp.

## Giới hạn

Không có kiểm kê thủ công, hao hụt, hủy hàng, gắn xuất dùng với lô cụ thể hay tồn theo chi nhánh trong phạm vi này.

## Tồn theo từng lần nhập (lô)

Mỗi lần nhập đã nhận có hạn dùng riêng nên màn Tồn kho liệt kê **từng lần nhập**. Xuất dùng không gắn với lô cụ thể; số đã dùng của vật liệu được phân bổ vào các lần nhập theo thứ tự **hạn dùng gần nhất trước (FEFO)**, lần không có hạn dùng xếp sau, hòa thì nhận sớm hơn trước. `used` và `remaining` của từng dòng là số phân bổ này (suy ra, không lưu). Tổng `remaining` của một vật liệu bằng tồn dùng để chặn thiếu kho. Số lô là trường của lần nhập (xem `MATERIAL-PURCHASES.md`) và hiển thị trong bảng Tồn kho; API tồn kho trả thêm `lotNumber`.

## Chọn vật liệu dùng từ kho (06/11/2026)

Hộp "Vật liệu dùng" trên phiếu điều trị chỉ liệt kê dụng cụ/vật liệu **đang có tồn** (tồn > 0), kèm số tồn và đơn vị dùng để nha sĩ chọn. API: `GET /api/system-settings/material-stock/available?q=&page=&pageSize=` → `{ items: [{ materialId, name, categoryName, usageUnit, stock }], total, page, pageSize }`, chỉ vật liệu còn hoạt động có tồn > 0, sắp theo tên; quyền `catalog.materials.read`. Hộp báo "Kho chưa có vật liệu nào" khi rỗng. Máy chủ vẫn kiểm tra tồn khi lưu.

Bảng Tồn kho: các cột Đã nhập, Đã dùng, Còn lại chỉ hiện số, không kèm đơn vị.
