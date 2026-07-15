$ErrorActionPreference = "Stop"
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
docker compose --env-file .env up -d
Write-Host "Infrastructure FleetOps démarrée."
