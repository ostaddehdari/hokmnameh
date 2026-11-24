#!/usr/bin/env bash
# Fail-closed host checks. This script makes no host changes.
set -euo pipefail
cd "$(dirname "$0")/../.."

[ "$(id -u)" -ne 0 ] || { echo 'Refusing deployment as root' >&2; exit 1; }
[ "$(id -un)" = 'hokmdeploy' ] || { echo 'Deployment must run as hokmdeploy' >&2; exit 1; }
command -v docker >/dev/null || { echo 'Docker required' >&2; exit 1; }
docker compose version >/dev/null
docker info --format '{{json .SecurityOptions}}' | grep -q 'rootless' || {
  echo 'Rootless Docker is required; docker-group access is root-equivalent' >&2
  exit 1
}
[ -f .env ] || { echo 'Missing protected .env' >&2; exit 1; }
[ ! -L .env ] || { echo '.env must not be a symbolic link' >&2; exit 1; }
[ "$(stat -c '%a' .env)" = '600' ] || { echo '.env mode must be 0600' >&2; exit 1; }
[ "$(stat -c '%U' .env)" = 'hokmdeploy' ] || { echo '.env owner must be hokmdeploy' >&2; exit 1; }
if grep -q 'CHANGE_TO_A_LONG_RANDOM_PASSWORD' .env; then
  echo 'Refusing sample database password' >&2
  exit 1
fi

docker compose config --quiet
printf 'Host preflight passed for user %s.\n' "$(id -un)"
