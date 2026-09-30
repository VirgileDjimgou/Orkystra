#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

docker compose --env-file .env -f docker-compose.yml -f docker-compose.pilot.yml -f docker-compose.demo.yml down "$@"
echo "FleetOps hosted Demo stopped."
