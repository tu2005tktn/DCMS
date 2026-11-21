# Rút gọn 8 bảng ít cột còn lại (18/11/2026)

Áp dụng sau đợt rút gọn 9 bảng nhiều cột nhằm loại bỏ hoàn toàn các cột chuẩn hóa trùng lặp (`NormalizedName`/`NormalizedCode`), dấu vết tạo/sửa đã có trong `COMMAND_RECEIPT`, cột suy diễn qua khóa ngoại và các trường legacy không sử dụng trên 8 bảng còn lại, trong khi giữ nguyên **32 bảng EF / 33 bảng vật lý** trong schema `public` (gồm `__EFMigrationsHistory`). Tổng số cột toàn schema `public` giảm từ **468 xuống 439 cột** (`-29` cột trên 8 bảng mục tiêu từ `90 -> 61` cột; còn **437 cột** nghiệp vụ).

Bốn bảng đã gọn hoặc thuộc kho kỹ thuật dùng chung được giữ nguyên: `WORKING_SCHEDULE` (`12`), `CLINIC_SETTINGS_VERSION` (`6`), `PUSH_DEVICE` (`5`), `COMMAND_RECEIPT` (`25`), `AUDIT_LOG` (`50`).

## 1. Tổng hợp thay đổi trên 8 bảng

| STT | Bảng | Trước -> Sau | Giảm | Cột giữ lại | Cột loại bỏ & thay thế |
| --- | --- | --- | --- | --- | --- |
| 1 | `MATERIAL_USAGE` | `12 -> 6` | `-6` | `Id`, `ActivityId`, `MaterialId`, `Quantity`, `UsedAt`, `RecordedBy` | Bỏ `MaterialName`, `UsageUnit` (join `MATERIAL_CATALOG_ITEM` qua `MaterialId`); bỏ `PatientId`, `BranchId`, `DossierId` (join `CLINICAL_ACTIVITY` -> `TREATMENT_DOSSIER` qua `ActivityId`); bỏ `CreatedAt` (dùng `UsedAt`). Thêm khóa ngoại `FK_MATERIAL_USAGE_CLINICAL_ACTIVITY_ActivityId` (`ON DELETE CASCADE`). |
| 2 | `REFERENCE_ENTRY` | `13 -> 8` | `-5` | `Id`, `Kind`, `Code`, `Name`, `Description`, `IsReserved`, `IsActive`, `Version` | Bỏ `NormalizedName` (dùng functional unique index `IX_REFERENCE_ENTRY_Kind_Name_Upper` trên `("Kind", UPPER("Name"))`); bỏ `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` (lịch sử đã lưu trong `COMMAND_RECEIPT` `SystemCatalogChange`, UI [ReferenceCatalogPanel.tsx](../../web/src/features/system-settings/ReferenceCatalogPanel.tsx) chỉ xem lịch sử từ drawer). |
| 3 | `ACCESS_GROUP` | `14 -> 10` | `-4` | `Id`, `Code`, `Name`, `Description`, `BaseRole`, `Active`, `Version`, `Grants` (`jsonb`), `RecordKind`, `IdentityRoleId` | Bỏ `NormalizedCode`, `NormalizedName` (dùng 2 functional unique index `IX_ACCESS_GROUP_RecordKind_Code_Upper`, `IX_ACCESS_GROUP_RecordKind_Name_Upper`); bỏ `RoleConcurrencyStamp` (dùng `Version.ToString()`); gộp `RoleClaims` vào `Grants` (`jsonb`). |
| 4 | `SCHEDULING_SERVICE` | `13 -> 10` | `-3` | `Id`, `BranchId`, `Name`, `PublicDurationMinutes`, `Active`, `PublicBookable`, `IsInitialExamination`, `ReferenceUnitPriceVnd`, `PriceVersion`, `Unit` | Bỏ `BillingUnit`, `IsInvestigationService`, `InvestigationKind` (chỉ có `Unit` được UI/API/Export sử dụng). |
| 5 | `TREATMENT_TEMPLATE` | `11 -> 8` | `-3` | `Id`, `BranchId`, `Name`, `Version`, `CreatedBy`, `CreatedAt`, `DeletedAt`, `Items` (`jsonb`) | Bỏ `DefinitionKind`, `Versions` (`jsonb` — nhánh `LegacySteps` đã ngừng dùng), `StorageStamp` (`Version` là concurrency token duy nhất, tự tăng khi ghi nhận thay đổi). |
| 6 | `MATERIAL_CATALOG_ITEM` | `9 -> 6` | `-3` | `Id`, `Name`, `CategoryId`, `UsageUnit`, `Version`, `IsDeleted` | Bỏ `NormalizedName` (dùng partial functional unique index `IX_MATERIAL_CATALOG_ITEM_CategoryId_Name_Upper` trên `("CategoryId", UPPER("Name")) WHERE NOT "IsDeleted"`); bỏ `CreatedAt`, `UpdatedAt`. |
| 7 | `MATERIAL_CATEGORY` | `7 -> 4` | `-3` | `Id`, `Name`, `Version`, `IsDeleted` | Bỏ `NormalizedName` (dùng partial functional unique index `IX_MATERIAL_CATEGORY_Name_Upper` trên `(UPPER("Name")) WHERE NOT "IsDeleted"`); bỏ `CreatedAt`, `UpdatedAt`. |
| 8 | `CUSTOMER_FAMILY_MEMBER` | `11 -> 9` | `-2` | `Id`, `OwnerAccountId`, `FullName`, `Relationship`, `BirthDate`, `Gender`, `ContactPhone`, `Version`, `DeletedAt` | Bỏ `CreatedAt`, `UpdatedAt` (lịch sử chống ghi trùng nằm ở `COMMAND_RECEIPT`). |

## 2. Quy tắc tương thích và truy vấn

- **Tìm kiếm & sắp xếp không phân biệt hoa thường:** PostgreSQL database `dcms_dev` dùng collation `en_US.utf8`, bảo đảm `UPPER("Name")` xử lý chính xác tiếng Việt có dấu đồng nhất với `ToUpperInvariant()`. Các truy vấn LINQ trong `SystemCatalogService`, `ReferenceCatalogService`, `SettingsService`, `CareTaskService` dùng `.Name.ToUpper()` và `.Code.ToUpper()`.
- **`ACCESS_GROUP` (`IdentityRole` + `Grants`):** Ánh xạ `AccessGroup.Grants` dùng value converter + value comparer `jsonb` (`AccessGroupGrant` gồm `GroupId`, `Permission`, `ClaimId?`, `ClaimValue?`), giúp EF Core theo dõi mọi chỉnh sửa phần tử danh sách. Bản ghi `RecordKind = 'IdentityRole'` đọc `RoleClaims` từ `Grants` (`Type = Permission`, `Value = ClaimValue`, `Id = ClaimId`) và dùng `Version` làm `ConcurrencyStamp`.
- **`MATERIAL_USAGE`:** `MaterialStockService.UsageAsync` join `CLINICAL_ACTIVITY` -> `TREATMENT_DOSSIER` (theo `DossierId`) và `MATERIAL_CATALOG_ITEM` để lọc phạm vi chi nhánh, tìm kiếm theo tên vật tư và trả về đầy đủ `MaterialUsageDto` như trước. Không thêm FK từ `RecordedBy` tới `EMPLOYEE_PROFILE` vì cột này lưu `USER_ACCOUNT.Id`.
- **`REFERENCE_ENTRY`:** `ReferenceEntryDto` rút gọn còn 9 trường (`Id`, `Kind`, `Code`, `Name`, `Description`, `Reserved`, `Active`, `Version`, `PatientCount`), giữ nguyên giao diện và lịch sử thay đổi từ `COMMAND_RECEIPT`.

## 3. Migration hai chiều (`Up` / `Down`)

Migration EF Core: `20261118070657_SimplifyRemainingEightSmallTables`.

- **`Up`:**
  1. Dọn bản ghi `MATERIAL_USAGE` không còn `CLINICAL_ACTIVITY` (nếu có) trước khi tạo `FK_MATERIAL_USAGE_CLINICAL_ACTIVITY_ActivityId` (`ON DELETE CASCADE`).
  2. Xóa các bản ghi `TREATMENT_TEMPLATE` có `DefinitionKind <> 'ItemPlan'` trước khi bỏ cột `DefinitionKind`.
  3. Chuyển các phần tử trong `ACCESS_GROUP."RoleClaims"` (`RecordKind = 'IdentityRole'`) sang `ACCESS_GROUP."Grants"`.
  4. Xóa 5 unique index cũ gắn với `NormalizedName` / `NormalizedCode`, xóa 29 cột thừa, rồi tạo 5 functional unique index `*_Upper`.
- **`Down`:**
  1. Xóa 5 functional unique index `*_Upper` và `FK_MATERIAL_USAGE_CLINICAL_ACTIVITY_ActivityId`.
  2. Thêm lại 29 cột đã bỏ, backfill dữ liệu phục hồi (`UPPER("Code")`, `UPPER("Name")`, `RoleConcurrencyStamp = Version::text`, giải nén `RoleClaims` từ `Grants` khi `RecordKind = 'IdentityRole'`, điền `MaterialName`/`UsageUnit`/`DossierId`/`PatientId`/`BranchId`/`CreatedAt` cho `MATERIAL_USAGE` qua join `MATERIAL_CATALOG_ITEM`, `CLINICAL_ACTIVITY`, `TREATMENT_DOSSIER`, gán `BillingUnit = Unit` cho `SCHEDULING_SERVICE`, khôi phục `DefinitionKind = 'ItemPlan'`, `Versions = '[]'::jsonb`, `StorageStamp = gen_random_uuid()` cho `TREATMENT_TEMPLATE`).
  3. Tạo lại 5 unique index `Normalized*` cũ.
