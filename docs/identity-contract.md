# Public identity contract — sanitized T3

Account creation is **invitation-only**. No client identity, credential, email address or secret is committed in this repository. Resend is a possible outbound mail transport, not an identity provider.

## Specs
- S1: A user without an invitation cannot create an account. Never map framework-provided public `POST /register` or `POST /api/v1/auth/register`; neither should change account counts. An unauthenticated caller cannot create invitations or access domain data.
- S2: An authorized admin can invite an email address with one-use, expiring, non-plaintext-at-rest token. Acceptance proves address control and sets a password that meets ASP.NET Core Identity policy. Reuse/expiry/invalid token or mismatched email must fail without creating an active account. No default account/password in source.
- S3: Login succeeds only for a verified active invited user, rejects wrong password and unknown users without leaking account existence, and supports logout/session revocation. Prefer secure, HttpOnly, SameSite cookie through a same-origin reverse proxy for web; future mobile credentials require separate contract. Protect state-changing cookie requests against CSRF.
- S4: Password-reset request returns indistinguishable result for known/unknown email, sends a one-time expiry-bound link only for active accounts, and reset invalidates old credentials/sessions. Tests cover replay/expiry and no stored-data mutation for rejected requests.
- S5: Email sending is abstracted, with a local non-production sink and a separately configured Resend adapter (API key in environment only, verified sender required). Never log links/tokens. Rate-limit anonymous auth endpoints in a way compatible with future multiple instances; no real customer emails in tests.
- S6: Domain routes remain unexposed until full authorization, migration and real HTTP tests succeed. A health endpoint may remain public. All test fixtures use invented email domains (`example.test`).

## Tasks
- T3a: Establish ASP.NET Core Identity/PostgreSQL account model, invitation workflow and security tests in one reviewable slice. If the full login/reset transport cannot be safely completed, leave it unexposed and report precisely what is pending rather than ship insecure stubs. | done
- T3b: Complete verified login/logout/password recovery and email adapter with isolated HTTP/DB tests before enabling any authenticated business route. | domain layer done (invite/accept/login/password-reset, `IEmailSender` abstraction with a local non-production sink); HTTP wiring intentionally deferred until a real staging/production deployment target exists

## Resolved scope (domain layer)
Invitation, acceptance, login and password reset are fully implemented and isolated-tested end to end in `src/AgroOperations.Api/Identity/IdentityService.cs`:
- Invitation tokens are dispatched only through `IEmailSender`, never returned to the caller or logged; a local file sink (`LocalFileEmailSender`) stands in for a verified Resend sender until a production email domain exists.
- Acceptance is transactional and sets both `email_verified` and `active` (receiving and using a dispatched one-use token is the proof of address control this spec requires); rejected input (bad token, mismatched email, weak password, replay, expiry) never mutates stored state.
- Login checks both `email_verified` and `active`, and never takes an observably different path for an unknown email versus a wrong password (a dummy password-hash verification runs on both failure branches).
- Password-reset request is a no-op with an identical return path for an unknown or a known-but-inactive email, and only dispatches for a genuinely active account; reset itself rejects a weak password before touching the database, is transactional, and rotates a `security_stamp` column so a previously issued login result is invalidated.
- Tokens are stored only as SHA-256 digests (`identity_invitations.token_hash`, `identity_password_resets.token_hash`); passwords use ASP.NET Core Identity's `PasswordHasher`, not a custom scheme.
- Not yet built: any HTTP route, session cookie, or CSRF wiring for this identity scheme (distinct from the temporary local-admin bootstrap in `docs/local-access-contract.md`), since there is no staging/production host to serve it yet; wiring it is a future, separately reviewed slice once a real deployment target and a verified outbound-mail domain exist.

## Log
- This public contract intentionally omits private requester notes. The selected access policy is invitation-only; the private feature record holds the full request.
