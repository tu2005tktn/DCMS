# S03 · Account Recovery (UC-02) — Acceptance Evidence

Screen: **S03 Account Recovery**, owner **Tong Nguyen Tuan**
Use case: **UC-02 Recover Account Access**
Date: 12/10/2026

---

## 1. What was delivered

| Layer | Status |
| --- | --- |
| Backend — application service reusable by S03 and S04 | Done |
| Backend — endpoints, audit, EF migration, config options | Done |
| Web (React + TypeScript + Ant Design) — 4 steps | Done |
| Mobile (React Native + Expo) — 4 steps | Done |
| Unit tests (no database required) | **30 passing** |
| Integration tests (require PostgreSQL) | Written, **not executed in this environment** |
| Web E2E (Playwright) | Written, **not executed in this environment** |

## 2. What was actually run

| Check | Command | Result |
| --- | --- | --- |
| Backend build | `dotnet build backend/DCMS.sln` | Success, 0 warnings, 0 errors |
| Backend unit tests | `dotnet test backend/tests/DCMS.UnitTests` | **30 passed**, 0 failed |
| Web type-check + build | `npm run build` (web) | Success |
| Mobile type-check | `npm run typecheck` (mobile) | Success |
| Backend integration tests | `dotnet test backend/tests/DCMS.IntegrationTests` | **Not run — no PostgreSQL/Docker on the authoring machine** |
| Web E2E | `npm run test:e2e` | **Not run — needs API, Vite, PostgreSQL and `E2E_DEMO_PASSWORD`** |
| Mobile app run | `npx expo start` | **Not run — needs a device/emulator** |

> The integration and E2E suites are complete and compile. They must be run on a machine with the
> development database available before this change can be called verified end to end.

## 3. Acceptance criteria → test mapping

| # | Criterion | Test(s) | Result |
| --- | --- | --- | --- |
| 1 | Valid recovery changes the password; the OLD password can no longer sign in | `RecoveryApiTests.C1_ValidRecoveryChangesPasswordAndOldPasswordNoLongerSignsIn` | Not executed |
| 1 | Same, on the real HTTP surface | web `recovery.spec.ts` — "request, verify, reset and landing on sign-in for an existing account" | Not executed |
| 2 | Wrong / expired / used / over-limit code rejected; no double reset | `RecoveryApiTests.C2_WrongExpiredUsedAndOverLimitProofsAreRejected` | Not executed |
| 2 | Same, over the attempt limit | `RecoveryApiTests.C2_OverTheAttemptLimitRefusesEvenTheCorrectProof` | Not executed |
| 2 | Concurrent double reset → exactly one success, one consumed token | `RecoveryApiTests.C2_ConcurrentDoubleResetProducesExactlyOneSuccess` | Not executed |
| 2 | Replay of a used grant is refused | `RecoveryApiTests.AdminAssistedVerificationRecordsTheAdminAsActor` (replay assertion) | Not executed |
| 2 | Client-side guard: mismatch, weak password, cooldown, service failure | web `recovery.spec.ts` — "validation, resend cooldown and service failure are handled" | Not executed |
| 3 | Existing and non-existing accounts get the same neutral response, incl. timing | `RecoveryApiTests.C3_ExistingAndMissingAccountsReturnTheSameNeutralResponse` | Not executed |
| 3 | Same wording on the screen | web `recovery.spec.ts` — "unknown account produces the same neutral request answer" | Not executed |
| 4 | Reset does not auto-unlock; locked account still needs Admin | `RecoveryApiTests.C4_ResetDoesNotUnlockALockedAccount` | Not executed |
| 5 | Old sessions revoked after reset | `RecoveryApiTests.C5_OldSessionsAreRevokedAfterReset` | Not executed |
| 6 | No Patient link created/verified, no role elevation | `RecoveryApiTests.C6_RecoveryDoesNotVerifyAPatientLinkOrRaisePermissions` | Not executed |
| 7 | No secret stored anywhere by the recovery process | `RecoveryApiTests.C7_NoSecretIsStoredAnywhereByTheRecoveryProcess` | Not executed |
| 7 | Development adapter never claims a real send, refuses outside Development | `DevelopmentContactDeliveryAdapterTests` (4 tests) | **Passing** |
| 7 | Development proof aid is absent outside Development, requires a session | `RecoveryApiTests.TheDevelopmentProofAidIsAbsentOutsideDevelopment`, `...RequiresASignedInSession` | Not executed |
| — | Audit records events without storing secrets | `RecoveryApiTests.AuditRecordsTheRecoveryEventsWithoutStoringSecrets` | Not executed |
| — | Admin path records the Admin as actor (GBR-028/081) | `RecoveryApiTests.AdminAssistedVerificationRecordsTheAdminAsActor` | Not executed |
| — | Admin path re-checks Admin permission (GBR-075) | `RecoveryApiTests.AdminAssistedRecoveryRequiresAdminPermission` | Not executed |
| — | Admin path keeps the shared cooldown | `RecoveryApiTests.AdminRecoveryKeepsTheSharedCooldown` | Not executed |
| — | Proof lifecycle, hashing, constant-time compare | `RecoveryProofTests` (9 tests) | **Passing** |
| — | Configurable policy defaults and bounds (GBR-082) | `RecoverySettingsTests` (5 tests) | **Passing** |
| — | Exact message wording (MSG02/04/06/12/13/108/109/144) | `RecoverySystemMessageTests` (8 tests) | **Passing** |
| — | Endpoint shape: never leaves a proof/grant/password in the URL | web `recovery.spec.ts` — "no proof, grant or password is written into the URL" | Not executed |
| — | Sign-in page links to recovery instead of claiming it is unavailable | web `recovery.spec.ts` — "sign-in page links to recovery…" | Not executed |

## 4. How to run the outstanding checks

```powershell
# one time
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
notepad .env                      # set POSTGRES_PASSWORD

& .\infrastructure\scripts\initialize-development.ps1
docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d
& .\infrastructure\scripts\import-development-env.ps1

# backend
& $dotnet test backend/tests/DCMS.IntegrationTests   # 20 tests

# web E2E (needs the API + Vite running and E2E_DEMO_PASSWORD from the ignored .env)
$env:E2E_DEMO_PASSWORD = '<password DevSeed:Password>'
npm --prefix web run test:e2e

# mobile
npm --prefix mobile install; npm --prefix mobile run typecheck; npx expo start
```

### Reading a verification code without a real mail provider

No production channel exists yet, so in **Development** the contact adapter keeps the delivered
proof in memory and exposes it through an endpoint that requires a signed-in session:

```powershell
# sign in as an account that can list accounts, then:
GET http://localhost:5080/api/dev/recovery/proof/{accountId}
```

The proof is **never** written to the database, the outbox, the logs or an audit entry, and both the
adapter and this endpoint are registered only when the host runs in Development.

> Accounts created before this change have no verified contact, because the development seeder skips
> accounts that already exist. Run recovery against a freshly seeded account, or set a confirmed
> email on the account first.

## 4b. Email delivery through SMTP (added 18/10/2026)

The recovery code can now be delivered as a real email.

- **Selection:** when `Smtp:Host` and `Smtp:FromAddress` are configured, `SmtpContactDeliveryAdapter` is the
  `IContactDeliveryAdapter` in every environment. Without SMTP settings, Development keeps the
  development adapter above (logged only, proof readable through the Development endpoint) and other
  environments have no adapter, so recovery cannot claim a message was sent.
- **Channel:** only the Email channel is delivered by SMTP. A phone/SMS channel is reported as a delivery
  failure (no SMS provider exists) and recorded in the outbox as `unsupported-channel`.
- **Content:** plain-text Vietnamese email to the account's verified address: the code, its lifetime
  (`Recovery:TokenLifetimeMinutes`), single use, the link when `Recovery:VerificationBaseUrl` is set, and a
  note to ignore the mail if the person did not ask. No diagnosis, account list or other personal data.
- **Secrets:** the code is never written to logs, the outbox, audit or the database (only its hash, as
  before). SMTP credentials come from environment variables loaded from `.env`
  (`SMTP_HOST`, `SMTP_PORT`, `SMTP_ENABLE_SSL`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM_ADDRESS`,
  `SMTP_FROM_NAME` → `Smtp__*`); `.env` is never committed.
- **Outbox:** provider `smtp`, status `sent` on success or `failed` when the SMTP server rejects the
  message; a failure makes the request return the existing delivery-failure response (MSG144) without
  claiming success.
- **Local testing:** `docker compose --env-file .env -f infrastructure/docker/compose.yaml --profile mail up -d`
  starts Mailpit (SMTP `127.0.0.1:1025`, inbox UI http://127.0.0.1:8025). Use `SMTP_HOST=127.0.0.1`,
  `SMTP_PORT=1025`, `SMTP_ENABLE_SSL=false`, `SMTP_FROM_ADDRESS=no-reply@dcms.local`. Mailpit only captures
  mail locally; nothing leaves the machine.
- **Real mailbox:** use a real SMTP account (for example Gmail with an app password: `smtp.gmail.com`, port
  587, SSL true). The account must have a confirmed real email; demo accounts use `@example.invalid`.

## 5. [PROPOSED] decisions awaiting confirmation

| # | Decision | Where |
| --- | --- | --- |
| 1 | All recovery limits are development placeholders: token 30 min, cooldown 2 min, 3 resends, 5 verify attempts, 5/hour per account, 20/hour per IP, 6-digit code, Email channel | `appsettings.Development.json` → `Recovery` |
| 2 | Resend returns a deliberately neutral body, so the countdown is estimated client-side (120 s) rather than learned from the API | `RESEND_COOLDOWN_SECONDS` in web/mobile validation |
| 3 | Resetting a password revokes **all** previous sessions, matching the lock behaviour | `AccountRecoveryService.ApplyResetAsync` |
| 4 | An Admin may set a new credential only after the owner has completed verification; an Admin can start the process but not skip it | `AccountRecoveryService.ResetForAccountAsync` |
| 5 | A mismatched confirmation reuses MSG04 — no exact message exists in v1.2 | `AccountRecoveryService.ValidatePassword` |
| 6 | Admin-assisted requests obey the same resend cooldown as self-service | `AccountRecoveryService.RequestForAccountAsync` |
| 7 | Recovery status is Admin-only; for an anonymous caller it would confirm an account exists | `GET /api/admin/recovery/status/{accountId}` |
| 8 | Development demo accounts carry a synthetic `@example.invalid` confirmed email so the flow can be exercised | `DevelopmentAccountSeeder` |
| 9 | SMTP delivery is plain text, subject "Mã khôi phục tài khoản DCMS"; the SMS channel stays unsupported | `SmtpContactDeliveryAdapter` |

## 6. Message usage

| Code | Where used | Text source |
| --- | --- | --- |
| MSG12 | After every recovery request and every resend (neutral, unchanged by account existence) | API + client i18n resource |
| MSG13 | Success toast after a reset | API + client i18n resource |
| MSG108 | Invalid, expired, used or over-limit proof | API |
| MSG109 | Password violates the shared S02 policy | API |
| MSG144 | Connection / service / delivery failure | API + client fallback |
| MSG02 | Required field | API |
| MSG03 | Field over the maximum length | API |
| MSG04 | Wrong format, mismatched confirmation | API + client |
| MSG06 | Admin action without the permission | API |

Client-side resources: `web/src/features/recovery/recovery.messages.ts` and
`mobile/src/features/auth/recovery/recovery.messages.ts`. Both hold the same exact wording as the
API so a message can be rendered from either side, and unit tests pin the text on the backend.

## 7. Endpoints

### Self-service (anonymous)
| Method | Route | Notes |
| --- | --- | --- |
| POST | `/api/auth/recovery/request` | Always neutral `MSG12` for a valid-format identifier |
| POST | `/api/auth/recovery/verify` | Returns a single-use reset grant, or `MSG108` |
| POST | `/api/auth/recovery/resend` | Always neutral `MSG12`; cooldown/cap enforced server-side |
| POST | `/api/auth/recovery/reset` | `MSG13` on success, `MSG108`/`MSG109` on failure |
| GET | `/api/auth/policy` | Shared S02 password policy used for the hints |

### S04 Admin-assisted (`ManageAccounts`)
| Method | Route | Notes |
| --- | --- | --- |
| POST | `/api/admin/recovery/request/{accountId}` | Starts the same process; service re-checks Admin |
| POST | `/api/admin/recovery/verify/{accountId}` | Audit records the Admin as actor |
| POST | `/api/admin/recovery/reset/{accountId}` | Only after the owner verified |
| GET | `/api/admin/recovery/status/{accountId}` | Admin-only; never exposed anonymously |

### Development only
| Method | Route | Notes |
| --- | --- | --- |
| GET | `/api/dev/recovery/proof/{accountId}` | Signed-in session required; not mapped outside Development |

## 8. Migration

`20261012160545_AccountRecovery` is additive: it creates `RECOVERY_TOKEN`, `RECOVERY_AUDIT` and
`RECOVERY_OUTBOX` with their indexes and foreign keys. It contains no `DropTable`, `DropColumn`,
`DropIndex` or data deletion, so existing development data is preserved.

```powershell
& $dotnet ef database update --project backend/src/DCMS.Infrastructure --startup-project backend/src/DCMS.Api
# or
& .\infrastructure\scripts\start-api.ps1 -Migrate
```

## 9. Known gaps

- Integration and E2E tests could not be executed while authoring this change; criteria 1–7 are
  therefore **proven by written tests only**, not by an observed green run.
- Criterion 5 (session revocation) has no S01 cooperation test yet; the recovery side revokes every
  active session and `RecoveryApiTests.C5_...` asserts it.
- The recovery channel has no production provider. `RecoverySettings.Channel` selects the channel,
  but only the Development adapter ships today.

## 10. Public email scope — observed 18/10/2026

This section records the newer, narrower customer implementation. Earlier sections describe the original staff/general S03 baseline and historical test state; use [the Public email specification](specs/S03-PUBLIC-EMAIL-RECOVERY.md) for the current customer behavior.

- Added the authenticated Public email-confirmation API and an additive `EMAIL_VERIFICATION_TOKEN` migration. New Public registration remains unconfirmed until a code is delivered through configured SMTP and accepted. No SMS or Patient link is created. The migration ran successfully in isolated PostgreSQL test schemas; it was **not applied to the project's existing database**.
- Added `/api/customer/recovery/{request,resend,verify,reset}` for email-only Public recovery. Existing `/api/auth/recovery/*`, staff Web sign-in and Admin S04 routes remain available. The development proof endpoint now requires `ManageAccounts` so a Public session cannot read any account's proof.
- A successful Public reset invalidates existing access sessions and active refresh tokens. The API returns neutral MSG12 for unconfirmed and unknown valid emails. No SMTP configuration means Public email confirmation returns a service error instead of claiming a send.
- Observed with a separate temporary PostgreSQL 17 container and synthetic accounts: **22/22** integration tests passed (`PublicEmailRecoveryTests` 3, existing `RecoveryApiTests` 19); **7/7** SMTP adapter unit tests passed. `dotnet build backend/DCMS.sln --no-restore` passed with 0 warnings and 0 errors. Web `npm run build` and Mobile `npm run typecheck` passed. Web build emitted the existing Vite bundle-size warning.
- Browser and physical-device testing, and delivery through a real SMTP provider, were not run in this automated pass. [Development run instructions](implementation/RUN-S03-PUBLIC-EMAIL.vi.md) use Mailpit. No project database test data was inserted.

## 11. Registered email is recovery-eligible — decision 18/10/2026

The user removed the prerequisite to verify an email separately after Public registration. Customer Web and Mobile now request a recovery code for the email stored on any Public account, including an existing account with `EmailConfirmed=false`. The code sent to that inbox is still required before password reset. Registration and recovery do not set `EmailConfirmed=true`, alter roles, link a Patient, or change staff/S04 recovery policy. The customer home no longer prompts for separate email verification.

Observed with a new isolated PostgreSQL 17 container and synthetic accounts: **22/22** integration tests passed (`PublicEmailRecoveryTests` 3 and existing `RecoveryApiTests` 19). Backend solution build passed with 0 warnings and 0 errors; Web build and Mobile typecheck passed. No production Gmail message was sent by these tests and no project account was modified. The already-applied migration does not need a new migration or data update for this policy change.
