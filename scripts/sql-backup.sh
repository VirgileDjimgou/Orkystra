#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${ENV_FILE:-.env}"
DATABASE="${1:-FleetOps}"
OUTPUT_PATH="${2:-}"

if [[ ! -f "$ROOT_DIR/$ENV_FILE" ]]; then
  echo "Missing $ENV_FILE" >&2
  exit 1
fi

if [[ -z "$OUTPUT_PATH" ]]; then
  OUTPUT_PATH="backups/$(echo "$DATABASE" | tr '[:upper:]' '[:lower:]')-$(date +%Y%m%d-%H%M%S).bak"
fi

SQL_PASSWORD="$(grep '^MSSQL_SA_PASSWORD=' "$ROOT_DIR/$ENV_FILE" | head -n1 | cut -d= -f2-)"
if [[ -z "$SQL_PASSWORD" ]]; then
  echo "Missing MSSQL_SA_PASSWORD in $ENV_FILE" >&2
  exit 1
fi

mkdir -p "$ROOT_DIR/$(dirname "$OUTPUT_PATH")"
RESOLVED_OUTPUT="$ROOT_DIR/$OUTPUT_PATH"
CONTAINER_FILE_NAME="$(basename "$RESOLVED_OUTPUT")"
CONTAINER_BACKUP_PATH="/var/opt/mssql/backups/$CONTAINER_FILE_NAME"

docker compose --env-file "$ROOT_DIR/$ENV_FILE" -f "$ROOT_DIR/docker-compose.yml" -f "$ROOT_DIR/docker-compose.pilot.yml" \
  exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SQL_PASSWORD" -C \
  -Q "BACKUP DATABASE [$DATABASE] TO DISK = N'$CONTAINER_BACKUP_PATH' WITH INIT, COPY_ONLY, CHECKSUM"

docker compose --env-file "$ROOT_DIR/$ENV_FILE" -f "$ROOT_DIR/docker-compose.yml" -f "$ROOT_DIR/docker-compose.pilot.yml" \
  cp "sqlserver:$CONTAINER_BACKUP_PATH" "$RESOLVED_OUTPUT" >/dev/null

echo "SQL backup created at $RESOLVED_OUTPUT"
