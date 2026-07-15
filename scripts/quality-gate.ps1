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
    $runtimeDir = Join-Path $root ".runtime"
    $gpsDll = "simulators\GpsSimulator\bin\Release\net10.0\GpsSimulator.dll"
    if (-not (Test-Path (Join-Path $root $gpsDll))) {
      throw "Missing GPS simulator build output: $gpsDll"
    }
    New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null
    $logPath = Join-Path $runtimeDir "quality-gps.log"
    $errPath = Join-Path $runtimeDir "quality-gps.err"
    Remove-Item $logPath, $errPath -ErrorAction SilentlyContinue
    $process = Start-Process dotnet -ArgumentList @('exec', $gpsDll, '--dry-run') `
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
    $runtimeDir = Join-Path $root ".runtime"
    $apiDll = "apps\backend\FleetOps.Api\bin\Release\net10.0\FleetOps.Api.dll"
    if (-not (Test-Path (Join-Path $root $apiDll))) {
      throw "Missing API build output: $apiDll"
    }
    New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null
    $logPath = Join-Path $runtimeDir "quality-api.log"
    $errPath = Join-Path $runtimeDir "quality-api.err"
    Remove-Item $logPath, $errPath -ErrorAction SilentlyContinue
    $previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
    $previousUrls = $env:ASPNETCORE_URLS
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = "http://localhost:5080"
    $process = Start-Process dotnet -ArgumentList @('exec', $apiDll) `
      -WorkingDirectory $root `
      -WindowStyle Hidden `
      -RedirectStandardOutput $logPath `
      -RedirectStandardError $errPath `
      -PassThru
    try {
      $deadline = (Get-Date).AddSeconds(20)
      $response = $null
      do {
        Start-Sleep -Seconds 1
        try {
          $response = Invoke-WebRequest -UseBasicParsing 'http://localhost:5080/health'
        }
        catch {
          $response = $null
        }
      } while (-not $response -and (Get-Date) -lt $deadline)
      if (-not $response) {
        throw "API health endpoint did not become reachable on http://localhost:5080/health"
      }
      if ($response.StatusCode -ne 200) {
        throw "Unexpected health status code: $($response.StatusCode)"
      }
    }
    finally {
      if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
      }
      $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
      $env:ASPNETCORE_URLS = $previousUrls
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
