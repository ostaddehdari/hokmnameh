#!/usr/bin/env bash
# Encrypt a consistent custom-format PostgreSQL dump with age before saving to disk.
set -euo pipefail
cd "$(dirname "$0")/../.."
command -v age >/dev/null || { echo 'age required for encrypted backups' >&2; exit 1; }
command -v docker >/dev/null || { echo 'Docker required' >&2; exit 1; }
command -v sha256sum >/dev/null || { echo 'sha256sum required' >&2; exit 1; }
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
checksum="$(sha256sum "$file" | cut -d' ' -f1)"
printf '%s  %s\n' "$checksum" "$(basename "$file")" > "$file.sha256"
cat > "$file.metadata.json" <<EOF
{
  "schema_version": "1.0",
  "created_at": "$(date -u +%FT%TZ)",
  "format": "postgres-custom-streamed-to-age",
  "encrypted_sha256": "$checksum",
  "source_service": "hokmnameh-postgres",
  "restore_required": true
}
EOF
chmod 600 "$file" "$file.sha256" "$file.metadata.json"
echo "Encrypted backup stored: $file"
echo 'A restore drill and off-host copy are mandatory before the production gate.'
