# Agro Operations

A .NET 10 ASP.NET Core Web API and Next.js web client for a private agronomy operations backoffice (clients, fields, plots, crops, campaigns, labor types and internal treatment recipes), backed by PostgreSQL.

`GET /api/v1/health` is always public; every other route is Development-only and loopback-only while there is no staging/production deployment. See `docs/local-access-contract.md` for the temporary local administrator bootstrap and `tools/start-local.ps1` (the single local launcher behind `C:\shortcuts\run-agro-operations-local.bat`) to run the whole local stack, `docs/domain-contract.md` and `docs/platform-contract.md` for the operations/agronomy domain, and `docs/local-preview-contract.md` for the separate login-free synthetic demo at `web/`.

## Identity (domain layer only)

`src/AgroOperations.Api/Identity/IdentityService.cs` implements the full invitation-only account lifecycle described in `docs/identity-contract.md`: admin-issued one-use expiring invitations, acceptance, login and password reset, with tokens dispatched only through an `IEmailSender` abstraction (a local non-production file sink today; a verified Resend sender once a real sending domain exists). No HTTP route, cookie, or CSRF wiring exists for it yet — that is a separate, later slice once a real staging/production deployment target exists. This is independent from the temporary local-admin bootstrap that already protects `/backoffice`.

## Local development

Requires .NET 10 SDK. Run `dotnet run --project src/AgroOperations.Api/AgroOperations.Api.csproj` and request `GET /api/v1/health` at the URL shown by the server. The public response is `{"status":"healthy"}`. `/api/v1` is reserved for future web and mobile contracts.

For an optional **local-only** PostgreSQL container, set a private `POSTGRES_PASSWORD` in your shell or ignored `.env` file, then run `docker compose up -d postgres`. It binds only to `127.0.0.1:5433`. The internal migrations under `src/AgroOperations.Api/Persistence/Migrations/` apply in numeric order through your deployment migration process; never reuse a local-only password in staging or production, and never point a test database at a real one.

Run the deterministic HTTP smoke check with `dotnet run --project tests/AgroOperations.Smoke/AgroOperations.Smoke.csproj`; build with `dotnet build AgroOperations.slnx`. For isolated PostgreSQL service tests set `AGRO_TEST_DATABASE` to a dedicated test database connection string, then run `dotnet run --project tests/AgroOperations.Domain/AgroOperations.Domain.csproj`. For isolated invitation tests, use a separate dedicated test database connection in `AGRO_TEST_DATABASE` and run `dotnet run --project tests/AgroOperations.Identity/AgroOperations.Identity.csproj`. Each test creates and drops a unique schema in that database; never point it at a production database. The container image can be built from `Dockerfile`; `.dockerignore` limits the build context to API source. Neither the image nor the repository contains credentials. Keep connection strings and secrets out of Git and provide them only through secure runtime configuration when persistence is implemented.
