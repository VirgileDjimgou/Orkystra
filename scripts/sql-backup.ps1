$ErrorActionPreference = "Stop"

param(
  [string]$Database = "FleetOps",
  [string]$OutputPath = "",
  [string]$EnvFile = ".env"
)

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

  if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputPath = Join-Path "backups" "$($Database.ToLowerInvariant())-$timestamp.bak"
  }

  $resolvedOutput = [System.IO.Path]::GetFullPath((Join-Path $root $OutputPath))
  $localDirectory = Split-Path -Parent $resolvedOutput
  New-Item -ItemType Directory -Path $localDirectory -Force | Out-Null

  $sqlPassword = Get-DotEnvValue -Path $EnvFile -Key "MSSQL_SA_PASSWORD"
  $containerFileName = [System.IO.Path]::GetFileName($resolvedOutput)
  $containerBackupPath = "/var/opt/mssql/backups/$containerFileName"
  $composeArgs = @("--env-file", $EnvFile, "-f", "docker-compose.yml", "-f", "docker-compose.pilot.yml")

  docker compose @composeArgs exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd `
    -S localhost `
    -U sa `
    -P $sqlPassword `
    -C `
    -Q "BACKUP DATABASE [$Database] TO DISK = N'$containerBackupPath' WITH INIT, COPY_ONLY, CHECKSUM"

  docker compose @composeArgs cp "sqlserver:$containerBackupPath" $resolvedOutput | Out-Null
  Write-Host "SQL backup created at $resolvedOutput"
}
finally {
  Pop-Location
}
