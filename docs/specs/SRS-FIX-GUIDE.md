# Hướng dẫn sửa SRS (Report 3) theo code

Chuẩn: **code**. Mỗi mục ghi: **Tìm** (chỗ trong SRS) → **Sửa thành** (nội dung dán vào, tiếng Anh cho phần SRS tiếng Anh, tiếng Việt cho câu thông báo) → **Căn cứ** (file code).
Chỉ ghi những gì tôi đã đọc trực tiếp trong code. Chỗ nào chưa kiểm chứng được thì ghi rõ "CHƯA KIỂM".

## 0. Phạm vi

| Phần SRS | Trạng thái |
|---|---|
| Change Log, Definitions | Không cần đối chiếu code. Chỉ nên thêm dòng Change Log cho đợt sửa này. |
| I.1 Context Diagram, I.2 ba lưu đồ, I.3.1 ERD, I.4.3 Use Case Diagram, I.5.1a Screen Flow | **Là ảnh, tôi không đọc được** → phải tự vẽ lại theo mục 1–5 dưới đây. |
| I.3.2 Entity, I.3.3 Data Rules | Mục 1–2 |
| I.4.1 Actors, I.4.2 UC list, I.4.4 Permission Matrix | Mục 3 |
| I.5.1–5.3 Screens, APIs, Jobs | Mục 4 |
| II Use Case Specification (44 UC) | Mục 5 |
| III Functional Requirements, 6.x API/Job | Mục 6 |
| IV NFR | Mục 7 |
| V.1 GBR (83 dòng), V.2 MSG (145 dòng) | Mục 8, 9 |

Điểm chung cần quyết định trước khi sửa (đã nêu ở lượt trước, code cho đáp án):
- Đặt lịch ẩn danh: **API có**, **giao diện web/mobile không có** (trang đặt lịch nằm sau đăng nhập khách). Khuyến nghị ghi theo cả hai: "API hỗ trợ khách vãng lai; ứng dụng hiện yêu cầu đăng nhập".
- Đăng ký tài khoản: **không xác minh liên hệ**.
- Không có khóa tài khoản sau nhiều lần đăng nhập sai.

---

## 1. Mục I.3.2 — Entity Descriptions

Tên thực thể trong SRS là khái niệm, không cần trùng tên bảng. Nhưng **nội dung** (thuộc tính, trạng thái) phải theo code.

### 1.1 User_account → sửa dòng
- **Key business attributes**: `Username, full name, email address, phone number, account type (Staff or Public), assigned roles, sign-in method (password or Google), account status.`
- **Business identity**: `Username is unique. For a public account the username is the Vietnamese mobile number (format 0[35789]xxxxxxxx). Email is unique.`
- **Status / lifecycle**: `Active; Locked. A locked account loses access immediately and keeps its history. There is no "Inactive" state and no automatic lockout after failed sign-ins.`
- Căn cứ: `ApplicationUser.cs`, `RegistrationService.cs:19-61`, `AccountService.cs:22,149`.

### 1.2 Role → sửa
- **Business identity**: `The role catalog is fixed: Admin, Dentist, ClinicAssistant, ClinicOwner, Guest, Patient. ClinicOwner also holds Dentist rights; Patient also holds Guest rights.`
- **Status / lifecycle** thêm: `Beyond the fixed roles, access groups (a base role of Admin, Dentist or ClinicAssistant plus a set of screen/action grants, scoped to branches) can be assigned to staff.`
- Căn cứ: `AccountRoles.cs`, `SettingsGrants.cs:62-80`, `SettingsService.cs:40-43`.

### 1.3 PATIENT_PROFILE → sửa
- **Key business attributes**: `Patient code, full name, date of birth, sex (Male/Female/Other), phone number, address, guardian (name, relationship, phone), note, profile date, patient source, customer group, occupation, intake history (administrative, patient-reported), archive status and reason.`
- **Bỏ** "emergency contact".
- **Status / lifecycle**: `Active; Archived (with reason). Archiving or restoring never creates a visit and never grants account access. Every change is kept in an audit history.`
- Căn cứ: `PatientProfile.cs`.

### 1.4 EMPLOYEE_PROFILE → sửa
- **Key business attributes**: `Employee code, full name, linked account. Phone and email belong to the account. Branch membership records whether the employee is active in a branch, can act as a dentist, and can manage schedules.`
- **Bỏ**: job title, specialty, professional license (code không lưu).
- **Status / lifecycle**: `Follows the linked account (Active/Locked) and the branch membership (active or not).`
- Căn cứ: `EmployeeProfile.cs` (chỉ Id, AccountId, EmployeeCode, FullName), `SchedulingModels.cs:SchedulingMember`.

### 1.5 WORKING_SCHEDULE → sửa
- **Key business attributes**: `Employee, branch, week (Monday start) or effective date, periods of kind Work or Break (day, start, end, reason), published version.`
- **Business identity**: `Work periods of one employee on one day must not overlap. Every Break must lie inside a Work period and needs a reason. Times are multiples of 15 minutes. At most 140 periods per week.`
- **Status / lifecycle**: `Published revisions are immutable; a later revision for the same effective date supersedes the earlier. A publication that would affect confirmed appointments is refused until they are handled.`
- Căn cứ: `SchedulingModels.cs:ScheduleRules.ValidWeek`.

### 1.6 APPOINTMENT → sửa
- **Key business attributes**: `Booking code (AP- followed by 32 hex characters), branch, booker (account, name, phone, email), attendee (name, birth date, relation Self/Relative, linked patient if known), kind (Standard, WalkIn, FollowUp), source (App, Phone, Zalo, Facebook, InPerson), service, visit reason, dentist, optional assistant, chair, start and end time, follow-up link, exception reason, attendance confirmation (Pending/Yes/No), cancellation reason.`
- **Business identity**: `Booking code is unique. A dentist, an assistant, a chair and an identified attendee cannot have overlapping appointments.`
- **Status / lifecycle**: `Confirmed; CheckedIn (after reception); InProgress (examination started); Completed (examination completed — independent of payment); Cancelled; NoShow. There is no Pending state. NoShow is a separate status, not a kind of Cancelled.`
- Căn cứ: `Appointment.cs`, `ReceptionService.cs:248`, `ExaminationService.cs:96-99`, `AppointmentChangeService.cs:107,218`.

### 1.7 MEDICAL VISIT → sửa
- **Key business attributes**: `Appointment, patient, branch, dentist, arrival time, attendance purpose (InitialExamination, TreatmentContinuation, PostoperativeOrWarrantyReview, PeriodicOrNewProblem), note, flag that the dentist must review the source.`
- **Status / lifecycle**: `Queued (checked in, waiting); InProgress; Completed; Cancelled. Completing a visit does not trigger billing.`
- Căn cứ: `Visit.cs`.

### 1.8 MEDICAL_RECORD → sửa
- Code **không có thực thể "hồ sơ bệnh án" riêng**. Gợi ý: đổi tên thành `EXAMINATION` và viết: `Clinical content of a visit (history, allergies, medications — each with state and "verified" flag —, findings by tooth, diagnoses, conclusion), saved as versions; a finalized examination is amended by a new version that keeps the old content, author, time and reason.`
- Số hồ sơ riêng (`Medical record number`) không tồn tại → bỏ.
- Căn cứ: `ClinicalModels.cs:VisitExamination`, `ExaminationService.cs`, `Contracts.cs:MedicalFact`.

### 1.9 CLINICAL_ORDER → sửa
- **Key business attributes**: `Visit, kind (for example X-ray), name, region, reason, required-for-diagnosis flag, billing snapshot (service, quantity, unit price), provider, result text, result documents, interpretation, cancellation reason.`
- **Status / lifecycle**: `Ordered; ResultAvailable (result received); Reviewed (dentist read it); Cancelled.`
- Căn cứ: `InvestigationOrder.cs`, `InvestigationService.cs:96-120`.

### 1.10 CLINICAL_DOCUMENT → sửa
- Bỏ "external imaging reference". Thêm: `File name, content type (JPEG, PNG or PDF; at most 20 MB per file and 10 files per upload), SHA-256, document type, source and date, link to patient/visit/treatment dossier/plan/investigation, file state (PendingScan → Ready, or rejected with reason), reviews by dentist, audit history.`
- Căn cứ: `ClinicalModels.cs:ClinicalDocument`, `ClinicalDocumentService.cs`, `ClinicalFileScanner.cs`.

### 1.11 TREATMENT_PLAN → sửa (code gọi là hồ sơ điều trị / phiếu điều trị)
- Đổi tên: `TREATMENT_DOSSIER (treatment sheet)` và thêm `TREATMENT_ITEM`.
- **Key business attributes**: `Dossier code, patient, branch, dentist, start date, diagnosis, goals, items (service, region/teeth, quantity, unit, agreed unit price, estimated minutes, return window, discount, patient decision, progress, warranty), payment terms, deposit exception.`
- **Status / lifecycle**: `Dossier: Open; Closed (with outcome and reason; can be reopened with a reason); Cancelled. Plan: None; Draft; Committed. Item decision: Proposed → Agreed/Declined… ; item progress: NotStarted → … → Completed.`
  (CHƯA KIỂM: danh sách đầy đủ các giá trị `Decision`/`Progress` của item — trong code mặc định là `Proposed` và `NotStarted`; hãy mở `TreatmentItemRules.cs` để lấy đủ trước khi ghi.)
- Căn cứ: `TreatmentDossier.cs`, `TreatmentSheetService.cs:334-339`.

### 1.12 SERVICE → sửa
- Trong code, "dịch vụ" là `SchedulingService` (dịch vụ đặt lịch, theo chi nhánh) cộng giá điều trị trong `ServicePriceService`.
- **Key business attributes**: `Branch, name, unit, billing unit, public booking duration (minutes, 1–480, default 30), public-bookable flag, initial-examination flag, investigation flag and kind, reference unit price (VND) with price version, active flag.`
- **Bỏ**: service code, category, description (không có trong `SchedulingService`).
- CHƯA KIỂM: có danh mục dịch vụ điều trị riêng (mã/nhóm) ở `ServicePriceService` hay không — cần đọc file đó nếu muốn ghi mã/nhóm.

### 1.13 PRESCRIPTION → sửa
- **Key business attributes**: `Visit, patient, branch, prescribing dentist, revision number, diagnosis, general advice, lines (medicine code and name, active ingredient, strength, dosage form, unit, route, morning/noon/afternoon/evening dose, meal timing, days, quantity, instructions), verification code, correction reason.`
- **Status / lifecycle**: `Draft; Finalized (immutable); Superseded (replaced by a corrected revision that keeps the original). All prescriptions are for purchase outside the clinic; they never change stock or create a charge.`
- Căn cứ: `PrescriptionModels.cs`, `PrescriptionService.cs:526-549`.

### 1.14 MEDICINE → sửa
- **Key business attributes**: `Code, name, active ingredient, strength, dosage form, base unit, route, packaging, contraindications, allergy warnings, instructions, reference price, prescription-eligible flag, active flag.`
- **Bỏ**: "dispensing unit", "selling price" (code chỉ có giá tham khảo).
- **Status**: `Active; Inactive (hidden from new choices, kept in past prescriptions).`
- Căn cứ: `SupplyCatalog.cs:CatalogMedicine`.

### 1.15 BILL → **xóa thực thể**
- Code không có bảng hóa đơn. Thay bằng hai thực thể:
  - `TREATMENT_PAYMENT LEDGER`: `Each cash payment, transfer payment, advance, advance allocation, deposit exception and forfeit is a ledger entry (type FinanceLedger) linked to the treatment dossier; paid amount per performed activity and per lab order is derived from the ledger.`
  - `PAYOS_ORDER`: `QR order created for a dossier (amount, expiry, status); settled only by an authoritative query or an authenticated webhook.`
- Căn cứ: `TreatmentPaymentEndpoints.cs:53-70`, `TreatmentPayOsWorker.cs:29-39` (bảng `AUDIT_LOG`, `AuditType` = `FinanceLedger` / `FinancePayOsOrder`).

### 1.16 PAYMENT → sửa
- **Key business attributes**: `Dossier, activity or lab order, amount (VND), payment date (not in the future), method (cash or PayOS transfer), PayOS order reference, recording owner.`
- **Status**: `Cash: recorded immediately. PayOS: order pending until the gateway confirms; a PENDING answer never changes the ledger; expired orders are dropped.`
- **Bỏ**: Failed, "confirming employee" (chỉ Chủ phòng khám ghi nhận), hoàn tiền (không có).
- Căn cứ: `TreatmentPayOsEndpoints.cs`, `TreatmentPaymentEndpoints.cs`.

### 1.17 FOLLOW_UP INSTRUCTION → sửa
- **Key business attributes**: `Patient, branch, treatment dossier/item/step, purpose, reason, guidance, earliest/target/latest date, duration, dentist, capability, status, active appointment.`
- **Status**: `NeedsBooking; Booked; Completed; Cancelled; Superseded. Cancel or no-show of the active appointment returns it to NeedsBooking.`
- (Giá trị `Booked/NeedsBooking/Completed/Cancelled/Superseded` đều có trong `TreatmentProgressionService.cs`.)

### 1.18 NOTIFICATION → sửa
- Tách hai ý: `IN_APP_NOTIFICATION (title, body, kind, appointment/request link, deep link, read time)` và `NOTIFICATION_DELIVERY (channel InApp/Push, status Pending/Retrying/Delivered/Failed, attempt count, next retry, last error)`; thêm `STAFF_HANDLING_ITEM (open item for staff after final delivery failure)` và `PUSH_DEVICE`.
- Căn cứ: `Appointment.cs` (cuối file), `AppointmentNotificationDispatcher.cs:135-165`.

### 1.19 Thực thể thiếu → thêm (mỗi thực thể một khối Field/Value như trên)
FAMILY_MEMBER, FAMILY_RECORD_ACCESS (Pending/…, scopes Examinations/Treatment/Prescriptions/Documents), PATIENT_ACCOUNT_LINK (PENDING, VERIFIED, REJECTED, REVOKED, WITHDRAWN), APPOINTMENT_SUPPORT_REQUEST (Change, Cancel, ConfirmToAttend, WithdrawAttend; Pending…), BRANCH và CHAIR, TREATMENT_TEMPLATE (mẫu kế hoạch), LAB_ORDER (NotSent, Sent, Received, Delivered, Rework), LAB_PROVIDER / LAB_WORK_TYPE, MATERIAL_CATALOG_ITEM / MATERIAL_PURCHASE / MATERIAL_USAGE, CARE_TASK (Open, InProgress, Done, Cancelled; contact attempts; escalations), REFERENCE_ENTRY (nhóm danh mục), ACCESS_GROUP. Căn cứ: `DcmsDbContext.cs`.

---

## 2. Mục I.3.3 — Data Business Rules (9 dòng)

| Dòng SRS | Việc cần làm |
|---|---|
| One account identifier | Giữ. Thêm: `For a public account the username is the mobile number.` |
| Unique verified contact | **Sửa** thành `Unique account contact`: `Email and phone number must be unique across accounts. Registration does not verify the contact channel.` (code: `RegistrationService.cs:41-48`, `EmailConfirmed=false`). |
| Separate patient record | Giữ. |
| Verified patient linkage | Giữ. Thêm: `At most one active self link per account and per patient.` (MSG142). |
| Appointments cannot overlap | **Sửa**: `A dentist, an assistant, a chair and an identified attendee cannot have overlapping Confirmed or Checked-in appointments; chair capacity must cover the whole interval.` (`WorkingScheduleService.cs:Evaluate`). |
| Accounts retain history | Giữ. Đổi "Deactivate or lock" thành "Lock". |
| Clinical record authorization | Giữ. |
| **Completed prescription required** (dispense) | **Xóa.** Thay: `Prescriptions are external: finalizing a prescription never changes stock or creates a charge.` |
| **Stock adjustments traceable** (medicine) | **Xóa.** Thay: `Material stock = purchases minus recorded usage per lot; usage that exceeds the available stock is refused. Purchases and usage rows are never overwritten.` (CHƯA KIỂM chi tiết theo lô: đọc `MaterialStock.cs`.) |

Thêm các rule dữ liệu mới (đều có trong code, đã đối chiếu ở các UC): `Only the Clinic Owner records payments`; `Lab order status moves only along NotSent→Sent; Sent→Received|Rework; Received→Delivered|Rework; Delivered→Rework; Rework→Sent` (`LabOrder.cs`); `A payment line cannot exceed the amount still due`; `Payment date cannot be in the future` (`TreatmentPaymentEndpoints.cs:47`); `Treatment quantity > 0, whole for unit răng/hàm/lần/gói` (`TreatmentSheetRules.cs`).

---

## 3. Mục I.4 — Actors, Use Case list, Permission Matrix

### 3.1 Use Case list (4.2)
Bảng 44 UC **khớp** với phần II mới. Không cần sửa. (Xóa nếu còn bản cũ UC-01…UC-37 ở Change Log dòng 03/10: đổi mô tả thành `UC-01 to UC-44`.)

### 3.2 Actors (4.1) — sửa từng dòng
| # | Sửa |
|---|---|
| 1 Administrator | `Creates and edits staff accounts, assigns roles and access groups, locks and unlocks staff accounts, views the account audit, manages branches, chairs and general settings. Does not access clinical records, appointments or payments by default.` (Code: tài khoản chỉ `AccountType=Staff` — `AccountService.cs:112`.) Bỏ "and patients". |
| 2 Dentist | Thêm: `reads and writes the treatment sheet, orders investigations, reviews documents, manages treatment templates and service prices, can see work statistics.` Bỏ "Authorized dentists also manage … reports" → `work statistics only`. |
| 3 Clinic Assistant | **Viết lại**: `Maintains patient administrative data, verifies account-to-record links, books/changes/cancels appointments for patients, records attendance and no-shows, handles the support inbox, checks patients in, manages customer-care tasks, uploads documents, prints finalized prescriptions (read), sees work statistics and tracks lab orders. Does NOT record payments, dispense medicine or manage stock.` |
| 4 Patient | Bỏ "obtain support through the chatbot". Thêm: `links the account to a patient record, manages family members and requests access to a relative's record, views own prescriptions and treatment records.` |
| 5 AI/LLM Service | **Xóa.** |
| 6 Push Notification Service | Giữ (Firebase). |
| 7 X-ray/PACS | **Xóa.** |
| 8 Payment Gateway | Đổi `PayOS payment gateway: receives QR payment orders, returns status by query and by an authenticated webhook (HMAC-SHA256). Refunds are not supported.` Bỏ "(conditional)" nếu đã cấu hình; nếu chạy dev chưa cấu hình PayOS thì giữ "conditional". |
| 9 Guest | `A person who is not signed in. The API accepts booking and support requests from a guest, but the web and mobile apps currently require sign-in before booking.` |
| 10 Clinic Owner | **Sửa**: `Holds Dentist rights plus: the only role that records payments and deposits, creates PayOS orders, views financial statistics, configures general settings and clinic catalogs, approves price and deposit exceptions, manages materials and stock. Cannot assign Admin or assistant rights automatically.` Bỏ "medicine inventory". |
| Thêm | `Google Sign-In` (xác thực token Google), `SMTP e-mail service` (mã khôi phục). |

### 3.3 Permission Matrix (4.4)
Thay các dòng sau. Ký hiệu như bảng cũ. Cột: Admin | Dentist | Assistant | Patient | Guest | Owner (Owner = Dentist + quyền riêng).

| Entity / Action | Admin | Dentist | Assistant | Patient | Guest | Owner | Ghi chú / căn cứ |
|---|---|---|---|---|---|---|---|
| Staff account Create/Edit/Lock/Unlock | Full | No | No | No | No | No | `ScreenGrants` accounts.* → Admin |
| Patient account Lock/Unlock/Restore | **No** | No | No | No | No | No | chỉ tài khoản Staff |
| Working schedule Create/Edit | No | Restricted² | **Restricted⁴** | No | No | Full | cần cờ CanManageSchedules (`WorkingScheduleService.cs:53`) |
| Appointment Book | No | Restricted² (chỉ tái khám của kế hoạch mình) | Full | Restricted³ | Restricted³ | **No** (trừ khi cũng có vai Assistant) | `AppointmentService.cs:45-51` |
| Appointment Change/Cancel/No-show | No | No | Full | Restricted³ | Restricted³ | No | `AppointmentChangeService.cs:61` |
| Support inbox (yêu cầu đổi/hủy) | No | No | Full | No | No | No | `SupportRequestService.cs:388` |
| Patient reception: Check in | No | No | Full | No | No | Full (owner có quyền Assistant ở lễ tân: `ReceptionService.cs:40`) | |
| Patient record Search / Create / Edit admin | No | Read | Full | No | No | Read | `patients.read` = Dentist/Assistant |
| Examination Record/Complete | No | Restricted² | No | No | No | Restricted² | |
| Clinical document Upload | No | Restricted² | Restricted⁴ | No | No | Restricted² | `documents.*` |
| Clinical document Review | No | Restricted² | **No** | No | No | Restricted² | `documents.review` = Dentist |
| Treatment dossier/plan/activity | No | Restricted² | **No** (kể cả xem) | No | No | Restricted² | mọi `treatment.*` = Dentist (trừ prices/commercial: + Admin) |
| Treatment price & commercial approval | Full | Full | No | No | No | Full | `ScreenGrants.cs:Allows` |
| Prescription | No | Restricted² | **Read-only** | Restricted⁵ (own) | No | Restricted² | `prescriptions.read` cho Assistant |
| Lab order manage | No | Restricted² | **Track only** (`treatment.lab.*` Dentist only → Assistant **No**) | No | No | Restricted² | |
| Materials purchase / stock / usage | Full | Full | **No** | No | No | Full | `materials.*` |
| Customer care (CSKH) tasks | No | Full | Full | No | No | Full | `care-tasks.*` |
| Payment, deposit, PayOS order, forfeit deposit, cancel dossier | No | No | **No** | No | No | **Full (only)** | hard-coded `IsOwner` |
| Finance statistics | No | No | No | No | No | **Full (only)** | `FinanceStatisticsEndpoints.cs:83` |
| Work statistics | Full | Full | Full | No | No | Full | `ScreenGrants.cs:81` |
| Service catalog / reference prices | Full | Full | No | No | No | Full | |
| Medicine catalog | Full | **No** | **No** | No | No | Full | `catalog.medicines.*` chỉ Owner/Admin |
| Other catalogs (materials, providers, lab types) | Full | Full | No | No | No | Full | `ScreenGrants.cs:Allows` |
| General settings, branches, chairs | Full | No | No | No | No | Full | |
| Access groups, assign group to staff | Full | No | No | No | No | Full (owner: toàn quyền) | |
| Notification view | Staff | Staff | Staff | Restricted³ | No | Staff | `notifications.*` |
| Own medical record (examinations, treatment, prescriptions, documents) | No | No | No | Restricted⁵ | No | No | |
| Family member management; family record access request | No | No | No | Restricted³ | No | No | |

**Xóa hẳn các dòng**: Medical bill (Prepare/Update/Pay), Cash payment (Assistant), Medication dispensing, Payment correction, Medicine receipt, Medicine stock, X-ray analysis (AI), Chatbot support, "Notification — Manage reminder preferences" (đổi thành chỉ "View notifications / register or remove push device").
**Giải thích thêm trong chú thích**: `Default permissions come from the assigned role; an access group can narrow or extend them per screen and branch.` (`SettingsGrants.RoleAllows`).

---

## 4. Mục I.5 — System Functionalities

### 4.1 Screen List (5.1b) — thay toàn bộ bảng 42 dòng
Giữ lại: 1 Sign In, 3 Account Recovery, 5/6 Patient Search & Profile, 7 Account Link Request, 8 Link Verification, 9 Weekly Working Schedule, 10 Appointment Calendar, 12 Appointment Detail, 13 Mobile Home, 14 My Appointments, 15 Book Appointment, 16 Appointment Detail & Change, 17/18 Reception & Check-in, 19 Clinical Visit Workspace, 20 Clinical Documents, 21/22 Treatment (nay là Treatment Sheet), 23 Prescription Editor, 31 Service Catalog (nay là Thiết lập giá điều trị), 41 My Medical Records.

Sửa mô tả:
- 2 Account Registration: bỏ "after contact verification"; `Allows a person to create a customer account with name, mobile number, e-mail and password.`
- 4 User Account Management: `staff accounts only`.
- 11 Availability Search and Booking: đây là hộp thoại trong lịch hẹn của nhân viên.
- 15: `30- or 60-minute slot` → `a free slot of the length configured for the chosen service (default 30 minutes)`.
- 17: bỏ `identifies appointments eligible for No-show processing` → `lists today's arrivals and queue`.
- 26 Notification Center: chỉ có trên **mobile** (`(home)/notifications.tsx`); web nhân viên không có trang thông báo.
- 39 Payment and Receipt: `Payment dialog of a treatment sheet; Owner only; cash and PayOS QR`.

**Xóa**: 24 Follow-up Instruction (không có màn riêng; chỉ định tái khám nằm trong phiếu điều trị), 25 (thay bằng Chăm sóc khách hàng), 27, 28, 29, 30 (sai chức năng — xem dưới), 32–37, 38, 40 (thay), 42.

**Thêm** (route trong `web/src/app/App.tsx`; mobile trong `mobile/src/app`):
| Màn mới | Route | Mô tả |
|---|---|---|
| Access Portal | `/access` | chọn cổng khách hoặc nhân viên |
| Customer Sign In / Register / Recover | `/customer/sign-in`, `/customer/register`, `/customer/recover` | |
| Customer Home | `/customer/home` | |
| Family Members | `/customer/family-members` (+ mobile) | thêm, sửa, tìm, xóa, xin quyền xem hồ sơ |
| My Prescriptions | `/customer/prescriptions[/:id]` | |
| My Treatment Records | `/customer/treatment-records` | |
| Support Inbox | `/support-inbox` | yêu cầu đổi/hủy/xác nhận tham dự của khách (**không phải chatbot**) |
| Customer Care | `/customer-care` | CSKH, liên hệ, chuyển bác sĩ |
| Treatment Templates | `/treatment-templates` | |
| Treatment Prices & Commercial Approval | `/commercial/treatment` | |
| Lab Orders | `/lab-orders` | |
| Materials (purchase, stock, usage) | `/materials` | |
| Medicines | `/medicines` | danh mục thuốc |
| Work Statistics | `/work-statistics` | |
| Finance Statistics | `/finance/statistics` | Owner |
| Booking Settings | `/booking-settings` | khoảng cách ô giờ, thời lượng dịch vụ |
| System Settings | `/system-settings`, `/settings/:section` | chi nhánh, ghế, nhóm quyền, cấu hình chung, danh mục, nhật ký |
| Patient Documents | `/patients/:id/documents` | |
| Visit Workspace | `/visits/:visitId` | |
| Mobile staff: Support requests, Schedule impact | `(home)/staff/requests`, `(home)/staff/schedule-impact` | |

### 4.2 External API Inventory (5.2) — 6 dòng
| # | Sửa |
|---|---|
| 1 Push | Giữ. Đổi mô tả: `Sends appointment and reminder push notifications through Firebase Cloud Messaging. Content is generic.` (`FirebasePushAdapter.cs`). Bỏ "follow-up reminders" nếu chưa gửi push (CHƯA KIỂM). |
| 2 AI Chatbot | **Xóa** |
| 3 X-ray/PACS | **Xóa** |
| 4 Payment and Refund | Sửa thành `PayOS Payment Link API: creates a QR payment order for a treatment dossier and queries its status. No refund.` (`TreatmentPayOsGateway.cs`) |
| 5 Payment Status Webhook | Sửa: `PayOS → DCMS, POST /api/payments/payos/webhook. DCMS verifies the HMAC-SHA256 signature, matches order and amount and prevents double recording.` |
| 6 Contact Verification | **Xóa.** Thay hai dòng: `Google Identity token verification (customer sign-in)` và `SMTP e-mail delivery (account-recovery code)`. |

### 4.3 Background Job Inventory (5.3) — 9 dòng → 4 job có thật + 1 xử lý khi truy vấn
| Job SRS | Việc cần làm |
|---|---|
| 1 Recalculate availability | **Xóa.** Thay ghi chú: `Free slots are computed on request from schedules, reservations and chairs; no background job.` |
| 2 Plan reminders | **Sửa**: tạo nhắc lịch **24 giờ và 2 giờ** trước giờ hẹn; xóa "notification preferences" (`AppointmentNotificationDispatcher.cs:33`). |
| 3 Dispatch + retry | **Sửa**: thử lại sau **1, 5, 15 phút**; sau lần cuối thất bại tạo `StaffHandlingItem` (`:34,135-165`). |
| 4 Mark no-show | **Xóa.** Thay: `No-show is recorded manually by staff (POST /api/appointments/{code}/no-show) once the appointment start time has passed; after that clinic day (UTC+7) is over, CareTaskIngestionService creates one customer-care task, with or without a patient record.` |
| 5 Follow-ups due | Sửa: `TreatmentDueScanWorker finds follow-up instructions whose window is due or overdue and records events; customer-care tasks are created from them.` |
| 6 Medicine alerts | **Xóa.** |
| 7 Reconcile gateway | Sửa: `TreatmentPayOsWorker queries pending PayOS orders every 15 seconds (orders from the last 2 hours) and records confirmed payments once.` |
| 8 Backup | **Xóa** (không có trong code; nếu làm ở hạ tầng, ghi vào phần vận hành). |
| 9 Large report export | **Xóa.** Thay: `Excel exports run synchronously.` |
| Thêm | `Clinical file scan (ClinicalScanWorker): moves uploaded files from PendingScan to Ready or rejected.` |

---

## 5. Phần II — Đặc tả use case (44 UC)

Các UC này đã được viết lại theo code. Còn các điểm sau cần sửa tay:
| UC | Sửa |
|---|---|
| UC-05, nhánh A2 | `User turns off push` → `User removes the device registration (DELETE /api/notifications/devices)`; không có "tắt trong cài đặt". |
| UC-05, bước "mark all as read" | Đổi thành `mark selected notifications as read`. Không tìm thấy nút "đánh dấu tất cả" (CHƯA KIỂM kỹ trên mobile). |
| UC-29 | Nút `Lưu contact` → `Lưu liên hệ` (`CareTaskDrawer.tsx:93`). |
| UC-06/07/17 | Ghi chú: web/mobile bắt đăng nhập; API cho khách. |
| UC-16, 18, 21–23, 25–27, 36, 37, 42–44 | **CHƯA KIỂM từng luồng**: tôi mới kiểm các điểm chính (trạng thái, giới hạn). Nếu muốn, tôi đọc từng UC với code ở lượt sau. |

---

## 6. Phần III — Functional Requirements, và mục 6.x

- III đã viết từ code ở các lượt trước: không phát hiện sai lệch mới khi đối chiếu nhanh các điểm: đăng ký không cần mã xác nhận, thống kê tài chính Owner, nút trả tiền chỉ Owner, "Đang xem xét" không do nút nào đặt.
- **Lỗi trình bày cần sửa tay**: mục `2.3` không có tên; `3.8.Diagnosis…`, `4.1.Reception List` sai cỡ chữ; "Stafff"; "Wwjen owner account"; `3.6.Examination` trùng tên với `3.5`; tham chiếu chéo kiểu "dialog 13.3", "(9.5)", "S12" theo đánh số cũ — cần đánh lại.
- Mục 6.1 chỉ là mẫu trống → điền hoặc xóa.
- Mục 6.2, 6.3 (VnPay, SePay): **xóa**; thay một mục `PayOS webhook` — `POST /api/payments/payos/webhook`, kiểm chữ ký HMAC-SHA256, đối chiếu đơn và số tiền, ghi một lần. Câu "triggers access grant creation" thuộc hệ thống khác → bỏ.

---

## 7. Phần IV — Non-Functional Requirements

| Mục | Việc cần làm |
|---|---|
| 2.3 Account lockout sau nhiều lần sai | **Xóa** hoặc ghi `Not implemented`. Code: `AuthenticationService.cs` dùng `CheckPasswordAsync`, không có đếm lần sai; khóa chỉ do Admin (`MSG15`). Có `LockoutEnd` trong Identity nhưng không bật theo số lần sai. |
| Staff idle timeout | **Thêm** nếu SRS chưa có: phiên nhân viên hết hạn sau một khoảng không hoạt động **cấu hình được theo phút** (`StaffIdleTimeoutMinutes`, xem trước số phiên bị ảnh hưởng; client báo hoạt động qua `POST /session/activity`). Ghi "configurable"; CHƯA KIỂM giá trị mặc định (xem `StaffIdleService.cs` / cấu hình chung). |
| Clinical document access | Giữ: kiểm quyền mỗi lần tải; không có đường dẫn công khai. |
| Upload | **Thêm**: JPEG/PNG/PDF; 20 MB/tệp; 10 tệp/lần; quét tệp trước khi dùng. |
| Hiệu năng (2 giây, 100 người) | Không kiểm chứng được từ code; giữ nhưng gắn nhãn `target`. |
| Các mục có nhắc chatbot, AI, PACS, backup, tồn kho thuốc | Xóa hoặc đánh dấu `out of scope`. |

---

## 8. Phụ lục V.1 — GBR (83 dòng)

Cách đọc: **Giữ** = khớp code; **Sửa** = đổi câu; **Xóa** = không có trong code; **Cột "Applies To"** của mọi dòng phải đổi sang số UC mới (44 UC) — việc này làm sau cùng bằng cách tra bảng UC ở phần II.

| GBR | Quyết định | Nội dung |
|---|---|---|
| 001 | Giữ | Thêm: break/block phải nằm trong khoảng Work, cần lý do; bước 15 phút. |
| 002 | Giữ | |
| 003 | **Sửa** | `Appointment duration comes from the service's configured public duration (default 30 minutes, 1–480). Customers cannot enter a free duration. A counter booking uses the service duration too; only a replacement booking may enter another duration, limited to the time left in the missed slot. Follow-up duration comes from the treatment step.` |
| 004 | **Sửa** | `Slots start on the branch slot interval (default 15 minutes, 1–120, set in Booking Settings). An appointment must lie inside a Work period and cannot overlap a Break.` Bỏ "30 minutes / two consecutive units". |
| 005 | Sửa nhẹ | Chặn trùng cùng bác sĩ, **cùng trợ lý, cùng ghế (hết chỗ)** và cùng người khám đã xác định (`patient:{id}`). Cảnh báo hồ sơ trùng chỉ ở bước check-in (MSG44). |
| 006–008 | Giữ | |
| 009 | Giữ | (code: nhân viên đặt gần hơn 60 phút phải có `exceptionReason`; MSG24) |
| 010, 011 | Giữ | (retry cùng `requestKey` trả lại lịch cũ, MSG30) |
| 012 | Giữ | Đúng 12 giờ vẫn tự đổi được (`>=`). |
| 013 | Giữ | |
| 014 | Giữ | |
| 015 | Giữ | |
| 016 | Giữ | |
| 017 | **Sửa** | `A staff member records No-show manually after the appointment start time, only for a Confirmed appointment that has not been checked in. There is no automatic no-show and no 15-minute rule. A mistaken No-show can be undone by staff only on the clinic day it was recorded, if the interval was not booked again. On that day staff can also move a late or No-show appointment to a free future time without the 60-minute lead time.` |
| 018 | Giữ | Thêm: chỉ tạo khi lịch đã gắn bệnh nhân (`PatientId` có giá trị). |
| 019 | **Sửa** | `Contact attempt: channel Phone/InPerson/Other; result Reached, NoAnswer, Rebooked, Declined or Other; Rebooked requires a Confirmed rebooked appointment.` Bỏ "Not contacted", "No longer interested". |
| 020 | Sửa nhẹ | Đặt lại lịch cho khách không đến: lịch thay thế là WalkIn/InPerson, nằm trong khung giờ của lịch cũ (`AppointmentService.cs:Replaces…`). |
| 021 | Sửa | Mô tả theo luật trên (cùng khung giờ lịch cũ, chưa có lượt khám). |
| 022 | Sửa nhẹ | Chỉ định tái khám sinh ra từ bước/hạng mục điều trị; trường: purpose, reason, guidance, earliest/target/latest, duration, dentist, capability. |
| 023 | Giữ | Thêm MSG258: ngày hẹn lệch quá 4 ngày so với ngày dự kiến thì bị chặn. |
| 024 | **Sửa** | `Due or overdue follow-ups create staff customer-care tasks; no patient reminder is sent by the due scan.` (CHƯA KIỂM: có thông báo cho bệnh nhân — grep không thấy MSG60.) |
| 025 | Sửa | Cancel/no-show → `NeedsBooking`. Phần "Dentist confirms Completed/Cancelled/Superseded": code có các trạng thái này; CHƯA KIỂM ai được chuyển. |
| 026 | **Sửa** | `Dentist (and Owner) own clinical decisions and service prices; Assistant owns reception, patient administration, scheduling exceptions, support and customer care; Owner alone records payments, approves exceptions and sees finance; Admin owns accounts, branches and settings.` Bỏ "billing, collections and medicine stock" của Assistant. |
| 027 | Giữ | |
| 028 | **Sửa** | Admin chỉ khóa/mở khóa và quản lý tài khoản **nhân viên**; bệnh nhân tự đăng ký và tự khôi phục. |
| 029 | **Sửa** | `Registration does not verify the contact channel. It does not create a Patient record or authorize clinical access.` |
| 030 | Giữ | |
| 031 | Sửa | Khách vãng lai: API cho phép đặt và gửi yêu cầu hỗ trợ bằng mã lịch + xác minh liên hệ bởi nhân viên; app hiện yêu cầu đăng nhập. |
| 032–035 | Giữ | |
| 036 | Giữ | (trạng thái `Unknown` + cờ `Verified` trong `MedicalFact`) |
| 037 | Giữ | |
| 038 | Giữ | |
| 039 | Giữ | Thêm liên kết tới dossier/investigation. |
| 040 | **Sửa** | Bỏ "raw AI output". |
| 041 | **Sửa** | `Staff cash recording and PayOS QR payments are supported; PACS and imaging AI are not part of the system.` |
| 042, 043, 044, 083 | **Xóa** | AI ảnh |
| 045, 046 | Giữ | Bỏ "consent documents when available" nếu không dùng; code có `ConsentDocumentId` (giữ được). |
| 047 | **Xóa** | Không có gói dịch vụ / trừ kho thuốc. |
| 048 | Sửa | Bác sĩ kê và chốt; trợ lý chỉ **xem** (prescriptions.read). CHƯA KIỂM: nút in của trợ lý. |
| 049 | **Sửa** | `All prescriptions are for purchase outside the clinic. Creating or finalizing never changes stock or creates a charge.` |
| 050 | Giữ | (revision, Superseded, lý do đính chính) |
| 051–062 | **Xóa** | Toàn bộ nhóm tồn kho/cấp phát/trả/kiểm kê/lô thuốc. Thay bằng 3 rule vật liệu: nhập hàng ghi lô/hạn/nhà cung cấp; xuất dùng trong hoạt động điều trị; chặn khi tồn không đủ. (CHƯA KIỂM chi tiết: đọc `MaterialStock.cs`, `TreatmentActivityMaterials.cs`.) |
| 063 | **Sửa** | Giá do Dentist/Owner/Admin quản lý (`treatment.prices.manage`); thời lượng đặt lịch theo dịch vụ (1–480 phút). Giá đã thỏa thuận không đổi khi giá tham khảo đổi. |
| 064 | **Sửa** | `Charges come from performed treatment activities and agreed unit prices; discounts need approval.` Bỏ medicine. |
| 065 | Sửa | Có đặt cọc, tạm ứng, phân bổ tạm ứng, điều khoản thanh toán theo đợt; thu tiền mặt hoặc chuyển khoản (PayOS) riêng từng dòng; **quy tắc 30% labo** (khoản thu phải ưu tiên đủ tối thiểu 30% phí labo — `TreatmentLabFee.Minimum30PercentVnd`). |
| 066 | **Sửa** | PayOS: chỉ ghi nhận khi truy vấn PayOS xác nhận hoặc webhook đúng chữ ký; mỗi đơn ghi một lần. |
| 067 | **Sửa** | Không có trạng thái hóa đơn PAID/PARTIALLY_PAID. Công nợ tính theo từng hoạt động (price − paid) và từng phiếu labo. |
| 068 | **Sửa** | `Only the Clinic Owner records cash received.` |
| 069 | **Sửa** | `Recorded payments are never deleted. The system does not refund automatically (MSG245). There is no payment-correction function.` |
| 070 | Giữ | Bỏ "medicine issue" → "material usage". |
| 071 | **Sửa** | Thống kê tài chính chỉ Owner; thống kê công việc: Owner, Admin, Dentist, Assistant. |
| 072, 073 | Giữ | |
| 074 | Giữ | |
| 075 | Sửa nhẹ | Bỏ "chatbot action". |
| 076 | Giữ | |
| 077 | **Sửa** | Nhắc 24 giờ và 2 giờ; múi giờ Asia/Ho_Chi_Minh; push chỉ có nội dung chung; bỏ "preferences". |
| 078 | **Sửa** | Không có cài đặt tắt push; người dùng hủy đăng ký thiết bị. Thất bại cuối cùng → `StaffHandlingItem`. |
| 079, 080 | **Xóa** | Chatbot. |
| 081 | Sửa | Bỏ "backups and recovery procedures" (không có trong code). |
| 082 | Sửa | Bỏ "AI providers", "support hours", "verification channel". |
| Thêm mới | | GBR cho: quy tắc nhãn Owner-only; luồng labo; quy tắc vật liệu; tệp (JPEG/PNG/PDF, 20 MB, 10 tệp); 15 phút/Work-Break; đặt cọc và mất cọc khi khách không đến (`ForfeitNoShowDeposit`). |

---

## 9. Phụ lục V.2 — System Messages

### 9.1 Giữ nguyên (nội dung code = SRS)
01, 02, 03, 04, 06, 07, 09, 11–20, 25, 27, 28, 31–35 (35 có {appointment_time}), 37, 39, 102, 108–111, 113, 115, 116, 139, 142–144.

### 9.2 Có trong code nhưng **nội dung khác** → sửa câu SRS thành câu của code
| MSG | Câu theo code |
|---|---|
| 22 | Không tìm thấy khung giờ phù hợp. Vui lòng đổi ngày, bác sĩ hoặc dịch vụ. |
| 23 | Vui lòng nhập thông tin người khám thực tế. |
| 24 | Lịch đặt công khai phải bắt đầu sau ít nhất 60 phút. Vui lòng chọn giờ khác. |
| 26 | Không còn khoảng thời gian liên tiếp đủ cho lịch hẹn. Vui lòng chọn giờ khác. (Khi đặt lịch, code còn kèm số phút: "…đủ {n} phút…".) |
| 29 | Đặt lịch thành công. Vui lòng lưu mã lịch hẹn để tra cứu. |
| 30 | Yêu cầu đặt lịch này đã được xử lý. Hệ thống trả lại lịch hẹn đã tạo. |
| 38 | Thay đổi này ảnh hưởng đến các lịch hẹn đã xác nhận. Vui lòng xử lý các lịch liên quan trước khi cập nhật. (không có {affected_count}) |
| 40 | Không còn khung giờ phù hợp trong ngày. Vui lòng chọn ngày khác. |
| 42 | Lịch hẹn không đủ điều kiện tiếp nhận trong ngày hiện tại. Vui lòng kiểm tra hoặc xếp lịch hợp lệ. |
| 43 | Thông tin người đến khám không khớp hồ sơ hoặc lịch đã chọn. Vui lòng đối chiếu lại. |
| 44 | Có hồ sơ có thể trùng. Vui lòng đối chiếu trước khi tạo mới. |
| 47 | Lịch hẹn hoặc nguồn lực không còn hợp lệ để tiếp nhận. Vui lòng kiểm tra và xếp lại lịch. |
| 94 | Lịch hẹn đã được xác nhận nhưng thông báo chưa gửi được. Nhân viên sẽ kiểm tra. |
| 114 | Bệnh nhân đã được check-in. Mở lượt khám hiện có. |
| 122 | Vui lòng ghi lý do trước khi mở lại hồ sơ điều trị. (Ngữ cảnh đổi: mở lại hồ sơ điều trị.) |
| 127 | Chỉ định quay lại đã có lịch hẹn đang hiệu lực. Vui lòng kiểm tra lịch hiện tại. |

### 9.3 SRS có, **code không phát ra mã** → xóa hoặc đánh dấu "text do giao diện tự hiển thị, không có mã"
- Nhóm gắn với tính năng đã xóa:
  - Chatbot/AI/PACS: 95, 96, 97, 98, 106, 107, 118, 125, 126, 141, 145.
  - Thuốc/kho/hóa đơn/gateway/thanh toán sửa: 64–81 (trừ nếu giữ danh mục thuốc: 64 và 133), 85–90, 93, 103–105, 128–130, 134–136.
  - Tự động no-show và nhắc: 46, 60, 61, 91, 92.
- Còn lại không có mã trong code (UI có thể tự hiển thị chữ khác): 05, 08, 10, 21, 36 (SRS: hộp thoại xác nhận hủy; web chỉ có `MSG36` = "Đã ghi nhận không đến." ở `RequestDrawer.tsx:431` → **ngữ cảnh khác**), 41, 45, 48–59 và 62–63, 82–84, 99–101, 112, 117, 119–121, 123, 124, 131, 132, 137, 138, 140.
- Ghi chú: các dải ở trên chỉ để bạn khoanh vùng; một số mã có thể nằm ở nhóm tính năng đã xóa lẫn nhóm "không có mã". Khi sửa từng dòng, quyết định theo câu hỏi: màn hình hiện tại có tình huống đó không?
- Lưu ý: "không có mã" **không** có nghĩa màn hình không báo gì; chỉ có nghĩa code không dùng mã đó. Muốn chắc từng dòng, đối chiếu chữ hiển thị trên giao diện.

### 9.4 Code có, SRS **chưa có** → thêm dòng
| Mã | Nội dung (code) |
|---|---|
| MSG201 | Mã {entity} đã tồn tại. Vui lòng chọn mã khác. |
| MSG202 | Bản ghi đã ngừng sử dụng. Vui lòng khôi phục hoặc chọn bản ghi hợp lệ. |
| MSG203 | Không thể đánh dấu hạng mục chưa thực hiện là đã hoàn tất. |
| MSG205 | Danh mục đã được sử dụng. Bạn có thể ngừng áp dụng cho giao dịch mới. |
| MSG222 | Đã ghi nhận {entity} với mã {code}. |
| MSG229 | Các bước điều trị hoặc thời gian dự kiến chưa hợp lệ. Vui lòng kiểm tra kế hoạch. |
| MSG230 | Điều kiện để thực hiện bước điều trị chưa đáp ứng. Vui lòng kiểm tra lại. |
| MSG231 | Đã lưu chỉ định quay lại. Chưa có lịch hẹn được đặt. |
| MSG233 | Thay đổi kế hoạch ảnh hưởng buổi tiếp theo hoặc lịch đã đặt. Vui lòng xem và xử lý các mục liên quan. |
| MSG234 | Bước điều trị đã đặt lịch nay bị chặn. Vui lòng kiểm tra lịch liên quan với phòng khám. |
| MSG235 | Chưa có khung giờ phù hợp. Chỉ định quay lại vẫn được giữ để phòng khám xử lý. |
| MSG236 | Mẫu điều trị đã thay đổi hoặc ngừng sử dụng. Vui lòng kiểm tra lại phiên bản. |
| MSG237 | Đã tiếp nhận; bác sĩ cần xác nhận nguồn và nội dung chuyên môn. |
| MSG239 | Thông tin cần bác sĩ kiểm tra: {field}. Vui lòng ghi đánh giá và hướng xử trí. |
| MSG240 | Nội dung đồng ý chưa khớp phương án hiện tại. Vui lòng tư vấn và ghi nhận lại quyết định trước khi thực hiện. |
| MSG241 | Đã lưu thay đổi tạm dừng bước điều trị. |
| MSG243 | Còn hạng mục chưa được xử lý khi kết thúc kế hoạch. Vui lòng ghi kết quả và lý do cho phần chưa hoàn tất. |
| MSG244 | Vui lòng ghi căn cứ bảo hành hoặc miễn phí và phạm vi hạng mục được áp dụng. |
| MSG245 | Phương án đã thay đổi. Vui lòng kiểm tra khoản đã ghi nhận, công nợ và số dư trước khi điều chỉnh; hệ thống không tự hoàn tiền. |
| MSG249 | Đã ghi nhận quyết định của bệnh nhân. Hạng mục chưa đồng ý sẽ không được thực hiện; có thể kết thúc lượt tư vấn. |
| MSG252 | Vui lòng ghi mức giá hoặc giảm trừ đã thỏa thuận, lý do và hạng mục áp dụng. Khoản đã chốt cần điều chỉnh theo đúng quyền. |
| MSG255 | {field} chưa được phòng khám xác nhận hoặc cấu hình. Chưa tạo dịch vụ, lịch hẹn hay thông báo gửi ra ngoài. |
| MSG256 | Phân bổ thanh toán chưa khớp số tiền nhận hoặc vượt số dư có thể sử dụng. Vui lòng kiểm tra từng dòng và phần tiền ứng trước khi xác nhận. |
| MSG257 | Giá dịch vụ không được âm. Giảm giá, voucher hoặc khấu trừ vui lòng ghi nhận bằng chương trình khuyến mãi hoặc giảm trừ có lý do. |
| MSG258 | Ngày hẹn lệch quá 4 ngày so với ngày dự kiến của kế hoạch điều trị. Vui lòng chọn ngày trong khoảng cho phép hoặc đổi ngày dự kiến trong kế hoạch. |

Ngoài ra code trả **lỗi bằng mã riêng hoặc chữ thường** (không có mã MSG), ví dụ `FAMILY_MEMBER_NOT_FOUND`, `APPOINTMENT_REPLACEMENT_UNAVAILABLE`, `APPOINTMENT_PATIENT_CONSENT_REQUIRED`, `MATERIAL_STOCK_INSUFFICIENT`, "Thông tin thu tiền không hợp lệ.", "Số tiền vượt giới hạn." Nếu muốn SRS đầy đủ, thêm các dòng tương ứng với mã là chính chuỗi đó.

---

## 10. Thứ tự làm gợi ý (ít sửa lại nhất)
1. Quyết định 3 điểm ở mục 0.
2. Sửa Actors + Permission Matrix (mục 3) — ảnh hưởng nhiều phần khác.
3. Sửa Entity + Data Rules (mục 1–2) rồi **vẽ lại ERD**.
4. Sửa Screen/API/Job (mục 4) rồi vẽ lại Screen Flow và Use Case Diagram.
5. Sửa GBR (mục 8): làm "Applies To" **sau cùng**.
6. Sửa MSG (mục 9).
7. Sửa NFR (mục 7), lỗi trình bày (mục 6), Change Log.

## 11. Những gì CHƯA KIỂM (để bạn biết giới hạn)
- Các hình/ảnh trong SRS.
- Giá trị đầy đủ của `Decision`/`Progress` của hạng mục điều trị; mã/nhóm dịch vụ điều trị trong `ServicePriceService`.
- Chi tiết tồn kho vật liệu theo lô (`MaterialStock.cs`).
- Thời gian hết hạn phiên nhân viên (`StaffIdleSettings.cs`).
- Có/không thông báo cho bệnh nhân khi chỉ định tái khám đến hạn; ai được chuyển chỉ định sang Completed/Cancelled; nút in đơn thuốc của trợ lý.
- Luồng chi tiết của UC-16, 18, 21–23, 25–27, 36, 37, 42–44.
