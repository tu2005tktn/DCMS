# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

DCMS is a dental clinic management system: a .NET 10 API + PostgreSQL, a React 19/Vite/Ant Design web app (staff workspace and a customer portal under `/customer`), and an Expo (React Native) customer/staff mobile app. The team writes in Vietnamese; docs and UI strings are Vietnamese, code identifiers are English. Answer the user in Vietnamese.

## Commands (Windows / PowerShell, run from repo root)

```powershell
# Database (PostgreSQL in Docker; never use `down -v`, it erases dev data)
docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d

# Backend: pick local SDK if present, else global dotnet
$dotnet = if (Test-Path .\.tools\dotnet\dotnet.exe) { '.\.tools\dotnet\dotnet.exe' } else { 'dotnet' }
& $dotnet build backend/DCMS.sln
& .\infrastructure\scripts\start-api.ps1 -Migrate -SeedDemo   # first run: apply migrations + seed demo accounts
& .\infrastructure\scripts\start-api.ps1                      # API on http://localhost:5080 (/health, /openapi/v1.json)

# Web (port 5173, strict)
cd web; npm.cmd run dev        # build: npm.cmd run build (tsc -b && vite build); unit: npm.cmd run test:unit

# Mobile
cd mobile; npm.cmd run typecheck; npm.cmd start
```

On Linux/macOS use `npm`/`npx` instead of `npm.cmd`/`npx.cmd`; the `.ps1` scripts need `pwsh`.

`dev.bat` starts DB + API + web together; `.claude/launch.json` defines `dcms-api`, `dcms-web`, `dcms-mobile-web` for the preview tool.

- Backend tests: `& $dotnet test backend/tests/DCMS.UnitTests` (EF InMemory for most `*CreateTests`, a few need PostgreSQL) or `backend/tests/DCMS.IntegrationTests` (isolated PostgreSQL schemas). Load env first with `. .\infrastructure\scripts\import-development-env.ps1` (sets `$dotnet`, connection string, without printing secrets). Single test: `& $dotnet test <test project> --filter "FullyQualifiedName~<TestName>"`.
- Browser E2E: `cd web; npx.cmd playwright test` (several `playwright.*.config.ts` per area; needs API + Vite running and the demo seed). Wrapper: `infrastructure/scripts/test-accounts.ps1 [-Browser]`.
- New EF migration: dot-source `import-development-env.ps1`, then from `backend`: `dotnet ef migrations add <Name> --project src/DCMS.Infrastructure --startup-project src/DCMS.Api --output-dir Persistence/Migrations`. Do not recreate the initial migration.
- Secrets: `.env` (git-ignored) holds `POSTGRES_PASSWORD`, `DEV_SEED_PASSWORD`, JWT keys. Never write the demo password or connection strings into tracked files, docs or chat.

## Architecture

**Backend** (`backend/src`), Clean Architecture with feature folders: `Domain` → `Application` → `Infrastructure` → `Api`. A feature (e.g. `Appointments`, `Treatment`, `Examinations`, `Prescriptions`, `SystemSettings`) has the same folder name in each layer: entities/rules in Domain, contracts/DTOs and `SystemMessages` in Application, EF services and background workers in Infrastructure, minimal-API `*Endpoints.cs` in Api. `Program.cs` wires endpoints, SignalR (`/hubs/notifications`) and hosted services. Single `DcmsDbContext` + `Persistence/*Model.cs` configure all tables; migrations are in `Infrastructure/Persistence/Migrations`.

Cross-cutting patterns you need to know before editing services:
- **Idempotent commands**: write endpoints take a client `RequestKey`; services store a `*Receipt` row (fingerprint + result) and replay it on retry (mismatched fingerprint → MSG111). Optimistic concurrency via `Version`/`ExpectedVersion`.
- **Errors/messages**: `BusinessException(status, SystemMessage)`; message ids (`MSGxx`) and Vietnamese text live in `Application/Common/SystemMessages.cs`. Some errors use plain text or custom codes instead.
- **Permissions** are layered: fixed roles (`AccountRoles`: Admin, Dentist, ClinicAssistant, ClinicOwner, Guest, Patient; `Effective()` makes Owner ⊃ Dentist, Patient ⊃ Guest) → screen/action grants (`ScreenGrants.cs`, `SettingsGrants.cs`, configurable access groups per branch) → per-service checks. Payment and finance-statistics endpoints are hard-coded Clinic-Owner-only (`IsOwner`), not grant-driven.
- **Scheduling** serializes through a Postgres advisory lock (`WorkingScheduleService.TransactionLock`); availability is computed on request, not by a job. Slot step is per-branch (default 15 min).
- **Payments**: treatment payments are `AUDIT_LOG` rows (`AuditType` = `FinanceLedger` / `FinancePayOsOrder`), not a bills table. PayOS QR orders are settled by webhook (`/api/payments/payos/webhook`, HMAC-SHA256) and a 15 s reconcile worker.
- **Background workers**: appointment notification dispatcher (reminders 24 h/2 h, retries 1/5/15 min), treatment due scanner, clinical file scanner, PayOS worker. There is no automatic no-show job.

**Web** (`web/src`): `app/App.tsx` holds all routes (staff routes wrapped in `Protected permission=…` using server-sent permission keys; customer routes under `/customer` behind `CustomerGuard`). Code is organized by `features/<name>` with CSS per feature. JWTs live in memory only (reload requires sign-in).

**Mobile** (`mobile/src`): Expo Router; customer flows plus a few staff screens (support requests, schedule impact); push via Firebase.

**AI service** (`ai-service/`): optional FastAPI + YOLO dental X-ray caries detection (`POST /v1/xray/analyze`, port 8090). The API calls it when `DENTAL_AI_BASE_URL` is set in `.env`; model weights are not in Git (`AI_CARIES_WEIGHTS`). Spec: `docs/specs/AI-CARIES-DETECTION.vi.md`.

**CI/deploy** (`.github/workflows`): every push to `main` builds `dcms-api`/`dcms-web` images to ghcr.io and deploys them to the production server (`infrastructure/docker/compose.prod.yaml`, self-hosted runner); `mobile-apk.yml` builds the Android APK.

## Specs and conventions

- Business source of truth: `docs/specs/README.md` (DCMS v2.5 specs: UC/GBR/MSG) and per-feature specs in `docs/specs/`; evidence in `docs/implementation/`. Code can be newer than older docs; when they disagree, trust the code. `AI_HANDOFF.md` summarizes the treatment/payment screens.
- Update `docs/` when changing a feature, API, data or UI. Keep React + Ant Design and Clean Architecture/feature folders.
- Do not commit, push, or run migrations/seeding against `dcms_dev` unless the user explicitly asks; assume the working tree contains the user's uncommitted changes (e.g. staged deletions under `backend/tests/DCMS.IntegrationTests`).
- Run only the tests relevant to the change, not the full suite.
- `docs/specs/USE-CASE-SPECIFICATIONS.md` and `SRS-FIX-GUIDE.md` are the capstone SRS work (code-derived); keep them consistent with code if features change.
