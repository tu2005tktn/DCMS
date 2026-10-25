# S17 S18 Reception and Visit contract

Baseline: DCMS screen and use-case specifications v2.3 supplied 25 October 2026, S17/S18, UC-05/07/24/38, GBR-016/034/081/137/149/153/156/157, AT12/25/27/31/42/46/47/48; DCMS-92/93. The request and Epic decisions override old Waiting labels: the only waiting status is `Queued`, displayed as `Chờ khám`. This specification precedes implementation.

## Scope and invariants

- ClinicAssistant checks in and reorders within active branch membership. Assigned Dentist (including effective Dentist of ClinicOwner) reads only own queue. Admin/public accounts have no reception access. Server recomputes scope for every request.
- Date boundaries use `Asia/Ho_Chi_Minh`. Reception lists appointments not received separately from Visit queue, with separate appointment and visit status filters, dentist, and accent-insensitive name/appointment code/patient code search (0–100 characters).
- Explicitly compare booker, expected attendee and actual Patient; shared contact never selects or merges identity. Patient records are global in the existing master schema; eligibility is an active Patient, authorized appointment branch, and source ownership in that branch. If an appointment already has another Patient, reject MSG43; otherwise a corrected attendee may be linked with a mandatory identity-verification note. Preserve the original booking identity for audit. Reuse UC-38 duplicate-review form; unknown fields remain null and no app link is granted.
- An eligible appointment is Confirmed, scheduled on the current clinic day, with an intact active resource reservation. Check-in does not change its time or release staff/chair. Walk-in opens the existing S11 booking drawer and uses availability plus POST /api/appointments before reception. Actual arrival may precede the slot and must survive this navigation. Check-in requires neither payment nor an app account.
- `actualArrivalAt` defaults to server now. Explicit historical arrival requires `arrivalReason`; store UTC actual arrival and separate `enteredAt`, expose clinic timezone. Future arrival or another clinic day is invalid. UI normal mode omits actualArrivalAt so time spent verifying does not fabricate retrospective input.
- Check-in atomically locks shared scheduling resources (same advisory lock as booking), appointment, and patient; validates status/version/ownership, links Patient, sets Appointment `CheckedIn`, creates exactly one Visit `Queued`. Queue row is a projection of Visit, not a second lifecycle table. Unique AppointmentId and command receipt constraints prevent double reception. Retry returns current authoritative Visit. Reusing requestKey with another payload conflicts. A different command on an already received appointment returns the same visit only for the same patient; it cannot replace identity/purpose.
- Purposes: `InitialExamination`, `TreatmentContinuation`, `PostoperativeOrWarrantyReview`, `PeriodicOrNewProblem`. Additional concern 0–1000 chars. Continuation accepts dossier/item/step; review accepts prior item/activity and `reviewReason` Postoperative/Warranty. Every supplied source must resolve to the same patient/branch and a consistent hierarchy, otherwise MSG237. Missing optional sources are explicitly pending dentist review. Holds are visible and retained. Purpose never grants priority, clears a hold, creates clinical work or changes fees.
- This checkout contains no clinical dossier/plan implementation. `IReceptionSourceReader` is the replaceable integration boundary for DCMS-94/95/96/99. A compact `ClinicalSourceReference` read model carries authoritative source IDs, kind, parent, patient/branch, label and blocker, populated by those modules in their clinical transaction. Reception exposes read-only selection, and never creates fictional dossiers/items/activities. No public source-write API is introduced. Until sources are published, missing sources display MSG237; supplied unresolved IDs are rejected. Source ownership is validated server-side and current blockers are re-read on refresh.
- Queue ordering defaults to reserved appointment time, then actual arrival. Reorder requires assistant permission, nonblank reason, expected Visit version and requestKey; moves only a Queued visit before another Queued visit in the same branch/date. It records actor, timestamp, before/after order, reason and optional deciding dentist. Clinical priority requires authenticated deciding dentist via a separate dentist-only command; an assistant cannot impersonate that decision by submitting a dentist ID. No appointment/reservation times change.
- No-show eligibility is a read-only indicator (Confirmed and start +15 minutes elapsed). This task does not implement no-show/reminder jobs. Confirmation of intent remains separate and never creates Visit.

## HTTP and handoff

All endpoints require authenticated staff and return shared SystemMessage errors (401 MSG07, 403 MSG06, 404 MSG113, validation MSG02/03/04/42/43/47/202/237, 409 MSG111). All timestamps are ISO-8601; IDs for Visit/source/appointment are UUID and Patient/branch/employee IDs are int. `expectedVersion` is an int and requestKey is a non-empty UUID. Receipts are scoped by actor and requestKey across reception commands.

`source` preserves canonical IDs; `sourceLinks` additionally returns the resolved selected sources and ancestors as `{id, kind, parentId, label, blocker, version}` for readable summaries and downstream navigation. This is a projection of the same source records. For invalid appointment day/elapsed capacity use MSG42; NoShow uses MSG47, with wording from the supplied message catalog. All explicit earlier arrival times require a reason, without an undocumented grace period. Reorder history stores the entire affected before/after queue order.

| Method | Path | Purpose |
| --- | --- | --- |
| GET | /api/reception/options | Authorized branches and dentists plus permissions |
| GET | /api/reception?branchId=1&date=2026-10-25&dentistId=2&appointmentStatus=Confirmed&visitStatus=Queued&keyword=BN | S17 notArrived + queue, serverNow/timeZone |
| GET | /api/reception/appointments/{id-or-code} | S18 original booker/expected attendee and existing Visit |
| GET | /api/reception/sources?branchId=1&patientId=4 | Authorized source choices with hierarchy and current blockers |
| GET | /api/reception/visits/{visitId} | Authoritative Visit/queue/source handoff |
| POST | /api/reception/check-ins | Atomic check-in |
| POST | /api/reception/visits/{visitId}/reorder | Operational order with audit |
| POST | /api/reception/visits/{visitId}/priority | Dentist-authored clinical priority decision |
| GET | /api/reception/visits/{visitId}/history | Reception/reorder/priority history |

Example check-in request (omitted arrival uses server now):

```json
{"appointment":"AP-...","branchId":1,"patientId":4,"identityVerified":true,"identityNote":"Compared name and birth date with attendee","purpose":"InitialExamination","source":{"dossierId":null,"itemId":null,"stepId":null,"activityId":null},"reviewReason":null,"additionalConcern":null,"note":null,"actualArrivalAt":null,"arrivalReason":null,"expectedVersion":1,"requestKey":"d09a8e69-d539-4a5e-bd86-9a785053b854"}
```

Response shape:

```json
{"appointmentId":"00000000-0000-0000-0000-000000000001","appointmentCode":"AP-...","appointmentStatus":"CheckedIn","appointmentVersion":2,"visitId":"00000000-0000-0000-0000-000000000002","visitCode":"VS-...","visitStatus":"Queued","version":1,"branchId":1,"patientId":4,"patientCode":"BN000004","patientName":"Example","dentistId":2,"dentistName":"Example dentist","appointmentAt":"2026-10-25T02:00:00Z","actualArrivalAt":"2026-10-25T01:45:00Z","enteredAt":"2026-10-25T01:45:00Z","purpose":"InitialExamination","source":{"dossierId":null,"itemId":null,"stepId":null,"activityId":null},"sourceReviewPending":false,"blockers":[],"queue":{"position":1,"order":1024,"waitingMinutes":0},"clinicalPriority":null,"priorityDentistId":null}
```

Reorder body: `{"beforeVisitId":"UUID or null for last","reason":"Operational reason","expectedVersion":1,"requestKey":"UUID"}`. Dentist priority body: `{"priority":"Urgent or Routine","reason":"Clinical decision","expectedVersion":1,"requestKey":"UUID"}`. History carries actorAccountId, decidingDentistId, action, reason, occurredAt and before/after JSON.

UI preserves form fields on errors, distinguishes loading/empty/validation/network/session/permission/conflict, and explicitly reloads authoritative state after a conflict before retrying. Submission retains requestKey for identical retry, generating a new key only when payload changes. Repeat check-in opens existing Visit. S17 refreshes wait time and current blockers. No clinical editor is claimed in the read-only Visit handoff.

## Reception eligibility feedback correction on 25 October 2026

The normal local database currently has upcoming appointments starting on 26 October, and no appointments for 25 October. Receiving an appointment for another clinic day correctly returns HTTP 409/MSG42. This is a business rejection, not necessarily a stale version. Do not weaken the same-day server rule or silently move an appointment to make a demo succeed.

S17 must label ineligible Confirmed rows as viewing reception conditions rather than offering an immediate check-in action. S18 checks clinic date (`Asia/Ho_Chi_Minh`), appointment lifecycle and elapsed slot before rendering the Patient verification form; a future/past-day, expired or invalid-status appointment shows its reason, planned clinic date and the current clinic date, with links back to today's reception and the appointment. Existing Visit still opens normally even when viewing historical reception. Eligibility is checked again before submission and while the screen remains open. Client checks are guidance; server transaction validation remains authoritative.

Only 409/MSG111 requires the stale-version reload-and-review state. MSG42/47, MSG43, MSG202 and resource conflicts retain the server reason and use a relevant business-error label instead of claiming that data changed. On a failed submission, scroll/focus the visible error summary so an assistant at the bottom of a long S18 form can see why it failed; retain the selected Patient and draft. Browser/system tests run on a separate API/web port when the user is testing on 5080/8081; do not switch the active user's server to a different schema or modify their appointment times or working hours for testing.

## Verification required

Real PostgreSQL/API: concurrency with same/different actors and keys, replay/fingerprint conflict, stale version, branch/role/self scope, wrong day/status, archived and shared-phone Patients, UC-38 no app access, source ownership/hierarchy/hold/missing review, all purposes, retrospective arrival, no reservation release, reorder and dentist decision audit. Three physical chair fixture with independent staff proves chair/dentist/assistant collisions and valid simultaneous reservations through existing booking. Browser: real API S17/S18, identity comparison/create, duplicate click/reopen, error field retention, walk-in round trip and queue/history. Restore/build .NET 10, npm install/build, PostgreSQL health. Record exact evidence and remaining cross-task integration gaps in docs/implementation/S17-S18-RECEPTION.md.
