# VPS deployment — operator checklist (NOT YET PERFORMED)

Target supplied by project owner:
- SSH `root@5.61.27.185` port `2233`.
- HTTPS path `https://srun.ir/hokmnameh` (requires DNS, TLS and existing web server config verification).
- Repo `https://github.com/ostaddehdari/hokmnameh`.

**Current operator note:** GitHub source has been committed using the connected GitHub integration. Direct SSH from this execution environment to port `2233` still returns `Connection refused`; the VPS has NOT been modified. Direct `git` clone from this environment has DNS restrictions, but cloning from the VPS may work. Do NOT assume any existing server site has been modified.

## Prerequisites — on a trusted admin workstation

1. Verify domain DNS points to the intended server, and inspect current web server sites. Confirm existing service before adding route.
2. Verify SSH through VPN/firewall/security-group first. Do not share root passwords or SSH private keys in chat. Prefer a dedicated limited `deploy` user and SSH key, then `sudo` only for required tasks.
3. Install Docker Engine + Compose plugin from official distribution on your Linux host, and confirm sufficient disk/RAM and UFW/firewall status.
4. Put this repository at `/opt/hokmnameh`. Use deploy SSH key **on the server**, not embedded in the repository. Clone after GitHub push.
5. Generate a secure database password using `openssl rand -base64 48` and set it only in `/opt/hokmnameh/.env` (chmod 600). Do NOT commit `.env`.

```bash
# On the VPS AFTER GitHub main branch exists and you have authenticated repo access
sudo mkdir -p /opt/hokmnameh
sudo chown "$(id -u):$(id -g)" /opt/hokmnameh
# If repository was initialized and pushed:
git clone https://github.com/ostaddehdari/hokmnameh.git /opt/hokmnameh
cd /opt/hokmnameh
cp .env.example .env
# Edit .env using a secure editor. Never reuse sample password.
chmod 600 .env
./deploy/scripts/deploy.sh
```

## Nginx routing

Open the **existing HTTPS srun.ir vhost**, then include `deploy/nginx/hokmnameh-location.conf` inside its `server {}` block. Do not replace its current config or use the snippet as a whole vhost. Before reload:

```bash
sudo nginx -t && sudo systemctl reload nginx
curl -fsS https://srun.ir/hokmnameh/health/live
curl -fsS https://srun.ir/hokmnameh/health/ready
```

Proxy must preserve `/hokmnameh` prefix because ASP.NET maps route group there. HTTP-to-HTTPS redirects and valid TLS certificates must already be in place. WebSocket upgrade headers are included in the snippet.

## Versioned database migrations — S01

`deploy/scripts/migrate.sh` applies `backend/db/migrations/NNN_name.sql` through an advisory-locked transaction and records the SHA-256 in `public.schema_migrations`. It refuses a modified previously applied migration; add the next numbered file instead. `001_core.sql` is additive and idempotent for compatibility with earlier prototype volumes. A PostgreSQL 18 volume must not be deleted to rerun migrations. The old Docker `docker-entrypoint-initdb.d` mount has been removed.

`DEPLOY_ENV=staging ./deploy/scripts/deploy.sh` is operator-initiated. Production additionally requires `CONFIRM_PRODUCTION_DEPLOY=YES` and `HOKM_BACKUP_AGE_RECIPIENT`, performing an encrypted database backup before migrations. Image rollback does **not** roll back the DB. See `docs/operations/RESTORE_DRILL.md` and `docs/stages/S01_EXECUTION.md`.

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
