#!/usr/bin/env bash
# Restore into a separate disposable container, never into the live database.
set -euo pipefail
[ "$#" -eq 1 ] || { echo 'Usage: AGE_IDENTITY_FILE=/secure/path/identity ./deploy/scripts/restore-drill.sh backup.dump.age' >&2; exit 1; }
backup="$1"
[ -f "$backup" ] || { echo "No backup at $backup" >&2; exit 1; }
[ -f "${AGE_IDENTITY_FILE:-/dev/null}" ] || { echo 'AGE_IDENTITY_FILE is required' >&2; exit 1; }
command -v docker >/dev/null || { echo 'Docker required' >&2; exit 1; }
command -v age >/dev/null || { echo 'age required' >&2; exit 1; }
name="hokm-restore-drill-$$"
pass="$(openssl rand -hex 32)"
trap 'docker rm -f "$name" >/dev/null 2>&1 || true' EXIT
docker run -d --rm --name "$name" --network none -e "POSTGRES_PASSWORD=$pass" postgres:18-alpine >/dev/null
for i in $(seq 1 60); do
  if docker exec "$name" pg_isready -U postgres >/dev/null 2>&1; then break; fi
  sleep 2
done
docker exec "$name" createdb -U postgres hokm_restore
age -d -i "$AGE_IDENTITY_FILE" "$backup" | docker exec -i "$name" pg_restore -U postgres -d hokm_restore --no-owner --no-acl --exit-on-error
tables="$(docker exec "$name" psql -X -Atq -U postgres -d hokm_restore -c "SELECT count(*) FROM information_schema.tables WHERE table_schema='public'" | tr -d '\r')"
[ "$tables" -ge 5 ] || { echo "Restored only $tables tables; expected baseline schema" >&2; exit 1; }
echo "Restore drill successful: $tables public tables in isolated disposable PostgreSQL."
