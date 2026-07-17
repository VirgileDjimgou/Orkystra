# Pilot Runbook

## Purpose

This runbook defines the minimum operating routine for a FleetOps pilot environment.

## Services

- `sqlserver`: transactional source of truth.
- `api`: ASP.NET Core application exposing `/health` and `/health/ready`.
- `worker`: alert scan and webhook delivery background processing.
- `web`: operator and administrator console.
- `minio`, `mosquitto`, `mailpit`: supporting services for documents, edge simulation, and development mail traces.

## Start the pilot stack

1. Copy `.env.example` to `.env`.
2. Replace `JWT_SIGNING_KEY` and `MEDIA_SIGNING_KEY` with two independent random values of at least 32 characters.
3. For an empty database, set the bootstrap organization name/slug and temporary administrator credentials. The application refuses to seed demo accounts in Production.
4. Restrict access to `.env`, start the stack, change the temporary password, and enable MFA.

```powershell
Copy-Item .env.example .env
docker compose --env-file .env -f docker-compose.yml -f docker-compose.pilot.yml up -d --build
```

The API fails fast when the Production connection string or signing keys are missing, known development values, or otherwise unsafe.

## Readiness checks

- Confirm `docker compose ps` reports all containers as running.
- Confirm `http://localhost:5080/health` returns `200`.
- Confirm `http://localhost:5080/health/ready` returns `200`.
- Confirm `http://localhost:8081` loads the web shell.

## MFA baseline

- Sign in as an administrator.
- Open `Security & data`.
- Generate an authenticator secret, verify a code, and store the recovery codes offline.

## Observability baseline

- Configure `OTEL_EXPORTER_OTLP_ENDPOINT` in `.env` when a collector is available.
- API and worker logs are emitted as JSON for ingestion by the collector or host log pipeline.
- Exclude `/health` probes from high-signal tracing dashboards.

## Backup and restore

Create a backup:

```powershell
./scripts/sql-backup.ps1
```

Restore a backup:

```powershell
./scripts/sql-restore.ps1 -InputPath backups/fleetops-YYYYMMDD-HHMMSS.bak
```

## Incident checklist

1. Freeze destructive admin actions if tenant integrity is uncertain.
2. Capture `docker compose logs api worker`.
3. Export tenant data from `Security & data` before manual cleanup.
4. Restore SQL from the latest verified backup when integrity cannot be recovered safely in place.
5. Re-run the health endpoints and the sprint demo path after restoration.
