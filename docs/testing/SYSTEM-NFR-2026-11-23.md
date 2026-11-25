# System Test Plan & Report — Non-Functional Requirements (2026-11-23)

## 1. Scope & Alignment
This document defines the Non-Functional Requirements (NFR) system test specification, execution environment, codebase adjustments, and test evidence for **DCMS (Dental Clinic Management System)** as of **23/11/2026 (Asia/Ho_Chi_Minh)**.

- **SRS Reference**: [IV. Non-Functional Requirements](../architecture/NON-FUNCTIONAL-REQUIREMENTS.md) (updated 20/11/2026).
- **Report Template**: `Report-5.4_System Test_NFRs` (`Security` and `Performance` sheets matching user template, extended with `Reliability`, `Usability`, `Compatibility`, and `Summary` sheets matching SRS IV and DCMS quality attributes).
- **Deliverable Workbook**: `outputs/nfr-20261123/Report-5.4_System Test_NFRs_DCMS.xlsx` (historical `outputs/nfr-test-scenarios/` and `outputs/nfr-20261111/` remain untouched).

### Baseline Environment
- **Git Branch & Commit**: `feat/ai-service-server` @ `f7409c7e97dcd490b6930d5a7a09fed061e5ab4a` (with uncommitted local working tree updates on backend & web).
- **Host Machine**: Microsoft Windows 11 Home Single Language, 11th Gen Intel(R) Core(TM) i7-11800H @ 2.30GHz (8 logical CPUs), 15.71 GB RAM.
- **Runtime Stack**:
  - Backend: .NET 10 ASP.NET Core Minimal API (`DCMS.Api`, Kestrel on `http://localhost:5080` for live probes + isolated `WebApplicationFactory`/Kestrel loopback in `DCMS.IntegrationTests` with `dcms_test_*` schemas).
  - Database: PostgreSQL 17 (`dcms-postgres-1` Docker container, port `5432`) + Mailpit (`dcms-mailpit-1`).
  - Web Production Bundle: `web/dist` (React + TypeScript + Vite + Ant Design) served via local static/proxy preview matching `web/nginx.conf` so measurements exclude Vite dev cold-transform overhead.
- **Safety & Isolation Rules**:
  - Zero production code or database schema changes during test execution.
  - Zero load or mutating traffic sent to external production (`https://nhakhoangockhang.id.vn`).
  - Live API probes use freshly registered synthetic accounts (`SYNTHETIC NFR ...`) or read-only checks; seeded demo accounts and local dev Postgres container state are never altered or restarted destructively.
  - No passwords, JWTs, refresh tokens, or connection strings are stored in evidence or reports.

---

## 2. Adjustments for Current Codebase (2026-11-23)
Compared to earlier NFR drafts (`2026-11-11` and `2026-11-19`), the current codebase incorporates the following verified implementation changes:

1. **System Message Codes (`DCMS.Application.Common.SystemMessages`)**:
   - `401 Unauthorized` (unauthenticated / expired / invalid token) -> `MSG06`.
   - `403 Forbidden` (insufficient role or grant) -> `MSG05`.
   - Invalid credentials / locked account sign-in -> `MSG07`.
   - Neutral account recovery response -> `MSG09`.
   - Password policy failure -> `MSG39`.
   - Stale optimistic concurrency / version conflict -> `MSG41`.
   - AI analysis or storage unavailable (`503`) -> `MSG49`.
   - Payment/deposit `requestKey` payload mismatch (`409`) -> `MSG113`.
2. **ClamAV Antivirus Removal (Commit `f7409c7`)**:
   - `DisabledClinicalFileScanner` is registered in `DependencyInjection.cs`.
   - Uploaded clinical documents still enforce server-side MIME, extension, content-signature (JPEG/PNG/PDF magic bytes), 20 MiB size limit, and 10-file batch limit; valid files become `Ready` with `ScanSkipped` audit, while `PendingScan`/`Rejected` remain blocked (`409`).
3. **Removed Endpoints in Current Working Tree**:
   - `/api/auth/sign-out-all` was removed; single-session revocation (`POST /api/auth/sign-out`) + refresh token rotation/reuse detection are tested.
   - `ServiceCatalogExport.cs` was removed; Excel formula-injection safety and export performance are tested via `/api/system-settings/materials/export` and `/api/system-settings/catalogs/medicines/export` (`Xlsx.Write` inline strings).

---

## 3. Test Case Catalog (61 Cases across 5 NFR Categories)

### 3.1 Security (`TC-SEC-001` .. `TC-SEC-027`)
| ID | Scenario | Primary Verification Method |
|---|---|---|
| TC-SEC-001 | Clinical documents accessible only via authenticated & authorized endpoints | `SystemNfrReportTests` + `ExaminationApiTests` |
| TC-SEC-002 | End-to-end business flow over HTTPS without mixed content | Passive TLS check only (`Blocked` for mutating E2E — no staging HTTPS) |
| TC-SEC-003 | Block anonymous API access (`401 MSG06` + `WWW-Authenticate: Bearer`) | `SystemNfrReportTests` + Live HTTP probes |
| TC-SEC-004 | Reject forged, tampered, wrongly-signed, or expired JWTs (`401 MSG06`) | `SystemNfrReportTests` + Live JWT probes |
| TC-SEC-005 | Refresh token rotation & reuse detection revokes whole chain (`401`) | Live HTTP probe |
| TC-SEC-006 | Sign-out revokes active access and refresh tokens immediately | Live HTTP probe |
| TC-SEC-007 | Locking an account revokes active sessions and blocks sign-in | Integration / code + live/test check |
| TC-SEC-008 | Role/grant revocation takes effect immediately on next request (`403 MSG05`) | `AllScreensPermissionApiTests` + `ExaminationApiTests` |
| TC-SEC-009 | Admin cannot read clinical patient records (GBR-026, `403 MSG05`) | Live HTTP probe + `AppointmentQueryApiTests` |
| TC-SEC-010 | Dynamic screen & API permission enforcement by access group | `AllScreensPermissionApiTests` |
| TC-SEC-011 | Cross-branch data isolation | `BranchIsolationTests` + `AllScreensPermissionApiTests` |
| TC-SEC-012 | IDOR prevention across patients (`403`/`404`) | `AppointmentQueryApiTests` + `ExaminationApiTests` |
| TC-SEC-013 | Delegated family record access obeys consent, verification, branch, expiry | `FamilyRecordApiTests` |
| TC-SEC-014 | PayOS webhook rejects forged/modified signatures (`400`) | Live HTTP probe + `TreatmentPayOsApiTests` |
| TC-SEC-015 | SQL injection & XSS prevention in search and web UI rendering | Live HTTP probe + Playwright XSS check |
| TC-SEC-016 | Excel export formula-injection neutralization (`inlineStr`) | `Xlsx.cs` verification + live materials export `.xlsx` inspection |
| TC-SEC-017 | Server-side file upload validation (fake MIME/signature, >20 MiB, >10 files) | `ExaminationApiTests` |
| TC-SEC-018 | Security response headers (`Cache-Control: no-store`, `X-Content-Type-Options: nosniff`) | `SystemNfrReportTests` + Live HTTP probe |
| TC-SEC-019 | CORS rejects untrusted origins on preflight & requests | `SystemNfrReportTests` + Live HTTP probe |
| TC-SEC-020 | Web tokens stored only in `sessionStorage`, never `localStorage` | Playwright storage inspection |
| TC-SEC-021 | Sign-in brute-force protection / temporary lockout (SRS IV §2.3) | Live 20-attempt probe |
| TC-SEC-022 | Account recovery neutral response, 5-attempt cap & rate limits | `RecoveryApiTests` |
| TC-SEC-023 | Server-side password policy enforcement (8–256 chars, hash storage) | `RegistrationApiTests` |
| TC-SEC-024 | Server-side staff web idle timeout enforcement | Playwright `staff-idle.spec.ts` / `AuthenticationService` |
| TC-SEC-025 | SignalR `/hubs/notifications` requires authentication & branch scoping | Live negotiate probe |
| TC-SEC-026 | Dev/OpenAPI/proof endpoints disabled in `Production` environment | `RecoveryApiTests` + `Program.cs` inspection |
| TC-SEC-027 | Dependency vulnerability & hardcoded secret hygiene | `dotnet list --vulnerable`, `npm audit`, secret scan |

### 3.2 Performance (`TC-PERF-001` .. `TC-PERF-010`)
| ID | Scenario | Target |
|---|---|---|
| TC-PERF-001 | Customer & staff dashboard load (1 user, production web build) | `< 2 s` (3 cold + 3 warm samples) |
| TC-PERF-002 | 5,000,000-byte PDF clinical document upload & retrieval | `< 2 s` (SRS IV §2.2; `< 10 s` template) |
| TC-PERF-003 | Concurrent stability & latency under 100 active user sessions (60 s) | `100 users, error rate < 1%, p95 < 500 ms` |
| TC-PERF-004 | Core read APIs (`auth/me`, `me/appointments`, `patients`) 1 user (30 calls) | `p95 < 500 ms` |
| TC-PERF-005 | Booking slot availability lookup (`/api/availability`, 14-day span, 30 calls) | `p95 < 500 ms` |
| TC-PERF-006 | Online booking form submission acknowledgment (`POST /api/appointments`) | `< 2 s` |
| TC-PERF-007 | 12-month finance statistics query (`/api/finance/statistics`) | `< 3 s` |
| TC-PERF-008 | Catalog Excel export (`/api/system-settings/materials/export`) | `< 5 s` |
| TC-PERF-009 | AI X-ray caries analysis latency / 120 s timeout (`MSG49`) | `< 120 s` (or `Blocked` if local `ai-service` unmounted) |
| TC-PERF-010 | Mobile APK cold start & Appointments tab latency (Android 4G) | `Blocked` (no physical Android test device attached) |

### 3.3 Reliability (`TC-REL-001` .. `TC-REL-010`)
- `TC-REL-001` Booking idempotency by `requestKey` (`AppointmentApiTests`)
- `TC-REL-002` Slot/chair concurrency lock — zero double-booking (`AppointmentApiTests`)
- `TC-REL-003` PayOS webhook & payment `requestKey` idempotency (`TreatmentPayOsApiTests`)
- `TC-REL-004` File storage & download byte-for-byte SHA-256 integrity (`SystemNfrReportTests`)
- `TC-REL-005` Non-Ready clinical file blocking & hash tamper protection (`ExaminationApiTests`)
- `TC-REL-006` AI service unavailable graceful fallback (`503 MSG49` + `AiAnalysisFailed` audit, `ClinicalAiAnalysisTests`)
- `TC-REL-007` Optimistic concurrency version conflict (`409 MSG41`, `ExaminationStorageTests` / `FamilyMemberApiTests`)
- `TC-REL-008` Web UI error banner & retry recovery on HTTP `503` (Playwright)
- `TC-REL-009` PostgreSQL `pg_dump` backup & restore consistency check (Docker isolated temp DB)
- `TC-REL-010` PostgreSQL temporary outage & recovery without API restart (`Blocked` — shared local DB protection)

### 3.4 Usability (`TC-USE-001` .. `TC-USE-008`)
- `TC-USE-001` Customer pages responsive at 390 / 768 / 1440 px with <= 1 px overflow (Playwright)
- `TC-USE-002` Keyboard navigation (`Tab` / `Enter` focus & activation, Playwright)
- `TC-USE-003` Consistent Vietnamese `MSG` codes without technical stack traces (Playwright)
- `TC-USE-004` Inline form validation preserves entered input (Playwright)
- `TC-USE-005` Loading state & duplicate-submit prevention during async actions (Playwright)
- `TC-USE-006` Confirmation dialog before irreversible actions (Playwright)
- `TC-USE-007` Vietnamese `dd/MM/yyyy` dates, 24h time, and VND currency formatting (Playwright)
- `TC-USE-008` Zero uncaught JavaScript / React `pageerror` exceptions across main flows (Playwright)

### 3.5 Compatibility (`TC-COM-001` .. `TC-COM-007`)
- `TC-COM-001` Clean build verification from repository (`dotnet build` + `npm run build`)
- `TC-COM-002` Multi-browser desktop verification (Chromium / Edge / Firefox availability check)
- `TC-COM-003` 2D/3D dental tooth chart (Odontogram) rendering & 2D toggle (Playwright)
- `TC-COM-004` Customer web on physical iOS Safari & Android Chrome (`Blocked` — no physical mobile devices)
- `TC-COM-005` Patient Android APK compatibility across Android 10/12/14 (`Blocked` — no physical devices/APK test bench)
- `TC-COM-006` Production Docker Compose manifest & health endpoint verification (`compose.prod.yaml` + `/health`)
- `TC-COM-007` Automated backend code coverage meets `>= 70%` line coverage quality gate (`Coverlet` + `ReportGenerator`)

---

## 4. Execution Results

Completed **23–24/11/2026 (Asia/Ho_Chi_Minh)**. All 62 test cases were evaluated across `Security`, `Performance`, `Reliability`, `Usability`, and `Compatibility`, producing **[Report-5.4_System Test_NFRs_DCMS_v2.xlsx](../../outputs/nfr-20261123/Report-5.4_System%20Test_NFRs_DCMS_v2.xlsx)** (generated as `_v2.xlsx` while `Report-5.4_System Test_NFRs_DCMS.xlsx` was open in Excel) and raw artifacts in `outputs/nfr-20261123/evidence/`.

### Summary by Category & `>= 70%` Test Coverage Gate

| Category | Total | Pass | Fail | Blocked | Not Run | Test Coverage `(Pass+Fail)/Total` | Test Successful Coverage `Pass/Total` | Executed Pass Rate | `>= 70%` Gate |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Security | 27 | 23 | 2 | 2 | 0 | 92.6% | 85.2% | 92.0% | PASS |
| Performance | 10 | 7 | 2 | 1 | 0 | 90.0% | 70.0% | 77.8% | PASS |
| Reliability | 10 | 9 | 0 | 1 | 0 | 90.0% | 90.0% | 100.0% | PASS |
| Usability | 8 | 8 | 0 | 0 | 0 | 100.0% | 100.0% | 100.0% | PASS |
| Compatibility | 7 | 4 | 1 | 2 | 0 | 71.4% | 57.1% | 80.0% | PASS |
| **TOTAL** | **62** | **51** | **5** | **6** | **0** | **90.3%** | **82.3%** | **91.1%** | **PASS** |

### Backend Code Coverage Quality Gate (`>= 70%` Line Coverage)
Measured via `backend/tests/run-integration-coverage.ps1` (`Coverlet` + `ReportGenerator`, `coverage.runsettings` excluding `Migrations/*.cs`; 505 tests: 504 Passed, 1 N/A opt-in on 23/11/2026; saved in `outputs/nfr-20261123/evidence/backend-coverage-summary.json`):

| Assembly | Classes | Covered / Coverable Lines | Line Coverage | Branch Coverage | Method Coverage | `>= 70%` Gate |
|---|---:|---:|---:|---:|---:|---|
| `DCMS.Api` | 33 | 1,782 / 1,799 | 99.0% | 91.8% (336/366) | 84.2% (48/57) | PASS |
| `DCMS.Application` | 23 | 263 / 330 | 79.6% | 56.9% (337/592) | 72.0% (31/43) | PASS |
| `DCMS.Domain` | 23 | 135 / 170 | 79.4% | 59.9% (121/202) | 92.4% (73/79) | PASS |
| `DCMS.Infrastructure` | 98 | 2,852 / 3,278 | 87.0% | 62.0% (959/1,546) | 84.0% (469/558) | PASS |
| **Overall Backend** | **177** | **5,032 / 5,577** | **90.2%** | **64.7% (1,753/2,706)** | **84.2% (621/737)** | **PASS** |

### Automated xUnit & Playwright Runs
- `nfr-system.trx` (`SystemNfrReportTests`): **1 / 1 Passed** (20-user/60s baseline: 4,196 reqs, 0 errors, `p95 = 32.46 ms`, `69.72 req/s`; 5,000,000-byte PDF upload `261.74 / 137.14 / 74.47 ms`; 10 document access probes).
- `nfr-regressions.trx` (13 targeted NFR integration suites): **197 / 197 Passed** (0 Failed, 0 Skipped, 4m 33s).
- `nfr-account-ai.trx` (`StaffAccountTests` + `DentalAiLiveContractTests`): **7 / 7 Passed** (including live GPU CUDA `ai-service` contract verification).
- `nfr-settings.trx` (`GeneralSettingsApiTests`): **6 / 6 Passed**.
- Legacy Playwright `web/tests/staff-idle.spec.ts`: **1 / 3 Passed, 2 Failed** (tests still call removed `/api/settings/staff-idle` route replaced by `/api/system-settings/general`).

### Defects Found (`5` Failed Cases)
1. **`NFR-DEF-01` (`TC-SEC-021`, High — Security)**: Missing brute-force lockout / rate limiting on `POST /api/auth/sign-in`. 20 consecutive wrong passwords returned HTTP `401 MSG07` (`throttledOrLocked=false`) and attempt #21 with valid password succeeded (`200 OK`).
2. **`NFR-DEF-02` (`TC-SEC-027`, High — Security)**: Vulnerable dependency advisories reported: `System.Security.Cryptography.Pkcs 10.0.0` (High + Moderate across backend projects), `SQLitePCLRaw.lib.e_sqlite3 2.1.11` (High in `DCMS.UnitTests`), `mobile/` npm (6 High, 12 Moderate), `web/` npm (1 Moderate: `vite 6.4.4`). Secret scan found 0 real hardcoded secrets (temporary dev helper `web/public/uc36-test.html` was bundled into `web/dist/`).
3. **`NFR-DEF-03` (`TC-COM-001`, Medium — Compatibility / Build)**: `dotnet build backend/DCMS.sln -c Release --no-restore` failed with 17 compile errors inside `DCMS.UnitTests` (`Create/AppointmentCreateTests.cs`, `LabWorkTypeCreateTests.cs`, `MaterialCatalogCreateTests.cs` out of sync with uncommitted backend refactoring). `DCMS.Domain`, `DCMS.Application`, `DCMS.Infrastructure`, `DCMS.Api`, `DCMS.IntegrationTests`, and `web` production bundle all compiled with 0 errors.
4. **`NFR-DEF-04` (`TC-PERF-003`, High — Performance)**: 100-user 60s concurrent load (`load-100u.json`, 4,423 reqs, `69.54 req/s`) had `0.136%` errors (`6` HTTP `503`s) and `p50 = 184.22 ms`, but **`p95 = 4,823.55 ms`** (`p99 = 9,686.69 ms`, `max = 13,828.92 ms`), failing SRS IV §2.2 (`p95 < 500 ms`) and template (`p95 < 1,000 ms`). Traced to `AuthenticationService.ValidateSessionAsync` opening a write transaction + `SELECT ... FOR UPDATE` on every request, saturating the default Npgsql pool (`Max Pool Size=100`) against PostgreSQL `max_connections=100`.
5. **`NFR-DEF-05` (`TC-PERF-001`, Medium — Performance)**: Production `web/dist` Customer Dashboard loaded in `263.02–293.58 ms` (cold) and `158.36–170.66 ms` (warm), whereas Staff Workspace exhibited recurring `~4.08–4.53 s` spikes (`cold=[311.62, 4083.30, 313.93] ms`, `warm=[238.49, 4447.71, 181.17] ms`; dual-stack repeat `cold=[490.68, 4208.60, 367.06] ms`, `warm=[206.12, 4533.55, 231.75] ms`, traced to `GET /api/auth/me` taking `3,696 ms` on outlier load). Web `index` JS chunk is `2,237.11 kB` (>500 kB warning).

### Key Passing Metrics & Blocked Scope
- **API Latency (1 user, 30 samples)**: `auth/me` `p95 = 9.24 ms`; `me/appointments` `p95 = 11.16 ms`; `patients` `p95 = 58.33 ms`; `/api/availability` `p95 = 57.41 ms`; booking submit `22.57–48.29 ms` (samples 1–2 `201`); 12-month finance stats `p95 = 54.66 ms`; materials Excel export `p95 = 47.02 ms`; 5 MB PDF upload `74.47–261.74 ms`; live CUDA `ai-service` `82.63–470.30 ms` (`3/3` HTTP `200`).
- **Usability & Browser Compatibility**: `0 px` horizontal overflow at `390 / 768 / 1440 px`; `Tab`/`Enter` keyboard navigation Pass; `503` UI error & Retry recovery Pass; `0` JS `pageerror`/console errors; Chrome `158` & MS Edge `158` Pass; WebGL 3D Odontogram + 2D switch Pass; `compose.prod.yaml` config check & `/health` Pass (`TC-COM-006`).
- **Database Backup/Restore (`TC-REL-009`)**: `pg_dump -n public --clean --if-exists` (`2.2 MB`) restored cleanly into temporary `dcms_nfr_restore_check` with identical row counts (`APPOINTMENT=87`, `CLINICAL_DOCUMENT=8`, `PATIENT_PROFILE=78`, `TREATMENT_DOSSIER=19`, `USER_ACCOUNT=70`).
- **Blocked (`6` cases)**: `TC-SEC-002` (no dedicated HTTPS staging), `TC-SEC-024` (5-min live idle expiry wait + legacy E2E script route mismatch), `TC-PERF-010` / `TC-COM-004` / `TC-COM-005` (no physical iOS/Android devices attached), `TC-REL-010` (avoid stopping shared local `dcms-postgres-1`).
