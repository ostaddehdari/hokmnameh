#!/usr/bin/env bash
# Suitable for cron/systemd timer; nonzero exit + journal log for alerting.
set -euo pipefail
BASE="${BASE:-http://127.0.0.1:5080/hokmnameh}"
curl --fail --silent --show-error --max-time 5 "$BASE/health/ready" >/dev/null || {
  echo "HOKMNAMEH_ALERT: API readiness failed at $(date -u +%FT%TZ)" >&2
  exit 1
}
