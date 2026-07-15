$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$summary = [System.Collections.Generic.List[string]]::new()

function Invoke-Step {
  param(
    [Parameter(Mandatory = $true)][string]$Name,
    [Parameter(Mandatory = $true)][scriptblock]$Action
  )

  Write-Host "== $Name =="
  & $Action
  if ($LASTEXITCODE -ne 0) {
    throw "Step failed: $Name"
  }
  $summary.Add("PASSED :: $Name")
}

Push-Location $root
try {
  if (-not (Test-Path .env)) {
    throw "Missing .env. Copy .env.example to .env before running the quality gate."
  }

  if (-not $env:ANDROID_HOME) {
    $defaultAndroidSdk = Join-Path $env:LOCALAPPDATA "Android\Sdk"
    if (Test-Path $defaultAndroidSdk) {
      $env:ANDROID_HOME = $defaultAndroidSdk
      $env:ANDROID_SDK_ROOT = $defaultAndroidSdk
    }
  }

  $androidStudioJbr = "C:\Program Files\Android\Android Studio\jbr"
  if (Test-Path $androidStudioJbr) {
    $env:JAVA_HOME = $androidStudioJbr
  }

  Invoke-Step "Git Status" { git status --short --branch }
  Invoke-Step "Dotnet Tools" { dotnet tool restore }
  Invoke-Step "Docker Compose Config" { docker compose --env-file .env config --quiet }

  Invoke-Step "Backend Restore" { dotnet restore FleetOps.slnx }
  Invoke-Step "Backend Format" { dotnet format FleetOps.slnx --verify-no-changes }
  Invoke-Step "Backend Build" { dotnet build FleetOps.slnx --no-restore -c Release }
  Invoke-Step "Backend Test" { dotnet test FleetOps.slnx --no-build -c Release }

  Invoke-Step "GPS Dry Run" {
    $logPath = Join-Path $root ".agent\quality-gps.log"
    $errPath = Join-Path $root ".agent\quality-gps.err"
    Remove-Item $logPath, $errPath -ErrorAction SilentlyContinue
    $process = Start-Process dotnet -ArgumentList @('run', '--project', 'simulators/GpsSimulator', '--', '--dry-run') `
      -WorkingDirectory $root `
      -WindowStyle Hidden `
      -RedirectStandardOutput $logPath `
      -RedirectStandardError $errPath `
      -PassThru
    Start-Sleep -Seconds 4
    if (-not $process.HasExited) {
      Stop-Process -Id $process.Id -Force
    }
    if (-not (Test-Path $logPath) -or -not (Select-String -Path $logPath -Pattern '"VehicleId"' -Quiet)) {
      throw "GPS dry-run did not produce a telemetry payload."
    }
  }

  Invoke-Step "Web Install" {
    Push-Location apps/web
    try { npm ci } finally { Pop-Location }
  }
  Invoke-Step "Web Format" {
    Push-Location apps/web
    try { npm run format:check } finally { Pop-Location }
  }
  Invoke-Step "Web Lint" {
    Push-Location apps/web
    try { npm run lint } finally { Pop-Location }
  }
  Invoke-Step "Web Test" {
    Push-Location apps/web
    try { npm run test } finally { Pop-Location }
  }
  Invoke-Step "Web Build" {
    Push-Location apps/web
    try { npm run build } finally { Pop-Location }
  }

  Invoke-Step "API Health Check" {
    $logPath = Join-Path $root ".agent\quality-api.log"
    $errPath = Join-Path $root ".agent\quality-api.err"
    Remove-Item $logPath, $errPath -ErrorAction SilentlyContinue
    $process = Start-Process dotnet -ArgumentList @('run', '--project', 'apps/backend/FleetOps.Api') `
      -WorkingDirectory $root `
      -WindowStyle Hidden `
      -RedirectStandardOutput $logPath `
      -RedirectStandardError $errPath `
      -PassThru
    try {
      Start-Sleep -Seconds 8
      $response = Invoke-WebRequest -UseBasicParsing 'http://localhost:5080/health'
      if ($response.StatusCode -ne 200) {
        throw "Unexpected health status code: $($response.StatusCode)"
      }
    }
    finally {
      if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
      }
    }
  }

  Invoke-Step "Android Wrapper" {
    if (-not (Test-Path apps/android-driver/gradlew.bat)) {
      throw "Gradle wrapper missing in apps/android-driver."
    }
    if (-not $env:ANDROID_HOME -or -not (Test-Path $env:ANDROID_HOME)) {
      throw "ANDROID_HOME is not set to a valid Android SDK path."
    }
    if (-not $env:JAVA_HOME -or -not (Test-Path $env:JAVA_HOME)) {
      throw "JAVA_HOME is not set to a valid JDK path."
    }
  }

  Invoke-Step "Android Build" {
    Push-Location apps/android-driver
    try { ./gradlew.bat testDebugUnitTest assembleDebug --stacktrace } finally { Pop-Location }
  }

  Write-Host "== Summary =="
  $summary | ForEach-Object { Write-Host $_ }
  Write-Host "Quality gate passed."
}
finally {
  Pop-Location
}
