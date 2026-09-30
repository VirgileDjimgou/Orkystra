param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not (Test-Path .env)) { Copy-Item .env.example .env }
    $settings = @{}
    foreach ($line in Get-Content .env) {
        if ($line -match '^([^#=]+)=(.*)$') { $settings[$matches[1]] = $matches[2] }
    }
    $requiredKeys = @('MSSQL_SA_PASSWORD', 'MINIO_ROOT_USER', 'MINIO_ROOT_PASSWORD', 'MINIO_ACCESS_KEY', 'MINIO_SECRET_KEY', 'MINIO_KMS_SECRET_KEY', 'JWT_SIGNING_KEY', 'MEDIA_SIGNING_KEY', 'INTERNAL_API_KEY')
    $missingKeys = @($requiredKeys | Where-Object { [string]::IsNullOrWhiteSpace($settings[$_]) })
    if ($missingKeys.Count -gt 0) {
        throw "Missing required hosted Demo settings in .env: $($missingKeys -join ', '). Copy the missing values from .env.example."
    }
    if ($settings['INTERNAL_API_KEY'].Length -lt 32 -or $settings['JWT_SIGNING_KEY'].Length -lt 32 -or $settings['MEDIA_SIGNING_KEY'].Length -lt 32) {
        throw "INTERNAL_API_KEY, JWT_SIGNING_KEY and MEDIA_SIGNING_KEY must each be at least 32 characters."
    }

    $composeArguments = @('compose', '--env-file', '.env', '-f', 'docker-compose.yml', '-f', 'docker-compose.pilot.yml', '-f', 'docker-compose.demo.yml')
    if ($SkipBuild) {
        & docker @composeArguments up -d
    }
    else {
        & docker @composeArguments up -d --build
    }
    if ($LASTEXITCODE -ne 0) { throw "docker compose up failed with exit code $LASTEXITCODE." }

    $webPort = if ($settings['FLEETOPS_WEB_PORT']) { $settings['FLEETOPS_WEB_PORT'] } else { '8081' }
    $apiPort = if ($settings['FLEETOPS_API_PORT']) { $settings['FLEETOPS_API_PORT'] } else { '5080' }
    Write-Host "FleetOps hosted Demo is starting."
    Write-Host "Web: http://localhost:$webPort/demo"
    Write-Host "API health: http://localhost:$apiPort/health/ready"
    Write-Host "Validate it with: pwsh -ExecutionPolicy Bypass -File scripts/demo-smoke.ps1 -SkipBuild"
}
finally {
    Pop-Location
}
