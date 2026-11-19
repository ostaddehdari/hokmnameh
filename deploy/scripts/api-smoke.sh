#!/usr/bin/env bash
set -euo pipefail
BASE="${BASE:-http://127.0.0.1:5080/hokmnameh}"
command -v jq >/dev/null || { echo 'jq required' >&2; exit 1; }
curl -fsS "$BASE/health/live" | jq .
GUEST="$(curl -fsS -X POST "$BASE/api/v1/identity/guest")"
TOKEN="$(echo "$GUEST" | jq -r '.accessToken')"
test -n "$TOKEN" && test "$TOKEN" != null
ME="$(curl -fsS -H "Authorization: Bearer $TOKEN" "$BASE/api/v1/identity/me")"
echo "$ME" | jq .
ROOM="$(curl -fsS -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{"mode":"practice"}' "$BASE/api/v1/rooms")"
echo "$ROOM" | jq .
ROOM_ID="$(echo "$ROOM" | jq -r '.room.id')"
curl -fsS -H "Authorization: Bearer $TOKEN" "$BASE/api/v1/rooms/$ROOM_ID" | jq .
echo 'Smoke API calls passed. Do not log tokens outside local test terminals.'
