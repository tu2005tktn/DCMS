# DCMS.UnitTests

Unit tests for the **create** methods only. Each test builds the service on an EF Core InMemory
`DcmsDbContext` (no PostgreSQL, no HTTP), feeds one input set field by field, and checks the exact
error message the code throws.

```powershell
& .\infrastructure\scripts\import-development-env.ps1
& $dotnet test backend/tests/DCMS.UnitTests
```

| File (in `Create/`) | Method under test | Cases |
| --- | --- | --- |
| `StaffAccountCreateTests` | `AccountService.CreateAsync` | 11 |
| `PatientCreateTests` | `PatientService.CreateAsync` | 12 |
| `FamilyMemberCreateTests` | `FamilyMemberService.CreateAsync` | 6 |
| `CustomerRegisterTests` | `RegistrationService.RegisterAsync` | 11 |
| `AppointmentCreateTests` | `AppointmentService.CreateAsync` | 10 |
| `TreatmentDossierCreateTests` (PostgreSQL) | `TreatmentDossierService.QuickCreateAsync` | 3 |
| `ItemPlanCreateTests` (PostgreSQL) | `ItemPlanService.CreateAsync` | 5 |
| `ItemPlanRowCreateTests` (PostgreSQL) | `ItemPlanService.SaveRowAsync` (add item, no row id) | 10 |
| `LabWorkTypeCreateTests` (SQLite) | `LabWorkTypeCatalog.CreateAsync` | 11 |
| `MaterialCatalogCreateTests` | `SystemCatalogService.CreateCategoryAsync` / `CreateMaterialAsync` | 3 + 5 |
| `BranchCreateTests` (SQLite) | `BranchManagementService.SaveBranchAsync` (no id) | 12 |
| `ChairCreateTests` (SQLite) | `BranchManagementService.SaveChairAsync` (no id; duplicate code needs PostgreSQL) | 8 |
| `AccessGroupCreateTests` (SQLite) | `SettingsService.SaveGroupAsync` (no id) | 11 |

`CreateTestKit` holds the shared setup (InMemory database, SQLite database for services that take the
PostgreSQL advisory lock, `UserManager`, `ErrorOf`). `NewSqliteDb()` registers no-op versions of the
Postgres functions the model uses (`pg_advisory_xact_lock`, `btrim`, `regexp_replace`, `nextval`).

Scope and limits:

- Only validation failures that happen before the first PostgreSQL-specific statement are covered
  (`nextval`, `pg_advisory_xact_lock`, raw SQL are not supported by InMemory). Success paths and
  uniqueness conflicts belong to `DCMS.IntegrationTests`.
- `TreatmentDossierService.QuickCreateAsync` and `ItemPlanService.CreateAsync` take a PostgreSQL advisory lock and read
  branch membership from a JSON collection, so they run on the real PostgreSQL server
  (`ConnectionStrings__DefaultConnection`, run `import-development-env.ps1` first). The tests create one
  schema `dcms_unit_<guid>` per run, migrate it once and drop it afterwards (`PostgresCollection`); they never
  touch the development tables. They fail with a clear message when the variable is not set.
- Expected messages are written as literal Vietnamese text so a wording change in `SystemMessages`
  fails the test.
- The report with these cases is the Google Sheet "5.1" (Unit Test Report), one sheet per module.
