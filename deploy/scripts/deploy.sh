#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
command -v docker >/dev/null || { echo "Docker not installed" >&2; exit 1; }
test -f .env || { echo "Create .env from .env.example with a strong POSTGRES_PASSWORD" >&2; exit 1; }
if grep -q 'CHANGE_TO_A_LONG_RANDOM_PASSWORD' .env; then
  echo "Refusing default database password" >&2; exit 1
fi
docker compose config --quiet
docker compose up -d --build
docker compose ps
printf '\nLOCAL HEALTH CHECK:\n'
curl --fail --silent --show-error http://127.0.0.1:5080/hokmnameh/health/live
printf '\nDATABASE CHECK:\n'
curl --fail --silent --show-error http://127.0.0.1:5080/hokmnameh/health/ready
printf '\n'
