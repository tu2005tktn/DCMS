# DCMS agent notes

- Stack: .NET 10 ASP.NET Core Web API, PostgreSQL, EF Core/Npgsql 10; React, TypeScript, Vite, Ant Design. Mobile (Expo) and AI (FastAPI/YOLO) are future work.
- Backend: Clean Architecture with feature folders. Domain has no project dependencies; Application depends on Domain; Infrastructure implements Application contracts; Api composes Application and Infrastructure.
- Documentation first: before implementing any new feature, feature modification, or feature bug fix, update the relevant specifications and documentation in `docs/` to describe the intended behavior and affected requirements, workflows, and API contracts. If the relevant specification or documentation does not exist, create it first. Only start changing implementation code after these updates are complete, and keep the implementation consistent with the updated documents.
- Keep credentials, patient records, and model weights outside Git. Use environment variables and `.env.example` as a template.
- Checks: use .NET 10 (`.tools/dotnet/dotnet.exe` if installed locally) for `restore backend/DCMS.sln` and `build backend/DCMS.sln --no-restore`; from `web`, run `npm install` and `npm run build`; start PostgreSQL with `docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d`; call `Invoke-RestMethod http://localhost:5080/health`.
