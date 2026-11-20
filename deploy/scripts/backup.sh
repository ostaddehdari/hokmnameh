#!/usr/bin/env bash
# Encrypt a consistent custom-format PostgreSQL dump with age before saving to disk.
set -euo pipefail
cd "$(dirname "$0")/../.."
command -v age >/dev/null || { echo 'age required for encrypted backups' >&2; exit 1; }
command -v docker >/dev/null || { echo 'Docker required' >&2; exit 1; }
[ -n "${HOKM_BACKUP_AGE_RECIPIENT:-}" ] || { echo 'HOKM_BACKUP_AGE_RECIPIENT not configured' >&2; exit 1; }
BACKUP_DIR="${HOKM_BACKUP_DIR:-/var/backups/hokmnameh}"
umask 077
mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"
file="$BACKUP_DIR/pg-$(date -u +%Y%m%dT%H%M%SZ).dump.age"
partial="${file}.part"
trap 'rm -f "$partial"' EXIT
# pg_dump includes schema and data; do not place a plaintext dump on disk.
set -o pipefail
docker compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom --no-owner --no-acl' | age -r "$HOKM_BACKUP_AGE_RECIPIENT" -o "$partial"
[ -s "$partial" ] || { echo 'Backup empty' >&2; exit 1; }
mv "$partial" "$file"
sha256sum "$file" > "$file.sha256"
chmod 600 "$file" "$file.sha256"
echo "Encrypted backup stored: $file"
echo 'A restore drill and off-host copy are mandatory before the production gate.'
