# 1. Scope of Testing

Project: Dental Clinic Management System (DCMS) — web application, customer portal, API and mobile app.

## 1.1 Test scope

### 1.1.1 Features and functions to be tested

| No | Module | Features / functions under test |
|---|---|---|
| 1 | Account, sign-in and access control | Staff sign-in/sign-out, protected routes, role-based menu and screen permissions, customer registration, staff account administration (create account, sign in with the new account). |
| 2 | Customer online booking | Booking wizard for self and family members, form validation, slot conflict, own appointment list, reschedule and cancel requests, isolation between customers. |
| 3 | Working schedule and staff appointments | Weekly working schedule (publish, slot preview, overlap validation, discard unsaved changes), appointment list by day, edit, cancel, no-show, concurrent-edit conflict. |
| 4 | Patient records and reception | Create, search and edit patients, duplicate review, validation, check-in, missing-record handling, walk-in registration, branch isolation. |
| 5 | Examination and prescription | Start and finish an examination, tooth chart findings, diagnosis, investigation orders and results, prescription (create, validation), assistant read-only access. |
| 6 | Treatment dossier and payment | Treatment plan (items, validation, finalize), work done, lab orders, deposit and payment by the owner, close and reopen a dossier. |
| 7 | System settings and catalogs | Medicine, service and booking settings, access-group assignment and its history, permission-based visibility of settings. |
| 8 | Statistics, customer care and support | Statistics screens, customer care tasks, customer support requests. |

The 67 system test cases in `Report-5.3_System Test_FRs.xlsx` cover these eight modules.

### 1.1.2 Features and functions not tested

- Account recovery by e-mail verification (needs a real, verified mailbox).
- Printing a prescription and issuing a corrected prescription version (the prescription page is not reachable in the current build).
- Uploading result files for investigations, and material stock reduction after work done (only the screens were exercised).
- Payment-gateway and e-mail/SMS delivery with real third-party services (not available in the test environment).
- The AI service and the mobile app: covered only indirectly through the shared API; no dedicated test cases in this plan.
- Browsers other than Chromium, and native/device-specific behaviour.

### 1.1.3 Non-functional requirements

Non-functional requirements are recorded in `docs/testing/SYSTEM-NFR-2026-11-11.vi.md`. In this plan:

| Requirement | Treatment |
|---|---|
| Security / authorization | Tested through role- and branch-based cases (sign-in, protected routes, permission-based menus, branch and customer isolation, assistant read-only access). |
| Data validation and integrity | Tested through validation, duplicate and concurrency-conflict cases. |
| Usability / responsive layout | Checked on the customer portal by UI specs; no formal usability test. |
| Performance | Tested by the response-time and upload-size cases of the non-functional requirements document (dashboard load, 5 MB upload, 20 concurrent users). |
| Stress, backup and recovery | Not tested in this plan. |

## 1.2 Test levels

| Level | In charge | Inputs / time | Focus | Acceptance criteria |
|---|---|---|---|---|
| Unit test | Developers (owner of the module) | Source of the application services; xUnit project `backend/tests/DCMS.UnitTests`; runs on every change before merge. | Validation of the create methods of the services (staff account, patient, family member, customer registration, appointment, treatment dossier, item plan, lab work type, material catalog): each input field is checked and the exact error message is asserted. | All unit tests pass; each invalid input is rejected with the expected message. |
| Integration test | Developers (owner of the module) | Real API over HTTP (`WebApplicationFactory`) on a throw-away PostgreSQL schema; `backend/tests/DCMS.IntegrationTests` and web integration specs in `web/tests`; run before each release candidate. | Create endpoints and the flows that cross API, database and permissions: accounts, registration, patients, family members, dossiers, item plans, catalogs, appointments, finance and examination. | All tests pass; correct status codes and stored data; no data outside the test schema is touched. |
| System test | Test member(s) of the project team | System test cases (`Report-5.3_System Test_FRs.xlsx`); web (`http://localhost:5173`) and API (`http://localhost:5080`) with the development database and demo seed; Round 1 executed on 16/11/2026 with Playwright UI specs in `web/tests-system`. | End-to-end workflows through the web UI per role (admin, owner, dentist, assistant, customer) across the eight modules in 1.1.1. | Every case has a result (Passed, Failed, Pending); no open critical or high defect; test coverage and successful coverage reported in the Test Statistics sheet. Round 1: 67 cases, 64 Passed, 0 Failed, 3 Pending. |

## 1.3 Constraints and assumptions

Constraints

- System tests run on the shared development database with the demo seed, not on a clean production-like environment; they leave test data behind (patients, appointments, dossiers, services).
- The three pending cases (SYS3-08, SYS4-07, SYS8-04) need a free appointment slot or a past appointment on the day of the run, so they depend on the date and time of execution.
- The guest customer account cannot sign in through the UI; customer cases use customers registered at run time.
- Unit tests cover the create methods only; success paths and uniqueness conflicts belong to the integration tests.
- Third-party services (e-mail, SMS, payment) are not available, so flows that depend on them cannot be executed end to end.
- The statistics figures are entered by hand.

Assumptions

- The API, web application and PostgreSQL database are running and the demo accounts (admin, owner, dentist, assistant, assistant of branch B) are active.
- The demo password is supplied through the environment and never stored in test documents.
- The scope and expected results are taken from the screens and features of the current build; a change of the UI or rules requires the test cases to be updated.
- Tests run in Chromium with one worker, so cases do not interfere with each other.
