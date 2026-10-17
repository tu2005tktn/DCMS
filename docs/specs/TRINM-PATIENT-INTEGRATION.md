# TriNM S05/S06 integration — 16 October 2026

The S05/S06 behavior and API contracts remain those in [UC-08/09/38](../implementation/UC08-09-38-S05-S06.md). This integration repairs duplicated or incomplete merge fragments that prevent the current main branch from building.

- Authentication must return one permission list: Admin retains accounts.manage; Clinic Assistant retains patient-links.verify and gains patients.read/patients.manage; Dentist and inherited Clinic Owner gain patients.read only. Public accounts gain no staff patient permissions. Verified self-link detection remains unchanged.
- The web workspace must render one navigation menu, retain all existing entries, include Patients only with patients.read, and keep the Patients menu selected on profile routes.
- S07 must retain one shared S01 session hook/token and one auth-failure handler. Remove duplicated merge fragments; HTTP 401 clears the shared session and cached link/record data, HTTP 403 preserves the session, and explicit sign-out returns to S01. Link API operations require the authenticated session token. This does not change Expo dependencies.
- The EF snapshot must retain both patient master/audit and appointment/scheduling mappings, including the restrictive required patient-audit foreign key. Repairing syntax must not add or remove database objects or create a new migration.
- Previously migrated development databases must accept the additive patient migration; fresh isolated test schemas must also migrate, and the snapshot must match the runtime model.

Verification covers server permissions, create/read/search (accent-insensitive name, phone, code and date), duplicate review, immutable unique code, validation, optimistic concurrency, archive/restore, and audit. Administrative edits must preserve patient links. Browser checks exercise actual API-backed create/search/profile/edit/archive/restore, duplicate warning and read-only dentist access. Only synthetic test data may be created.

Unimplemented clinical tabs, visits, branch scoping and check-in evidence remain explicitly deferred as documented in the module contract. Full backend regression failures outside this feature must be reported separately.
