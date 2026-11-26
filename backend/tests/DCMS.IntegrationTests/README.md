# DCMS integration tests (create flows)

Each test calls the real API over HTTP (`WebApplicationFactory<Program>`) against a real PostgreSQL database.
A throw-away schema is migrated once per run and dropped afterwards, so no existing data is touched.

Scope: only the **create** endpoints that the web and mobile screens use.

| File | Endpoint | Cases |
|---|---|---|
| `StaffAccountTests` | `POST /api/accounts` | ACC-01..06 |
| `CustomerRegisterTests` | `POST /api/auth/register` | REG-01..09 |
| `PatientTests` | `POST /api/patients` | PAT-01..09 |
| `FamilyMemberTests` | `POST /api/me/family-members` | FAM-01..09 |
| `TreatmentDossierTests` | `POST /api/patients/{id}/dossiers/quick` | DOS-01..06 |
| `ItemPlanTests` | `POST /api/treatment-templates/item-plans` and `.../{id}/items` | PLN-01..07, ROW-01..10 |
| `CatalogTests` | lab work types, material groups, materials | LWT-01..08, CAT-01..05, MAT-01..06 |
| `AppointmentTests` | `POST /api/appointments` (form validation only) | APT-01..10 |
| `SystemSettingsCatalogApiTests` | medicines, providers, lab work types, reference catalogs (`/api/system-settings/settings/catalogs/...`) | MED-01..08, PRV-01..02, REF-01..04 |
| `GeneralSettingsApiTests` | general settings, patient code sequence, patient form policy | GEN-01..05, SEQ-01 |
| `PatientLinkApiTests` | account ↔ patient link: `/api/patient-links/me`, `/api/patient-link-reviews` | LNK-01..04 |

The other test classes cover full workflows (scheduling, appointments, reception, examination, treatment, payments,
stock, permissions) without `[Case]` attributes. Module grouping for the report and the coverage target are in
[`docs/testing/INTEGRATION-TEST-COVERAGE.vi.md`](../../../docs/testing/INTEGRATION-TEST-COVERAGE.vi.md).

A successful booking needs a published working schedule and a matching schedule version; it is not covered here yet.

## Run

PostgreSQL must be running and `DEV_SEED_PASSWORD` set (import it from `.env` in the same shell):

```powershell
.\infrastructure\scripts\import-development-env.ps1
dotnet test backend/tests/DCMS.IntegrationTests
```

With code coverage and the HTTP log used by the report (results in `TestResults/<Name>/`):

```powershell
powershell -ExecutionPolicy Bypass -File backend/tests/run-integration-coverage.ps1 -Name final
```

Test classes run one after another (`xunit.runner.json`): every fixture hosts its own API and schema, and running them
in parallel exhausts PostgreSQL connections.

Set `DCMS_IT_TRACE` to a file path to also write every case with its real HTTP exchanges as JSON
(used to fill the test report sheet). Each exchange has `Status`, `StatusLine` (for example `HTTP 409 Conflict`) and `Body`.
