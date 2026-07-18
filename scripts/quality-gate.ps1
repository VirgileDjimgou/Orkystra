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
  Invoke-Step "Pilot Compose Config" { docker compose --env-file .env -f docker-compose.yml -f docker-compose.pilot.yml config --quiet }
  Invoke-Step "Private Object Storage" {
    docker compose --env-file .env up -d --wait minio
    docker compose --env-file .env run --rm --no-deps minio-init
    $localEnvironment = @{}
    foreach ($line in Get-Content .env) {
      if ($line -match '^([^#=]+)=(.*)$') { $localEnvironment[$matches[1]] = $matches[2] }
    }
    $minioPort = if ($localEnvironment['MINIO_PORT']) { $localEnvironment['MINIO_PORT'] } else { '9000' }
    $env:FLEETOPS_TEST_MINIO_ENDPOINT = "http://localhost:$minioPort"
    $env:FLEETOPS_TEST_MINIO_ACCESS_KEY = $localEnvironment['MINIO_ACCESS_KEY']
    $env:FLEETOPS_TEST_MINIO_SECRET_KEY = $localEnvironment['MINIO_SECRET_KEY']
  }
  Invoke-Step "Recovery Script Parse" {
    $parseFailures = [System.Collections.Generic.List[string]]::new()
    foreach ($scriptPath in @("scripts\sql-backup.ps1", "scripts\sql-restore.ps1")) {
      $tokens = $null
      $errors = $null
      [System.Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $root $scriptPath),
        [ref]$tokens,
        [ref]$errors) | Out-Null
      foreach ($parseError in $errors) {
        $parseFailures.Add("$scriptPath :: $($parseError.Message)")
      }
    }
    if ($parseFailures.Count -gt 0) {
      throw ($parseFailures -join [Environment]::NewLine)
    }
  }

  Invoke-Step "Backend Restore" { dotnet restore FleetOps.slnx }
  Invoke-Step "Backend Format" { dotnet format FleetOps.slnx --verify-no-changes }
  Invoke-Step "Backend Build" { dotnet build FleetOps.slnx --no-restore -c Release }
  Invoke-Step "Backend Test (Fast)" {
    dotnet test FleetOps.slnx --no-build -c Release --filter "Category!=SqlServer&Category!=Minio"
  }
  Invoke-Step "Backend Test (MinIO)" {
    dotnet test tests/backend/FleetOps.UnitTests/FleetOps.UnitTests.csproj --no-build -c Release --filter "Category=Minio"
  }
  Invoke-Step "Backend Test (SqlServer)" {
    dotnet test tests/backend/FleetOps.UnitTests/FleetOps.UnitTests.csproj --no-build -c Release --filter "Category=SqlServer"
  }

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

  Invoke-Step "Full Multi-Tenant Simulation" {
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/run-full-simulation.ps1 -SkipScreenshots
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
  Invoke-Step "Web E2E Browser" {
    Push-Location apps/web
    try { npx playwright install chromium } finally { Pop-Location }
  }
  Invoke-Step "Web E2E" {
    Push-Location apps/web
    try { npm run e2e } finally { Pop-Location }
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
    $previousUseInMemory = $env:Testing__UseInMemoryDatabase
    $previousDatabaseName = $env:Testing__DatabaseName
    $previousSeedDemoData = $env:Bootstrap__SeedDemoData
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = "http://localhost:5080"
    $env:Testing__UseInMemoryDatabase = "true"
    $env:Testing__DatabaseName = "quality-gate-api"
    $env:Bootstrap__SeedDemoData = "true"
    $process = Start-Process dotnet -ArgumentList @('exec', $apiDll) `
      -WorkingDirectory $root `
      -WindowStyle Hidden `
      -RedirectStandardOutput $logPath `
      -RedirectStandardError $errPath `
      -PassThru
    try {
      $deadline = (Get-Date).AddSeconds(20)
      $response = $null
      $readinessResponse = $null
      do {
        Start-Sleep -Seconds 1
        try {
          $response = Invoke-WebRequest -UseBasicParsing 'http://localhost:5080/health'
          $readinessResponse = Invoke-WebRequest -UseBasicParsing 'http://localhost:5080/health/ready'
        }
        catch {
          $response = $null
          $readinessResponse = $null
        }
      } while ((-not $response -or -not $readinessResponse) -and (Get-Date) -lt $deadline)
      if (-not $response) {
        throw "API health endpoint did not become reachable on http://localhost:5080/health"
      }
      if ($response.StatusCode -ne 200) {
        throw "Unexpected health status code: $($response.StatusCode)"
      }
      if (-not $readinessResponse) {
        throw "API readiness endpoint did not become reachable on http://localhost:5080/health/ready"
      }
      if ($readinessResponse.StatusCode -ne 200) {
        throw "Unexpected readiness status code: $($readinessResponse.StatusCode)"
      }
    }
    finally {
      if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
      }
      $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
      $env:ASPNETCORE_URLS = $previousUrls
      $env:Testing__UseInMemoryDatabase = $previousUseInMemory
      $env:Testing__DatabaseName = $previousDatabaseName
      $env:Bootstrap__SeedDemoData = $previousSeedDemoData
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
    try { ./gradlew.bat lintDebug testDebugUnitTest assembleDebug assembleDebugAndroidTest --stacktrace } finally { Pop-Location }
  }

  if ($env:FLEETOPS_ENABLE_ANDROID_CONNECTED -eq "1") {
    Invoke-Step "Android Connected Test" {
      Push-Location apps/android-driver
      $adb = Join-Path $env:ANDROID_HOME "platform-tools\adb.exe"
      $verifierOriginal = $null
      $manageAdbVerifier = $env:FLEETOPS_MANAGE_ANDROID_ADB_VERIFIER -eq "1"
      $testExitCode = 1
      try {
        if ($manageAdbVerifier) {
          $verifierOriginal = (& $adb shell settings get global verifier_verify_adb_installs).Trim()
          & $adb shell settings put global verifier_verify_adb_installs 0
          $verifierApplied = (& $adb shell settings get global verifier_verify_adb_installs).Trim()
          if ($verifierApplied -ne "0") {
            throw "Unable to temporarily disable ADB-only package verification on the connected Android device."
          }
        }

        ./gradlew.bat connectedDebugAndroidTest --stacktrace
        $testExitCode = $LASTEXITCODE
        if ($testExitCode -ne 0) {
          $resultRoot = "app\build\outputs\androidTest-results\connected\debug"
          $resultText = Get-ChildItem -Path $resultRoot -Recurse -Filter "*.xml" -ErrorAction SilentlyContinue |
            Get-Content -Raw
          $installTimeout = $resultText -match "Failed to install split APK\(s\)" -and
            $resultText -match "ShellCommandUnresponsiveException"

          if ($installTimeout) {
            Write-Warning "Android package installation timed out on the connected device; retrying once after an ADB/package-manager readiness check."
            & $adb wait-for-device
            & $adb shell pm path android | Out-Null
            Start-Sleep -Seconds 5
            ./gradlew.bat connectedDebugAndroidTest --stacktrace
            $testExitCode = $LASTEXITCODE
          }
        }
      }
      finally {
        if ($manageAdbVerifier -and $null -ne $verifierOriginal) {
          if ($verifierOriginal -eq "null" -or [string]::IsNullOrWhiteSpace($verifierOriginal)) {
            & $adb shell settings delete global verifier_verify_adb_installs | Out-Null
          }
          else {
            & $adb shell settings put global verifier_verify_adb_installs $verifierOriginal
          }
        }
        Pop-Location
      }

      if ($testExitCode -ne 0) {
        throw "Connected Android instrumentation failed with exit code $testExitCode."
      }
    }
  }
  else {
    $summary.Add("SKIPPED :: Android Connected Test (set FLEETOPS_ENABLE_ANDROID_CONNECTED=1 with an emulator or device)")
  }

  Write-Host "== Summary =="
  $summary | ForEach-Object { Write-Host $_ }
  Write-Host "Quality gate passed."
}
finally {
  Pop-Location
}
