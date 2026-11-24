# S01 — Safe VPS operator runbook

Public endpoint requested: `https://srun.ir/hokmnameh`. Host address, SSH port, DNS, owner identity and existing services must be verified from the private inventory before changes. Never commit or paste a root password or private SSH key.

## First inspection (no destructive commands)
```bash
# Connect using the verified private inventory and a key-authenticated admin account.
id; hostnamectl; df -h; free -h
command -v docker; docker compose version
sudo nginx -t
sudo nginx -T | sed -n '/server_name srun.ir/,/}/p'  # inspect carefully; not a full vhost parser
ss -ltnp | grep -E ':(80|443|5080|5432|6379)\\b' || true
```

**Do not** overwrite `/etc/nginx/sites-available/default` or another vhost. Save a timestamped copy of any Nginx file before editing. The provided `deploy/nginx/hokmnameh-location.conf` is only a snippet to include inside the existing `server {}` for HTTPS. Run `nginx -t` before reload, check other sites afterward.

## Host hardening
Create the dedicated `hokmdeploy` user with key-only SSH and rootless Docker. Do not add it to the rootful `docker` group. Apply `deploy/ssh/sshd_config.hokmnameh.example` only after reviewing the full host config, proving a second key-authenticated admin session and running `sshd -t`; otherwise stop. Nginx remains an independently authorized admin action. Ensure port 5080 binds only to localhost; never publish PostgreSQL 5432 or Valkey 6379.

## First staging boot
```bash
# An administrator provisions /opt/hokmnameh once; all following commands run as hokmdeploy.
git clone https://github.com/ostaddehdari/hokmnameh.git /opt/hokmnameh
cd /opt/hokmnameh
cp .env.example .env
chmod 600 .env
# Set random password, never the example; prevent world-readable files.
./deploy/scripts/preflight-host.sh
DEPLOY_ENV=staging ./deploy/scripts/deploy.sh
./deploy/scripts/api-smoke.sh
```

## Mandatory checks
```bash
docker compose ps
docker compose logs --tail=80 api  # inspect for accidentally logged secrets; do not share raw logs
curl -fS http://127.0.0.1:5080/hokmnameh/health/ready
curl -fS https://srun.ir/hokmnameh/health/ready
sudo nginx -t
```

## Backup and rollback
- Set `HOKM_BACKUP_AGE_RECIPIENT` from an external secret manager and execute `./deploy/scripts/backup.sh`; copy encrypted file off-host.
- Set `AGE_IDENTITY_FILE` privately and run `./deploy/scripts/restore-drill.sh /path/to/file.dump.age` (isolated disposable database).
- App-code rollback is attempted automatically on failed deployment readiness. For an operator drill, run `CONFIRM_ROLLBACK=YES ./deploy/scripts/rollback.sh`; it must pass live/ready checks and restores the current image if the rollback candidate fails. Database schema updates are **forward-only**; use additive migrations and forward fixes. Never run `docker compose down -v` on production.

## Staging evidence gate

Record only redacted evidence: source commit, `docker compose config --quiet`, image UID/GID, localhost/public health status and rollback result. Do not mark production deployment complete from source CI alone. A failed preflight, unavailable previous image or unhealthy rollback is a hard stop.

## Monitoring
Install systemd health timer templates in `deploy/systemd` only after making sure user paths/permissions are valid. The timer logs failures to journald but **does not send an alert until a monitoring receiver is wired**. Verify the receiver and a failure drill, then enable it.
