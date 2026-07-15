#!/usr/bin/env bash
set -euo pipefail
[[ -f .env ]] || cp .env.example .env
docker compose --env-file .env up -d
echo "Infrastructure FleetOps démarrée."
