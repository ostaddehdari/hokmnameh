#!/usr/bin/env bash
# Operator-controlled deployment; no CI / PR event can reach production.
set -euo pipefail
cd "$(dirname "$0")/../.."
DEPLOY_ENV="${DEPLOY_ENV:-staging}"
case "$DEPLOY_ENV" in staging|production) ;; *) echo 'DEPLOY_ENV must be staging or production' >&2; exit 1 ;; esac
command -v docker >/dev/null || { echo 'Docker required' >&2; exit 1; }
command -v curl >/dev/null || { echo 'curl required' >&2; exit 1; }
[ -f .env ] || { echo 'Create a protected .env' >&2; exit 1; }
./deploy/scripts/preflight-host.sh
if grep -q 'CHANGE_TO_A_LONG_RANDOM_PASSWORD' .env; then
  echo 'Refusing sample database password' >&2; exit 1
fi
if [ "$DEPLOY_ENV" = production ]; then
  [ "${CONFIRM_PRODUCTION_DEPLOY:-}" = 'YES' ] || { echo 'Production deployment requires CONFIRM_PRODUCTION_DEPLOY=YES and an approved change window' >&2; exit 1; }
  [ -n "${HOKM_BACKUP_AGE_RECIPIENT:-}" ] || { echo 'Production deployment requires HOKM_BACKUP_AGE_RECIPIENT' >&2; exit 1; }
fi
docker compose config --quiet
# Capture previously running image for a code rollback; data migrations are forward-only.
previous_id=''
previous_container="$(docker compose ps -q api || true)"
if [ -n "$previous_container" ]; then previous_id="$(docker inspect -f '{{.Image}}' "$previous_container" 2>/dev/null || true)"; fi
rollback_image=''
if [ -n "$previous_id" ]; then
  rollback_image="hokmnameh-api:rollback-$(date -u +%Y%m%d%H%M%S)"
  docker image tag "$previous_id" "$rollback_image"
fi
if [ "$DEPLOY_ENV" = production ]; then
  ./deploy/scripts/backup.sh
fi
./deploy/scripts/migrate.sh
docker compose build api
docker compose up -d --no-build api
if ./deploy/scripts/check.sh; then
  if [ -n "$rollback_image" ]; then
    umask 077
    mkdir -p .deploy
    chmod 700 .deploy
    printf '%s\n' "$rollback_image" > .deploy/rollback-image
    chmod 600 .deploy/rollback-image
  fi
  echo "Deployment successful ($DEPLOY_ENV)."
else
  echo 'API readiness failed; attempting code-only rollback' >&2
  if [ -n "$rollback_image" ]; then
    HOKM_API_IMAGE="$rollback_image" docker compose up -d --no-build --pull never api || true
    ./deploy/scripts/check.sh || true
  fi
  echo 'Database migrations are NOT reversed; investigate before retrying.' >&2
  exit 1
fi
