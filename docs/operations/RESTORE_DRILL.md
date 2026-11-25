# PostgreSQL backup & restore verification — S01-W03-T02

**Storage policy:** backups are encrypted with `age` during streaming; neither raw dumps nor identities are committed to git. Every encrypted dump has a SHA-256 sidecar and redacted metadata record. The `HOKM_BACKUP_AGE_RECIPIENT` is a public age recipient set in the server's protected environment. Backups must also be copied off-host over a verified secure channel and tested regularly.

```bash
# On the authorized host, with Compose database up:
export HOKM_BACKUP_AGE_RECIPIENT='age1...'
./deploy/scripts/backup.sh

# Restore verification runs in a temporary isolated container only:
export AGE_IDENTITY_FILE=/secure/off-host/age-identity.txt
./deploy/scripts/restore-drill.sh /var/backups/hokmnameh/pg-YYYYmmddTHHMMSSZ.dump.age
```

The drill verifies the encrypted-file checksum before decryption, restores into a separate `--network none` PostgreSQL container, validates at least five public tables, validates the migration ledger and prints the measured duration. GitHub CI performs this drill with an ephemeral key and a second disposable PostgreSQL; that proves the artifact path, not production retention or off-host custody.

Add selected-record checks, defined RPO/RTO targets and a real disaster-recovery exercise before public launch. Confirm retention, recipient rotation and independent off-host access. Never test restore against the live `hokmnameh` DB. If the drill cannot run, keep the production gate blocked.
