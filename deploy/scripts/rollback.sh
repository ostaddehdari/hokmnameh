#!/usr/bin/env bash
# Operator-controlled, code-image-only rollback. Database migrations are forward-only.
set -euo pipefail
cd "$(dirname "$0")/../.."

[ "${CONFIRM_ROLLBACK:-}" = 'YES' ] || {
  echo 'Rollback requires CONFIRM_ROLLBACK=YES' >&2
  exit 1
}
./deploy/scripts/preflight-host.sh

rollback_image="${HOKM_ROLLBACK_IMAGE:-}"
if [ -z "$rollback_image" ] && [ -f .deploy/rollback-image ]; then
  rollback_image="$(cat .deploy/rollback-image)"
fi
[ -n "$rollback_image" ] || { echo 'No rollback image was supplied or recorded' >&2; exit 1; }
docker image inspect "$rollback_image" >/dev/null 2>&1 || {
  echo "Rollback image is unavailable locally: $rollback_image" >&2
  exit 1
}

current_container="$(docker compose ps -q api || true)"
current_id=''
recovery_image=''
if [ -n "$current_container" ]; then
  current_id="$(docker inspect -f '{{.Image}}' "$current_container")"
  recovery_image="hokmnameh-api:recovery-$(date -u +%Y%m%d%H%M%S)"
  docker image tag "$current_id" "$recovery_image"
fi

HOKM_API_IMAGE="$rollback_image" docker compose up -d --no-build --pull never api
if ./deploy/scripts/check.sh; then
  umask 077
  mkdir -p .deploy
  chmod 700 .deploy
  if [ -n "$recovery_image" ]; then
    printf '%s\n' "$recovery_image" > .deploy/rollback-image
    chmod 600 .deploy/rollback-image
  fi
  printf 'Code rollback succeeded: %s\n' "$rollback_image"
  exit 0
fi

echo 'Rollback image failed readiness; restoring the previously running image' >&2
if [ -n "$recovery_image" ]; then
  HOKM_API_IMAGE="$recovery_image" docker compose up -d --no-build --pull never api || true
  ./deploy/scripts/check.sh || true
fi
echo 'No database migration was reversed.' >&2
exit 1
