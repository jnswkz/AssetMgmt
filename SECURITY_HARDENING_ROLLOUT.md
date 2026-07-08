# Security hardening rollout

## Required configuration

- Set `JWT_SECRET` to a unique value of at least 32 characters.
- Set `CORS_ALLOWED_ORIGINS` to the exact frontend origin list. Production rejects a missing value or `*`.
- Set `DB_USER`, `DB_PASSWORD`, and the other database variables explicitly.
- If seeded placeholder users still exist, set a non-default `SEED_DEFAULT_PASSWORD` before the first production start.
- Mount persistent storage at `Storage__HandoverRoot` (the container default is `/app/private/handovers`).
- Keep `HangfireDashboard__Enabled=false`; an explicitly enabled dashboard is still loopback-only.

## Deployment order

1. Back up the database and the current `wwwroot/handovers` directory.
2. Apply `Migrations/manual/005_security_hardening.sql`; deployment must stop if `sqlcmd -b` fails.
3. Mount the private handover volume and deploy the backend. Startup copies legacy PDFs to GUID-named private files while retaining the originals.
4. Verify `/handovers/<old-name>.pdf` returns 404 and authenticated download endpoints return the PDF only for an authorized user.
5. Deploy the frontend. The production build uses same-origin `/api` URLs rather than localhost.
6. After the copied files and database storage keys are verified, archive/remove the old public handover directory.

All access and refresh tokens issued before this release are intentionally rejected. Users must sign in again because old access tokens have no security-stamp claim and old refresh tokens have no registered refresh session.

## Smoke checks

- Cross-user request and handover IDs return no data.
- Reusing an already rotated refresh token revokes its token family.
- AI create/approve/reject requests create a pending action and do not mutate data before the explicit confirm call.
- Login, refresh, and AI endpoints return 429 with `Retry-After` after their configured limit.
- Production refuses wildcard CORS, a default/short JWT secret, or an implicit seeded password.
- `dotnet list package --vulnerable --include-transitive` has no High/Critical result and `npm audit --omit=dev` passes.

## Known remaining risk

HTTPS redirection, HSTS, and trusted reverse-proxy configuration are intentionally deferred. TLS termination must still be provided by the deployment environment before exposing the application to an untrusted network.
