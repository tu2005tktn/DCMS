# Scripts

- `initialize-development.ps1`: generate missing JWT/demo secrets in ignored root `.env`, preserve existing values.
- `import-development-env.ps1`: dot-source to load database/auth settings and the local/global .NET command without printing secrets.
- `start-api.ps1`: run the API; `-Migrate` applies pending migrations, `-SeedDemo` adds missing synthetic accounts in Development and then exits.
- `test-accounts.ps1`: PostgreSQL integration tests in isolated schemas; `-Browser` runs real API/Vite browser tests with the local demo password.

See [account setup and testing](../../docs/implementation/RUN-ACCOUNTS.vi.md).

- `import-clinic-catalog.ps1`: nạp `infrastructure/seed/clinic-catalog.seed.sql` (dịch vụ, kế hoạch điều trị, vật liệu, labo) vào PostgreSQL Docker; xem `docs/specs/CLINIC-CATALOG-SQL-SEED.md`.
