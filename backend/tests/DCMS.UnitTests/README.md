# DCMS.UnitTests

Pure unit tests for domain and application rules, with no database or HTTP pipeline.

These tests run without PostgreSQL:

```powershell
& .\infrastructure\scripts\import-development-env.ps1
& $dotnet test backend/tests/DCMS.UnitTests
```

What is covered here:

- `RecoveryProofTests` — verification proof lifecycle: length, randomness, hashing,
  constant-time comparison, grant generation, and rejection of unsupported lengths.
- `RecoverySettingsTests` — the configurable UC-02 policy defaults and their bounds. The defaults
  are development placeholders and are marked `[PROPOSED]` until confirmed.
- `RecoverySystemMessageTests` — the exact wording of the messages used by UC-02 (MSG02, MSG04,
  MSG06, MSG12, MSG13, MSG108, MSG109, MSG144), including Vietnamese diacritics.
- `DevelopmentContactDeliveryAdapterTests` — the development contact adapter boundary: it reports
  success only as "handed to the outbox", never as a real send, and it refuses to run outside
  Development.

Tests that exercise the HTTP endpoints and PostgreSQL live in `../DCMS.IntegrationTests`.
