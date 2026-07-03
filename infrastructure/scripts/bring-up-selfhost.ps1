param(
    [string]$ApiKey = "orkystra-dev-local-key",
    [string]$TenantId = "north-hub-demo",
    [string]$ScenarioName = "Self-Host Demo",
    [int]$Seed = 42,
    [int]$AdvanceMinutes = 15,
    [switch]$IncludeDisruption,
    [switch]$SkipBootstrap,
    [int]$MaxWaitSeconds = 180
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $repoRoot

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Assert-Command {
    param([string]$CommandName)

    if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
        throw "Required command '$CommandName' was not found in PATH."
    }
}

function Assert-DockerEngine {
    docker info | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker is installed but the Docker engine is not reachable. Start Docker Desktop (or your Docker daemon) before running bring-up-selfhost.ps1."
    }
}

function Wait-ForHttp {
    param(
        [string]$Url,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -Method Get -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                return $true
            }
        } catch {
            Start-Sleep -Seconds 3
        }
    }

    return $false
}

Assert-Command -CommandName "docker"
Assert-DockerEngine

Write-Step "Starting packaged self-host stack"
docker compose -f infrastructure/docker-compose.stack.yml up -d --build

$healthUrl = "http://127.0.0.1:8080/health/sanity"
Write-Step "Waiting for API health on $healthUrl"

if (-not (Wait-ForHttp -Url $healthUrl -TimeoutSeconds $MaxWaitSeconds)) {
    throw "API did not become reachable within $MaxWaitSeconds seconds."
}

Write-Step "API is reachable"

if (-not $SkipBootstrap) {
    $bootstrapBody = @{
        scenarioName = $ScenarioName
        seed = $Seed
        advanceMinutes = $AdvanceMinutes
        includeDisruption = [bool]$IncludeDisruption
    } | ConvertTo-Json -Depth 5

    Write-Step "Bootstrapping deterministic demo data"
    Invoke-RestMethod `
        -Uri "http://127.0.0.1:8080/api/bootstrap/demo" `
        -Method Post `
        -Headers @{
            "X-Api-Key" = $ApiKey
            "X-Tenant-Id" = $TenantId
        } `
        -ContentType "application/json" `
        -Body $bootstrapBody | Out-Null

    Write-Step "Demo bootstrap completed"
} else {
    Write-Step "Skipping bootstrap as requested"
}

Write-Host ""
Write-Host "Orkystra self-host stack is ready." -ForegroundColor Green
Write-Host "Frontend: http://127.0.0.1:8081"
Write-Host "API:      http://127.0.0.1:8080"
Write-Host "Health:   $healthUrl"
Write-Host ""
Write-Host "Default protected headers for local evaluation:"
Write-Host "  X-Api-Key:   $ApiKey"
Write-Host "  X-Tenant-Id: $TenantId"
Write-Host ""
Write-Host "Suggested next checks:"
Write-Host "  - Open http://127.0.0.1:8081"
Write-Host "  - Review docs/operations/smoke-test-checklist.md"
Write-Host "  - Run infrastructure/scripts/release-preflight.ps1 before any release tag"
