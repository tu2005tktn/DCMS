# II. Implementation & Schedule

## 1. Test Data

Rules: unit and integration tests are self-contained (they create their own data in a throw-away schema and never use data left by another test or a sequence-dependent ID). System tests run on the demo seed; every record they need beyond the seed is created at run time with a unique suffix.

### 1.1 Minimum Test Account

The password of all demo accounts is the development seed password (`DEV_SEED_PASSWORD` in the local `.env`); it is not written in this document.

| Username | Role | Password | Account Status | Purpose |
|---|---|---|---|---|
| admin.demo | Admin | [test password] | ACTIVE | Account administration, access groups and their history |
| owner.demo | Clinic Owner | [test password] | ACTIVE | Working schedule, booking settings, catalogs, payments, all-branch data |
| dentist.demo | Dentist | [test password] | ACTIVE | Examination, prescription, treatment plan and work done |
| assistant.demo | Clinic Assistant (branch A) | [test password] | ACTIVE | Reception, patients, appointments; read-only examination; branch isolation |
| assistant.b.demo | Clinic Assistant (branch B) | [test password] | ACTIVE | Branch B data; checks that branch A data is not visible |
| dentist.b.demo | Dentist (branch B) | [test password] | ACTIVE | Branch B schedule and examinations |
| dual.demo | Dentist and Clinic Assistant | [test password] | ACTIVE | Combined permissions of two roles |
| guest.demo | Guest (public) | [test password] | ACTIVE | Public account; cannot sign in through the staff UI |
| Customer (created at run time) | Customer | Generated per run | ACTIVE | Online booking, own appointments, family members, isolation between customers |

### 1.2 Minimum Domain Data

| Entity | Count | Required State | Purpose |
|---|---|---|---|
| Branch | 2 | Branch A (S09 - Chi nhánh thử nghiệm) and branch B, both active | Branch isolation, branch-based schedules and appointments |
| Dentist with working schedule | 2 | Dentist demo (branch A) and dentist of branch B, each with a published weekly schedule | Free slots for booking, schedule preview and overlap tests |
| Service | ≥ 1 per branch | Active, with duration and price | Booking wizard, reception walk-in, treatment plan items |
| Patient record | ≥ 3 | One with a confirmed future appointment, one with an examination visit, one with a treatment dossier | Search, edit, check-in, examination and treatment cases |
| Appointment | ≥ 2 | Confirmed, in the future, in branch A and in branch B | List by day, edit, cancel, concurrent-edit conflict, branch isolation |
| Reception visit | ≥ 1 | Checked in, waiting for the dentist | Start examination, assistant read-only access |
| Treatment dossier | ≥ 1 | Open, with one plan item and one work done | Plan, payment, close and reopen cases |
| Medicine | ≥ 1 | Active in the medicine catalog | Prescription cases |
| Customer account | ≥ 1 | Registered, with at least one family member | Online booking, own appointments, isolation between customers |

## 2. Test Cases

The detailed test cases are kept in the files below, one per level, using the report templates.

| Level | Test case file | Content |
|---|---|---|
| Unit Test | `backend/tests/DCMS.UnitTests` (see its `README.md`) | Create-method validation cases of the application services, one test class per service |
| Integration Test | `docs/test/DCMS_Integration_Test_Report_v1.0.xlsx`; code in `backend/tests/DCMS.IntegrationTests` | Create-endpoint cases over HTTP on a throw-away PostgreSQL schema |
| System Test | `docs/test/Report-5.3_System Test_FRs.xlsx` (Google Sheet copy) | 67 end-to-end cases in 8 modules (SYS1 to SYS8) with Round 1 results |
| Acceptance Test | Not prepared yet | To be agreed with the clinic representative |
