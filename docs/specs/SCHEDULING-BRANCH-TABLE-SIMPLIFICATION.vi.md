# Rút gọn bảng `SCHEDULING_BRANCH` theo Phương án A (18/11/2026)

Yêu cầu người dùng ngày 18/11/2026: bảng `SCHEDULING_BRANCH` đang có quá nhiều cột so với giao diện quản lý chi nhánh (`BranchesPanel`) và phần lớn là trường trùng hoặc không dùng trong luồng vận hành. Phương án đã chốt là **Phương án A**: hợp nhất entity `BranchProfile` vào `SchedulingBranch`, giữ đúng **11 cột** trên `SCHEDULING_BRANCH` (giảm từ **20 xuống 11 cột**, bỏ `9` cột dư), giữ nguyên số bảng (`32` bảng trong model EF / `33` bảng vật lý trong schema `public`).

## Cấu trúc bảng sau khi rút gọn (11 cột)

| Nhóm | Cột | Kiểu / Ràng buộc | Ghi chú |
| --- | --- | --- | --- |
| Định danh & mã | `Id` | `int`, PK, `NOT NULL` | Khóa chính chi nhánh (Identity) |
| | `Code` | `varchar(40)?`, `UNIQUE` (`IX_SCHEDULING_BRANCH_Code`) | Mã chi nhánh; chi nhánh tạo cũ trước khi nhập mã dùng fallback `BR-{Id:D2}` khi hiển thị |
| Thông tin cơ sở | `Name` | `varchar(160)`, `NOT NULL` | Tên chi nhánh |
| | `Address` | `varchar(1000)?` | Địa chỉ chi nhánh (hiển thị bảng và in đầu đơn thuốc) |
| | `Phone` | `varchar(20)?` | Điện thoại liên hệ (hiển thị bảng và in đầu đơn thuốc) |
| | `Email` | `varchar(254)?` | Email liên hệ |
| Trạng thái & đặt lịch | `Active` | `bool`, `NOT NULL` | Trạng thái hoạt động |
| | `SlotIntervalMinutes` | `int`, `NOT NULL`, default `15` | Bước nhảy khung giờ đặt lịch (`CK_BRANCH_SLOT_INTERVAL`: `BETWEEN 1 AND 120`) |
| Khóa đồng thời | `Version` | `int`, `NOT NULL`, default `1` | Concurrency token duy nhất của dòng chi nhánh (`CK_BRANCH_SLOT_INTERVAL`: `"Version" > 0`) |
| Tài nguyên JSONB | `Members` | `jsonb`, `NOT NULL`, default `'[]'::jsonb` | Danh sách `SchedulingMember` của chi nhánh |
| | `Chairs` | `jsonb`, `NOT NULL`, default `'[]'::jsonb` | Danh sách `SchedulingChair` (ghế điều trị) của chi nhánh |

## 9 cột bị xóa và lý do

| Cột bị xóa | Lý do và cách thay thế |
| --- | --- |
| `StorageStamp` (`uuid`), `BookingSettingsVersion` (`int`) | Hợp nhất về một khóa đồng thời duy nhất `Version`; khi `SchedulingBranch` (kể cả `Members`, `Chairs` JSONB hoặc `SlotIntervalMinutes`) thay đổi qua EF Core, `Version` tự động tăng 1. `BookingSettings` và `AvailabilitySlot.BookingSettingsVersion` đọc/kiểm tra theo `SchedulingBranch.Version` (giữ nguyên tên field `bookingSettingsVersion` trong API đặt lịch để tương thích web/mobile). |
| `TimeZoneId` (`varchar(64)`) | Hệ thống cố định múi giờ phòng khám `Asia/Ho_Chi_Minh`, bỏ khỏi UI và DB. |
| `Website` (`varchar(255)`) | Không sử dụng trên cổng bệnh nhân hay mẫu in; bỏ khỏi UI và DB. |
| `LogoAssetId` (`uuid`), `Assets` (`jsonb`) | Bỏ tính năng upload/lưu logo chi nhánh cùng thực thể `BranchAsset`, `BranchAssetStore`, route `POST /api/settings/branches/{id}/logo` và `GET /api/settings/branches/{id}/logo/{assetId}`; `PrescriptionPrintDto.LogoDataUrl` trả `null`. |
| `BankCode`, `AccountNumber`, `PayeeName` | Chuyển khoản QR thanh toán sử dụng PayOS động qua [TreatmentPayOsEndpoints](../../backend/src/DCMS.Api/Features/Finance/TreatmentPayOsEndpoints.cs), không đọc cấu hình ngân hàng tĩnh của chi nhánh; bỏ khỏi UI và DB. |

## Hợp nhất thực thể và hợp đồng API chi nhánh

- Bỏ tách đôi entity (`table-splitting`): xóa `BranchProfile` và `BranchAsset` khỏi `SettingsModels`; toàn bộ truy vấn/cập nhật chi nhánh đi qua `SchedulingBranch` (`SCHEDULING_BRANCH`).
- Hợp đồng `BranchDto`, `BranchDetailDto` và `SaveBranchCommand` chỉ giữ các trường còn lại (`Id`, `Code`, `Name`, `Address`, `Phone`, `Email`, `SlotIntervalMinutes`, `IsActive`, `Version`, kèm `ChairCount` / `Chairs`).
- Giao diện [BranchesPanel.tsx](../../web/src/features/system-settings/BranchesPanel.tsx) bỏ cột `Tài khoản chuyển khoản` và các trường `Website`, `Múi giờ`, `Logo chi nhánh`, `Tài khoản nhận chuyển khoản` trên Drawer chi nhánh.

## Migration `20261118023932_SimplifySchedulingBranchTable`

- `Up`: xóa constraint `CK_BRANCH_SLOT_INTERVAL`, đồng bộ `"Version" = GREATEST("Version", "BookingSettingsVersion", 1)` cho các dòng hiện có, đổi default `"Version"` thành `1`, xóa 9 cột dư và tạo lại `CK_BRANCH_SLOT_INTERVAL` (`"SlotIntervalMinutes" BETWEEN 1 AND 120 AND "Version" > 0`).
- `Down`: xóa `CK_BRANCH_SLOT_INTERVAL`, đổi default `"Version"` về `0`, khôi phục 9 cột với giá trị mặc định hợp lệ (`Assets = '[]'::jsonb`, `StorageStamp = gen_random_uuid()`, `BookingSettingsVersion = GREATEST("Version", 1)`, `TimeZoneId = 'Asia/Ho_Chi_Minh'`), và tạo lại constraint cũ (`"SlotIntervalMinutes" BETWEEN 1 AND 120 AND "BookingSettingsVersion" > 0`).
