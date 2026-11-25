#!/usr/bin/env bash
# Restore into a separate disposable container, never into the live database.
set -euo pipefail
[ "$#" -eq 1 ] || { echo 'Usage: AGE_IDENTITY_FILE=/secure/path/identity ./deploy/scripts/restore-drill.sh backup.dump.age' >&2; exit 1; }
backup="$1"
[ -f "$backup" ] || { echo "No backup at $backup" >&2; exit 1; }
[ -f "${AGE_IDENTITY_FILE:-/dev/null}" ] || { echo 'AGE_IDENTITY_FILE is required' >&2; exit 1; }
command -v docker >/dev/null || { echo 'Docker required' >&2; exit 1; }
command -v age >/dev/null || { echo 'age required' >&2; exit 1; }
command -v sha256sum >/dev/null || { echo 'sha256sum required' >&2; exit 1; }
command -v openssl >/dev/null || { echo 'openssl required' >&2; exit 1; }
[ -f "$backup.sha256" ] || { echo 'Backup checksum sidecar is required' >&2; exit 1; }
(
  cd "$(dirname "$backup")"
  sha256sum -c "$(basename "$backup.sha256")" >/dev/null
) || { echo 'Encrypted backup checksum verification failed' >&2; exit 1; }
started="$(date +%s)"
name="hokm-restore-drill-$$"
pass="$(openssl rand -hex 32)"
trap 'docker rm -f "$name" >/dev/null 2>&1 || true' EXIT
docker run -d --rm --name "$name" --network none -e "POSTGRES_PASSWORD=$pass" postgres:18-alpine >/dev/null
ready=false
for i in $(seq 1 60); do
  if docker exec "$name" pg_isready -U postgres >/dev/null 2>&1; then ready=true; break; fi
  sleep 2
done
[ "$ready" = true ] || { echo 'Restore PostgreSQL did not become ready' >&2; exit 1; }
docker exec "$name" createdb -U postgres hokm_restore
age -d -i "$AGE_IDENTITY_FILE" "$backup" | docker exec -i "$name" pg_restore -U postgres -d hokm_restore --no-owner --no-acl --exit-on-error
tables="$(docker exec "$name" psql -X -Atq -U postgres -d hokm_restore -c "SELECT count(*) FROM information_schema.tables WHERE table_schema='public'" | tr -d '\r')"
[ "$tables" -ge 5 ] || { echo "Restored only $tables tables; expected baseline schema" >&2; exit 1; }
migrations="$(docker exec "$name" psql -X -Atq -U postgres -d hokm_restore -c 'SELECT count(*) FROM public.schema_migrations' | tr -d '\r')"
[ "$migrations" -ge 1 ] || { echo 'Restored migration ledger is empty' >&2; exit 1; }
invalid_checksums="$(docker exec "$name" psql -X -Atq -U postgres -d hokm_restore -c "SELECT count(*) FROM public.schema_migrations WHERE sha256 !~ '^[0-9a-f]{64}$'" | tr -d '\r')"
[ "$invalid_checksums" -eq 0 ] || { echo 'Restored migration ledger has invalid checksums' >&2; exit 1; }
duration="$(( $(date +%s) - started ))"
echo "RESTORE_DRILL PASS tables=$tables migrations=$migrations duration_seconds=$duration isolated=true"
