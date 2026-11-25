# DCMS – Quan hệ giữa các bảng và cách bố trí ERD

Dùng kèm `DCMS_drawio_import.sql` (đã bỏ cột `Version`). 32 bảng, 75 khoá ngoại.

Ký hiệu crow's foot (draw.io: Edge style → ERmandOne / ERzeroToOne / ERmany / ERzeroToMany):

| Ký hiệu | Nghĩa |
|---|---|
| `\|\|` | đúng một (bắt buộc) |
| `o\|` | không hoặc một |
| `}o` | không hoặc nhiều |
| `}\|` | một hoặc nhiều |

Cách đọc mỗi dòng: `CHA ||──o{ CON : cột FK` = một bản ghi cha có 0..N bản ghi con.
Cha `||` khi cột FK là NOT NULL, `o|` khi cột FK cho phép NULL.

---

## 1. Bố trí tổng thể (6 vùng)

```
┌──────────────────────── HÀNG 1 ─────────────────────────────────────────────┐
│ [A] TÀI KHOẢN                    │ [C] CHI NHÁNH & LỊCH                     │
│ ACCESS_GROUP → USER_ACCOUNT      │ SCHEDULING_BRANCH  SCHEDULING_SERVICE    │
│ PUSH_DEVICE  IN_APP_NOTIFICATION │ WORKING_SCHEDULE   TREATMENT_TEMPLATE    │
├──────────────────────── HÀNG 2 ─────────────────────────────────────────────┤
│ [B] BỆNH NHÂN & GIA ĐÌNH         │ APPOINTMENT  (cầu nối B ↔ C ↔ D)         │
│ REFERENCE_ENTRY → PATIENT_PROFILE│                                          │
│ PATIENT_ACCOUNT_LINK             │                                          │
│ CUSTOMER_FAMILY_MEMBER           │                                          │
│ FAMILY_RECORD_ACCESS             │                                          │
├──────────────────────── HÀNG 3 ─────────────────────────────────────────────┤
│ [D] KHÁM & ĐIỀU TRỊ (trung tâm)                                             │
│ VISIT → TREATMENT_DOSSIER → TREATMENT_PLAN_ITEM → CLINICAL_ACTIVITY         │
│ CLINICAL_DOCUMENT        PRESCRIPTION                                       │
├──────────────────────── HÀNG 4 ─────────────────────────────────────────────┤
│ [E] LAB & VẬT TƯ                                                            │
│ LAB_ORDER  LAB_WORK_TYPE  CATALOG_PROVIDER                                  │
│ MATERIAL_CATEGORY → MATERIAL_CATALOG_ITEM → MATERIAL_PURCHASE / _USAGE      │
├──────────────────────── DẢI DƯỚI CÙNG (không nối dây) ──────────────────────┤
│ [F] AUDIT_LOG  COMMAND_RECEIPT  CARE_TASK  CATALOG_MEDICINE                 │
│     CLINIC_SETTINGS_VERSION                                                 │
└─────────────────────────────────────────────────────────────────────────────┘
```

Vị trí gợi ý trên lưới 5 cột × 5 hàng (đọc trái → phải theo luồng nghiệp vụ):

| | Cột 1 | Cột 2 | Cột 3 | Cột 4 | Cột 5 |
|---|---|---|---|---|---|
| **Hàng 1** | ACCESS_GROUP | USER_ACCOUNT | WORKING_SCHEDULE | SCHEDULING_BRANCH | SCHEDULING_SERVICE |
| **Hàng 2** | PUSH_DEVICE | IN_APP_NOTIFICATION | APPOINTMENT | TREATMENT_TEMPLATE | |
| **Hàng 3** | REFERENCE_ENTRY | PATIENT_PROFILE | VISIT | TREATMENT_DOSSIER | TREATMENT_PLAN_ITEM |
| **Hàng 4** | CUSTOMER_FAMILY_MEMBER | PATIENT_ACCOUNT_LINK | CLINICAL_DOCUMENT | PRESCRIPTION | CLINICAL_ACTIVITY |
| **Hàng 5** | FAMILY_RECORD_ACCESS | CATALOG_PROVIDER | LAB_WORK_TYPE | LAB_ORDER | MATERIAL_USAGE |
| **Hàng 6** | | MATERIAL_CATEGORY | MATERIAL_CATALOG_ITEM | MATERIAL_PURCHASE | |
| **Dưới cùng** | AUDIT_LOG | COMMAND_RECEIPT | CARE_TASK | CATALOG_MEDICINE | CLINIC_SETTINGS_VERSION |

Nguyên tắc:
- **USER_ACCOUNT** và **SCHEDULING_BRANCH** là 2 bảng bị trỏ tới nhiều nhất → đặt ở hàng trên cùng để dây đi xuống, không cắt ngang sơ đồ.
- **PATIENT_PROFILE** đặt cạnh VISIT/APPOINTMENT vì 3 bảng này nối nhau nhiều nhất.
- Chuỗi điều trị VISIT → TREATMENT_DOSSIER → TREATMENT_PLAN_ITEM → CLINICAL_ACTIVITY nằm trên một hàng ngang, đọc như một luồng.
- Bảng nhỏ (catalog, danh mục) đặt ở rìa, bảng nghiệp vụ lớn ở giữa.

---

## 2. Quan hệ theo vùng

### [A] Tài khoản

```
ACCESS_GROUP        o|──o{ USER_ACCOUNT        : GroupId
USER_ACCOUNT        ||──o{ PUSH_DEVICE         : AccountId
USER_ACCOUNT        o|──o{ IN_APP_NOTIFICATION : AccountId
APPOINTMENT         o|──o{ IN_APP_NOTIFICATION : AppointmentId
IN_APP_NOTIFICATION o|──o{ IN_APP_NOTIFICATION : NotificationId (tự tham chiếu)
```

### [B] Bệnh nhân & gia đình

```
REFERENCE_ENTRY        o|──o{ PATIENT_PROFILE        : CustomerGroupId, OccupationId, SourceId (3 dây, có thể gộp 1 dây ghi 3 cột)
USER_ACCOUNT           ||──o| PATIENT_ACCOUNT_LINK   : AccountId (1–1)
PATIENT_PROFILE        o|──o| PATIENT_ACCOUNT_LINK   : PatientId (1–1)
USER_ACCOUNT           o|──o{ PATIENT_ACCOUNT_LINK   : ReviewingEmployeeId, RevokingEmployeeId
USER_ACCOUNT           ||──o{ CUSTOMER_FAMILY_MEMBER : OwnerAccountId
CUSTOMER_FAMILY_MEMBER ||──o| FAMILY_RECORD_ACCESS   : FamilyMemberId (1–1)
USER_ACCOUNT           ||──o{ FAMILY_RECORD_ACCESS   : OwnerAccountId
USER_ACCOUNT           o|──o{ FAMILY_RECORD_ACCESS   : DecidedBy
PATIENT_PROFILE        o|──o{ FAMILY_RECORD_ACCESS   : PatientId
SCHEDULING_BRANCH      o|──o{ FAMILY_RECORD_ACCESS   : BranchId
```

### [C] Chi nhánh & lịch

```
SCHEDULING_BRANCH ||──o{ SCHEDULING_SERVICE : BranchId
SCHEDULING_BRANCH ||──o{ TREATMENT_TEMPLATE : BranchId
SCHEDULING_BRANCH o|──o{ WORKING_SCHEDULE   : BranchId
USER_ACCOUNT      ||──o{ WORKING_SCHEDULE   : EmployeeId, PublishedBy
```

### Lịch hẹn (cầu nối)

```
PATIENT_PROFILE        o|──o{ APPOINTMENT : PatientId
CUSTOMER_FAMILY_MEMBER o|──o{ APPOINTMENT : FamilyMemberId
USER_ACCOUNT           ||──o{ APPOINTMENT : DentistId (→ USER_ACCOUNT.EmployeeId)
USER_ACCOUNT           o|──o{ APPOINTMENT : BookerAccountId
SCHEDULING_BRANCH      ||──o{ APPOINTMENT : BranchId
SCHEDULING_SERVICE     o|──o{ APPOINTMENT : (ServiceId, BranchId) – khoá ghép
TREATMENT_DOSSIER      o|──o{ APPOINTMENT : DossierId
```

### [D] Khám & điều trị

```
APPOINTMENT       ||──o| VISIT               : AppointmentId (1 lịch hẹn → tối đa 1 lượt khám)
PATIENT_PROFILE   ||──o{ VISIT               : PatientId
SCHEDULING_BRANCH ||──o{ VISIT               : BranchId
USER_ACCOUNT      ||──o{ VISIT               : DentistId, RecordedBy, (UpdatedBy nullable)

PATIENT_PROFILE   ||──o{ TREATMENT_DOSSIER   : PatientId
SCHEDULING_BRANCH ||──o{ TREATMENT_DOSSIER   : BranchId
VISIT             o|──o{ TREATMENT_DOSSIER   : SourceExaminationVisitId

TREATMENT_DOSSIER   ||──o{ TREATMENT_PLAN_ITEM : DossierId
TREATMENT_DOSSIER   ||──o{ CLINICAL_ACTIVITY   : DossierId
TREATMENT_PLAN_ITEM o|──o{ CLINICAL_ACTIVITY   : ItemId

VISIT             o|──o{ CLINICAL_DOCUMENT   : VisitId
TREATMENT_DOSSIER o|──o{ CLINICAL_DOCUMENT   : DossierId   (CHECK: phải có VisitId hoặc DossierId)
USER_ACCOUNT      ||──o{ CLINICAL_DOCUMENT   : UploaderId

PATIENT_PROFILE   ||──o{ PRESCRIPTION        : PatientId
SCHEDULING_BRANCH ||──o{ PRESCRIPTION        : BranchId
USER_ACCOUNT      ||──o{ PRESCRIPTION        : PrescriberEmployeeId
```

### [E] Lab & vật tư

```
CLINICAL_ACTIVITY     ||──o| LAB_ORDER             : ActivityId (1–1)
CATALOG_PROVIDER      o|──o{ LAB_WORK_TYPE         : ProviderId
MATERIAL_CATEGORY     ||──o{ MATERIAL_CATALOG_ITEM : CategoryId
MATERIAL_CATALOG_ITEM ||──o{ MATERIAL_PURCHASE     : MaterialId
CATALOG_PROVIDER      o|──o{ MATERIAL_PURCHASE     : SupplierId
MATERIAL_CATALOG_ITEM ||──o{ MATERIAL_USAGE        : MaterialId
CLINICAL_ACTIVITY     ||──o{ MATERIAL_USAGE        : ActivityId
```

### [F] Bảng hệ thống (khuyên KHÔNG nối dây)

| Bảng | Có FK tới | Ghi chú |
|---|---|---|
| AUDIT_LOG | USER_ACCOUNT (×4), PATIENT_PROFILE, APPOINTMENT, VISIT, TREATMENT_DOSSIER, CLINICAL_DOCUMENT, PATIENT_ACCOUNT_LINK, FAMILY_RECORD_ACCESS | 11 FK, toàn bộ nullable – nhật ký kiểm toán |
| COMMAND_RECEIPT | USER_ACCOUNT (×3), PATIENT_PROFILE, APPOINTMENT, VISIT, TREATMENT_DOSSIER, PRESCRIPTION, FAMILY_RECORD_ACCESS | 9 FK, toàn bộ nullable – chống gửi lệnh trùng (idempotency) |
| CARE_TASK | — | Có cột PatientId, BranchId, AppointmentId nhưng **không có FK** trong DB; nếu muốn thể hiện thì vẽ nét đứt |
| CATALOG_MEDICINE | — | Danh mục thuốc, độc lập |
| CLINIC_SETTINGS_VERSION | — | Cấu hình hệ thống, độc lập |

Hai bảng AUDIT_LOG và COMMAND_RECEIPT chiếm 20/75 dây. Bỏ dây của chúng đi, sơ đồ còn 55 dây và gọn hơn hẳn; ghi một chú thích: *"AUDIT_LOG, COMMAND_RECEIPT tham chiếu tuỳ chọn tới các bảng nghiệp vụ chính"*.

---

## 3. Mẹo cho gọn

1. **Gộp dây trùng cha–con**: VISIT → USER_ACCOUNT có 3 FK (DentistId, RecordedBy, UpdatedBy) → vẽ 1 dây, nhãn ghi 3 cột. Tương tự PATIENT_PROFILE → REFERENCE_ENTRY, WORKING_SCHEDULE → USER_ACCOUNT.
2. **Bỏ dây "người thao tác"** nếu vẫn rối: RecordedBy, UpdatedBy, PublishedBy, DecidedBy, ReviewingEmployeeId, RevokingEmployeeId, UploaderId. Chỉ giữ dây nghiệp vụ (DentistId, PrescriberEmployeeId, OwnerAccountId, AccountId).
3. **Dây vuông góc**: draw.io → chọn tất cả dây → Edge style: *Orthogonal* hoặc *Entity Relation*; bật *Line jumps → Arc* để dây cắt nhau có cầu nhảy.
4. **Tô màu theo vùng**: mỗi vùng A–F một màu nền nhạt, thêm khung nhóm (Container) có tên vùng.
5. **Chừa khoảng trống** ~80px giữa các bảng để dây đi qua, đừng xếp sát.
