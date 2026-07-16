#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${ENV_FILE:-.env}"
INPUT_PATH="${1:-}"
DATABASE="${2:-FleetOps}"

if [[ -z "$INPUT_PATH" ]]; then
  echo "Usage: ./scripts/sql-restore.sh <backup-path> [database]" >&2
  exit 1
fi

if [[ ! -f "$ROOT_DIR/$ENV_FILE" ]]; then
  echo "Missing $ENV_FILE" >&2
  exit 1
fi

RESOLVED_INPUT="$ROOT_DIR/$INPUT_PATH"
if [[ ! -f "$RESOLVED_INPUT" ]]; then
  echo "Backup file not found: $RESOLVED_INPUT" >&2
  exit 1
fi

SQL_PASSWORD="$(grep '^MSSQL_SA_PASSWORD=' "$ROOT_DIR/$ENV_FILE" | head -n1 | cut -d= -f2-)"
if [[ -z "$SQL_PASSWORD" ]]; then
  echo "Missing MSSQL_SA_PASSWORD in $ENV_FILE" >&2
  exit 1
fi

CONTAINER_FILE_NAME="$(basename "$RESOLVED_INPUT")"
CONTAINER_BACKUP_PATH="/var/opt/mssql/backups/$CONTAINER_FILE_NAME"

docker compose --env-file "$ROOT_DIR/$ENV_FILE" -f "$ROOT_DIR/docker-compose.yml" -f "$ROOT_DIR/docker-compose.pilot.yml" \
  cp "$RESOLVED_INPUT" "sqlserver:$CONTAINER_BACKUP_PATH" >/dev/null

docker compose --env-file "$ROOT_DIR/$ENV_FILE" -f "$ROOT_DIR/docker-compose.yml" -f "$ROOT_DIR/docker-compose.pilot.yml" \
  exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SQL_PASSWORD" -C \
  -Q "IF DB_ID(N'$DATABASE') IS NOT NULL BEGIN ALTER DATABASE [$DATABASE] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; END; RESTORE DATABASE [$DATABASE] FROM DISK = N'$CONTAINER_BACKUP_PATH' WITH REPLACE, RECOVERY; ALTER DATABASE [$DATABASE] SET MULTI_USER;"

echo "SQL restore completed from $RESOLVED_INPUT"
