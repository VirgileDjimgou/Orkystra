#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

if [ ! -f .env ]; then
  cp .env.example .env
fi

for key in MSSQL_SA_PASSWORD MINIO_ROOT_USER MINIO_ROOT_PASSWORD MINIO_ACCESS_KEY MINIO_SECRET_KEY MINIO_KMS_SECRET_KEY JWT_SIGNING_KEY MEDIA_SIGNING_KEY INTERNAL_API_KEY; do
  value="$(grep -E "^${key}=" .env | cut -d= -f2- || true)"
  if [ -z "$value" ]; then
    echo "Missing required hosted Demo setting ${key} in .env. Copy the missing values from .env.example." >&2
    exit 2
  fi
done
internal_key="$(grep -E '^INTERNAL_API_KEY=' .env | cut -d= -f2- || true)"
if [ "${#internal_key}" -lt 32 ]; then
  echo "Set INTERNAL_API_KEY in .env to an independent secret of at least 32 characters before starting the hosted Demo." >&2
  exit 2
fi

docker compose --env-file .env -f docker-compose.yml -f docker-compose.pilot.yml -f docker-compose.demo.yml up -d --build
echo "FleetOps hosted Demo is starting. Validate it with: pwsh -File scripts/demo-smoke.ps1 -SkipBuild"
