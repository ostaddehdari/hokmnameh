# S01-W02 — VPS security evidence

Status: source implementation complete; no VPS mutation claimed.

## Acceptance mapping

| Task | Acceptance | Evidence |
|---|---|---|
| S01-W02-T01 | API has an internal host port; PostgreSQL/Valkey are not public | `deploy/security-contract.json`; Compose publishes only `127.0.0.1:5080:8080`; PostgreSQL has no `ports`; Valkey is not deployed and its future contract is internal-only |
| S01-W02-T02 | key-based SSH, least privilege, no root application process | SSH hardening example; read-only `preflight-host.sh`; rootless Docker requirement; API UID/GID `10001:10001`; systemd health probe runs as `hokmdeploy` |
| S01-W02-T03 | release and rollback are documented and fail closed | `deploy.sh`, explicit `rollback.sh`, readiness verification, failed-rollback recovery, forward-only database statement and operator runbook |

## Deterministic checks

Run from the repository root:

```bash
bash -n deploy/scripts/*.sh
POSTGRES_PASSWORD=contract-validation-only docker compose config --quiet
node tools/verify-vps-security-contract.mjs
node 0000/change-control/verify-change-control.mjs --self-test
```

CI additionally builds the API image and asserts that its configured runtime user is exactly `10001:10001`.

## Operational boundary

This Work creates and verifies the deployment contract, scripts and runbook. It does not prove SSH access, change the existing Nginx virtual host, install rootless Docker, deploy a container or run a staging rollback drill on the actual VPS. Those claims require an authorized runner and redacted host evidence. Source CI alone must never be represented as a production deployment.
