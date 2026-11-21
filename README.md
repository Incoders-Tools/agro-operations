# Agro Operations API bootstrap

A .NET 10 ASP.NET Core Web API scaffold. Internal operations domain persistence uses Npgsql and PostgreSQL; no database connection, identity provider, business routes, or real data are configured in the HTTP host.

## Local development

Requires .NET 10 SDK. Run `dotnet run --project src/AgroOperations.Api/AgroOperations.Api.csproj` and request `GET /api/v1/health` at the URL shown by the server. The public response is `{"status":"healthy"}`. `/api/v1` is reserved for future web and mobile contracts. No domain routes are available; add authentication and authorization before exposing any domain endpoint.

For an optional **local-only** PostgreSQL container, set a private `POSTGRES_PASSWORD` in your shell or ignored `.env` file, then run `docker compose up -d postgres`. It binds only to `127.0.0.1:5433`. The API does **not** connect to the database yet; the internal schema migration is `src/AgroOperations.Api/Persistence/Migrations/001_operations.sql`. Apply it transactionally to the intended database through your deployment migration process; do not expose domain routes before authentication and authorization exist. No customer data is present. Never reuse that password in staging or production.

Run the deterministic HTTP smoke check with `dotnet run --project tests/AgroOperations.Smoke/AgroOperations.Smoke.csproj`; build with `dotnet build AgroOperations.slnx`. For isolated PostgreSQL service tests set `AGRO_TEST_DATABASE` to a dedicated test database connection string, then run `dotnet run --project tests/AgroOperations.Domain/AgroOperations.Domain.csproj`. The test creates and drops a unique schema in that database; never point it at a production database. The container image can be built from `Dockerfile`; `.dockerignore` limits the build context to API source. Neither the image nor the repository contains credentials. Keep connection strings and secrets out of Git and provide them only through secure runtime configuration when persistence is implemented.
