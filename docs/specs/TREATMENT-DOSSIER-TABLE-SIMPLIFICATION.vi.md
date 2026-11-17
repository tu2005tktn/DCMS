# Rút gọn bảng `TREATMENT_DOSSIER` theo Phương án A (18/11/2026)

Yêu cầu người dùng ngày 18/11/2026: bỏ các cột dư thừa trên bảng `TREATMENT_DOSSIER` (giữ các cột cốt lõi hiển thị/vận hành trực tiếp trên phiếu điều trị). Phương án đã chốt là **Phương án A**: giữ đúng **14 cột** trên `TREATMENT_DOSSIER` (giảm từ **34 xuống 14 cột**, bỏ 20 cột dư), dùng `Version` làm khóa đồng thời duy nhất và không thêm bảng mới (giữ nguyên **32 bảng EF / 33 bảng vật lý** trong schema `public`).

> Lưu ý chuẩn 1NF: hệ thống không tách bảng `TREATMENT_PLAN` riêng và một phiếu điều trị chứa nhiều hạng mục kế hoạch (`TREATMENT_PLAN_ITEM`) cùng nhiều hoạt động điều trị (`CLINICAL_ACTIVITY`). Hai bảng con đều giữ khóa ngoại `DossierId` trỏ về `TREATMENT_DOSSIER("Id")` (quan hệ `1 - N`), do đó `TREATMENT_DOSSIER` không đặt cột `PlanId` hay `ClinicalActivityId`.

## Cấu trúc bảng sau khi rút gọn (14 cột)

| Nhóm | Cột | Kiểu / Ràng buộc | Ghi chú |
| --- | --- | --- | --- |
| Định danh & mã | `Id` | `uuid`, PK, `NOT NULL` | Khóa chính duy nhất (gồm cả vai trò `PlanId`) |
| | `Code` | `varchar(40)`, `UNIQUE`, `NOT NULL` | Mã phiếu điều trị (`HS...`) |
| Chủ thể & phụ trách | `PatientId` | `int`, FK `PATIENT_PROFILE(Id)`, `NOT NULL` | Bệnh nhân sở hữu phiếu |
| | `BranchId` | `int`, FK `SCHEDULING_BRANCH(Id)`, `NOT NULL` | Chi nhánh tạo/thực hiện |
| | `DentistId` | `int`, FK `USER_ACCOUNT(BranchId, EmployeeId)`, `NOT NULL` | Nha sĩ phụ trách phiếu |
| Nội dung lâm sàng | `StartedOn` | `date`, `NOT NULL` | Ngày bắt đầu điều trị |
| | `Diagnosis` | `varchar(4000)`, `NOT NULL` | Chẩn đoán điều trị |
| | `Note` | `varchar(4000)?` | Ghi chú chung trên phiếu |
| | `SourceExaminationVisitId` | `uuid?`, FK `VISIT(Id)` | Lượt khám nguồn bàn giao sang phiếu |
| Trạng thái & đồng thời | `State` | `varchar(16)`, `NOT NULL` | `Open`, `Completed`, `Stopped`, `Cancelled`, `Deleted` |
| | `Version` | `int`, `NOT NULL` | Khóa đồng thời duy nhất của phiếu |
| Thời điểm | `CreatedAt` | `timestamptz`, `NOT NULL` | Ngày lập phiếu |
| | `UpdatedAt` | `timestamptz`, `NOT NULL` | Thời điểm cập nhật gần nhất |
| JSONB | `SheetDrafts` | `jsonb`, `NOT NULL` | Các dòng nháp kế hoạch/quá trình chưa xác nhận trên phiếu |

## 20 cột bị xóa và cách thay thế

| Cột bị xóa | Cách thay thế |
| --- | --- |
| `StorageStamp` | Dùng `Version` làm khóa đồng thời duy nhất trên `TreatmentDossier`. |
| `PlanId` | Hợp nhất về `TREATMENT_DOSSIER."Id"`. Các tài liệu gắn `CLINICAL_DOCUMENT."PlanId"` hoặc `TREATMENT_STEP."PlanId"` đều dùng `DossierId`. |
| `PlanVersion`, `PlanState` | Suy xuất động từ `TREATMENT_PLAN_ITEM."DecisionPlanVersion"` qua `CurrentPlanVersion()`, `HasDraftPlan()` (`Items.Any(x => !x.SheetDeleted && x.DecisionPlanVersion == null)`) và `HasCommittedPlan()`. |
| `Goals`, `PaymentTermsJson` | Xóa bỏ hoàn toàn (không có trên giao diện phiếu điều trị). |
| `RevisionDrafts` | Gỡ bỏ hoàn toàn luồng S62 revision draft (`RevisionContracts.cs`, `TreatmentRevisionService.cs`, `/api/dossiers/{dossierId}/plan/revisions*`, `/api/dossiers/{dossierId}/plan/revision-drafts*`). |
| `DisplayCompleted` | Xóa cột cấp phiếu; trạng thái phiếu dùng trực tiếp `State` (`Completed`). Cờ hiển thị cấp dòng `TREATMENT_PLAN_ITEM."DisplayCompleted"` giữ nguyên. |
| `ClosureOutcome`, `ClosureReason`, `ClosedAt`, `ClosedBy` | Thông tin chi tiết khi đóng/mở lại phiếu nằm trong `AUDIT_LOG` (`TreatmentEvent`: `DossierClosed`, `DossierReopened`, `DossierCancelled`). Gỡ modal `DossierClosureModal.tsx`, menu trạng thái phiếu gọi trực tiếp `close` / `reopen`. |
| `DepositExceptionReason`, `DepositExceptionBy`, `DepositExceptionAt` | Gỡ bỏ cùng endpoint `POST /api/commercial/dossiers/{dossierId}/deposit-exception` và phần giao diện ngoại lệ cọc tại `CommercialTreatmentPage.tsx`. |
| `SourceExaminationId`, `SourceExaminationVersion`, `SourceExaminationJson` | Hợp nhất về `SourceExaminationVisitId` (FK `VISIT(Id)`); snapshot nội dung khám nguồn nằm trong sự kiện `ExaminationSourceCaptured` của `AUDIT_LOG`. |
| `SourceTemplateId`, `SourceTemplateVersion` | Ghi nhận qua sự kiện `plan.template.applied` trong `AUDIT_LOG`; xóa cột và index `IX_TREATMENT_DOSSIER_SourceTemplateId`. |

## Hợp đồng API & giao diện

- `TreatmentDossierDto` (`GET /api/dossiers/{dossierId}`, các lệnh cập nhật phiếu): giữ `planVersion` (suy từ `CurrentPlanVersion()`) và `sourceExaminationVisitId`; bỏ `planId`, `planState`, `goals`, `paymentTerms`, `closureOutcome`, `closureReason`, `depositExceptionReason`, `depositExceptionBy`, `depositExceptionAt`, `displayCompleted`, `sourceExaminationId`, `sourceExaminationVersion`.
- `DossierListItem` (`GET /api/patients/{patientId}/dossiers`): giữ `id`, `code`, `patientId`, `branchId`, `dentistId`, `diagnosis`, `state`, `version`, `updatedAt`.
- `SavePlanDraftRequest`: bỏ `goals`, `paymentTerms`; chỉ còn `expectedVersion`, `items`, `requestKey`.
- `CommercialPlanDto` (`GET /api/commercial/dossiers/{dossierId}/plan`): bỏ `planState`, `depositExceptionReason`.
- Gỡ các endpoint `POST /api/dossiers/{dossierId}/display-status`, `POST /api/commercial/dossiers/{dossierId}/deposit-exception` và nhóm `/api/dossiers/{dossierId}/plan/revision*`.
- Web (`DossierList.tsx`, `TreatmentSheet.tsx`, `CommercialTreatmentPage.tsx`): xóa `DossierClosureModal.tsx`, chuyển nút mở nguồn khám sang `dossier.sourceExaminationVisitId`, bỏ khối ngoại lệ cọc trên trang thương mại.

## Migration `20261117195636_SimplifyTreatmentDossierTable`

- `Up`: đồng bộ `SourceExaminationVisitId` từ `SourceExaminationId` hợp lệ trong `VISIT`; đồng bộ `CLINICAL_DOCUMENT."PlanId" = "DossierId"` (khi khác `NULL`); chuẩn hóa `TREATMENT_PLAN_ITEM."DecisionPlanVersion"` theo `PlanState`/`PlanVersion` cũ; gỡ index `IX_TREATMENT_DOSSIER_SourceTemplateId` và xóa 20 cột khỏi `TREATMENT_DOSSIER`.
- `Down`: tạo lại đầy đủ 20 cột với kiểu và giá trị mặc định ban đầu, khôi phục `PlanId = Id`, suy xuất lại `PlanVersion`, `PlanState`, `SourceExaminationId`, `DisplayCompleted` và thông tin đóng phiếu (`ClosureOutcome`, `ClosureReason`, `ClosedAt`, `ClosedBy`) từ `AUDIT_LOG`, đồng thời khôi phục index `IX_TREATMENT_DOSSIER_SourceTemplateId`.
