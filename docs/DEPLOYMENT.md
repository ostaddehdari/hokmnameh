# VPS deployment — operator checklist (NOT YET PERFORMED)

Target supplied by project owner:
- HTTPS path `https://srun.ir/hokmnameh` (requires DNS, TLS and existing web server config verification).
- Repo `https://github.com/ostaddehdari/hokmnameh`.

**Current operator note:** the VPS has NOT been modified by this source change. Do not place host addresses, credentials, private keys or raw production logs in this public repository. Use the separately verified host inventory and an authorized admin channel.

## Prerequisites — on a trusted admin workstation

1. Verify domain DNS points to the intended server, and inspect current web server sites. Confirm existing service before adding route.
2. Verify SSH through VPN/firewall/security-group first. Disable root/password login only after a second key-authenticated admin session has been proven. The application deployment identity is `hokmdeploy`.
3. Install rootless Docker Engine + Compose plugin for `hokmdeploy`; Docker-group access is root-equivalent and fails the project preflight.
4. Put this repository at `/opt/hokmnameh`, owned by `hokmdeploy`. Keep Nginx changes with a separate authorized host administrator.
5. Generate a secure database password using `openssl rand -base64 48` and set it only in `/opt/hokmnameh/.env` (chmod 600). Do NOT commit `.env`.

```bash
# As hokmdeploy, after an administrator has provisioned /opt/hokmnameh ownership
git clone https://github.com/ostaddehdari/hokmnameh.git /opt/hokmnameh
cd /opt/hokmnameh
cp .env.example .env
# Edit .env using a secure editor. Never reuse sample password.
chmod 600 .env
DEPLOY_ENV=staging ./deploy/scripts/deploy.sh
```

## Nginx routing

Open the **existing HTTPS srun.ir vhost**, then include `deploy/nginx/hokmnameh-location.conf` inside its `server {}` block. Do not replace its current config or use the snippet as a whole vhost. Before reload:

```bash
sudo nginx -t && sudo systemctl reload nginx
curl -fsS https://srun.ir/hokmnameh/health/live
curl -fsS https://srun.ir/hokmnameh/health/ready
```

Proxy must preserve `/hokmnameh` prefix because ASP.NET maps route group there. HTTP-to-HTTPS redirects and valid TLS certificates must already be in place. WebSocket upgrade headers are included in the snippet. Compose connects the API to both the private `internal` database network and a separate `edge` bridge so the localhost-only port can reach Nginx; PostgreSQL remains on `internal` only.

## Versioned database migrations — S01

`deploy/scripts/migrate.sh` applies contiguous `backend/db/migrations/NNN_name.sql` files through an advisory-locked transaction and records the SHA-256 in `public.schema_migrations`. The audited list and immutable baseline checksums are in `backend/db/migrations/manifest.json`. It refuses a gap or a modified previously applied migration; add the next numbered file instead. `001_core.sql` is additive and idempotent for compatibility with earlier prototype volumes. A PostgreSQL 18 volume must not be deleted to rerun migrations. The old Docker `docker-entrypoint-initdb.d` mount has been removed.

`DEPLOY_ENV=staging ./deploy/scripts/deploy.sh` is operator-initiated. Production additionally requires `CONFIRM_PRODUCTION_DEPLOY=YES` and `HOKM_BACKUP_AGE_RECIPIENT`, performing an encrypted database backup before migrations. Image rollback does **not** roll back the DB. See `docs/operations/RESTORE_DRILL.md` and `docs/stages/S01_EXECUTION.md`.

An explicit code rollback requires `CONFIRM_ROLLBACK=YES ./deploy/scripts/rollback.sh`. It uses the locally recorded previous image, runs both health probes, and restores the pre-rollback image if the candidate is unhealthy. Database migrations remain forward-only.

For monitoring, create `/opt/hokmnameh/.monitoring.env` owned by `hokmdeploy` with mode `0600`, containing only `HOKM_ENVIRONMENT=staging|production` and an HTTPS `HOKM_ALERT_WEBHOOK_URL`. Install the supplied systemd service/timer only after a test alert is observed by the authorized receiver.

## Stage S01 deployment state

This repository contains implementation scripts and CI tests, but the VPS has **not** been accessed or modified by the source-authoring environment. Existing Nginx virtual-host config, TLS and SSH must be inspected on the actual server. See `docs/operations/S01_SERVER_RUNBOOK.md`.

## After boot

```bash
cd /opt/hokmnameh
docker compose ps
docker compose logs -n 80 api
./deploy/scripts/api-smoke.sh
```

## Before real public users

- Configure HTTPS, secret rotation, automated encrypted PostgreSQL backups, log shipping, structured audit logging, authenticated CI/CD and signed releases.
- Move match state into durable match workers / event log, design restart + reconnect recovery and failover.
- Abuse-resistant auth and guest onboarding; distributed rate limiting and WAF where necessary.
- Monitor endpoints, load test, central exception alerting, data retention, incident response.
- Do not publicly announce the service just because `/health` returns 200.

## S02 audit key for room creation

Generate a unique audit encryption key for each environment with `openssl rand -base64 32` and add it as `HOKM_AUDIT_KEY_BASE64` to the protected `.env` (mode 0600, owned by the deployment user). The deployment preflight rejects a missing or malformed key. Preserve it in secure off-host custody: losing it makes sealed round proofs undecryptable. Rotate only with a reviewed key-version migration and proof re-encryption; never commit or print the key. Apply the versioned migration before enabling room traffic. No production migration or deployment has been performed by this change.
