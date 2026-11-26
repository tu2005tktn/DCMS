# DCMS — Schema để vẽ ERD bằng tay

Sinh từ EF Core model (`DcmsDbContext`) ngày 2026-11-19. 32 bảng, 75 khoá ngoại.

Ký hiệu: **PK** khoá chính · **FK** khoá ngoại · **UQ** unique · **NN** NOT NULL

## Danh sách bảng

1. [ACCESS_GROUP](#access_group)
2. [APPOINTMENT](#appointment)
3. [AUDIT_LOG](#audit_log)
4. [CARE_TASK](#care_task)
5. [CATALOG_MEDICINE](#catalog_medicine)
6. [CATALOG_PROVIDER](#catalog_provider)
7. [CLINICAL_ACTIVITY](#clinical_activity)
8. [CLINICAL_DOCUMENT](#clinical_document)
9. [CLINIC_SETTINGS_VERSION](#clinic_settings_version)
10. [COMMAND_RECEIPT](#command_receipt)
11. [CUSTOMER_FAMILY_MEMBER](#customer_family_member)
12. [FAMILY_RECORD_ACCESS](#family_record_access)
13. [IN_APP_NOTIFICATION](#in_app_notification)
14. [LAB_ORDER](#lab_order)
15. [LAB_WORK_TYPE](#lab_work_type)
16. [MATERIAL_CATALOG_ITEM](#material_catalog_item)
17. [MATERIAL_CATEGORY](#material_category)
18. [MATERIAL_PURCHASE](#material_purchase)
19. [MATERIAL_USAGE](#material_usage)
20. [PATIENT_ACCOUNT_LINK](#patient_account_link)
21. [PATIENT_PROFILE](#patient_profile)
22. [PRESCRIPTION](#prescription)
23. [PUSH_DEVICE](#push_device)
24. [REFERENCE_ENTRY](#reference_entry)
25. [SCHEDULING_BRANCH](#scheduling_branch)
26. [SCHEDULING_SERVICE](#scheduling_service)
27. [TREATMENT_DOSSIER](#treatment_dossier)
28. [TREATMENT_PLAN_ITEM](#treatment_plan_item)
29. [TREATMENT_TEMPLATE](#treatment_template)
30. [USER_ACCOUNT](#user_account)
31. [VISIT](#visit)
32. [WORKING_SCHEDULE](#working_schedule)

## Quan hệ (FK → bảng cha)

Đọc: bảng con (nhiều) `}o──||` bảng cha (một). Cột FK cho phép NULL ⇒ quan hệ tuỳ chọn (0..1).

| # | Bảng con | Cột FK | → Bảng cha | Cột | Bản số | ON DELETE |
|---|---|---|---|---|---|---|
| 1 | APPOINTMENT | BookerAccountId | USER_ACCOUNT | Id | 0..1 — N | RESTRICT |
| 2 | APPOINTMENT | BranchId | SCHEDULING_BRANCH | Id | 1 — N | RESTRICT |
| 3 | APPOINTMENT | DentistId | USER_ACCOUNT | EmployeeId | 1 — N | RESTRICT |
| 4 | APPOINTMENT | DossierId | TREATMENT_DOSSIER | Id | 0..1 — N | RESTRICT |
| 5 | APPOINTMENT | FamilyMemberId | CUSTOMER_FAMILY_MEMBER | Id | 0..1 — N | RESTRICT |
| 6 | APPOINTMENT | PatientId | PATIENT_PROFILE | Id | 0..1 — N | RESTRICT |
| 7 | APPOINTMENT | ServiceId, BranchId | SCHEDULING_SERVICE | Id, BranchId | 0..1 — N | RESTRICT |
| 8 | AUDIT_LOG | AccessId | FAMILY_RECORD_ACCESS | Id | 0..1 — N | RESTRICT |
| 9 | AUDIT_LOG | ActorAccountId | USER_ACCOUNT | Id | 0..1 — N | RESTRICT |
| 10 | AUDIT_LOG | ActorId | USER_ACCOUNT | Id | 0..1 — N | RESTRICT |
| 11 | AUDIT_LOG | AppointmentId | APPOINTMENT | Id | 0..1 — N | RESTRICT |
| 12 | AUDIT_LOG | DecidingDentistId | USER_ACCOUNT | EmployeeId | 0..1 — N | RESTRICT |
| 13 | AUDIT_LOG | DocumentId | CLINICAL_DOCUMENT | Id | 0..1 — N | RESTRICT |
| 14 | AUDIT_LOG | DossierId | TREATMENT_DOSSIER | Id | 0..1 — N | RESTRICT |
| 15 | AUDIT_LOG | LinkId | PATIENT_ACCOUNT_LINK | Id | 0..1 — N | RESTRICT |
| 16 | AUDIT_LOG | PatientId | PATIENT_PROFILE | Id | 0..1 — N | RESTRICT |
| 17 | AUDIT_LOG | TargetId | USER_ACCOUNT | Id | 0..1 — N | RESTRICT |
| 18 | AUDIT_LOG | VisitId | VISIT | Id | 0..1 — N | RESTRICT |
| 19 | CLINICAL_ACTIVITY | DossierId | TREATMENT_DOSSIER | Id | 1 — N | RESTRICT |
| 20 | CLINICAL_ACTIVITY | ItemId | TREATMENT_PLAN_ITEM | Id | 0..1 — N | SET NULL |
| 21 | CLINICAL_DOCUMENT | DossierId | TREATMENT_DOSSIER | Id | 0..1 — N | RESTRICT |
| 22 | CLINICAL_DOCUMENT | UploaderId | USER_ACCOUNT | Id | 1 — N | RESTRICT |
| 23 | CLINICAL_DOCUMENT | VisitId | VISIT | Id | 0..1 — N | RESTRICT |
| 24 | COMMAND_RECEIPT | AccessId | FAMILY_RECORD_ACCESS | Id | 0..1 — N | RESTRICT |
| 25 | COMMAND_RECEIPT | ActorAccountId | USER_ACCOUNT | Id | 0..1 — N | RESTRICT |
| 26 | COMMAND_RECEIPT | ActorId | USER_ACCOUNT | Id | 0..1 — N | RESTRICT |
| 27 | COMMAND_RECEIPT | AppointmentId | APPOINTMENT | Id | 0..1 — N | RESTRICT |
| 28 | COMMAND_RECEIPT | DossierId | TREATMENT_DOSSIER | Id | 0..1 — N | RESTRICT |
| 29 | COMMAND_RECEIPT | OwnerAccountId | USER_ACCOUNT | Id | 0..1 — N | RESTRICT |
| 30 | COMMAND_RECEIPT | PatientId | PATIENT_PROFILE | Id | 0..1 — N | RESTRICT |
| 31 | COMMAND_RECEIPT | PrescriptionId | PRESCRIPTION | Id | 0..1 — N | RESTRICT |
| 32 | COMMAND_RECEIPT | VisitId | VISIT | Id | 0..1 — N | RESTRICT |
| 33 | CUSTOMER_FAMILY_MEMBER | OwnerAccountId | USER_ACCOUNT | Id | 1 — N | RESTRICT |
| 34 | FAMILY_RECORD_ACCESS | BranchId | SCHEDULING_BRANCH | Id | 0..1 — N | RESTRICT |
| 35 | FAMILY_RECORD_ACCESS | DecidedBy | USER_ACCOUNT | Id | 0..1 — N | RESTRICT |
| 36 | FAMILY_RECORD_ACCESS | FamilyMemberId | CUSTOMER_FAMILY_MEMBER | Id | 1 — 0..1 | RESTRICT |
| 37 | FAMILY_RECORD_ACCESS | OwnerAccountId | USER_ACCOUNT | Id | 1 — N | RESTRICT |
| 38 | FAMILY_RECORD_ACCESS | PatientId | PATIENT_PROFILE | Id | 0..1 — N | RESTRICT |
| 39 | IN_APP_NOTIFICATION | AccountId | USER_ACCOUNT | Id | 0..1 — N | RESTRICT |
| 40 | IN_APP_NOTIFICATION | AppointmentId | APPOINTMENT | Id | 0..1 — N | RESTRICT |
| 41 | IN_APP_NOTIFICATION | NotificationId | IN_APP_NOTIFICATION | Id | 0..1 — N | RESTRICT |
| 42 | LAB_ORDER | ActivityId | CLINICAL_ACTIVITY | Id | 1 — 0..1 | CASCADE |
| 43 | LAB_WORK_TYPE | ProviderId | CATALOG_PROVIDER | Id | 0..1 — N | RESTRICT |
| 44 | MATERIAL_CATALOG_ITEM | CategoryId | MATERIAL_CATEGORY | Id | 1 — N | RESTRICT |
| 45 | MATERIAL_PURCHASE | MaterialId | MATERIAL_CATALOG_ITEM | Id | 1 — N | RESTRICT |
| 46 | MATERIAL_PURCHASE | SupplierId | CATALOG_PROVIDER | Id | 0..1 — N | RESTRICT |
| 47 | MATERIAL_USAGE | ActivityId | CLINICAL_ACTIVITY | Id | 1 — N | CASCADE |
| 48 | MATERIAL_USAGE | MaterialId | MATERIAL_CATALOG_ITEM | Id | 1 — N | RESTRICT |
| 49 | PATIENT_ACCOUNT_LINK | AccountId | USER_ACCOUNT | Id | 1 — 0..1 | RESTRICT |
| 50 | PATIENT_ACCOUNT_LINK | PatientId | PATIENT_PROFILE | Id | 0..1 — 0..1 | RESTRICT |
| 51 | PATIENT_ACCOUNT_LINK | ReviewingEmployeeId | USER_ACCOUNT | EmployeeId | 0..1 — N | RESTRICT |
| 52 | PATIENT_ACCOUNT_LINK | RevokingEmployeeId | USER_ACCOUNT | EmployeeId | 0..1 — N | RESTRICT |
| 53 | PATIENT_PROFILE | CustomerGroupId | REFERENCE_ENTRY | Id | 0..1 — N | RESTRICT |
| 54 | PATIENT_PROFILE | OccupationId | REFERENCE_ENTRY | Id | 0..1 — N | RESTRICT |
| 55 | PATIENT_PROFILE | SourceId | REFERENCE_ENTRY | Id | 0..1 — N | RESTRICT |
| 56 | PRESCRIPTION | BranchId | SCHEDULING_BRANCH | Id | 1 — N | RESTRICT |
| 57 | PRESCRIPTION | PatientId | PATIENT_PROFILE | Id | 1 — N | RESTRICT |
| 58 | PRESCRIPTION | PrescriberEmployeeId | USER_ACCOUNT | EmployeeId | 1 — N | RESTRICT |
| 59 | PUSH_DEVICE | AccountId | USER_ACCOUNT | Id | 1 — N | CASCADE |
| 60 | SCHEDULING_SERVICE | BranchId | SCHEDULING_BRANCH | Id | 1 — 0..1 | RESTRICT |
| 61 | TREATMENT_DOSSIER | BranchId | SCHEDULING_BRANCH | Id | 1 — N | RESTRICT |
| 62 | TREATMENT_DOSSIER | PatientId | PATIENT_PROFILE | Id | 1 — N | RESTRICT |
| 63 | TREATMENT_DOSSIER | SourceExaminationVisitId | VISIT | Id | 0..1 — N | RESTRICT |
| 64 | TREATMENT_PLAN_ITEM | DossierId | TREATMENT_DOSSIER | Id | 1 — N | RESTRICT |
| 65 | TREATMENT_TEMPLATE | BranchId | SCHEDULING_BRANCH | Id | 1 — N | RESTRICT |
| 66 | USER_ACCOUNT | GroupId | ACCESS_GROUP | Id | 0..1 — N | RESTRICT |
| 67 | VISIT | AppointmentId | APPOINTMENT | Id | 1 — 0..1 | RESTRICT |
| 68 | VISIT | BranchId | SCHEDULING_BRANCH | Id | 1 — N | RESTRICT |
| 69 | VISIT | DentistId | USER_ACCOUNT | EmployeeId | 1 — N | RESTRICT |
| 70 | VISIT | PatientId | PATIENT_PROFILE | Id | 1 — N | RESTRICT |
| 71 | VISIT | RecordedBy | USER_ACCOUNT | Id | 1 — N | RESTRICT |
| 72 | VISIT | UpdatedBy | USER_ACCOUNT | Id | 0..1 — N | RESTRICT |
| 73 | WORKING_SCHEDULE | BranchId | SCHEDULING_BRANCH | Id | 0..1 — N | RESTRICT |
| 74 | WORKING_SCHEDULE | EmployeeId | USER_ACCOUNT | EmployeeId | 1 — N | RESTRICT |
| 75 | WORKING_SCHEDULE | PublishedBy | USER_ACCOUNT | Id | 1 — N | RESTRICT |

Bản số ghi theo dạng *cha — con*: ví dụ `1 — N` = một bản ghi cha có nhiều con, con bắt buộc có cha.

## Chi tiết từng bảng

### ACCESS_GROUP

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| RecordKind | varchar(24) |  | ✓ |  |
| IdentityRoleId | integer | UQ |  |  |
| Code | varchar(40) |  | ✓ |  |
| Name | varchar(160) |  | ✓ |  |
| Description | varchar(1000) |  |  |  |
| BaseRole | varchar(40) |  | ✓ |  |
| Active | boolean |  | ✓ |  |
| Version | integer |  | ✓ |  |
| Grants | jsonb |  | ✓ | DEFAULT ('[]'::jsonb) |

### APPOINTMENT

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| Code | varchar(40) | UQ | ✓ |  |
| BranchId | integer | FK→SCHEDULING_BRANCH FK→SCHEDULING_SERVICE | ✓ |  |
| BookerAccountId | integer | FK→USER_ACCOUNT |  |  |
| FamilyMemberId | uuid | FK→CUSTOMER_FAMILY_MEMBER |  |  |
| PatientId | integer | FK→PATIENT_PROFILE |  |  |
| AttendeeName | varchar(100) |  | ✓ |  |
| Kind | varchar(16) |  | ✓ |  |
| Source | varchar(24) |  | ✓ |  |
| ServiceId | integer | FK→SCHEDULING_SERVICE |  |  |
| Reason | varchar(1000) |  |  |  |
| DentistId | integer | FK→USER_ACCOUNT | ✓ |  |
| ChairId | integer |  | ✓ |  |
| StartAt | timestamptz |  | ✓ |  |
| EndAt | timestamptz |  | ✓ |  |
| DossierId | uuid | FK→TREATMENT_DOSSIER |  |  |
| Status | varchar(24) |  | ✓ |  |
| Version | integer |  | ✓ |  |
| CreatedAt | timestamptz |  | ✓ |  |
| BookingInfo | jsonb |  | ✓ |  |
| Reminders | jsonb |  | ✓ |  |
| SupportRequests | jsonb |  | ✓ |  |

### AUDIT_LOG

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| StorageId | uuid | PK | ✓ |  |
| AuditType | varchar(34) |  | ✓ |  |
| LegacyNumber | bigint |  |  | DEFAULT (nextval('audit_log_number_seq')) |
| ActorId | integer | FK→USER_ACCOUNT |  |  |
| TargetId | integer | FK→USER_ACCOUNT |  |  |
| OccurredAt | timestamptz |  |  |  |
| Action | varchar(64) |  |  |  |
| Reason | varchar(1000) |  |  |  |
| BeforeJson | jsonb |  |  |  |
| AfterJson | jsonb |  |  |  |
| LegacyId | uuid |  |  |  |
| AppointmentId | uuid | FK→APPOINTMENT |  |  |
| ActorAccountId | integer | FK→USER_ACCOUNT |  |  |
| ActorKind | varchar(16) |  |  |  |
| OldStart | timestamptz |  |  |  |
| OldEnd | timestamptz |  |  |  |
| OldDentistId | integer |  |  |  |
| OldChairId | integer |  |  |  |
| NewStart | timestamptz |  |  |  |
| NewEnd | timestamptz |  |  |  |
| NewDentistId | integer |  |  |  |
| NewChairId | integer |  |  |  |
| RequesterVerification | varchar(500) |  |  |  |
| RequestKey | uuid |  |  |  |
| RequestId | uuid |  |  |  |
| Notes | varchar(500) |  |  |  |
| ChannelType | varchar(16) |  |  |  |
| Result | varchar(20) |  |  |  |
| DocumentId | uuid | FK→CLINICAL_DOCUMENT |  |  |
| Note | varchar(500) |  |  |  |
| AccessId | uuid | FK→FAMILY_RECORD_ACCESS |  |  |
| LinkId | integer | FK→PATIENT_ACCOUNT_LINK |  |  |
| PatientId | integer | FK→PATIENT_PROFILE |  |  |
| ChangesJson | jsonb |  |  |  |
| BranchId | integer |  |  |  |
| VisitId | uuid | FK→VISIT |  |  |
| TemplateId | uuid |  |  |  |
| At | timestamptz |  |  |  |
| BranchIds | integer[] |  |  |  |
| Section | varchar(40) |  |  |  |
| Target | varchar(100) |  |  |  |
| Version | integer |  |  |  |
| DossierId | uuid | FK→TREATMENT_DOSSIER |  |  |
| Kind | varchar(80) |  |  |  |
| PayloadJson | jsonb |  |  |  |
| EventActorId | integer |  |  |  |
| DossierVersion | integer |  |  |  |
| AggregateId | uuid |  |  |  |
| StableKey | varchar(160) | UQ |  |  |
| DecidingDentistId | integer | FK→USER_ACCOUNT |  |  |

### CARE_TASK

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| PatientId | integer |  |  | trống: vắng hẹn của lịch chưa có hồ sơ |
| BranchId | integer |  | ✓ |  |
| AppointmentId | uuid |  |  |  |
| Type | varchar(40) |  | ✓ |  |
| Content | varchar(4000) |  | ✓ |  |
| OwnerAccountId | integer |  |  |  |
| DueAt | timestamptz |  | ✓ |  |
| Status | varchar(16) |  | ✓ |  |
| Version | integer |  | ✓ | DEFAULT 1 |
| ContactAttempts | jsonb |  | ✓ |  |

### CATALOG_MEDICINE

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| Code | varchar(40) | UQ | ✓ |  |
| Name | varchar(200) |  | ✓ |  |
| BaseUnit | varchar(40) |  | ✓ |  |
| ReferencePrice | bigint |  |  |  |
| PrescriptionEligible | boolean |  | ✓ |  |
| InternalStockTracked | boolean |  | ✓ |  |
| IsActive | boolean |  | ✓ |  |
| Version | integer |  | ✓ | DEFAULT 1 |
| Spec | jsonb |  | ✓ |  |

### CATALOG_PROVIDER

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| Type | varchar(16) |  | ✓ |  |
| Address | varchar(1000) |  |  |  |
| Phone | varchar(20) |  |  |  |
| Email | varchar(254) |  |  |  |
| Code | varchar(40) | UQ | ✓ |  |
| Name | varchar(160) |  | ✓ |  |
| IsActive | boolean |  | ✓ |  |
| Version | integer |  | ✓ | DEFAULT 1 |

### CLINICAL_ACTIVITY

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| DossierId | uuid | FK→TREATMENT_DOSSIER | ✓ |  |
| ItemId | uuid | FK→TREATMENT_PLAN_ITEM |  |  |
| PlanVersion | integer |  |  |  |
| IsPlanTransfer | boolean |  | ✓ |  |
| ServiceId | integer |  | ✓ |  |
| ServiceNameSnapshot | varchar(160) |  |  |  |
| Region | varchar(80) |  |  |  |
| Unit | varchar(40) |  | ✓ |  |
| Quantity | numeric(12,3) |  | ✓ |  |
| LabWorkSnapshot | varchar(1000) |  |  |  |
| PerformedAt | timestamptz |  | ✓ |  |
| PerformerId | integer |  | ✓ |  |
| AssistantId | integer |  |  |  |
| Outcome | varchar(4000) |  | ✓ |  |
| PriceSnapshotVnd | numeric(18,0) |  | ✓ |  |
| DiscountSnapshotVnd | bigint |  | ✓ |  |
| FeeStatus | varchar(24) |  | ✓ | DEFAULT 'Unavailable' |
| Version | integer |  | ✓ |  |
| CreatedAt | timestamptz |  | ✓ |  |

### CLINICAL_DOCUMENT

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| PatientId | integer |  | ✓ |  |
| VisitId | uuid | FK→VISIT |  |  |
| DossierId | uuid | FK→TREATMENT_DOSSIER |  |  |
| FileName | varchar(255) |  | ✓ |  |
| ContentType | varchar(64) |  | ✓ |  |
| DocumentType | varchar(24) |  | ✓ |  |
| SourceDate | date |  | ✓ |  |
| FileState | varchar(16) |  | ✓ |  |
| UploaderId | integer | FK→USER_ACCOUNT | ✓ |  |
| UploadedAt | timestamptz |  | ✓ |  |
| Version | integer |  | ✓ |  |
| AiAnalyses | jsonb |  | ✓ |  |
| Metadata | jsonb |  | ✓ |  |
| Reviews | jsonb |  | ✓ |  |

### CLINIC_SETTINGS_VERSION

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Version | integer | PK | ✓ |  |
| EffectiveAt | timestamptz |  | ✓ |  |
| ActorId | integer |  | ✓ |  |
| Reason | varchar(500) |  | ✓ |  |
| RestoredFromVersion | integer |  |  |  |
| ContentJson | jsonb |  | ✓ |  |

### COMMAND_RECEIPT

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| StorageId | uuid | PK | ✓ |  |
| RequestKey | uuid |  | ✓ |  |
| Fingerprint | varchar(64) |  | ✓ | DEFAULT '' |
| ResultJson | jsonb |  | ✓ | DEFAULT '{}' |
| ReceiptType | varchar(34) |  | ✓ |  |
| ActorAccountId | integer | FK→USER_ACCOUNT |  |  |
| AppointmentId | uuid | FK→APPOINTMENT |  |  |
| ActorScope | varchar(96) |  |  |  |
| SupportRequestId | uuid |  |  |  |
| Id | uuid |  |  |  |
| SessionId | uuid |  |  |  |
| ExpectedVersion | integer |  |  |  |
| ActorId | integer | FK→USER_ACCOUNT |  |  |
| VisitId | uuid | FK→VISIT |  |  |
| OwnerAccountId | integer | FK→USER_ACCOUNT |  |  |
| CreatedAt | timestamptz |  |  |  |
| AccessId | uuid | FK→FAMILY_RECORD_ACCESS |  |  |
| PatientId | integer | FK→PATIENT_PROFILE |  |  |
| PrescriptionId | uuid | FK→PRESCRIPTION |  |  |
| Command | varchar(48) |  |  |  |
| ResourceId | uuid |  |  |  |
| BeforeJson | jsonb |  |  |  |
| OccurredAt | timestamptz |  |  |  |
| ResultId | uuid |  |  |  |
| DossierId | uuid | FK→TREATMENT_DOSSIER |  |  |

### CUSTOMER_FAMILY_MEMBER

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| OwnerAccountId | integer | FK→USER_ACCOUNT | ✓ |  |
| FullName | varchar(100) |  | ✓ |  |
| Relationship | varchar(20) |  | ✓ |  |
| BirthDate | date |  |  |  |
| Gender | varchar(16) |  |  |  |
| ContactPhone | varchar(10) |  |  |  |
| Version | integer |  | ✓ |  |
| DeletedAt | timestamptz |  |  |  |

### FAMILY_RECORD_ACCESS

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| FamilyMemberId | uuid | FK→CUSTOMER_FAMILY_MEMBER UQ | ✓ |  |
| OwnerAccountId | integer | FK→USER_ACCOUNT | ✓ |  |
| Code | varchar(40) | UQ | ✓ |  |
| Status | varchar(16) |  | ✓ |  |
| PatientId | integer | FK→PATIENT_PROFILE |  |  |
| BranchId | integer | FK→SCHEDULING_BRANCH |  |  |
| Scopes | text[] |  | ✓ |  |
| RequestedAt | timestamptz |  | ✓ |  |
| ExpiresAt | timestamptz |  |  |  |
| DecidedBy | integer | FK→USER_ACCOUNT |  |  |
| Version | integer |  | ✓ |  |
| Details | jsonb |  | ✓ |  |

### IN_APP_NOTIFICATION

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| Data | jsonb |  | ✓ | DEFAULT ('{}'::jsonb) |
| NotificationRecordKind | varchar(34) |  | ✓ |  |
| AppointmentId | uuid | FK→APPOINTMENT |  |  |
| Kind | varchar(80) |  |  |  |
| Status | varchar(16) |  |  |  |
| CreatedAt | timestamptz |  |  |  |
| PublicId | varchar(40) | UQ |  |  |
| AccountId | integer | FK→USER_ACCOUNT |  |  |
| BranchId | integer |  |  |  |
| Title | varchar(160) |  |  |  |
| Body | varchar(1000) |  |  |  |
| ReadAt | timestamptz |  |  |  |
| DedupeKey | varchar(160) | UQ |  |  |
| NotificationId | uuid | FK→IN_APP_NOTIFICATION |  |  |
| Channel | varchar(16) |  |  |  |
| AttemptCount | integer |  |  |  |
| NextRetryAt | timestamptz |  |  |  |
| Error | varchar(1000) |  |  |  |

### LAB_ORDER

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| ActivityId | uuid | FK→CLINICAL_ACTIVITY UQ | ✓ |  |
| Code | varchar(40) |  |  |  |
| DentistId | integer |  | ✓ |  |
| LabProviderId | uuid |  |  |  |
| LabWorkTypeId | uuid |  |  |  |
| WorkName | varchar(160) |  | ✓ |  |
| OrderedOn | date |  | ✓ |  |
| DueOn | date |  |  |  |
| DeliveredOn | date |  |  |  |
| Quantity | integer |  | ✓ |  |
| TotalFeeVnd | bigint |  | ✓ |  |
| PaidVnd | bigint |  | ✓ |  |
| Note | varchar(1000) |  |  |  |
| Status | varchar(20) |  | ✓ | DEFAULT 'NotSent' |
| Version | integer |  | ✓ | DEFAULT 1 |
| Spec | jsonb |  | ✓ |  |
| StatusLogs | jsonb |  | ✓ |  |

### LAB_WORK_TYPE

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| ProviderId | uuid | FK→CATALOG_PROVIDER |  |  |
| Unit | varchar(16) |  | ✓ |  |
| ReferencePrice | bigint |  |  |  |
| Warranty | varchar(1000) |  |  |  |
| Code | varchar(40) | UQ | ✓ |  |
| Name | varchar(160) |  | ✓ |  |
| IsActive | boolean |  | ✓ |  |
| Version | integer |  | ✓ | DEFAULT 1 |

### MATERIAL_CATALOG_ITEM

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| Name | varchar(160) |  | ✓ |  |
| CategoryId | uuid | FK→MATERIAL_CATEGORY | ✓ |  |
| UsageUnit | varchar(40) |  |  |  |
| Version | integer |  | ✓ |  |
| IsDeleted | boolean |  | ✓ |  |

### MATERIAL_CATEGORY

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| Name | varchar(160) |  | ✓ |  |
| Version | integer |  | ✓ |  |
| IsDeleted | boolean |  | ✓ |  |

### MATERIAL_PURCHASE

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| MaterialId | uuid | FK→MATERIAL_CATALOG_ITEM | ✓ |  |
| SupplierId | uuid | FK→CATALOG_PROVIDER |  |  |
| LotNumber | varchar(60) |  |  |  |
| EnteredOn | date |  | ✓ |  |
| ReceivedOn | date |  |  |  |
| ExpiresOn | date |  |  |  |
| Quantity | numeric(12,3) |  | ✓ |  |
| Unit | varchar(40) |  | ✓ |  |
| ConversionFactor | numeric(12,3) |  | ✓ |  |
| UsageQuantity | numeric(18,3) |  | ✓ |  |
| UnitPriceVnd | bigint |  | ✓ |  |
| TotalVnd | bigint |  | ✓ |  |
| PaidVnd | bigint |  | ✓ |  |
| Version | integer |  | ✓ |  |
| IsDeleted | boolean |  | ✓ |  |
| CreatedAt | timestamptz |  | ✓ |  |

### MATERIAL_USAGE

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| ActivityId | uuid | FK→CLINICAL_ACTIVITY | ✓ |  |
| MaterialId | uuid | FK→MATERIAL_CATALOG_ITEM | ✓ |  |
| Quantity | numeric(18,3) |  | ✓ |  |
| UsedAt | timestamptz |  | ✓ |  |
| RecordedBy | integer |  |  |  |

### PATIENT_ACCOUNT_LINK

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | integer | PK |  | GENERATED BY DEFAULT AS IDENTITY |
| RequestCode | varchar(32) | UQ | ✓ |  |
| AccountId | integer | FK→USER_ACCOUNT UQ | ✓ |  |
| PatientId | integer | FK→PATIENT_PROFILE UQ |  |  |
| DeclaredFullName | varchar(160) |  | ✓ |  |
| Status | varchar(16) |  | ✓ |  |
| RequestedAt | timestamptz |  | ✓ |  |
| ReviewingEmployeeId | integer | FK→USER_ACCOUNT |  |  |
| RevokingEmployeeId | integer | FK→USER_ACCOUNT |  |  |
| Version | integer |  | ✓ |  |
| Details | jsonb |  | ✓ |  |

### PATIENT_PROFILE

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | integer | PK |  | GENERATED BY DEFAULT AS IDENTITY |
| PatientCode | varchar(40) | UQ | ✓ |  |
| FullName | varchar(160) |  | ✓ |  |
| NormalizedName | varchar(160) |  | ✓ |  |
| BirthDate | date |  |  |  |
| Gender | varchar(16) |  |  |  |
| Phone | varchar(24) |  |  |  |
| Address | varchar(255) |  |  |  |
| SourceId | uuid | FK→REFERENCE_ENTRY |  |  |
| CustomerGroupId | uuid | FK→REFERENCE_ENTRY |  |  |
| OccupationId | uuid | FK→REFERENCE_ENTRY |  |  |
| IsArchived | boolean |  | ✓ |  |
| CreatedAt | timestamptz |  | ✓ |  |
| UpdatedAt | timestamptz |  | ✓ |  |
| Version | integer |  | ✓ |  |
| Details | jsonb |  | ✓ | DEFAULT ('{}'::jsonb) |

### PRESCRIPTION

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| PatientId | integer | FK→PATIENT_PROFILE | ✓ |  |
| BranchId | integer | FK→SCHEDULING_BRANCH | ✓ |  |
| PrescriberEmployeeId | integer | FK→USER_ACCOUNT | ✓ |  |
| PrescribedOn | date |  | ✓ |  |
| Diagnosis | varchar(1000) |  | ✓ |  |
| OtherDiagnosis | varchar(1000) |  |  |  |
| Advice | varchar(4000) |  |  |  |
| Version | integer |  | ✓ | DEFAULT 1 |
| CreatedAt | timestamptz |  | ✓ |  |
| Items | jsonb |  | ✓ |  |

### PUSH_DEVICE

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| AccountId | integer | FK→USER_ACCOUNT | ✓ |  |
| Token | varchar(512) | UQ | ✓ |  |
| Platform | varchar(16) |  | ✓ |  |
| UpdatedAt | timestamptz |  | ✓ |  |

### REFERENCE_ENTRY

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| Kind | varchar(32) |  | ✓ |  |
| Code | varchar(40) |  |  |  |
| Name | varchar(160) |  | ✓ |  |
| Description | varchar(1000) |  |  |  |
| IsReserved | boolean |  | ✓ |  |
| IsActive | boolean |  | ✓ |  |
| Version | integer |  | ✓ |  |

### SCHEDULING_BRANCH

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | integer | PK |  | GENERATED BY DEFAULT AS IDENTITY |
| Code | varchar(40) | UQ |  |  |
| Name | varchar(160) |  | ✓ |  |
| Address | varchar(1000) |  |  |  |
| Phone | varchar(20) |  |  |  |
| Email | varchar(254) |  |  |  |
| Active | boolean |  | ✓ |  |
| SlotIntervalMinutes | integer |  | ✓ | DEFAULT 15 |
| Version | integer |  | ✓ | DEFAULT 1 |
| Chairs | jsonb |  | ✓ |  |
| Members | jsonb |  | ✓ |  |

### SCHEDULING_SERVICE

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | integer | PK |  | GENERATED BY DEFAULT AS IDENTITY |
| Unit | varchar(40) |  | ✓ | DEFAULT 'lần' |
| IsInitialExamination | boolean |  | ✓ |  |
| PublicBookable | boolean |  | ✓ | DEFAULT TRUE |
| BranchId | integer | FK→SCHEDULING_BRANCH UQ | ✓ |  |
| Name | varchar(160) |  | ✓ |  |
| PublicDurationMinutes | integer |  | ✓ |  |
| Active | boolean |  | ✓ |  |
| ReferenceUnitPriceVnd | bigint |  |  |  |
| PriceVersion | integer |  | ✓ | DEFAULT 1 |

### TREATMENT_DOSSIER

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| SourceExaminationVisitId | uuid | FK→VISIT |  |  |
| Code | varchar(40) | UQ | ✓ |  |
| PatientId | integer | FK→PATIENT_PROFILE | ✓ |  |
| BranchId | integer | FK→SCHEDULING_BRANCH | ✓ |  |
| DentistId | integer |  | ✓ |  |
| StartedOn | date |  | ✓ |  |
| Diagnosis | varchar(4000) |  | ✓ |  |
| Note | varchar(4000) |  |  |  |
| State | varchar(16) |  | ✓ |  |
| Version | integer |  | ✓ |  |
| CreatedAt | timestamptz |  | ✓ |  |
| UpdatedAt | timestamptz |  | ✓ |  |
| SheetDrafts | jsonb |  | ✓ |  |

### TREATMENT_PLAN_ITEM

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| DossierId | uuid | FK→TREATMENT_DOSSIER | ✓ |  |
| PlannedOrder | integer |  | ✓ |  |
| ServiceId | integer |  | ✓ |  |
| ServiceName | varchar(160) |  | ✓ |  |
| Region | varchar(80) |  |  |  |
| Unit | varchar(40) |  | ✓ |  |
| Quantity | numeric(12,3) |  | ✓ |  |
| EstimatedStart | date |  |  |  |
| EstimatedMinutes | integer |  |  |  |
| Note | varchar(1000) |  |  |  |
| ResponsibleDentistId | integer |  |  |  |
| AssistantId | integer |  |  |  |
| AgreedUnitPriceVnd | bigint |  | ✓ |  |
| DiscountType | varchar(16) |  |  |  |
| DiscountValue | numeric(18,3) |  |  |  |
| DiscountReason | varchar(1000) |  |  |  |
| DisplayCompleted | boolean |  | ✓ |  |

### TREATMENT_TEMPLATE

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| DeletedAt | timestamptz |  |  |  |
| BranchId | integer | FK→SCHEDULING_BRANCH | ✓ |  |
| Name | varchar(160) |  | ✓ |  |
| Version | integer |  | ✓ | DEFAULT 1 |
| CreatedBy | integer |  | ✓ |  |
| CreatedAt | timestamptz |  | ✓ |  |
| Items | jsonb |  | ✓ |  |

### USER_ACCOUNT

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | integer | PK |  | GENERATED BY DEFAULT AS IDENTITY |
| EmployeeId | integer |  | ✓ | DEFAULT (nextval('employee_id_seq')) |
| FullName | varchar(160) |  | ✓ |  |
| AccountType | varchar(16) |  | ✓ |  |
| IsActive | boolean |  | ✓ |  |
| Version | integer |  | ✓ |  |
| CreatedAt | timestamptz |  | ✓ |  |
| UpdatedAt | timestamptz |  | ✓ |  |
| AssignedRoles | text[] |  | ✓ |  |
| AuthData | jsonb |  | ✓ | DEFAULT ('{}'::jsonb) |
| EmployeeCode | varchar(40) | UQ |  |  |
| GroupId | uuid | FK→ACCESS_GROUP |  |  |
| BranchIds | integer[] |  |  |  |
| UserName | varchar(256) |  |  |  |
| Email | varchar(256) |  |  |  |
| EmailConfirmed | boolean |  | ✓ |  |
| PasswordHash | text |  |  |  |
| PhoneNumber | text |  |  |  |

### VISIT

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| AppointmentId | uuid | FK→APPOINTMENT UQ | ✓ |  |
| PatientId | integer | FK→PATIENT_PROFILE | ✓ |  |
| BranchId | integer | FK→SCHEDULING_BRANCH | ✓ |  |
| DentistId | integer | FK→USER_ACCOUNT | ✓ |  |
| Status | varchar(24) |  | ✓ |  |
| Version | integer |  | ✓ |  |
| ArrivalAt | timestamptz |  | ✓ |  |
| RecordedBy | integer | FK→USER_ACCOUNT | ✓ |  |
| ArrivalReason | varchar(500) |  |  |  |
| Purpose | varchar(40) |  | ✓ |  |
| Note | varchar(1000) |  |  |  |
| ExaminationContentJson | jsonb |  |  |  |
| UpdatedBy | integer | FK→USER_ACCOUNT |  |  |
| UpdatedAt | timestamptz |  |  |  |
| Investigations | jsonb |  | ✓ |  |

### WORKING_SCHEDULE

| Cột | Kiểu | Khoá | NN | Ghi chú |
|---|---|---|---|---|
| Id | uuid | PK | ✓ |  |
| EmployeeId | integer | FK→USER_ACCOUNT | ✓ |  |
| Version | integer |  | ✓ |  |
| PublishedBy | integer | FK→USER_ACCOUNT | ✓ |  |
| PublishedAt | timestamptz |  | ✓ |  |
| ScheduleKind | varchar(34) |  | ✓ |  |
| Date | date |  |  |  |
| Kind | varchar(16) |  |  |  |
| Reason | varchar(500) |  |  |  |
| BranchId | integer | FK→SCHEDULING_BRANCH |  |  |
| WeekStart | date |  |  |  |
| Periods | jsonb |  | ✓ |  |
