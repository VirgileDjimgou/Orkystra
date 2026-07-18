param(
  [switch]$IncludeAndroid,
  [switch]$SkipScreenshots,
  [int]$ApiPort = 5090,
  [int]$WebPort = 4174
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$runtime = Join-Path $root ".runtime\full-simulation"
$runId = (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmmss", [System.Globalization.CultureInfo]::InvariantCulture)
$apiUrl = "http://127.0.0.1:$ApiPort"
$webUrl = "http://127.0.0.1:$WebPort"
$apiProcess = $null
$webProcess = $null

function Wait-ForUrl {
  param([Parameter(Mandatory = $true)][string]$Url)
  $deadline = (Get-Date).AddSeconds(60)
  do {
    Start-Sleep -Milliseconds 500
    try { $response = Invoke-WebRequest -UseBasicParsing $Url } catch { $response = $null }
  } while (-not $response -and (Get-Date) -lt $deadline)
  if (-not $response) { throw "$Url did not become ready." }
}

function Stop-ProcessTree {
  param([int]$ProcessId)
  $children = Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq $ProcessId }
  foreach ($child in $children) { Stop-ProcessTree -ProcessId $child.ProcessId }
  Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
}

Push-Location $root
try {
  New-Item -ItemType Directory -Force $runtime | Out-Null
  dotnet build FleetOps.slnx -c Debug
  if ($LASTEXITCODE -ne 0) { throw "FleetOps build failed." }
  if (-not $SkipScreenshots) {
    Push-Location apps/web
    try { npm ci } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw "Web dependency installation failed." }
  }

  $env:ASPNETCORE_ENVIRONMENT = "Development"
  $env:ASPNETCORE_URLS = $apiUrl
  $env:Testing__UseInMemoryDatabase = "true"
  $env:Testing__DatabaseName = "fleetops-full-simulation-$runId"
  $env:Bootstrap__SeedDemoData = "true"
  $env:FLEETOPS_WEB_URL = $webUrl
  $env:Jwt__Issuer = "FleetOps.Simulation"
  $env:Jwt__Audience = "FleetOps.Simulation.Clients"
  $env:Jwt__SigningKey = "FleetOps_Simulation_Signing_Key_12345678901234567890"
  $env:Security__LoginPermitLimit = "100"
  $env:VITE_API_BASE_URL = $apiUrl
  $apiProcess = Start-Process dotnet -ArgumentList @('run', '--project', 'apps/backend/FleetOps.Api', '--no-build', '--urls', $apiUrl) -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runtime 'api.log') -RedirectStandardError (Join-Path $runtime 'api.err') -PassThru
  Wait-ForUrl "$apiUrl/health/ready"

  if (-not $SkipScreenshots) {
    $webProcess = Start-Process npm.cmd -ArgumentList @('run', 'dev', '--', '--host', '127.0.0.1', '--port', $WebPort) -WorkingDirectory (Join-Path $root 'apps/web') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runtime 'web.log') -RedirectStandardError (Join-Path $runtime 'web.err') -PassThru
    Wait-ForUrl "$webUrl/login"
  }

  dotnet run --project simulators/FleetOpsScenarioSimulator --no-build -- --api-url $apiUrl --output $runtime --run-id $runId
  if ($LASTEXITCODE -ne 0) { throw "Full-system simulator failed." }

  if (-not $SkipScreenshots) {
    $env:PLAYWRIGHT_API_BASE_URL = $apiUrl
    $env:PLAYWRIGHT_WEB_BASE_URL = $webUrl
    Push-Location apps/web
    try { npm run simulation:screenshots } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw "Web simulation screenshots failed." }
  }

  if ($IncludeAndroid) {
    & (Join-Path $root 'scripts/capture-android-simulation.ps1') -ApiPort $ApiPort
  }

  Write-Host "Full FleetOps simulation passed."
  if (-not $SkipScreenshots) { Write-Host "Web: $webUrl" }
  Write-Host "Reports: $runtime"
}
finally {
  if ($webProcess) { Stop-ProcessTree -ProcessId $webProcess.Id }
  $webListener = Get-NetTCPConnection -LocalPort $WebPort -State Listen -ErrorAction SilentlyContinue
  if ($webListener) { Stop-Process -Id $webListener.OwningProcess -Force -ErrorAction SilentlyContinue }
  if ($apiProcess) { Stop-ProcessTree -ProcessId $apiProcess.Id }
  Pop-Location
}
