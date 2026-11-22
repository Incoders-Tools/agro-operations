# Temporary local administrator boundary

This is a development-only bootstrap, separate from invitation-only production identity. It is not customer onboarding, production authentication, or a license to expose real data in the open notebook.

## Configuration

The API maps session endpoints only when `ASPNETCORE_ENVIRONMENT=Development`, `AGRO_LOCAL_ADMIN_EMAIL` is set, and `AGRO_LOCAL_ADMIN_PASSWORD` contains at least 24 characters. Generate the password locally; never put credentials in source, committed fixtures, chat, or logs. The HTTP host and local database must bind to `127.0.0.1`. Never enable this bootstrap in staging or production; remove it when verified invitation-based identity is ready.

`POST /api/v1/session` accepts a password, applies a bounded login rate limit and issues an HttpOnly, SameSite=Strict, eight-hour cookie. `GET /api/v1/session` returns the authenticated local identity and a CSRF token; `POST /api/v1/session/logout` requires `X-Agro-CSRF` and clears the session. The data-protection keys live under ignored `.local/keys` for local restarts. A login-free development notebook with invented fixtures remains a separate surface.

When `AGRO_LOCAL_DATABASE` is also configured for a loopback-only PostgreSQL host and the additive schema migrations are applied, `/api/v1/clients`, `/fields`, `/plots`, and `/crops` expose authenticated `GET` lists and `POST` creates; clients, fields and plots also expose `GET /{id}` and each master supports `PUT /{id}` with an expected `revision`. Anonymous requests return 401; every write requires the CSRF header. Blank names return 400, missing parents 404 and stale revisions 409. Do not supply a remote/production database or real records to the open notebook.

## Checks

The smoke test uses a generated test password and `example.test` email, and asserts anonymous denial, wrong password rejection, CSRF enforcement, logout and post-logout denial. This does not prove that business endpoints or real-data imports are ready.
