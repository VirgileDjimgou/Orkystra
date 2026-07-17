param(
  [Parameter(Mandatory = $true)][string]$InputPath,
  [string]$Database = "FleetOps",
  [string]$EnvFile = ".env"
)

$ErrorActionPreference = "Stop"

function Get-DotEnvValue {
  param(
    [string]$Path,
    [string]$Key
  )

  $line = Get-Content $Path | Where-Object { $_ -match "^${Key}=" } | Select-Object -First 1
  if (-not $line) {
    throw "Missing $Key in $Path."
  }

  return $line.Substring($Key.Length + 1)
}

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
  if (-not (Test-Path $EnvFile)) {
    throw "Missing $EnvFile."
  }

  $resolvedInput = [System.IO.Path]::GetFullPath((Join-Path $root $InputPath))
  if (-not (Test-Path $resolvedInput)) {
    throw "Backup file not found: $resolvedInput"
  }

  $sqlPassword = Get-DotEnvValue -Path $EnvFile -Key "MSSQL_SA_PASSWORD"
  $containerFileName = [System.IO.Path]::GetFileName($resolvedInput)
  $containerBackupPath = "/var/opt/mssql/backups/$containerFileName"
  $composeArgs = @("--env-file", $EnvFile, "-f", "docker-compose.yml", "-f", "docker-compose.pilot.yml")

  docker compose @composeArgs cp $resolvedInput "sqlserver:$containerBackupPath" | Out-Null
  docker compose @composeArgs exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd `
    -S localhost `
    -U sa `
    -P $sqlPassword `
    -C `
    -Q "IF DB_ID(N'$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; END; RESTORE DATABASE [$Database] FROM DISK = N'$containerBackupPath' WITH REPLACE, RECOVERY; ALTER DATABASE [$Database] SET MULTI_USER;"

  Write-Host "SQL restore completed from $resolvedInput"
}
finally {
  Pop-Location
}
