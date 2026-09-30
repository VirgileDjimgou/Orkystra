param(
    [switch]$ConfigOnly,
    [switch]$SkipBuild,
    [switch]$KeepRunning,
    [switch]$RemoveVolumes,
    [string]$ProjectName = 'fleetops-demo-smoke',
    [int]$ReadyTimeoutSeconds = 240,
    [int]$FleetTimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$checks = [System.Collections.Generic.List[string]]::new()

function Assert-Check {
    param([bool]$Condition, [string]$Name)
    if (-not $Condition) { throw "Smoke check failed: $Name" }
    $checks.Add("PASSED :: $Name")
    Write-Host "PASSED :: $Name"
}

function Get-HttpStatus {
    param(
        [string]$Uri,
        [string]$Method = 'Get',
        [Microsoft.PowerShell.Commands.WebRequestSession]$Session,
        [hashtable]$Headers,
        [string]$Body
    )
    try {
        $request = @{ Uri = $Uri; Method = $Method; UseBasicParsing = $true }
        if ($Session) { $request['WebSession'] = $Session }
        if ($Headers) { $request['Headers'] = $Headers }
        if ($Body) { $request['Body'] = $Body; $request['ContentType'] = 'application/json' }
        $response = Invoke-WebRequest @request
        return [int]$response.StatusCode
    }
    catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        throw
    }
}

function Wait-ForHttp {
    param([string]$Uri, [int]$TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) { return $response }
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }
    throw "Timed out waiting for $Uri"
}

Push-Location $root
$composeArguments = @('compose', '-p', $ProjectName, '--env-file', '.env', '-f', 'docker-compose.yml', '-f', 'docker-compose.pilot.yml', '-f', 'docker-compose.demo.yml')
try {
    if (-not (Test-Path .env)) { throw 'Missing .env. Copy .env.example to .env before running the Demo smoke test.' }
    $settings = @{}
    foreach ($line in Get-Content .env) {
        if ($line -match '^([^#=]+)=(.*)$') { $settings[$matches[1]] = $matches[2] }
    }
    $requiredKeys = @('MSSQL_SA_PASSWORD', 'MINIO_ROOT_USER', 'MINIO_ROOT_PASSWORD', 'MINIO_ACCESS_KEY', 'MINIO_SECRET_KEY', 'MINIO_KMS_SECRET_KEY', 'JWT_SIGNING_KEY', 'MEDIA_SIGNING_KEY', 'INTERNAL_API_KEY')
    $missingKeys = @($requiredKeys | Where-Object { [string]::IsNullOrWhiteSpace($settings[$_]) })
    if ($missingKeys.Count -gt 0) {
        throw "Missing required hosted Demo settings in .env: $($missingKeys -join ', '). Copy the missing values from .env.example."
    }
    $internalKey = $settings['INTERNAL_API_KEY']
    if ($internalKey.Length -lt 32 -or $settings['JWT_SIGNING_KEY'].Length -lt 32 -or $settings['MEDIA_SIGNING_KEY'].Length -lt 32) {
        throw 'INTERNAL_API_KEY, JWT_SIGNING_KEY and MEDIA_SIGNING_KEY must each be at least 32 characters.'
    }

    Write-Host '== Demo compose configuration =='
    & docker @composeArguments config --quiet
    if ($LASTEXITCODE -ne 0) { throw "Demo compose configuration is invalid (exit $LASTEXITCODE)." }
    Assert-Check $true 'Demo compose configuration is valid'
    if ($ConfigOnly) {
        Write-Host 'Demo smoke configuration check passed.'
        return
    }

    if ($SkipBuild) {
        & docker @composeArguments up -d
    }
    else {
        & docker @composeArguments up -d --build
    }
    if ($LASTEXITCODE -ne 0) { throw "docker compose up failed with exit code $LASTEXITCODE." }

    $apiPort = if ($settings['FLEETOPS_API_PORT']) { $settings['FLEETOPS_API_PORT'] } else { '5080' }
    $webPort = if ($settings['FLEETOPS_WEB_PORT']) { $settings['FLEETOPS_WEB_PORT'] } else { '8081' }
    $apiBaseUrl = "http://localhost:$apiPort"
    $webBaseUrl = "http://localhost:$webPort"
    Write-Host "Waiting for Demo readiness on $apiBaseUrl/health/ready..."
    try {
        $health = Wait-ForHttp -Uri "$apiBaseUrl/health" -TimeoutSeconds $ReadyTimeoutSeconds
        Assert-Check ($health.StatusCode -eq 200) 'API liveness endpoint is healthy'
        $ready = Wait-ForHttp -Uri "$apiBaseUrl/health/ready" -TimeoutSeconds $ReadyTimeoutSeconds
        Assert-Check ($ready.StatusCode -eq 200) 'API readiness endpoint reports migrated database ready'
        $web = Wait-ForHttp -Uri $webBaseUrl -TimeoutSeconds 60
        Assert-Check ($web.StatusCode -eq 200) 'Web client is served'

        $status = Invoke-RestMethod -Uri "$apiBaseUrl/api/v1/demo/public/status" -Method Get -UseBasicParsing
        Assert-Check ($status.enabled -eq $true) 'Public Demo launch is enabled'
        Assert-Check ($status.label -eq 'SIMULATED DEMO') 'Public Demo status is labelled SIMULATED DEMO'

        $anonymousInternal = Get-HttpStatus -Uri "$apiBaseUrl/api/internal/v1/tracking/scenarios/public-demo"
        Assert-Check ($anonymousInternal -eq 401) 'Anonymous internal engine access is refused in Demo'

        $launchResponse = Invoke-WebRequest -Uri "$apiBaseUrl/api/v1/demo/public/launch" -Method Post -UseBasicParsing -SessionVariable visitorSession
        Assert-Check ($launchResponse.StatusCode -eq 200) 'Public visitor can launch a Demo session'
        $launch = $launchResponse.Content | ConvertFrom-Json
        Assert-Check ($launch.label -eq 'SIMULATED DEMO') 'Launched session carries the SIMULATED DEMO label'
        Assert-Check ($launch.user.isDemo -eq $true) 'Launched identity is a Demo session'
        Assert-Check ($launch.user.roles -contains 'Operator') 'Launched identity is least-privilege Operator'

        $adminStatus = Get-HttpStatus -Uri "$apiBaseUrl/api/admin/users" -Session $visitorSession
        $sessionsStatus = Get-HttpStatus -Uri "$apiBaseUrl/api/v1/auth/sessions" -Session $visitorSession
        Assert-Check ($adminStatus -eq 403) 'Demo session cannot read administration surfaces'
        Assert-Check ($sessionsStatus -eq 403) 'Demo session cannot list identity sessions'

        $csrfHeaders = @{ 'X-CSRF-Token' = [string]$launch.csrfToken }
        $resetStatus = Get-HttpStatus -Uri "$apiBaseUrl/api/v1/demo/session/control" -Method Post -Session $visitorSession -Headers $csrfHeaders -Body '{"action":"RESET"}'
        Assert-Check ($resetStatus -eq 200) 'Demo session RESET control is accepted'
        $control = Invoke-RestMethod -Uri "$apiBaseUrl/api/v1/demo/session/control" -Method Get -UseBasicParsing -WebSession $visitorSession
        Assert-Check ($control.status -eq 'READY') 'Demo session state returns to READY after reset'

        Write-Host "Waiting for the Demo engine fleet on $apiBaseUrl/api/v1/tracking/positions..."
        $deadline = (Get-Date).AddSeconds($FleetTimeoutSeconds)
        $tracked = 0
        while ((Get-Date) -lt $deadline) {
            try {
                $positions = Invoke-RestMethod -Uri "$apiBaseUrl/api/v1/tracking/positions" -Method Get -UseBasicParsing -WebSession $visitorSession
                $tracked = @($positions).Count
                if ($tracked -ge 10) { break }
            }
            catch {
                $tracked = 0
            }
            Start-Sleep -Seconds 3
        }
        Assert-Check ($tracked -ge 10) "Demo engine animates the synthetic public fleet (tracked: $tracked)"

        Write-Host '== Smoke summary =='
        $checks | ForEach-Object { Write-Host $_ }
        Write-Host 'Demo smoke test passed.'
    }
    catch {
        Write-Host '== Demo stack state =='
        & docker @composeArguments ps
        Write-Host '== API logs =='
        & docker @composeArguments logs --tail 80 api
        Write-Host '== Worker logs =='
        & docker @composeArguments logs --tail 80 worker
        throw
    }
    finally {
        if (-not $KeepRunning) {
            $downArguments = $composeArguments + @('down')
            if ($RemoveVolumes) { $downArguments += '--volumes' }
            & docker @downArguments
            Write-Host 'Demo smoke stack stopped.'
        }
    }
}
finally {
    Pop-Location
}
