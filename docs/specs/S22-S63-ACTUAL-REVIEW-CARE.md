# S22 actual treatment, S63 review and S25/S60 care backend

S22 tích hợp S43 hiện áp dụng [TREATMENT-PAYMENT-V25.md](TREATMENT-PAYMENT-V25.md): Visit/consent dùng nguồn thật; Separate actual ghi outbox `clinical.fee.requested` cùng transaction với feeStatus Pending khi chưa có ledger. Không còn trả 503 chỉ vì thiếu dispatcher tài chính. Lab vẫn fail-closed khi chưa có xác nhận authoritative. Hoạt động Free/Included không sinh yêu cầu phí mới; Separate giá 0 yêu cầu chọn lại căn cứ Free rõ ràng (MSG244).

Source: DCMS Screen Specifications v2.3 and DCMS Use Cases Business Rules Messages v2.3, revised 24 October 2026, with the Vinh assignment updated 25 October 2026. Screens S22/S63 belong to the staff Web. Trí owns the S25/S60 UI; this module owns its backend. Relevant workflows: UC-10/14/17/18/25/39/40/43/53, GBR-154/155/159, AT43/44/45/50 and MSG230/237/240/242/244/246.

## Record boundaries

An Appointment is a reservation, a Visit is one clinical attendance, a dossier is a longitudinal course, a plan item/step is intended work, and an activity is confirmed work actually performed. Review, actual posting, and care contact are separate commands and separate database commits. Saving a review or navigating to S22 cannot create an actual, charge, stock movement, completed step or Appointment. A care task marked Done cannot complete a clinical instruction, Visit or dossier.

S22 requires an authenticated Dentist with branch authority, an InProgress Visit for the same Patient and branch, and either an Agreed current item/step or verified direct-treatment agreement. Production must reject posting if a source Visit or required consent cannot be verified. A step with a hold, unmet predecessor, unaccepted required lab work or mismatched consent cannot be posted. Partial actual does not unlock successors. Posting never completes the whole Visit/dossier or collects money.

S63 requires an InProgress current review Visit and a completed source activity/item for the same Patient; the source dossier may be closed. Review history is immutable. Coverage PendingAssessment/Covered/NotCovered and outcome AdviceOnly/FurtherAssessment/CorrectiveCareNeeded/NewProblem are separate from activity, step, Visit and finance state. A covered component, exclusions, policy/version and basis must be explicit. A new problem starts new work; review does not rewrite the source.

## Contract to finalize with source owners

All JSON is camelCase. Commands carry a nonempty UUID `requestKey`; changes also carry `expectedVersion` of the affected aggregate and source tokens. Same key plus same normalized payload returns the saved IDs and result; a different payload returns 409 MSG111. Server supplies actor, roles, Patient and branch from verified sources. Response reads ID, version and state after commit. These routes are proposed until implemented; a route name alone does not assert that its source adapter exists.

| Route | Purpose |
| --- | --- |
| `GET /api/clinical-services?branchId=...` | Active staff services in the authorized branch; direct treatment does not use public-bookable filtering |
| `GET /api/visits/{visitId}/treatment-context` | Visit, agreed scope, readiness, consent, remaining work and prior actuals |
| `POST /api/visits/{visitId}/activities/drafts`, `PUT /api/activities/{id}/draft` | Editable actual draft; no posted work or charge |
| `POST /api/activities/{id}/post` | Dentist-confirmed activity; charge source is unique |
| `POST /api/activities/{id}/amend` | Pending finance adjustment contract; do not modify a Posted activity in place |
| `POST /api/visits/{visitId}/care-notes` | Actual response/care note without billable quantity, completion or successor |
| `GET /api/visits/{visitId}/treatment-context`, `GET/POST /api/visits/{visitId}/reviews`, `GET /api/reviews/{id}` | S63 source from the shared clinical context, then assessment/history |
| `GET/POST /api/care-tasks`, `GET/PATCH /api/care-tasks/{id}` | Authorized list/calendar/detail/create/update |
| `POST /api/care-tasks/{id}/contact-attempts`, `POST /api/care-tasks/{id}/escalations`, `POST /api/care-escalations/{id}/acknowledge`, `/resolve` | Actual contact and clinician handoff history |

Activity input: `{visitId, dossierId?, itemId?, stepId?, planVersion?, serviceId, region?, unit, quantity, performedAt, elapsedMinutes?, outcome, nextSteps?, chargingBasis, chargingBasisEvidence?, priceSnapshotVnd?, reviewId?, expectedVisitVersion, expectedStepVersion?, requestKey}`. Quantity is positive with unit-specific precision, clinical outcome at most 4000 characters, next steps at most 2000. Performer comes from the session or verified assignment. `Free` requires an explicit basis, `Included` requires an agreed item/package source, and `Separate` requires the finance writer. Review-related Free also needs verified coverage. A read-only preview separates gross/reduction/tax/net from posted charge and received payment. Included package work and extra sessions do not repost the package fee.

Review input: `{sourceActivityId?, sourceItemId?, complaint, findings, coverage, outcome, coveredComponent?, exclusions?, policy?, policyVersion?, basis?, nextAction?, expectedVisitVersion, expectedSourceVersion, requestKey}`. Complaint and findings are each at most 4000 characters. `CorrectiveCareNeeded` may open S22 with review/source/basis prefilled, but only a later explicit Post records work. S24 instruction and S60 task remain their own commands.

Care task includes context/source, branch, owner, due time, task type/content, version and Open/InProgress/Done/Cancelled. Contact attempt stores channel/result/next action, actor/time and request key. `Rebooked` requires an existing committed Appointment. Escalation stores target clinician, reason/time and Pending/Acknowledged/Resolved; only an authorized clinician acknowledges/resolves. No contact status proves a notification was sent or a clinical instruction completed. Due/blocker/NoShow ingestion uses stable episode keys and idempotent receipts.

### Mock payloads for parallel UI work

```json
POST /api/care-tasks
{"patientId":123,"branchId":1,"instructionId":null,"appointmentId":null,"reviewId":"33333333-3333-4333-8333-333333333333","type":"FollowUp","content":"Gọi hỏi tình trạng sau điều trị","ownerAccountId":null,"dueAt":"2026-10-26T03:00:00Z","requestKey":"55555555-5555-4555-8555-555555555555"}

201
{"id":"66666666-6666-4666-8666-666666666666","patientId":123,"branchId":1,"instructionId":null,"appointmentId":null,"reviewId":"33333333-3333-4333-8333-333333333333","type":"FollowUp","content":"Gọi hỏi tình trạng sau điều trị","ownerAccountId":null,"dueAt":"2026-10-26T03:00:00Z","status":"Open","version":1,"allowedActions":["update","recordContact","escalate"]}
```

```json
POST /api/care-tasks/66666666-6666-4666-8666-666666666666/contact-attempts
{"channel":"Phone","result":"NoAnswer","nextAction":"Gọi lại ngày mai","rebookedAppointmentId":null,"expectedTaskVersion":1,"requestKey":"77777777-7777-4777-8777-777777777777"}

201
{"id":"88888888-8888-4888-8888-888888888888","taskId":"66666666-6666-4666-8666-666666666666","channel":"Phone","result":"NoAnswer","nextAction":"Gọi lại ngày mai","rebookedAppointmentId":null,"actorId":9,"occurredAt":"2026-10-25T10:00:00Z"}
```

These are synthetic IDs and examples, not records in the project database. A repeated request key and identical payload returns the original result; changed payload returns 409 `MSG111`. The UI must reload the task version after a successful contact before sending another command.

## Source integration and transaction rules

The Visit owner must supply a commit-safe verification of InProgress, Patient, branch and version. The S20 owner supplies consent verification bound to item/step/procedure and plan version. DCMS-97 consumes posted activity evidence for step completion. DCMS-100 supplies patient/step/spec-version-specific lab Accepted. DCMS-101/108 owns one charge-source writer or transactional outbox consumer; S22 and S37 may not both post for the same source. DCMS-103 owns separate actual medicine/material consumption, with no batch selector in S22. Source adapters that cannot verify at commit time fail closed; test fixtures are never treated as production evidence.

Post actual writes the activity, receipt, progress effect and eligible successor event in one transaction. Charge must either commit with the agreed finance contract or be explicitly pending with no success claim for charge; a case requiring atomic charge but lacking that contract is blocked. A posted amendment creates a new version and an authorized charge adjustment rather than silently changing paid amounts. Review save has its own transaction. Contact and escalation commands each have their own receipts and history.

## Web interaction and errors

S22: current Visit/Patient -> agreed work and readiness -> actual and charging basis -> review -> dentist confirms Post. It shows planned, reserved and elapsed actual time separately and never copies every plan item into performed services. S63: source/complaint -> findings -> coverage/basis when relevant -> outcome/next action -> Save review. Staff Web retains input after validation, 401/403, MSG111 stale, MSG230 blocker, MSG240 consent, MSG244 missing basis or MSG144 network/service failure. Loading, empty and source-unavailable states are distinct. The S25/S60 UI receives `allowedActions` from the server; staff handling notes are not exposed by own-record APIs.

## Verification

Use isolated PostgreSQL and synthetic fixtures, not the shared development database. Test same-Visit simple and multi-session work, Partial blocking successors, fail-closed Visit/consent/lab, retry and competing posts, separate review and actual commits, component-level warranty basis, care task/contact/escalation permissions and deduplication. Check that no Save review, care note, contact or navigation creates actual, charge, payment, stock movement or Appointment. Re-run end-to-end cases with the actual Visit, finance, lab and stock adapters before reporting those cases complete.

## Implementation and handoff status, 25 October 2026

- The API routes above are implemented except `POST /api/activities/{id}/amend`. Posted activities are immutable until DCMS-101 supplies an adjustment contract; the UI offers no silent correction.
- S22 and S63 Web routes and forms call the API. They require a real `visitId` from DCMS-95. This checkout has no Visit store or Visit screen, so its production Visit port returns unavailable. S20 consent verification and DCMS-101 charge writing also fail closed. A standalone Web test against the current shared API cannot post an actual or save a review until those ports are connected.
- A successful Post writes one `clinical.activity.posted` event in the same transaction, keyed `clinical-activity-posted:{activityId}`. The event carries activity/version, Visit, Patient, branch, plan and charging basis identifiers; it is a source handoff, not a claim that a charge or stock movement was posted. Event dispatch stays with the existing outbox owner.
- The care ingestion worker reads treatment due/blocker events and `appointment.no_show` outbox events. It creates a staff care task only for an identified Patient, using an episode key for deduplication; it does not send a message or mark the instruction complete.
- DCMS-100 lab readiness remains fail closed for steps requiring lab acceptance. Medicine/material consumption belongs to DCMS-103 and is not recorded by the S22 activity form.
- S25/S60 list/detail/create/update/contact/escalation API is implemented for the UI owner. `CareTaskDto.allowedActions` and `CareTaskDetail.contacts/escalations` are the handoff; there is no S25/S60 Web page in this task. Internal staff notes are not added to patient own-record responses.
- Isolated PostgreSQL tests cover review save and retry without posting, explicit Free corrective activity post and retry, care note retry, care task/contact retry and escalation authorization. They do not establish the full same-Visit/ledger/lab/stock end-to-end cases. No migration was applied to the shared development database during this implementation.
