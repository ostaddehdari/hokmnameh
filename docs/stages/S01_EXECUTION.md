# S01 — Infrastructure / GitHub CI / VPS deployment — execution record

Snapshot: 2026-11-20. Based on `docs/planning/backlog.json` S01 requirements (3 Works, 9 Tasks); no product-rule changes.

| Task | Engineering deliverable | Status / evidence |
|---|---|---|
| S01-W01-T01 | Segregated monorepo and ownership map, `.editorconfig`, `.github/CODEOWNERS` | IMPLEMENTED IN REPOSITORY; repository review required |
| S01-W01-T02 | Lint/static syntax, .NET build, smoke and migration jobs, container build | IMPLEMENTED; CI build/smoke/migration/docker confirmed green (run 44692067992); **branch protection not configured by code** |
| S01-W01-T03 | Versioned architecture & compatibility decisions | IMPLEMENTED via `docs/adr/0001`, `0002` |
| S01-W02-T01 | localhost-only API, private DB and safe Nginx include | CONFIGURED; not verified on VPS |
| S01-W02-T02 | Non-root app in Docker, protected secrets, SSH instructions | CONFIGURED; dedicated VPS account / key policy **requires server access** |
| S01-W02-T03 | Operator deploy script, ready checks and code image rollback | IMPLEMENTED; real staging/rollback drill **pending VPS** |
| S01-W03-T01 | Versioned checksum-verified migration runner with DB advisory lock | IMPLEMENTED; database CI job confirmed green (run 44692067992, migrations applied twice) |
| S01-W03-T02 | `age`-encrypted pg_dump, isolated disposable restore drill | IMPLEMENTED; actual off-site backup + restore **pending operator** |
| S01-W03-T03 | Existing guest rate limit + liveness/readiness, health-watch service/timer | PARTIAL: no real alert endpoint or VPS timer enabled |

## Important safeguards
1. No deployment has been made to `5.61.27.185:2233` by this development session; no credentials are accepted in Git or chat.
2. All scripts require real Docker, .env and operational tools; local static validation is not VPS acceptance.
3. `001_core.sql` is now managed via `migrate.sh` rather than `docker-entrypoint-initdb.d`; preserve production volumes. Its first application remains idempotent on a prior prototype cluster.
4. No changes to the shared rules engine, no speculative 5+4+4 implementation, no ranked/royal feature represented as completed.
5. The precise master-plan settings that require product approval remain `PENDING_APPROVAL`.

## Release gates
- CI: `.NET` build, smoke, PostgreSQL migrations applied twice without mutation, Docker image build.
- VPS: actual SSH reachable, domain certificate/DNS verified, current Nginx config backed up, route include tested with `nginx -t`, `docker compose ps`, both health checks 200, no DB port exposed.
- Production: branch protection/checks required, encrypted backup and off-host copy, restore drill on isolated instance, host monitoring and alert receiver, rootless deploy account, measured rollback.

## Operator commands (run only after host validation)
```bash
git clone https://github.com/ostaddehdari/hokmnameh.git /opt/hokmnameh
cd /opt/hokmnameh
cp .env.example .env && chmod 600 .env
# Set strong POSTGRES_PASSWORD, review configs and current web server; never paste secrets to chat.
DEPLOY_ENV=staging ./deploy/scripts/deploy.sh
./deploy/scripts/api-smoke.sh
# Inside the existing TLS-enabled Nginx vhost only: include deploy/nginx/hokmnameh-location.conf;
# nginx -t && systemctl reload nginx
```

To deploy production, set `CONFIRM_PRODUCTION_DEPLOY=YES DEPLOY_ENV=production` and `HOKM_BACKUP_AGE_RECIPIENT` **only after verifying the encryption key, independent restore, and authorization**.

## Verified GitHub Actions evidence

- Commit: `f9705a3610f1c6af553d6ad05e25148e0217efaf` (fix SDK feature-band selection after first Docker build failure).
- Run: https://github.com/ostaddehdari/hokmnameh/actions/runs/44692067992
- **PASS**: static validation; .NET 10 build + shared core smoke; PostgreSQL migration idempotency check; Docker API image build.
- These are CI checks, not production deployment, load testing, real backup restore or host hardening evidence.
