param(
    [int]$DurationSeconds = 900,
    [int]$Vehicles = 20,
    [int]$TickSeconds = 5,
    [ValidateSet('observe', 'load')][string]$Mode = 'observe',
    [int]$ApiPort = 5091,
    [int]$IntervalMs = 5000,
    [int]$SnapshotEverySeconds = 30,
    [int]$WorkerRestartAfterSeconds = 0,
    [int]$InjectDuplicatesEvery = 0,
    [int]$InjectOutOfOrderEvery = 0,
    [switch]$SkipWorker,
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot ".runtime/reliability/$runId"
}
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
$apiBaseUrl = "http://127.0.0.1:$ApiPort"
$statePath = Join-Path $OutputPath 'demo-engine-state.json'

Write-Host "== FleetOps reliability exercise =="
Write-Host "mode=$Mode vehicles=$Vehicles duration=${DurationSeconds}s tick=${TickSeconds}s api=$apiBaseUrl"
Write-Host "output=$OutputPath"

& dotnet build (Join-Path $repoRoot 'FleetOps.slnx') -c Debug --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$apiDll = Join-Path $repoRoot 'apps/backend/FleetOps.Api/bin/Debug/net10.0/FleetOps.Api.dll'
$workerDll = Join-Path $repoRoot 'apps/backend/FleetOps.Worker/bin/Debug/net10.0/FleetOps.Worker.dll'
$harnessDll = Join-Path $repoRoot 'simulators/FleetOpsReliabilityHarness/bin/Debug/net10.0/FleetOpsReliabilityHarness.dll'

$previousEnvironment = @{}
function Set-ExerciseEnvironment([hashtable]$values) {
    foreach ($entry in $values.GetEnumerator()) {
        if (-not $previousEnvironment.ContainsKey($entry.Key)) {
            $previousEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
        }
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
}

$apiProcess = $null
$workerProcess = $null
$harnessProcess = $null

try {
    Set-ExerciseEnvironment @{
        'ASPNETCORE_ENVIRONMENT' = 'Development'
        'Testing__UseInMemoryDatabase' = 'true'
        'Testing__DatabaseName' = "reliability-$runId"
        'Bootstrap__SeedDemoData' = 'true'
        'FLEETOPS_WEB_URL' = 'http://localhost:5183'
        'Jwt__Issuer' = 'FleetOps.Tests'
        'Jwt__Audience' = 'FleetOps.Tests.Web'
        'Jwt__SigningKey' = 'FleetOps_Tests_Signing_Key_12345678901234567890'
        'Security__LoginPermitLimit' = '1000'
        'Integrations__RetryBaseDelaySeconds' = '0'
    }

    $existingListener = Get-NetTCPConnection -LocalPort $ApiPort -State Listen -ErrorAction SilentlyContinue
    if ($existingListener) {
        throw "Port $ApiPort is already in use by process $($existingListener[0].OwningProcess). Stop it before running the exercise."
    }

    Write-Host 'Starting API...'
    $apiProcess = Start-Process -FilePath 'dotnet' `
        -ArgumentList @('exec', "`"$apiDll`"", '--urls', $apiBaseUrl) `
        -WorkingDirectory $repoRoot `
        -RedirectStandardOutput (Join-Path $OutputPath 'api.log') `
        -RedirectStandardError (Join-Path $OutputPath 'api.err.log') `
        -PassThru

    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($apiProcess.HasExited) {
            throw "API exited during startup with code $($apiProcess.ExitCode). See $OutputPath\api.err.log."
        }

        try {
            $response = Invoke-WebRequest -Uri "$apiBaseUrl/health/ready" -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200) { $ready = $true; break }
        }
        catch {
            Start-Sleep -Seconds 1
        }
    }

    if (-not $ready) { throw 'API did not become ready within 60 seconds.' }
    Write-Host 'API ready.'

    if ($Mode -eq 'observe' -and -not $SkipWorker) {
        Set-ExerciseEnvironment @{
            'Testing__UseInMemoryDatabase' = 'true'
            'Testing__DatabaseName' = "reliability-worker-$runId"
            'DemoEngine__Enabled' = 'true'
            'DemoEngine__RuntimeMode' = 'Demo'
            'DemoEngine__ApiBaseUrl' = $apiBaseUrl
            'DemoEngine__OrganizationSlug' = 'northwind'
            'DemoEngine__Scenario' = 'NORMAL_SHIFT'
            'DemoEngine__Seed' = '3101'
            'DemoEngine__TickSeconds' = $TickSeconds.ToString()
            'DemoEngine__SpeedMultiplier' = '1'
            'DemoEngine__StatePath' = $statePath
            'DemoEngine__SideEffectsSandboxed' = 'true'
        }

        Write-Host 'Starting hosted Demo engine (20-agent fleet)...'
        $workerProcess = Start-Process -FilePath 'dotnet' `
            -ArgumentList @('exec', "`"$workerDll`"") `
            -WorkingDirectory $repoRoot `
            -RedirectStandardOutput (Join-Path $OutputPath 'worker.log') `
            -RedirectStandardError (Join-Path $OutputPath 'worker.err.log') `
            -PassThru
    }

    Write-Host 'Starting reliability harness...'
    $harnessArguments = @(
        'exec', "`"$harnessDll`"",
        '--mode', $Mode,
        '--api-url', $apiBaseUrl,
        '--vehicles', $Vehicles.ToString(),
        '--duration-seconds', $DurationSeconds.ToString(),
        '--interval-ms', $IntervalMs.ToString(),
        '--snapshot-every-seconds', $SnapshotEverySeconds.ToString(),
        '--inject-duplicates-every', $InjectDuplicatesEvery.ToString(),
        '--inject-out-of-order-every', $InjectOutOfOrderEvery.ToString(),
        '--run-id', $runId,
        '--output', "`"$OutputPath`""
    )
    $harnessProcess = Start-Process -FilePath 'dotnet' `
        -ArgumentList $harnessArguments `
        -WorkingDirectory $repoRoot `
        -RedirectStandardOutput (Join-Path $OutputPath 'harness.log') `
        -RedirectStandardError (Join-Path $OutputPath 'harness.err.log') `
        -PassThru

    $startedAt = Get-Date
    $workerRestarted = $false
    while (-not $harnessProcess.HasExited) {
        $elapsed = (New-TimeSpan -Start $startedAt).TotalSeconds
        if (-not $workerRestarted -and $WorkerRestartAfterSeconds -gt 0 -and $elapsed -ge $WorkerRestartAfterSeconds -and $null -ne $workerProcess) {
            Write-Host "Fault injection: restarting Worker after $([int]$elapsed)s of sustained load."
            Stop-Process -Id $workerProcess.Id -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 3
            $workerProcess = Start-Process -FilePath 'dotnet' `
                -ArgumentList @('exec', "`"$workerDll`"") `
                -WorkingDirectory $repoRoot `
                -RedirectStandardOutput (Join-Path $OutputPath 'worker-restarted.log') `
                -RedirectStandardError (Join-Path $OutputPath 'worker-restarted.err.log') `
                -PassThru
            $workerRestarted = $true
        }

        Start-Sleep -Seconds 2
    }

    $exitCode = $harnessProcess.ExitCode
    Write-Host '--- harness output ---'
    Get-Content (Join-Path $OutputPath 'harness.log') -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
    $harnessErrors = Get-Content (Join-Path $OutputPath 'harness.err.log') -ErrorAction SilentlyContinue
    if ($harnessErrors) {
        Write-Host '--- harness stderr ---'
        $harnessErrors | ForEach-Object { Write-Host $_ }
    }

    Write-Host "Reliability exercise finished with exit code $exitCode."
    Write-Host "Report: $(Join-Path $OutputPath 'reliability-report.md')"
    exit $exitCode
}
finally {
    foreach ($process in @($harnessProcess, $workerProcess, $apiProcess)) {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }

    foreach ($entry in $previousEnvironment.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
}
