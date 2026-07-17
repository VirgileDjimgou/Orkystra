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
2. Replace `JWT_SIGNING_KEY`, `MEDIA_SIGNING_KEY`, MinIO credentials, and `MINIO_KMS_SECRET_KEY` with independent random values.
3. For an empty database, set the bootstrap organization name/slug and temporary administrator credentials. The application refuses to seed demo accounts in Production.
4. Restrict access to `.env`, start the stack, change the temporary password, and enable MFA.

```powershell
Copy-Item .env.example .env
docker compose --env-file .env -f docker-compose.yml -f docker-compose.pilot.yml up -d --build
```

The API fails fast when the Production connection string or signing keys are missing, known development values, or otherwise unsafe.

## Private media migration and rollback

Production uses the private `fleetops-private-media` S3 bucket. The bucket initializer keeps anonymous access disabled, and object writes request server-side encryption. Before switching an environment with existing filesystem media, keep the legacy volume mounted and run:

```powershell
docker compose --env-file .env -f docker-compose.yml -f docker-compose.pilot.yml run --rm worker --migrate-media
```

The JSON report records migrated, already migrated, and failed assets. Re-run the command until `errors` is zero, then exercise an authenticated proof download. Source files remain untouched for rollback. Restore `ObjectStorage__Provider=FileSystem` only while SQL metadata still references the legacy keys; after migration, rollback requires restoring the matching pre-migration SQL backup and filesystem volume together.

The worker revokes and deletes retained media only after its configured retention date. It also removes expired temporary, quarantined, and unpublished objects. Tenant export includes media checksums, retention dates, and revocation state; SQL backup and restore therefore recover the authoritative media manifest, while the object volume requires its own infrastructure backup policy.

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

## Alpha measurement routine

1. Before collecting pilot evidence, the tenant administrator opens **Administration > Pilot review**, informs the organization, and explicitly opts in to aggregate measurement.
2. Record one daily aggregate after the operating day. The snapshot is idempotent per tenant and UTC day: a repeated collection refreshes that day rather than creating another record.
3. Record P0/P1/P2 support incidents with a technical category, concise non-personal summary, and any validated workaround. Resolve the incident once the workaround is confirmed.
4. Before the weekly review, export the tenant’s pilot evidence package. It contains consent state, aggregate snapshots, incidents, and decisions; it does not include driver names, emails, raw locations, command payloads, or media.
5. Record only an evidence-supported `GO`, `SIMPLIFY`, `PIVOT`, or `STOP` decision. The application does not treat this entry as proof that a pilot has met commercial acceptance criteria.

Disable consent to stop new aggregate collection. Existing operational workflows continue independently; existing evidence remains subject to the tenant’s retention and data-lifecycle process.

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
