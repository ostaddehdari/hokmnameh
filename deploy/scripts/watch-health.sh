#!/usr/bin/env bash
# Suitable for cron/systemd timer; delivers a redacted alert on failure.
set -euo pipefail
BASE="${BASE:-http://127.0.0.1:5080/hokmnameh}"
ENVIRONMENT="${HOKM_ENVIRONMENT:-unknown}"
[[ "$ENVIRONMENT" =~ ^[a-z0-9_-]{1,32}$ ]] || { echo 'Invalid HOKM_ENVIRONMENT' >&2; exit 2; }

check='api-liveness'
status='failed'
if curl --fail --silent --show-error --max-time 5 "$BASE/health/live" >/dev/null; then
  check='database-readiness'
  if curl --fail --silent --show-error --max-time 5 "$BASE/health/ready" >/dev/null; then
    exit 0
  fi
fi

webhook="${HOKM_ALERT_WEBHOOK_URL:-}"
[ -n "$webhook" ] || { echo "HOKMNAMEH_ALERT_UNDELIVERED check=$check reason=receiver-missing" >&2; exit 2; }
[[ "$webhook" == https://* || "$webhook" == http://127.0.0.1:* ]] || {
  echo "HOKMNAMEH_ALERT_UNDELIVERED check=$check reason=receiver-policy" >&2
  exit 2
}
occurred_at="$(date -u +%FT%TZ)"
payload="$(printf '{\"service\":\"hokmnameh-api\",\"environment\":\"%s\",\"check\":\"%s\",\"status\":\"%s\",\"occurred_at\":\"%s\"}' "$ENVIRONMENT" "$check" "$status" "$occurred_at")"
if curl --fail --silent --show-error --max-time 5 -H 'Content-Type: application/json' --data "$payload" "$webhook" >/dev/null; then
  echo "HOKMNAMEH_ALERT_DELIVERED check=$check environment=$ENVIRONMENT" >&2
  exit 1
fi
echo "HOKMNAMEH_ALERT_UNDELIVERED check=$check reason=delivery-failed" >&2
exit 2
