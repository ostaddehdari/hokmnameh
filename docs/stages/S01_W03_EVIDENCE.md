# S01-W03 evidence — migrations, backup/restore and health controls

This record maps the three tasks to executable evidence. The final GitHub Actions run and source commit are recorded in `deployment-center`; CI evidence is not represented as VPS production execution.

| Task | Implemented control | Executable evidence |
|---|---|---|
| S01-W03-T01 | Contiguous migration manifest, immutable checksums, host + database serialization | `node tools/verify-s01-operations.mjs`; CI applies twice then proves an edited migration is rejected |
| S01-W03-T02 | Streamed `pg_dump` → `age`, SHA-256 + metadata, isolated restore | CI restores into a second `--network none` PostgreSQL and validates tables, migration ledger and duration |
| S01-W03-T03 | Guest, authenticated-command and WebSocket rate policies; live/ready probes; delivered alert | CI proves guest requests 1–10 return 201, request 11 returns 429 with `Retry-After`; `node tools/test-health-monitor.mjs` proves database alert delivery |

## Local deterministic checks

```bash
bash -n deploy/scripts/*.sh
node tools/verify-s01-operations.mjs
node tools/test-health-monitor.mjs
node 0000/change-control/verify-change-control.mjs --self-test
```

## Boundaries

- Restore evidence uses a separate disposable PostgreSQL service on a GitHub-hosted runner, not the source database and not the VPS.
- No production database, secret, Nginx configuration, systemd unit or external alert receiver is changed by this Work.
- Production remains blocked until an authorized operator verifies off-host backup custody, installs the timer and observes a real receiver test.
