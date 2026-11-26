# Bàn giao: Unit test mức method (white-box) cho DCMS

Tài liệu cho agent/người tiếp quản. Trả lời người dùng bằng **tiếng Việt**. Không ghi mật khẩu demo hay chuỗi kết nối vào file/chat.

## 1. Mục tiêu
- Hỗ trợ đồ án: viết unit test **mức method** cho các lớp logic thật, mỗi case có Input/Expected rõ, mã case `UT-XXX-nn`, ưu tiên `TheoryData`/`InlineData`, kiểm tra `BusinessException` đủ **status + code + thông báo tiếng Việt**.
- Mục tiêu hiện tại của người dùng: **Method coverage ≥ 70%** trên tập lớp logic thật (loại DTO record, property của entity, code sinh tự động, migration, seeder dev, SMTP, Firebase). Ngoài ra người dùng đã nói rõ chỉ cần Method coverage.
- Số liệu gần nhất đo được (tập lớp đã lọc): **Method 50.7% (788/1553)**, Line 57%, Branch 51.6%. Số này **chưa** gồm: CareTask, ReferenceCatalog, WorkingSchedule, GeneralSettings, Patient, Notification. Cần ≈1087 method để đạt 70%. Phải đo lại.

## 2. Quy tắc làm việc (từ CLAUDE.md và người dùng)
- Không commit/push nếu người dùng không yêu cầu. Không chạy migration/seed lên `dcms_dev`.
- Chỉ chạy test liên quan, trừ khi đo coverage (chạy toàn bộ UnitTests ~5 phút).
- Chưa cập nhật tài liệu test (README...) cho đến khi người dùng yêu cầu.
- Dùng "they/them" cho người chưa rõ đại từ.

## 3. Cách chạy
Từ thư mục gốc repo, PowerShell:
```powershell
. .\infrastructure\scripts\import-development-env.ps1   # nạp env, đặt $dotnet, KHÔNG in bí mật
& $dotnet test backend/tests/DCMS.UnitTests --filter "FullyQualifiedName~CareTaskServiceTests"
```
- Test dùng PostgreSQL thật phải có `[Collection(PostgresCollection.Name)]` và cần PostgreSQL trong Docker đang chạy (`docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d`; không bao giờ `down -v`).
- Các test Postgres dùng **một schema tạm dùng chung cho cả lần chạy** (`CreateTestKit.NewPostgresDbAsync()`), schema bị xóa cuối lần chạy. Collection chạy tuần tự.
- Lệnh >10 phút bị đẩy nền; không có Python (dùng sed/Edit).
- Kết quả test lỗi trong PowerShell có thể làm script chết vì stderr: chuyển hướng bằng `cmd /c "... > file 2>&1"`.

## 4. Các file test (backend/tests/DCMS.UnitTests/Methods/)
Tất cả **chưa commit**. Xanh khi chạy riêng lần cuối, trừ ghi chú.

| File | Case | Ghi chú |
|---|---|---|
| RegistrationServiceTests | 28 | UT-REG-12..39 (UT-REG-01..11 nằm ở Create/) |
| AccountRecoveryServiceTests | 186 | UT-RCV-01..186 |
| FamilyRecordServiceTests | 87 | UT-FAM |
| AppointmentQueryServiceTests | 47 | UT-AQS |
| CareTaskServiceTests | 67 | UT-CARE |
| ReferenceCatalogServiceTests | 56 | UT-REF |
| WorkingScheduleServiceTests | 81 | UT-SCH |
| GeneralSettingsServiceTests | 44 | UT-SET |
| PatientServiceTests | 48 | UT-PAT |
| **NotificationServiceTests** | ~45 | UT-NTF — **mới viết, CHƯA build/chạy lần nào**; có thể có lỗi biên dịch/assert cần sửa |
| TreatmentLabTests, TreatmentDossierServiceTests, TreatmentSheet*, TreatmentActivityCommandTests, TreatmentNextVisitTests, TreatmentSourceVisitTests, AppointmentChangeServiceTests, SupportRequestServiceTests, AppointmentServiceCreateTests, Reception*Tests, PrescriptionServiceTests, ServicePriceServiceTests, WorkStatisticsServiceTests, AuthenticationServiceTests, AccountServiceTests, RulesMethodTests | — | Đã xanh từ trước |

`Doubles.cs`: `TestClock(now)` (Now = 2030-01-10T02:00Z = 09:00 giờ phòng khám UTC+7), `TestBranchAccess`, `TestAvailability`, `TestQueries`.

### Lỗi có sẵn không phải do mình
`Create/ItemPlanRowCreateTests.AddRow_rejects_a_plan_that_already_has_100_items` fail (mong 400, nhận 409). Chưa sửa, đã báo người dùng.

### Lỗi thật phát hiện ở mã nguồn (chưa sửa, chưa khóa bằng test)
`GeneralSettingsService.PreviewAsync` ném `InvalidOperationException` khi bật bắt buộc "Ngày lập hồ sơ" (`ProfileDate`): `active.CountAsync(x => x.ProfileDate == null)` không dịch được sang SQL. Test `UT_SET_29` cố ý bỏ trường này.

## 5. Mẫu và bẫy kỹ thuật (đã gặp)
- Mẫu Env cho Postgres: xem đầu `CareTaskServiceTests.cs` / `WorkingScheduleServiceTests.cs`. Seed role `AccessGroup` (`RecordKind = "IdentityRole"`) cho mọi `AccountRoles.Staff` nếu thiếu; `CreateTestKit.SeedStaffAsync(db, userName, role, branchId, member)`; `NewRoleUserManager(db)`; id duy nhất `CreateTestKit.NextId()`; truy vấn phải lọc theo tag riêng của từng test vì schema dùng chung.
- Mỗi file có hàm `Rejects(Func<Task>, status, code)` dựa trên `CreateTestKit.ErrorOf`.
- `ExecuteUpdate` không sửa được thuộc tính lưu JSON/owned (dữ liệu auth của `ApplicationUser`, `SchedulingMember`, `Appointment.SupportRequests`): nạp entity có theo dõi, sửa, `SaveChanges`, rồi `ChangeTracker.Clear()`. Sửa `SchedulingMember`: `db.ChangeTracker.Clear(); (await db.Set<SchedulingBranch>().SingleAsync(...)).Members.Single(...)...`. Không dùng `Include(x => x.Chairs)` hay `Entry(...).Property(Members)`.
- Lệnh lỗi để lại entity được theo dõi → `ChangeTracker.Clear()` trước lần gọi service tiếp theo (mô phỏng request mới).
- `DateTimeOffset` ghi vào Postgres phải offset 0: `.ToUniversalTime()`.
- Record có mảng so sánh theo tham chiếu; tuple chứa mảng cũng vậy → so sánh riêng từng phần.
- Ràng buộc FK: `Visit` cần `Appointment` thật (cần chair, employee, tài khoản đặt) và `DentistId` là **tài khoản**; `Appointment.DentistId` là **employee id**.
- Mỗi member chỉ một `FamilyRecordAccess` đang hoạt động (chỉ mục duy nhất một phần).
- Service dùng `DateTimeOffset.UtcNow` thật (PatientService, ActiveMembers) → ngày trong test phải so với giờ thật, không phải `TestClock`. Khi so với `TestClock`, lưu ý lịch hẹn trên cùng khung giờ của test khác trong schema dùng chung: dùng `patientId` hiếm (`int.MaxValue - n`).
- Test đổi `ClinicSettingsVersions` (toàn cục) phải xóa bảng này khi `DisposeAsync` (xem GeneralSettings/Patient tests).
- Cần `using GeneralSettings = DCMS.Application.Features.SystemSettings.GeneralSettings;` vì trùng tên với namespace Settings.

## 6. Cách đo Method coverage (script nằm ở scratch, sẽ mất; tạo lại)
Công cụ (không nằm trong repo): `dotnet-coverage` và `reportgenerator` (cài vào một thư mục tools tạm, ví dụ `dotnet tool install --tool-path <dir> dotnet-coverage` và `... dotnet-reportgenerator-globaltool`).
1. Build: `dotnet build backend/tests/DCMS.UnitTests`.
2. Thu coverage toàn bộ UnitTests (~5 phút, chạy nền):
   `<tools>\dotnet-coverage.exe collect -f cobertura -o <out>\covall.xml "dotnet test --no-build"` (chạy trong `backend\tests\DCMS.UnitTests`, sau khi nạp env).
3. Lọc về tập "lớp logic thật" và xuất `JsonSummary`:
```
reportgenerator "-reports:<out>\covall.xml" "-targetdir:<out>\flt" -reporttypes:TextSummary;Html;JsonSummary
  "-classfilters:+DCMS.Infrastructure.*;+DCMS.Application.Common.SystemMessages;+DCMS.Application.Features.*Grants;+DCMS.Application.Features.*Messages;+DCMS.Domain.*Rules*;-*Migrations*;-*Development*;-*Smtp*;-*Firebase*;-System.Text.RegularExpressions.Generated*;-*.Generated.*"
```
4. Đọc `flt\Summary.txt` (dòng "Method coverage") và `Summary.json` để xếp lớp còn nhiều method chưa phủ.

## 7. Việc còn lại (theo thứ tự đề xuất)
1. Build và chạy `NotificationServiceTests`, sửa lỗi.
2. Đo lại Method coverage (mục 6).
3. Viết tiếp test cho các lớp còn nhiều method chưa phủ (số method chưa phủ ước tính ở lần đo trước): `ClinicalDocumentService` 25, `ExaminationService` 23, `MaterialPurchaseService` 22, `BranchManagementService` 20, `SystemCatalogService` 19, `MedicineCatalog` 19, `PatientLinkReviewService` 16, `OwnClinicalRecordService` 15, `AccountRoleStore` 14, `SettingsService` 12, `ProviderCatalog` 12, `AccountUserStore` 11, `FamilyMemberService` 11, `SettingsAuditService` 11, `PatientLinkService` 11, `PatientLinkSupport` 10, `SupplyCatalog` 9, `EmailVerificationService` 9, `InvestigationService` 8, `HttpDentalAiClient` 8, `ItemPlanService` 8, `BookingSettingsService` 7, `StaffIdleService` 7, `AppointmentNotificationDispatcher` 6, `ClinicalWorkService` 6, `MaterialStockService` 6, `GoogleTokenVerifier` 5, `ClinicalAccess` 5, và các lớp nhỏ (`DependencyInjection`, `SystemMessages`, `ScreenGrants`, `SettingsGrants`, `Xlsx`...) là phần rẻ.
4. Lặp đo lại đến khi ≥70%, rồi báo người dùng bằng tiếng Việt kèm cảnh báo: Method coverage dễ đạt nhưng yếu về chất lượng.

## 8. Việc người dùng đã được đề nghị nhưng chưa trả lời
- Thêm công cụ coverage vào repo (`dotnet-tools.json` + `infrastructure/scripts/coverage.ps1`).
- Dọn các test giá trị thấp: test khóa lỗi đã biết (UT-LAB-42, UT-RCV-88, UT-RCV-186), theory kiểm tra framework (UT-REG-35..39, UT-RCV-101), test mặc định cấu hình, test chuyển tiếp thuần (TreatmentActivityCommandTests).
- Chuyển `[Fact]` sang `[Theory]` có Input/Expected, lập bảng tổng hợp case, cập nhật README của UnitTests.
