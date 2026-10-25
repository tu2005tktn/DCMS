# Patient profile appointment integration

Contract: [S06 appointment tab](../specs/PATIENT-APPOINTMENTS-TAB.md).

Extend IAppointmentQueryService with a patient-scoped read and expose it under the patient read policy. Reuse staff appointment list DTOs and formatting, filtering PatientId and staff membership before pagination. No schema change or booking inference is required.

Replace only the S06 appointments placeholder with a lazy-loaded table and links to the existing staff detail route. Verify isolated PostgreSQL API filtering (patient, branch, dentist, scope, paging, denied access), browser tab loading/empty/error/detail navigation, and build both applications. Keep real API/database evidence distinct from mocked browser checks.

Validation on 19 October 2026: .NET restore/build and web build passed. AppointmentQueryApiTests passed 5/5 against isolated PostgreSQL, including exact PatientId matching, unlinked/same-name exclusion, branch/dentist restrictions, archived history, time scopes, pagination, missing/invalid/unauthenticated/public requests and detail read-back. Patient appointment Playwright checks passed 2/2 using mocked API responses (lazy load, UTC+7 time, filters, empty/error/retry and exact detail navigation). A real local dentist session received HTTP 200 from the new patient appointments endpoint with a valid empty result. API/database health passed after restarting the API.
