param(
    [switch]$RemoveVolumes,
    [string]$ProjectName
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $composeArguments = @('compose')
    if ($ProjectName) { $composeArguments += @('-p', $ProjectName) }
    $composeArguments += @('--env-file', '.env', '-f', 'docker-compose.yml', '-f', 'docker-compose.pilot.yml', '-f', 'docker-compose.demo.yml', 'down')
    if ($RemoveVolumes) { $composeArguments += '--volumes' }
    & docker @composeArguments
    if ($LASTEXITCODE -ne 0) { throw "docker compose down failed with exit code $LASTEXITCODE." }
    Write-Host "FleetOps hosted Demo stopped."
}
finally {
    Pop-Location
}
