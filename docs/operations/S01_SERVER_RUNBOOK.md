# S01 — Safe VPS operator runbook

Target requested: `ssh -p 2233 root@5.61.27.185` and `https://srun.ir/hokmnameh`. The port, DNS, owner identity and existing services must be VERIFIED on the host before changes. Never paste root password or private SSH key into ChatGPT.

## First inspection (no destructive commands)
```bash
ssh -p 2233 root@5.61.27.185
id; hostnamectl; df -h; free -h
command -v docker; docker compose version
sudo nginx -t
sudo nginx -T | sed -n '/server_name srun.ir/,/}/p'  # inspect carefully; not a full vhost parser
ss -ltnp | grep -E ':(2233|80|443|5080|5432)\\b' || true
```

**Do not** overwrite `/etc/nginx/sites-available/default` or another vhost. Save a timestamped copy of any Nginx file before editing. The provided `deploy/nginx/hokmnameh-location.conf` is only a snippet to include inside the existing `server {}` for HTTPS. Run `nginx -t` before reload, check other sites afterward.

## Host hardening
Create a dedicated user (e.g. `hokmdeploy`) with key-based SSH; restrict sudo commands to the deployment requirements and avoid persistent root sessions. Be aware that membership in `docker` group is effectively root-equivalent; consider rootless Docker or tightly controlled sudo and file permissions. Ensure port 5080 binds only to localhost; do not publish 5432 or Valkey. Check `ufw` and cloud security rules without disconnecting the SSH service.

## First staging boot
```bash
sudo mkdir -p /opt
# Clone as the designated deployment user, not root.
git clone https://github.com/ostaddehdari/hokmnameh.git /opt/hokmnameh
cd /opt/hokmnameh
cp .env.example .env
chmod 600 .env
# Set random password, never the example; prevent world-readable files.
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
- App-code rollback is attempted automatically on failed readiness if an old image exists. Database schema updates are **forward-only**; use additive migrations and forward fixes. Never run `docker compose down -v` on production.

## Monitoring
Install systemd health timer templates in `deploy/systemd` only after making sure user paths/permissions are valid. The timer logs failures to journald but **does not send an alert until a monitoring receiver is wired**. Verify the receiver and a failure drill, then enable it.
