#!/usr/bin/env bash
set -euo pipefail
cd "${1:-$HOME/hokmnameh}"
git fetch origin main
git switch main
git pull --ff-only origin main
git ls-files 'docs/planning/*' 'AGENTS.md'
