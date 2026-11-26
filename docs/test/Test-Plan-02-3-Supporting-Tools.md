## 2.3 Supporting Tools

| Purpose | Tool | Vendor/In-house | Version |
|---|---|---|---|
| Unit and integration test runner, backend | xUnit (with `dotnet test`, Microsoft.NET.Test.Sdk) | Open source | 2.9.3 (Test.Sdk 17.14.1, runner 3.1.4) |
| Database stand-in for unit tests, backend | EF Core InMemory and EF Core SQLite providers | Microsoft | 10.0.0 |
| Integration test host, backend | ASP.NET Core test host (WebApplicationFactory) | Microsoft | 10.0.0 |
| Test database for integration tests | PostgreSQL in Docker, one throw-away schema per run | Open source | 17 (postgres:17-alpine) |
| Unit test runner, web | Vitest | Open source | 3.2.x |
| End-to-end and system testing, web | Playwright (Chromium) | Microsoft | 1.64.x |
| E-mail capture during tests | Mailpit | Open source | 1.27 |
| Antivirus scan for file upload tests | ClamAV | Open source | stable |
| Test cases, results and statistics | Google Sheets (system test report) | Google | N/A |
| Manual API inspection | OpenAPI document published by the backend | Microsoft | Bundled with ASP.NET Core 10 |
| Test scripts, seed data and environment import | `web/tests-system`, `infrastructure/scripts` | In-house | N/A |
| Version control | Git and GitHub | GitHub | N/A |

Not used: mocking libraries, code coverage tools, a continuous integration pipeline for tests (GitHub Actions currently only builds the mobile APK) and a mobile test framework.
