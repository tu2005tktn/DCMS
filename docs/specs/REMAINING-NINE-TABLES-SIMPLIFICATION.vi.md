# Rút gọn 9 bảng nhiều cột còn lại (`198 -> 121` cột)

Ngày cập nhật: 18/11/2026  
Áp dụng sau phê duyệt cấu trúc schema hợp nhất cho 9 bảng nhiều cột còn lại trong schema `public` của database `dcms_dev`, đưa tổng số cột toàn bộ 33 bảng vật lý (32 bảng EF + `__EFMigrationsHistory`) từ **545 xuống 468 cột** (`-77` cột), giữ nguyên số bảng và toàn bộ quan hệ PK/FK trên ERD cùng các luồng giao diện Web (`web/src/app/App.tsx`) và Mobile (`mobile/src/app`) đang hoạt động.

## 1. Tổng hợp bảng và số cột

| Bảng | Trước | Sau | Giảm | Cột JSONB / Thay đổi chính |
| --- | ---: | ---: | ---: | --- |
| `USER_ACCOUNT` | 37 | 18 | -19 | Gom 13 thành phần bảo mật/phiên/token/xác minh/khôi phục vào `AuthData` (`jsonb`), dùng cột hệ thống PostgreSQL `xmin` làm row-version concurrency token thay `ConcurrencyStamp` & `AccessVersion`, bỏ `NormalizedUserName`/`NormalizedEmail`/`NormalizedPhone`/`GoogleSubject`/`EmployeeFullName` (dùng functional unique index). |
| `IN_APP_NOTIFICATION` | 28 | 19 | -9 | Gom `RequestId`, `DeepLink`, `HasExternalFailure`, `LastAttemptAt`, `PayloadJson`, `DispatchedAt` vào `Data` (`jsonb`); dùng chung `AppointmentId` (`Guid?`), `Kind` (`varchar(80)`), `CreatedAt`, `Status`, `Error`; thay index outbox sang `(Status, CreatedAt)`. |
| `PATIENT_PROFILE` | 26 | 16 | -10 | Gom 11 trường phụ trợ (`Guardian*`, `Note`, `ProfileDate`, `IntakeHistoryIds`, `IntakeHistoryOther`, `IntakeRecorded*`, `ArchivedAt`, `ArchiveReason`) vào `Details` (`jsonb`). |
| `FAMILY_RECORD_ACCESS` | 22 | 13 | -9 | Gom 10 trường khai báo/đối chiếu/quyết định (`DeclaredFullName`, `DeclaredBirthDate`, `Relationship`, `DeclaredPatientCode`, `MatchingInformation`, `SharingBasis`, `VerificationNote`, `DecisionReason`, `PublicNote`, `DecidedAt`) vào `Details` (`jsonb`). |
| `PATIENT_ACCOUNT_LINK` | 19 | 11 | -8 | Giữ cột tra cứu `DeclaredFullName`; gom 9 trường còn lại (`DeclaredBirthDate`, `DeclaredPatientCode`, `RequestNote`, `VerificationNote`, `DecisionReason`, `PublicNote`, `ReviewedAt`, `RevokedAt`, `WithdrawnAt`) vào `Details` (`jsonb`). |
| `CLINICAL_DOCUMENT` | 21 | 15 | -6 | Gom 6 trường kỹ thuật/ngữ cảnh (`Source`, `Description`, `SizeBytes`, `Sha256`, `RejectReason`, `InvestigationId`) vào `Metadata` (`jsonb`); loại bỏ `PlanId`. |
| `CARE_TASK` | 17 | 11 | -6 | Loại bỏ `CareEscalation` (`Escalations` JSONB & luồng `/api/care-tasks/{id}/escalations`, `/api/care-escalations/*`), `EpisodeKey`, `StorageStamp`, `CreatedBy`, `CreatedAt`, `UpdatedAt`; dùng partial unique index `UX_CARE_TASK_MissedAppointment`. |
| `CATALOG_PROVIDER` | 14 | 9 | -5 | Bỏ `SupplyEntity` columns (`NormalizedName`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`); dùng `IX_CATALOG_PROVIDER_Name_Upper`. Lịch sử đọc từ `COMMAND_RECEIPT` (`SystemCatalogChange`). |
| `LAB_WORK_TYPE` | 14 | 9 | -5 | Bỏ `SupplyEntity` columns (`NormalizedName`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`); dùng `IX_LAB_WORK_TYPE_Name_Upper`. Lịch sử đọc từ `COMMAND_RECEIPT` (`SystemCatalogChange`). |
| **Tổng (9 bảng)** | **198** | **121** | **-77** | Toàn schema `public` (33 bảng vật lý): **545 -> 468 cột**. |

---

## 2. Đặc tả chi tiết từng bảng

### 2.1. `USER_ACCOUNT` (`37 -> 18` cột)
- **18 cột giữ lại:** `Id`, `UserName`, `PasswordHash`, `FullName`, `Email`, `EmailConfirmed`, `PhoneNumber`, `AccountType`, `IsActive`, `AssignedRoles`, `EmployeeId`, `EmployeeCode`, `GroupId`, `BranchIds`, `Version`, `CreatedAt`, `UpdatedAt`, `AuthData` (`jsonb`).
- **JSONB `AuthData` (`UserAuthData`):** gồm `SecurityStamp`, `PhoneNumberConfirmed`, `TwoFactorEnabled`, `LockoutEnd`, `LockoutEnabled`, `AccessFailedCount`, `Claims`, `Logins`, `IdentityTokens`, `Sessions` (kèm `RefreshTokens`), `EmailVerifications`, `Recoveries`, `RecoveryDeliveries`. Các thuộc tính tương thích `IdentityUser<int>` trên `ApplicationUser` ủy quyền đọc/ghi sang `AuthData` và tính `NormalizedUserName`/`NormalizedEmail` theo `ToUpperInvariant()`.
- **Đồng thời (Concurrency):** Cột `Version` là phiên bản nghiệp vụ quản trị (`IsConcurrencyToken(false)`), chỉ tăng khi cập nhật tài khoản/trạng thái/gán nhóm quyền (`AccountService`, `SettingsService`). Kiểm soát ghi đồng thời mức hàng trên cả 3 thực thể table-splitting (`ApplicationUser`, `EmployeeProfile`, `StaffAccessAssignment`) sử dụng cột hệ thống ẩn `xmin` (`attnum = -2`, `.Property<uint>("xmin").IsRowVersion()`, 0 cột trong `information_schema.columns`). Khi tạo/gắn thực thể con chia sẻ bảng trong `DcmsDbContext.SaveChangesAsync`, `xmin` và `FullName` được đồng bộ từ bản ghi `ApplicationUser` chủ.
- **Table-splitting:** `EmployeeProfile` chia sẻ cột `"FullName"` (bỏ `"EmployeeFullName"`) và giữ `"EmployeeCode"` làm cột phân biệt bắt buộc; `StaffAccessAssignment` bỏ `"AccessVersion"` (API trả `user.Version`).
- **Index duy nhất dạng biểu thức:**
  - `UserNameIndex`: `UNIQUE ON "USER_ACCOUNT" (UPPER("UserName")) WHERE "UserName" IS NOT NULL`
  - `EmailIndex`: `UNIQUE ON "USER_ACCOUNT" (UPPER("Email")) WHERE "Email" IS NOT NULL`
  - `IX_USER_ACCOUNT_NormalizedPhone`: `UNIQUE ON "USER_ACCOUNT" ((NULLIF(regexp_replace(btrim("PhoneNumber"), '[ ()-]', '', 'g'), ''))) WHERE NULLIF(regexp_replace(btrim("PhoneNumber"), '[ ()-]', '', 'g'), '') IS NOT NULL`

### 2.2. `IN_APP_NOTIFICATION` (`28 -> 19` cột)
- **19 cột giữ lại:** `Id`, `NotificationRecordKind`, `PublicId`, `DedupeKey`, `AccountId`, `BranchId`, `AppointmentId`, `NotificationId`, `Kind`, `Title`, `Body`, `Channel`, `Status`, `AttemptCount`, `CreatedAt`, `ReadAt`, `NextRetryAt`, `Error`, `Data` (`jsonb`).
- **JSONB `Data` (`NotificationData`):** chứa `RequestId`, `DeepLink`, `HasExternalFailure`, `LastAttemptAt`, `PayloadJson`, `DispatchedAt`.
- **Thống nhất kiểu chia sẻ TPH:** `AppointmentOutboxEvent.AggregateId` đổi sang `Guid?` map chung `"AppointmentId"` với `InAppNotification` và `StaffHandlingItem`; `Kind` thống nhất `varchar(80)`; `AppointmentOutboxEvent.OccurredAt` map `"CreatedAt"`; `NotificationDelivery.LastError` map `"Error"`; `AppointmentOutboxEvent.Status` giữ `"Pending"` / `"Dispatched"` (tự đồng bộ khi gán `DispatchedAt`) và index `("Status", "CreatedAt")` lọc `NotificationRecordKind = 'AppointmentOutboxEvent'`.

### 2.3. `PATIENT_PROFILE` (`26 -> 16` cột)
- **16 cột giữ lại:** `Id`, `PatientCode`, `FullName`, `NormalizedName`, `BirthDate`, `Gender`, `Phone`, `Address`, `SourceId`, `CustomerGroupId`, `OccupationId`, `IsArchived`, `CreatedAt`, `UpdatedAt`, `Version`, `Details` (`jsonb`).
- **JSONB `Details` (`PatientProfileDetails`):** `GuardianName`, `GuardianRelationship`, `GuardianPhone`, `Note`, `ProfileDate`, `IntakeHistoryIds`, `IntakeHistoryOther`, `IntakeRecordedAt`, `IntakeRecordedBy`, `ArchivedAt`, `ArchiveReason`.
- **Truy vấn thống kê tham chiếu:** `ReferenceCatalogService.PatientCountsAsync` đọc tiền sử khai báo qua `jsonb_array_elements_text(COALESCE("Details"->'IntakeHistoryIds', '[]'::jsonb))::uuid`.

### 2.4. `FAMILY_RECORD_ACCESS` (`22 -> 13` cột)
- **13 cột giữ lại:** `Id`, `FamilyMemberId`, `OwnerAccountId`, `Code`, `Status`, `PatientId`, `BranchId`, `Scopes`, `RequestedAt`, `ExpiresAt`, `DecidedBy`, `Version`, `Details` (`jsonb`).
- **JSONB `Details` (`FamilyRecordAccessDetails`):** map qua `ComplexProperty(...).ToJson("Details")` chứa 10 trường khai báo/đối chiếu/ghi chú (`DeclaredFullName`, `DeclaredBirthDate`, `Relationship`, `DeclaredPatientCode`, `MatchingInformation`, `SharingBasis`, `VerificationNote`, `DecisionReason`, `PublicNote`, `DecidedAt`), bảo đảm lọc tìm kiếm `Details.DeclaredFullName.Contains(term)` dịch trực tiếp sang SQL.

### 2.5. `PATIENT_ACCOUNT_LINK` (`19 -> 11` cột)
- **11 cột giữ lại:** `Id`, `RequestCode`, `AccountId`, `PatientId`, `DeclaredFullName`, `Status`, `RequestedAt`, `ReviewingEmployeeId`, `RevokingEmployeeId`, `Version`, `Details` (`jsonb`).
- **JSONB `Details` (`PatientAccountLinkDetails`):** map qua `ComplexProperty(...).ToJson("Details")` chứa `DeclaredBirthDate`, `DeclaredPatientCode`, `RequestNote`, `VerificationNote`, `DecisionReason`, `PublicNote`, `ReviewedAt`, `RevokedAt`, `WithdrawnAt`.

### 2.6. `CLINICAL_DOCUMENT` (`21 -> 15` cột)
- **15 cột giữ lại:** `Id`, `PatientId`, `VisitId`, `DossierId`, `FileName`, `ContentType`, `DocumentType`, `SourceDate`, `FileState`, `UploaderId`, `UploadedAt`, `Version`, `Metadata` (`jsonb`), `Reviews` (`jsonb`), `AiAnalyses` (`jsonb`).
- **JSONB `Metadata` (`ClinicalDocumentMetadata`):** map qua `ComplexProperty(...).ToJson("Metadata")` chứa `Source`, `Description`, `SizeBytes`, `Sha256`, `RejectReason`, `InvestigationId` (hỗ trợ dịch SQL `x.Metadata.InvestigationId`).
- **Hạng mục loại bỏ:** `PlanId` (và tham chiếu kế hoạch trong `DocumentMetadata`, `DocumentQuery`, `DocumentContext`, API query/upload và Web `clinical-documents/*`).

### 2.7. `CARE_TASK` (`17 -> 11` cột)
- **11 cột giữ lại:** `Id`, `PatientId`, `BranchId`, `AppointmentId`, `Type`, `Content`, `OwnerAccountId`, `DueAt`, `Status`, `Version`, `ContactAttempts` (`jsonb`).
- **Hạng mục loại bỏ:** `CareEscalation` (`Escalations` JSONB, DTO/endpoint `/api/care-tasks/{id}/escalations`, `/api/care-escalations/*`), `EpisodeKey` (thay bằng partial unique index `UX_CARE_TASK_MissedAppointment` trên `"AppointmentId" WHERE "AppointmentId" IS NOT NULL AND "Type" = 'MissedAppointment'`), `StorageStamp` (`Version` là concurrency token duy nhất và tự tăng khi cập nhật `ContactAttempts`), `CreatedBy`, `CreatedAt`, `UpdatedAt`.

### 2.8. `CATALOG_PROVIDER` (`14 -> 9` cột) và `LAB_WORK_TYPE` (`14 -> 9` cột)
- **`CATALOG_PROVIDER` (9 cột):** `Id`, `Code`, `Name`, `Type`, `Address`, `Phone`, `Email`, `IsActive`, `Version`.
- **`LAB_WORK_TYPE` (9 cột):** `Id`, `Code`, `Name`, `ProviderId`, `Unit`, `ReferencePrice`, `Warranty`, `IsActive`, `Version`.
- **Hạng mục loại bỏ:** 5 cột kế thừa từ `SupplyEntity` (`NormalizedName`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`), đồng bộ cách thiết kế với `CATALOG_MEDICINE`. Kiểm tra trùng tên không phân biệt hoa thường dùng functional unique index `IX_CATALOG_PROVIDER_Name_Upper` / `IX_LAB_WORK_TYPE_Name_Upper` và `ILike`; hợp đồng `ProviderDto`/`LabWorkTypeDto` bỏ 4 trường audit vốn đã lưu tại `COMMAND_RECEIPT` (`SystemCatalogChange`).

---

## 3. Hợp đồng Migration & Rollback
Migration `SimplifyRemainingNineTables`:
- `Up`: thêm 6 cột JSONB mới với giá trị mặc định `'{}'::jsonb`, backfill dữ liệu từ các cột nguồn trước khi `DropColumn`, chuyển đổi khóa đồng thời/cột TPH dùng chung và tạo 6 index biểu thức/bộ lọc mới.
- `Down`: xóa 6 index mới, tạo lại đầy đủ 77 cột đã gỡ với default hợp lệ (gồm `'[]'::jsonb` cho các collection JSONB cũ của `USER_ACCOUNT` và `CARE_TASK."Escalations"`, biểu thức sinh cột `NormalizedPhone`), giải nén đầy đủ dữ liệu từ `AuthData`, `Data`, `Details`, `Metadata` trước khi xóa 4 cột JSONB tổng hợp, rồi khôi phục các index/FK cũ để các kiểm thử rollback (`StorageSimplificationTests`, `AccountStorageTests`, `CareLabStorageTests`, `ExaminationStorageTests`, `NotificationStorageTests`) hoạt động nguyên vẹn.
