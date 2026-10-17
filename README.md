# DCMS

**S04 / DCMS-75:** [Đặc tả quản lý tài khoản Web](docs/specs/S04-UC31.md) và [hướng dẫn chạy, migration, bằng chứng nghiệm thu](docs/implementation/S04-DCMS-75.md). Đăng nhập Admin dùng API/phiên chung S01; recovery S03 vẫn hoãn.

**Team setup (Tiếng Việt): [Hướng dẫn cài đặt và chạy dự án](docs/SETUP.vi.md).** Includes required tools, first-run commands, daily startup, verification, and troubleshooting for Windows/PowerShell.

This repository contains the .NET 10 API, PostgreSQL development container and React web app. UC-01 (Identity/JWT sign in/out) and core UC-31 (staff account management, current roles, lock/unlock, concurrency and audit) are implemented. The [mobile team scaffold](mobile/README.md) provides Expo routes and placeholder screens; mobile business workflows, AI, patient workflows and verified credential recovery remain future work.

The [37-table logical ERD](docs/architecture/DCMS_Logical_ERD_37_Tables.mmd) is a design reference; only account/employee/authentication tables have been migrated. Read the [UC/GBR/MSG implementation matrix](docs/implementation/UC01-UC31.md) and [Vietnamese account demo/test guide](docs/implementation/RUN-ACCOUNTS.vi.md).

## Requirements on Windows

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`dotnet --version` should show 10.x). If another SDK is installed globally, use the local SDK command below.
- Node.js 22 and npm
- Docker Desktop with the Linux engine running

PostgreSQL CLI (`psql`) is optional. Docker Compose and the API health endpoint provide the required checks.

## PostgreSQL in this project

You do not need to install PostgreSQL separately or use `psql`. Docker Compose runs one PostgreSQL container, `.env` supplies its local database name, user, and password, and the named Docker volume keeps data between container restarts. The API uses `infrastructure/scripts/start-api.ps1` to read the same `.env` file without displaying the password.

After the first setup, these commands manage the database from the repository root:

```powershell
docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d  # Start
docker compose --env-file .env -f infrastructure/docker/compose.yaml ps     # Check status
docker compose --env-file .env -f infrastructure/docker/compose.yaml down   # Stop; keeps data volume
```

Do not use `down -v` unless you intend to erase the development database.

To install .NET 10 only in this checkout (the `.tools` folder is ignored by Git):

```powershell
New-Item -ItemType Directory -Force .tools | Out-Null
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile .tools/dotnet-install.ps1
& .\.tools\dotnet-install.ps1 -Channel 10.0 -InstallDir .tools/dotnet -NoPath
$dotnet = '.\.tools\dotnet\dotnet.exe'
& $dotnet --version
```

If .NET 10 is already installed globally, use `$dotnet = 'dotnet'` instead. The local `.tools/dotnet` SDK is not included in Git; each teammate installs their own SDK.

## First run (PowerShell, from repository root)

1. Copy `.env.example` to `.env` only if it does not exist, then set a private `POSTGRES_PASSWORD`. Run `& .\infrastructure\scripts\initialize-development.ps1` to generate local JWT/demo secrets without overwriting existing settings. Copy `web/.env.example` to `web/.env` if needed.
2. Start PostgreSQL:

   ```powershell
   docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d
   docker compose --env-file .env -f infrastructure/docker/compose.yaml ps
   ```

3. Restore and build the backend:

   ```powershell
   $dotnet = '.\.tools\dotnet\dotnet.exe' # Or 'dotnet' when a global .NET 10 SDK is installed
   & $dotnet restore backend/DCMS.sln
   & $dotnet build backend/DCMS.sln --no-restore
   ```

4. Apply migrations and seed synthetic development accounts, then run the API. The script reads database/auth settings from ignored `.env`:

   ```powershell
   & .\infrastructure\scripts\start-api.ps1 -Migrate -SeedDemo
   & .\infrastructure\scripts\start-api.ps1
   ```

5. In another PowerShell window, install and run the web app:

   ```powershell
   cd web
   npm.cmd install
   npm.cmd run build
   npm.cmd run dev
   ```

6. Verify API and web:

   ```powershell
   Invoke-RestMethod http://localhost:5080/health
   Start-Process http://localhost:5173/sign-in
   ```

Sign in with `admin.demo`; its password is `DEV_SEED_PASSWORD` in your local `.env`. Other demo roles and step-by-step scenarios are in the [demo guide](docs/implementation/RUN-ACCOUNTS.vi.md). No password is stored in tracked files. JWTs are kept in browser memory; reloading requires sign-in.

`/health` returns HTTP 200 with `{ "api": "healthy", "database": "healthy" }` when PostgreSQL is reachable, or HTTP 503 with `database: "unavailable"` otherwise. The existing web status page is at `http://localhost:5173/health`. Development OpenAPI JSON is at `http://localhost:5080/openapi/v1.json`. CORS allows local Vite at port 5173.

## Tests

With PostgreSQL running, execute `& .\infrastructure\scripts\test-accounts.ps1`. Integration tests use isolated PostgreSQL schemas and preserve development data. With API/Vite running and demo seeded, install browser binaries with `npx.cmd playwright install chromium` from `web`, then run `& .\infrastructure\scripts\test-accounts.ps1 -Browser` from the root. Browser tests retain synthetic `e2e.*` accounts and their audit.

## EF Core migrations

`AccountsAndSessions` is included. Apply pending migrations using `start-api.ps1 -Migrate`; do not recreate the initial migration. For future changes:

```powershell
. .\infrastructure\scripts\import-development-env.ps1
Push-Location backend
try {
    & $dotnet tool restore
    & $dotnet ef migrations add <MigrationName> --project src/DCMS.Infrastructure --startup-project src/DCMS.Api --output-dir Persistence/Migrations
} finally { Pop-Location }
```

The helper sets `ConnectionStrings__DefaultConnection` without displaying credentials. Never commit `.env`, private keys, patient files, database dumps, or model weights. Recovery is intentionally unavailable until a verified channel/proof policy is configured; account administration cannot verify a patient link.
