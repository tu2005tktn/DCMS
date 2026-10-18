# Shared password policy change — 18 October 2026

Contract: [shared password policy](../specs/SHARED-PASSWORD-POLICY.md).

The API baseline and Development settings require 8 characters and disable the four character-class requirements. Registration, staff creation, recovery and seeding reuse Identity. Web/Mobile registration and reset already consume `/api/auth/policy`; the Mobile sign-in hint must describe entering the existing credential rather than repeat obsolete creation rules.

Existing database credentials remain hash-based. Updating the baseline or `.env` does not synchronize passwords of existing demo accounts. This change requires no database migration or account reset.

Validation covers the advertised policy, public registration boundaries, staff creation and recovery using an eight-digit password, plus backend/Web builds and Mobile typecheck.

## Validation performed

- Backend restore/build succeeded; the existing NU1902/NU1903 warnings for `System.Security.Cryptography.Pkcs` 9.0.0 remain.
- Web `npm install` and production build succeeded; the existing large-bundle warning remains. Mobile typecheck succeeded.
- Registration, recovery and account integration suites: 56 passed, 1 failed. All ten added/extended policy cases passed, including numeric staff creation and reset.
- The unrelated `RepeatedAndConcurrentRequestsCreateOneAccount` test still fails because its second phone reuses the first account's email while `EmailIndex` is unique: both concurrent requests return 409. Re-running that single test with the old 12-character/composition settings produces the same failure. No email-uniqueness behavior was changed in this task.
- Browser against the running API/PostgreSQL: public registration and customer sign-in using eight digits succeeded; staff creation and staff web sign-in using eight digits succeeded.
- Running `/api/auth/policy` advertises length 8, maximum 256 and all composition flags false. `/health` reports API/database healthy.

Integration fixtures use their own disposable schemas. The running user test API continues to use the previously created `dcms_manual_test_20261018` schema. This policy change neither resets existing credentials nor changes the API database selection.

## Local demo credential maintenance

On explicit operator request, the existing demo accounts in the original `public` schema may be reset to `DEV_SEED_PASSWORD` from ignored `.env`. Scope is the six staff/public demo accounts plus `patient.demo` and `patient2.demo`, when present. Use Identity's reset-token/password APIs, record `DevelopmentPasswordReset` without secrets, and revoke existing sessions/refresh tokens in the same transaction. Preserve roles, lock/active state and patient links. This is a local maintenance operation; ordinary seed continues to skip existing accounts.

After the requested reset, run the API against the original connection from `.env` and verify demo sign-in through API and the staff web page. The earlier isolated manual schema is retained as test data and is no longer selected by that API process.

Executed 18 October 2026: all eight existing named demo accounts were reset using Identity in one transaction in `public`; sessions and active refresh tokens were revoked and eight audit entries recorded. The restarted API now selects the original database/public schema. All eight demo sign-ins returned HTTP 200; browser sign-in as `admin.demo` reached the staff home with the Admin menu. `/health` reported API/database healthy. The running password is taken from ignored `.env`; no credential is stored in this document.
