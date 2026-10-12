# DCMS.IntegrationTests

UC-01/UC-31 integration tests use the real ASP.NET pipeline and PostgreSQL, with a fresh `dcms_test_<random>` schema for each run. They apply the checked-in migration, generate test credentials at runtime and seed only that isolated schema. Synthetic Patient/link sentinel tables verify account operations do not affect patient data. Test schemas are retained; development tables are not deleted or modified.

From the repository root with PostgreSQL running:

```powershell
& .\infrastructure\scripts\test-accounts.ps1
```

Coverage: sign in/out, current user, session expiry/tampering, role inheritance and multiple roles, authorization on management endpoints, immediate lock/revocation, duplicate identifiers and transactional rollback, stale/concurrent writers, audit and Patient invariants. Browser tests and demo instructions are in [RUN-ACCOUNTS.vi.md](../../../docs/implementation/RUN-ACCOUNTS.vi.md).
