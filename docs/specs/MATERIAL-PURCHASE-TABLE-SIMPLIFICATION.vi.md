# Rút gọn bảng `MATERIAL_PURCHASE` theo Phương án B (18/11/2026)

Yêu cầu người dùng ngày 18/11/2026: `MATERIAL_PURCHASE` có 24 cột, nhiều hơn những gì giao diện Nhập hàng cần. Đối chiếu với panel Nhập hàng (`MaterialPurchasesPanel`), panel Tồn kho, trang Thống kê tài chính và các service cho thấy 3 cột dấu vết chỉ ghi mà không đọc, còn 4 cột là bản chụp tên lấy lại được từ danh mục. Phương án đã chốt là **Phương án B**: bỏ cả 7 cột, giảm bảng từ **24 xuống 17 cột**. Không thêm hay bớt bảng, API giữ nguyên hình dạng DTO.

## Cột sau khi rút gọn (17 cột)

| Nhóm | Cột | Kiểu / Ràng buộc | Ghi chú |
| --- | --- | --- | --- |
| Khóa | `Id` | `uuid`, PK | |
| | `MaterialId` | `uuid`, FK Restrict `MATERIAL_CATALOG_ITEM(Id)`, `NOT NULL` | Tên, nhóm và đơn vị dùng đọc qua khóa này |
| | `SupplierId` | `uuid?`, FK Restrict `CATALOG_PROVIDER(Id)` | Tên công ty đọc qua khóa này |
| Ngày & lô | `EnteredOn` | `date`, `NOT NULL` | Ngày nhập |
| | `ReceivedOn` | `date?` | Trống = chưa nhận, chưa tính vào tồn kho |
| | `ExpiresOn` | `date?` | Hạn dùng |
| | `LotNumber` | `varchar(60)?` | Số lô |
| Số lượng | `Quantity` | `numeric(12,3)`, `NOT NULL` | Theo đơn vị nhập |
| | `Unit` | `varchar(40)`, `NOT NULL` | Đơn vị nhập trên chứng từ |
| | `ConversionFactor` | `numeric(12,3)`, `NOT NULL`, default 1 | 1 đơn vị nhập = bao nhiêu đơn vị dùng |
| | `UsageQuantity` | `numeric(18,3)`, `NOT NULL` | Server tính `Quantity × ConversionFactor`; dùng để cộng tồn kho |
| Tiền | `UnitPriceVnd` | `bigint`, `NOT NULL` | |
| | `TotalVnd` | `bigint`, `NOT NULL` | Server tính `Quantity × UnitPriceVnd`; dùng để lọc thanh toán và cộng tổng |
| | `PaidVnd` | `bigint`, `NOT NULL` | |
| Trạng thái | `Version` | `int`, concurrency token | |
| | `IsDeleted` | `bool`, `NOT NULL` | Xóa mềm |
| | `CreatedAt` | `timestamptz`, `NOT NULL` | Sắp xếp danh sách, thống kê và thứ tự phân bổ tồn kho |

## 7 cột bị xóa và cách thay

| Cột bị xóa | Cách thay |
| --- | --- |
| `CreatedBy`, `UpdatedAt`, `UpdatedBy` | Không có truy vấn hay màn hình nào đọc. Mọi lệnh tạo, sửa, xóa đã ghi người thao tác (`ActorId`), thời điểm (`OccurredAt`) và dữ liệu trước/sau vào `COMMAND_RECEIPT` (`SystemCatalogChange`, lệnh `PurchaseCreate`/`PurchaseUpdate`/`PurchaseDelete`). |
| `MaterialName`, `CategoryName` | Đọc `MATERIAL_CATALOG_ITEM.Name` và `MATERIAL_CATEGORY.Name` qua `MaterialId`. Danh mục chỉ xóa mềm nên join luôn có dòng. Tìm kiếm `q` của Nhập hàng và Tồn kho chuyển sang tên trong danh mục. |
| `SupplierName` | Đọc `CATALOG_PROVIDER.Name` qua `SupplierId` (công ty chỉ ngừng dùng, không xóa cứng). |
| `UsageUnit` | Đọc `MATERIAL_CATALOG_ITEM.UsageUnit` qua `MaterialId`. |

## Thay đổi hành vi

- **Đổi tên** vật tư, nhóm hoặc công ty cung cấp: mọi dòng nhập cũ, thẻ tồn kho và thống kê tài chính hiển thị tên mới. Đây là đánh đổi đã chốt khi chọn Phương án B; lịch sử tên cũ vẫn tra được trong `COMMAND_RECEIPT`.
- **Đổi vật tư sang nhóm khác**: dòng nhập cũ hiển thị nhóm mới.
- **Đổi đơn vị dùng** trong danh mục: không bị chặn (người dùng quyết định ngày 18/11/2026 không thêm quy tắc khóa). Dòng nhập cũ và thẻ tồn kho hiển thị đơn vị mới, nhưng `UsageQuantity` không được quy đổi lại: số lượng cũ vẫn là số đã tính theo hệ số lúc nhập. Người đổi đơn vị dùng tự chịu trách nhiệm đối chiếu và sửa các dòng nhập liên quan nếu cần.

## Hợp đồng API

Không đổi hình dạng. `MaterialPurchaseDto` vẫn trả `materialName`, `categoryName`, `usageUnit`, `supplierName`; `MaterialStockDto` vẫn trả `name`, `categoryName`, `usageUnit`, `supplierName`; `FinanceMaterialPurchaseEntry` vẫn trả tên vật tư, nhóm và công ty. Giá trị lấy từ danh mục lúc đọc thay vì bản chụp lúc lưu.

## Migration `SimplifyMaterialPurchaseTable`

- `Up`: xóa 7 cột. Không cần chuyển dữ liệu vì tên đọc lại từ danh mục.
- `Down`: thêm lại 7 cột rồi điền: `MaterialName`, `CategoryName`, `UsageUnit` từ danh mục vật tư và nhóm; `SupplierName` từ `CATALOG_PROVIDER`; `UpdatedAt` = `CreatedAt`; `CreatedBy` và `UpdatedBy` lấy `ActorId` của receipt `PurchaseCreate` và receipt mới nhất của dòng, không có thì 0.

## Ngoài phạm vi

`MATERIAL_USAGE` vẫn giữ bản chụp `MaterialName`, `UsageUnit`; sẽ xét riêng nếu người dùng muốn.
