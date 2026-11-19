#!/usr/bin/env bash
set -euo pipefail
curl -fsS http://127.0.0.1:5080/hokmnameh/health/live
printf '\n'
curl -fsS http://127.0.0.1:5080/hokmnameh/health/ready
printf '\n'
