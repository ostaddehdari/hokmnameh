#!/usr/bin/env bash
set -euo pipefail
BASE="${BASE:-http://127.0.0.1:5080/hokmnameh}"
ATTEMPTS="${ATTEMPTS:-30}"
for ((i=1;i<=ATTEMPTS;i++)); do
  if curl -fsS --max-time 4 "$BASE/health/live" >/dev/null 2>&1 && \
     curl -fsS --max-time 4 "$BASE/health/ready" >/dev/null 2>&1; then
    printf 'API ready: %s\n' "$BASE"
    exit 0
  fi
  sleep 2
done
echo "API not ready after $ATTEMPTS attempts: $BASE" >&2
exit 1
