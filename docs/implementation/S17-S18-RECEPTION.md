# S17 S18 reception implementation and evidence

DCMS-93 under [DCMS-92](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-92). Implemented against the supplied v2.3 documents and the 25 October 2026 request. Contract and examples were written before implementation in [S17-S18-RECEPTION.md](../specs/S17-S18-RECEPTION.md).

## Delivered behavior

Staff web `/reception` reads authorized branch/day appointments and a separate Visit queue from real API calls. It filters dentist, appointment status, Visit status and name/code (100 characters), displays arrival/wait/purpose/source/current blockers, refreshes every 30 seconds, and opens `/reception/check-in/{appointmentId}` for explicit identity comparison. Patient code/name/contact and optional date of birth search never select automatically. The existing UC-38 drawer checks duplicates and preserves unknown optional values; creating a Patient does not approve an account link. Booker and expected attendee remain distinct from the verified Patient.

Check-in uses a single PostgreSQL transaction with the existing scheduling advisory lock, appointment/patient row locks, source read locks, unique Visit.AppointmentId and actor/requestKey receipt. It checks branch membership, role, current version/status, clinic date, active Patient and the existing reservation through the shared availability service. It links Patient, sets Appointment CheckedIn and creates one Queued Visit. The queue is a projection of Visit. Check-in retains the chair/staff reservation using the existing scheduling status `Checked-in`, so waiting for medicine never frees resources. No deposit, account, diagnosis, plan, charge or receipt is created.

The four attendance purposes share `VisitSource` IDs. Missing context explicitly requires assessment (MSG237); supplied wrong-owner/branch/kind/hierarchy IDs are rejected. Current ancestor holds remain visible. Arrival defaults to server time; explicit earlier arrival requires a reason, with actualArrivalAt and enteredAt stored separately. Walk-in opens the shared S11 drawer with WalkIn/InPerson defaults, uses the existing availability and booking API, and then preserves the original arrival instant through reception, including seconds/milliseconds.

`/reception/visits/{visitId}` reads the committed Visit and history. Assistant reordering has expectedVersion/requestKey, a mandatory reason, affected before/after order and actor audit. It never changes appointment times. A separate command authenticates the assigned deciding dentist for clinical priority; assistants cannot impersonate the decision with a client-supplied dentist ID. Priority records do not bypass resource checks or silently reorder confirmed bookings.

## Persistence and module handoff

Migration `20261025075305_ReceptionVisits` adds VISIT, VISIT_AUDIT, RECEPTION_COMMAND_RECEIPT and CLINICAL_SOURCE_REFERENCE. It preserves existing data/tables. VISIT is the sole lifecycle record for the queue. The other tables provide append-only history, idempotency and a compact integration read model; no separate table per dossier/item/step/activity was added.

The development scheduling seeder creates exactly three distinct chair IDs in its original clinic fixture, upgrading its previous two-chair fixture idempotently. It does not add three chairs per branch. The isolated browser fixture asserts exactly three physical chairs and publishes synthetic working periods for actual availability checks.

DCMS-94/95 must consume `Visit.Id`/`VisitStatuses.Queued`, then update that same Visit with optimistic concurrency for start/completion. No `Waiting` enum or second Visit should be introduced. Their clinical work and financial effects remain separate commands. DCMS-96/99 consume patientId, branchId, purpose and dossier/item/step/activity IDs from `VisitResult`.

This checkout at baseline `7da445f` had no clinical dossier/plan/actual persistence. `IReceptionSourceReader` is the explicit integration boundary. Its default implementation reads `ClinicalSourceReference`: clinical modules publish their existing canonical source IDs (not newly allocated proxy IDs), kind, parentId, patientId, branchId, label, blocker and version in the same transaction as their clinical changes. A composite parent FK preserves owner/branch consistency. Reception exposes only a scoped read API and cannot write clinical sources. `sourceLinks` includes readable selected/ancestor records; `source` retains IDs for downstream commands. The reader can be replaced with a direct projection of the clinical schema when those modules are merged. Until then the application supports reassessment with pending source context; it does not invent source records or claim complete clinical-module integration. Integration tests seed synthetic source references to prove the real SQL/API ownership and hold behavior.

## Reproduction

Use the repository's ignored `.env`; never paste credentials into tracked files. From the repository root:

```powershell
. infrastructure/scripts/import-development-env.ps1
& $dotnet restore backend/DCMS.sln
& $dotnet build backend/DCMS.sln --no-restore
& $dotnet test backend/tests/DCMS.IntegrationTests --filter FullyQualifiedName~ReceptionApiTests
```

Apply migration and seed with `infrastructure/scripts/start-api.ps1 -Migrate -SeedDemo`, then start the API normally. Existing development settings and environment variables remain authoritative. Web installation/build is `npm install` and `npm run build` from `web`.

For browser evidence use a separate synthetic schema and port. The fixture creates a new schema and refuses an existing schema; it never deletes or overwrites development data. Keep this PowerShell open for the API:

```powershell
. infrastructure/scripts/prepare-reception-e2e.ps1
$env:ASPNETCORE_URLS = 'http://localhost:5082'
$env:Cors__AllowedOrigins__0 = 'http://localhost:8081'
$env:Cors__AllowedOrigins__1 = 'http://127.0.0.1:8081'
& $dotnet backend/src/DCMS.Api/bin/Debug/net10.0/DCMS.Api.dll --contentRoot "$repositoryRoot/backend/src/DCMS.Api"
```

In another terminal from `web`, start Vite with `$env:VITE_API_BASE_URL='http://localhost:5082'; node node_modules/vite/bin/vite.js --host 127.0.0.1 --port 8081`. In a third terminal from `web`:

```powershell
. ../infrastructure/scripts/import-development-env.ps1
$env:E2E_DEMO_PASSWORD = $env:DevSeed__Password
node node_modules/@playwright/test/cli.js test tests/reception.spec.ts --reporter=line
```

Stop these test servers after use; run the normal API on 5080 and Vite with its normal API setting for regular development. Test schemas are retained for inspection and have only synthetic identities.

## Evidence on 25 October 2026

| Check | Result and boundary |
| --- | --- |
| .NET 10 restore/build | Pass using local SDK 10.0.401 |
| npm install / web production build | Pass; existing bundle-size/SignalR annotation warnings |
| ReceptionApiTests | 27/27 pass against real PostgreSQL in isolated schemas |
| Full backend regression | 43/43 unit tests pass; 197/199 integration tests pass. The two auth failures below also reproduce at baseline 7da445f |
| Concurrent/replayed check-in | Same Visit for same-key retry and concurrent staff/different keys; one persisted reception audit; changed payload conflicts |
| Invalid status/day/version/Patient/role/branch | Rejected, no partial Visit; archived Patient and mismatched identity cannot be received |
| Source/purpose/arrival | All four purposes, cross-Patient and cross-branch references, wrong hierarchy, live hold changes, missing-context reassessment, historical reason, future rejection and actual arrival earlier than booked slot |
| Queue/intent/resources | Reorder history/version/retry; dentist-only priority audit; intent confirmation creates no Visit; chair/dentist/assistant collisions blocked and three independent reservations allowed |
| Playwright reception.spec.ts | 6/6 pass (25.5 s), real API and PostgreSQL: shared phone/manual selection, concurrent reopen, UC-38 create, missing-source continuation/reorder, walk-in booking through actual check-in with exact arrival retained |
| Connection fault case | One request is deliberately aborted; no fabricated success response. Form fields and requestKey survive, retry reaches the real API |
| Visual inspection | S17 and S18 browser screenshots inspected; readable cards/forms/tables, table overflow scrolls inside its region |
| Normal local runtime | API/database `/health` healthy on 5080; web on 8081 returns HTTP 200 after restoring the normal API setting |

Full backend regression and baseline comparison are recorded below. Source-module writer integration, start/completion/clinical work and physical-device mobile testing are not claimed by this task.

The existing `System.Security.Cryptography.Pkcs` 9.0.0 NU1902/NU1903 dependency warnings also appear on the baseline. Two existing auth tests fail independently of reception: `GoogleApiTests.ExistingLocalEmailCreatesSeparateGoogleAccount` expects OK but receives Conflict; `RegistrationApiTests.RepeatedAndConcurrentRequestsCreateOneAccount` receives two Conflict responses. Both failures were reproduced unchanged in a detached worktree at `7da445f` using their own isolated PostgreSQL schemas. No auth behavior was changed to make those assertions pass.

## Follow-up for the reported check-in 409 on 25 October

The active normal database had zero appointments today and upcoming Confirmed appointments on 26 October or later. A diagnostic request against its existing 26 October synthetic appointment returned 409/MSG42 with no Visit created; the same-day guard was working. S17/S18 incorrectly allowed the assistant to enter a complete reception draft for a future-day appointment and labelled every 409 as stale data. The frontend now shows an explicit clinic-day/lifecycle/elapsed-slot blocker before the verification form, labels S17 actions accordingly, repeats eligibility before submission, treats only MSG111 as the reload-required version conflict and brings failed-submission feedback into view. The backend same-day restriction remains intact.

Validation: five new `reception-eligibility.spec.ts` cases passed (same-day early arrival, future date, UTC versus clinic-midnight boundary, elapsed/past/cancelled slots and business versus stale-version errors); TypeScript/Vite production build passed. Using the connected Browser, the normal synthetic future appointment displayed the explicit 26/10 versus 25/10 warning. In a separate synthetic schema, API 5082 and web 8083, Browser check-in of the same-day fixture appointment `a01f62c1-2c72-445b-bae6-a7b78da9d34d` succeeded and opened Visit `1f3e5d21-0a84-47b7-8085-07d0b198f869` with Appointment CheckedIn, Visit Chờ khám, arrival before the reserved slot and a CheckIn history row. This browser proof used real HTTP and PostgreSQL. Artifacts are local under `.artifacts/reception-check-in-fix/` (`future-rejection.json`, `future-appointment.png`, `fixture.json`, `checked-in.png`). Normal API 5080 was not stopped or switched to another schema during this follow-up; no normal appointment time or working hours were edited to manufacture eligibility. The existing user's unconnected tab was not operated or reloaded.
