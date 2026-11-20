# ADR-0002 — Release gate, reversible app image, forward-only database migrations

Status: **ACCEPTED AS ENGINEERING ARCHITECTURE** · 2026-11-20 · Stage `S01-W02/W03`.

## Decision
- CI on push/PR builds and tests but **never deploys** to VPS using a repository secret or implicit trigger.
- Deployment is operator-initiated from a trusted machine or the host, with a short-lived change window and explicit `DEPLOY_ENV`.
- Nginx preserves the existing `srun.ir` vhost. API binds to `127.0.0.1:5080`, PostgreSQL remains private on Docker internal network, and processes run with limited privileges.
- Stage S01 uses Docker Compose, not premature Kubernetes. Nginx reverse proxies only `/hokmnameh` routes.
- PostgreSQL migration SQL is immutable; a `schema_migrations` ledger records SHA-256 and the applied timestamp. Scripts serialize changes and refuse changed checksums.
- For production, encrypted backup must succeed before migrations. App image can roll back; irreversible schema changes require forward recovery.
- Health probe failures cause a code-image rollback if a previous image is available. Rollback is not a database rollback and not a substitute for restore testing.

## Operations and security
`/opt/hokmnameh/.env` remains 0600, absent from git. SSH login uses dedicated non-root deploy account with key-based auth. Host secrets and `age` identities are never put into CI, tickets or logs. No status is described as deployed without verified SSH, HTTPS and database probes.

## Known limits
CI migration smoke is not evidence of production migrations being applied; production restore, TLS, privileges, branch protection and watchdog alert forwarding need owner/operator verification.
