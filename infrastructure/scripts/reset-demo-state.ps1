[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch]$StopContainers,
    [switch]$RemoveDockerVolumes,
    [switch]$RemoveLocalRuntimeConfig,
    [switch]$RemoveLocalSecrets,
    [switch]$Rebootstrap,
    [string]$ApiKey = "orkystra-dev-local-key",
    [string]$TenantId = "north-hub-demo",
    [string]$ScenarioName = "Self-Host Demo",
    [int]$Seed = 42,
    [int]$AdvanceMinutes = 15,
    [switch]$IncludeDisruption
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $repoRoot

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Remove-PathIfPresent {
    param(
        [string]$PathToRemove,
        [string]$Label
    )

    if (-not (Test-Path $PathToRemove)) {
        Write-Host "[skip] $Label not present: $PathToRemove" -ForegroundColor DarkYellow
        return
    }

    if ($PSCmdlet.ShouldProcess($PathToRemove, "Remove $Label")) {
        Remove-Item -LiteralPath $PathToRemove -Recurse -Force
        Write-Host "[ok] Removed ${Label}: $PathToRemove" -ForegroundColor Green
    }
}

$sqliteDatabasePath = Join-Path $repoRoot "backend\src\Orkystra.Api\output\persistence\orkystra-operations.db"
$auditDirectory = Join-Path $repoRoot "backend\src\Orkystra.Api\output\audit"
$localRuntimeConfigPath = Join-Path $repoRoot "backend\src\Orkystra.Api\appsettings.Local.json"
$localSecretsPath = Join-Path $repoRoot "backend\src\Orkystra.Api\appsettings.Secrets.local.json"

Write-Step "Resetting local demo state"

Remove-PathIfPresent -PathToRemove $sqliteDatabasePath -Label "SQLite persistence database"
Remove-PathIfPresent -PathToRemove $auditDirectory -Label "audit output directory"

if ($StopContainers) {
    $volumeArgs = if ($RemoveDockerVolumes) { "-v" } else { "" }
    Write-Step "Stopping Docker Compose services"

    if ($PSCmdlet.ShouldProcess("infrastructure/docker-compose.stack.yml", "docker compose down $volumeArgs")) {
        docker compose -f infrastructure/docker-compose.stack.yml down $volumeArgs
    }

    if ($PSCmdlet.ShouldProcess("infrastructure/docker-compose.yml", "docker compose down $volumeArgs")) {
        docker compose -f infrastructure/docker-compose.yml down $volumeArgs
    }
}

if ($RemoveLocalRuntimeConfig) {
    Remove-PathIfPresent -PathToRemove $localRuntimeConfigPath -Label "local runtime config"
}

if ($RemoveLocalSecrets) {
    Remove-PathIfPresent -PathToRemove $localSecretsPath -Label "local secrets config"
}

if ($Rebootstrap) {
    $bringUpScript = Join-Path $PSScriptRoot "bring-up-selfhost.ps1"

    if ($PSCmdlet.ShouldProcess($bringUpScript, "Rebootstrap packaged self-host demo")) {
        Write-Step "Rebootstrapping packaged self-host demo"
        & $bringUpScript `
            -ApiKey $ApiKey `
            -TenantId $TenantId `
            -ScenarioName $ScenarioName `
            -Seed $Seed `
            -AdvanceMinutes $AdvanceMinutes `
            -IncludeDisruption:$IncludeDisruption
    }
}

Write-Host ""
Write-Host "Local demo reset completed." -ForegroundColor Green
Write-Host "Default reset scope:"
Write-Host "  - SQLite operational persistence database"
Write-Host "  - audit output directory"
Write-Host ""
Write-Host "Optional flags:"
Write-Host "  -StopContainers           Stop compose services"
Write-Host "  -RemoveDockerVolumes      Drop Docker volumes when stopping containers"
Write-Host "  -RemoveLocalRuntimeConfig Remove backend appsettings.Local.json"
Write-Host "  -RemoveLocalSecrets       Remove backend appsettings.Secrets.local.json"
Write-Host "  -Rebootstrap              Re-run bring-up-selfhost.ps1 after reset"
