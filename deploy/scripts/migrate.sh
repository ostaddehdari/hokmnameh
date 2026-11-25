#!/usr/bin/env bash
# Apply each migration once, preserving a checksum and a serialized transaction.
set -euo pipefail
cd "$(dirname "$0")/../.."
command -v docker >/dev/null || { echo 'Docker required' >&2; exit 1; }
command -v sha256sum >/dev/null || { echo 'sha256sum required' >&2; exit 1; }
command -v flock >/dev/null || { echo 'flock required (util-linux)' >&2; exit 1; }
[ -f .env ] || { echo 'Missing .env' >&2; exit 1; }
# One migration runner per installation. The database transaction also uses an advisory lock.
exec 9>.migration.lock
flock -n 9 || { echo 'Another migration runner is active' >&2; exit 1; }
shopt -s nullglob
paths=(backend/db/migrations/[0-9][0-9][0-9]_*.sql)
[ "${#paths[@]}" -gt 0 ] || { echo 'No versioned migrations found' >&2; exit 1; }
expected=1
for path in "${paths[@]}"; do
  version="$(basename "$path" .sql)"
  [[ "$version" =~ ^[0-9]{3}_[A-Za-z0-9_-]+$ ]] || { echo "Invalid migration name: $version" >&2; exit 1; }
  raw_number="${version%%_*}"
  number=$((10#$raw_number))
  [ "$number" -eq "$expected" ] || { echo "Migration sequence gap: expected $(printf '%03d' "$expected"), found $raw_number" >&2; exit 1; }
  expected=$((expected + 1))
done
docker compose up -d --wait postgres
pg() { docker compose exec -T postgres sh -c 'exec psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" "$@"' sh "$@"; }
printf '%s\n' 'CREATE TABLE IF NOT EXISTS public.schema_migrations (version text PRIMARY KEY, sha256 text NOT NULL, applied_at timestamptz NOT NULL DEFAULT now());' | pg >/dev/null
for path in "${paths[@]}"; do
  version="$(basename "$path" .sql)"
  checksum="$(sha256sum "$path" | cut -d' ' -f1)"
  existing="$(printf 'SELECT sha256 FROM public.schema_migrations WHERE version = '\''%s'\'';\n' "$version" | pg -Atq | tr -d '\r')"
  if [ -n "$existing" ]; then
    [ "$existing" = "$checksum" ] || { echo "Migration checksum mismatch for $version; do not edit an applied migration" >&2; exit 1; }
    echo "SKIP: $version already applied"
    continue
  fi
  echo "APPLY: $version"
  {
    echo 'BEGIN;'
    echo "SELECT pg_advisory_xact_lock(562001);"
    # If a second operator raced from a different host, fail closed rather than applying twice.
    printf "DO \$\$ BEGIN IF EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '%s') THEN RAISE EXCEPTION 'Migration already applied by another operator'; END IF; END \$\$;\n" "$version"
    printf '\\i /migrations/%s.sql\n' "$version"
    printf "INSERT INTO public.schema_migrations(version,sha256) VALUES ('%s','%s');\n" "$version" "$checksum"
    echo 'COMMIT;'
  } | pg >/dev/null
  echo "OK: $version"
done
