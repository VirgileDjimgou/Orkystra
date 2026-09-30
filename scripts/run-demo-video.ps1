param(
    [double]$Pace = 1.0,
    [switch]$SkipBuild,
    [switch]$SkipBackendBuild,
    [switch]$SkipAndroid,
    [switch]$SkipWeb,
    [switch]$SkipMerge,
    [switch]$MergeOnly,
    [switch]$KeepRunning,
    [string]$OutputDirectory = '.runtime/demo-video',
    [string]$Grep = '',
    [int]$ApiPort = 5080,
    [int]$WebPort = 4177
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$script:ApiBaseUrl = "http://127.0.0.1:$ApiPort"
$script:WebBaseUrl = "http://127.0.0.1:$WebPort"
$script:Key = "FleetOps_Demo_Video_Internal_Key_1234567890"
$outputPath = Join-Path $root $OutputDirectory
$logPath = Join-Path $outputPath 'logs'
$rawWebPath = Join-Path $outputPath 'playwright'
$rawAndroidPath = Join-Path $outputPath 'android'
$processes = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$envBackup = @{}
$script:OperatorToken = $null
$script:AdminToken = $null
$script:DriverToken = $null

New-Item -ItemType Directory -Force -Path $outputPath, $logPath, $rawWebPath, $rawAndroidPath | Out-Null

function Set-DemoEnvironment {
    param([hashtable]$Values)
    foreach ($entry in $Values.GetEnumerator()) {
        if (-not $envBackup.ContainsKey($entry.Key)) {
            $envBackup[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
        }
        [Environment]::SetEnvironmentVariable($entry.Key, [string]$entry.Value, 'Process')
    }
}

function Clear-DemoEnvironment {
    foreach ($key in @($envBackup.Keys)) {
        [Environment]::SetEnvironmentVariable($key, $envBackup[$key], 'Process')
    }
    $envBackup.Clear()
    foreach ($key in @([Environment]::GetEnvironmentVariables('Process').Keys | Where-Object { $_ -like 'DemoEngine__Agents__*' })) {
        [Environment]::SetEnvironmentVariable($key, $null, 'Process')
    }
}

function Invoke-Api {
    param([string]$Method, [string]$Path, $Body, [string]$Token)
    $headers = @{}
    if ($Token) { $headers['Authorization'] = "Bearer $Token" }
    $request = @{ Uri = "$script:ApiBaseUrl$Path"; Method = $Method; UseBasicParsing = $true; TimeoutSec = 60 }
    if ($headers.Count -gt 0) { $request['Headers'] = $headers }
    if ($null -ne $Body) {
        $request['Body'] = ($Body | ConvertTo-Json -Depth 12 -Compress)
        $request['ContentType'] = 'application/json'
    }
    try {
        $response = Invoke-WebRequest @request
    }
    catch [System.Net.WebException] {
        $detail = ''
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $detail = $reader.ReadToEnd()
        }
        throw "API $Method $Path failed: $($_.Exception.Message) $detail"
    }
    if ([string]::IsNullOrWhiteSpace($response.Content)) { return $null }
    return ($response.Content | ConvertFrom-Json)
}

function Try-Api {
    param([string]$Method, [string]$Path, $Body, [string]$Token)
    try { return Invoke-Api -Method $Method -Path $Path -Body $Body -Token $Token }
    catch { Write-Host "IGNORED: $($_.Exception.Message)"; return $null }
}

function Login {
    param([string]$Email, [string]$Password)
    $response = Invoke-Api -Method Post -Path '/api/auth/login' -Body @{ email = $Email; password = $Password }
    return $response.accessToken
}

function Get-TokenClaim {
    param([string]$Token, [string]$Claim)
    $payload = $Token.Split('.')[1].Replace('-', '+').Replace('_', '/')
    switch ($payload.Length % 4) {
        2 { $payload += '==' }
        3 { $payload += '=' }
    }
    $json = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload)) | ConvertFrom-Json
    return $json.$Claim
}

function Wait-Url {
    param([string]$Url, [int]$TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) { return }
        }
        catch { Start-Sleep -Seconds 2 }
    }
    throw "Timed out waiting for $Url"
}

function Start-DemoApi {
    $apiDll = Join-Path $root 'apps/backend/FleetOps.Api/bin/Debug/net10.0/FleetOps.Api.dll'
    if (-not (Test-Path $apiDll)) { throw "Missing API build output: $apiDll" }
    Set-DemoEnvironment @{
        'ASPNETCORE_ENVIRONMENT' = 'DemoTesting'
        'ASPNETCORE_URLS' = $script:ApiBaseUrl
        'Testing__UseInMemoryDatabase' = 'true'
        'Testing__DatabaseName' = 'fleetops-demo-video'
        'Bootstrap__SeedDemoData' = 'true'
        'Bootstrap__PublicDemoOnly' = 'false'
        'PublicDemo__Enabled' = 'true'
        'PublicDemo__SideEffectsSandboxed' = 'true'
        'PublicDemo__SessionLifetimeSeconds' = '1800'
        'PublicDemo__LaunchPermitLimit' = '100'
        'PublicDemo__MaxConcurrentSessions' = '50'
        'InternalApi__Key' = $script:Key
        'FLEETOPS_WEB_URL' = $script:WebBaseUrl
        'Jwt__Issuer' = 'FleetOps.Tests'
        'Jwt__Audience' = 'FleetOps.Tests.Web'
        'Jwt__SigningKey' = 'FleetOps_Tests_Signing_Key_12345678901234567890'
        'Jwt__TokenLifetimeMinutes' = '300'
        'Security__LoginPermitLimit' = '5000'
        'Integrations__RetryBaseDelaySeconds' = '0'
        'Integrations__MaxWebhookAttempts' = '3'
    }
    $process = Start-Process -FilePath 'dotnet' -ArgumentList @('exec', "`"$apiDll`"") `
        -WorkingDirectory $root -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $logPath 'api.log') `
        -RedirectStandardError (Join-Path $logPath 'api.err.log') -PassThru
    $processes.Add($process) | Out-Null
    Wait-Url -Url "$script:ApiBaseUrl/health/ready" -TimeoutSeconds 120
    Write-Host 'API ready.'
}

function Start-DemoWorker {
    param([string]$Name, [hashtable]$Environment)
    $workerDll = Join-Path $root 'apps/backend/FleetOps.Worker/bin/Debug/net10.0/FleetOps.Worker.dll'
    if (-not (Test-Path $workerDll)) { throw "Missing Worker build output: $workerDll" }
    Set-DemoEnvironment $Environment
    $stdout = Join-Path $logPath "$Name.log"
    $stderr = Join-Path $logPath "$Name.err.log"
    $process = Start-Process -FilePath 'dotnet' -ArgumentList @('exec', "`"$workerDll`"") `
        -WorkingDirectory $root -WindowStyle Hidden `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    $processes.Add($process) | Out-Null
    Start-Sleep -Seconds 6
    if ($process.HasExited) {
        $tail = Get-Content $stderr -Tail 12 -ErrorAction SilentlyContinue
        throw "Worker $Name exited early with code $($process.ExitCode): $tail"
    }
    Write-Host "Worker $Name started (PID $($process.Id))."
}

function Get-VehicleByRegistration {
    param($Vehicles, [string]$Registration)
    $vehicle = $Vehicles | Where-Object { $_.registrationNumber -eq $Registration } | Select-Object -First 1
    if (-not $vehicle) { throw "Vehicle $Registration was not found in the seed." }
    return $vehicle
}

function New-AssignedMission {
    param(
        [string]$Reference,
        [string]$Title,
        [string]$DriverId,
        [string]$VehicleId,
        [datetimeoffset]$StartUtc
    )
    $mission = Invoke-Api -Method Post -Path '/api/v1/dispatch/missions' -Token $script:OperatorToken -Body @{
        reference = $Reference
        title = $Title
        scheduledStartUtc = $StartUtc.ToString('o')
        scheduledEndUtc = $StartUtc.AddHours(2).ToString('o')
        stops = @(
            @{ sequence = 1; name = 'Depot'; address = '1 Dispatch Way'; plannedArrivalUtc = $StartUtc.AddMinutes(30).ToString('o') },
            @{ sequence = 2; name = 'Client'; address = '22 Fleet Street'; plannedArrivalUtc = $StartUtc.AddMinutes(90).ToString('o') }
        )
    }
    $planned = Invoke-Api -Method Post -Path "/api/v1/dispatch/missions/$($mission.id)/status" -Token $script:OperatorToken -Body @{
        targetStatus = 1; rowVersion = $mission.rowVersion
    }
    $assigned = Invoke-Api -Method Put -Path "/api/v1/dispatch/missions/$($planned.id)/assignment" -Token $script:OperatorToken -Body @{
        driverId = $DriverId; vehicleId = $VehicleId; rowVersion = $planned.rowVersion
    }
    $ready = Invoke-Api -Method Post -Path "/api/v1/dispatch/missions/$($assigned.id)/status" -Token $script:OperatorToken -Body @{
        targetStatus = 2; rowVersion = $assigned.rowVersion
    }
    return $ready
}

function Add-DemoProof {
    param($Mission)
    $samplePng = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9pN96ZQAAAAASUVORK5CYII='
    $bytes = [Convert]::FromBase64String($samplePng)
    $session = Invoke-Api -Method Post -Path '/api/v1/driver/uploads/sessions' -Token $script:DriverToken -Body @{
        fileName = 'demo-proof.png'; contentType = 'image/png'; totalBytes = $bytes.Length; purpose = 2
    }
    Invoke-Api -Method Post -Path "/api/v1/driver/uploads/sessions/$($session.uploadSessionId)/chunks" -Token $script:DriverToken -Body @{
        offset = 0; base64Content = $samplePng
    } | Out-Null
    $asset = Invoke-Api -Method Post -Path "/api/v1/driver/uploads/sessions/$($session.uploadSessionId)/complete" -Token $script:DriverToken
    $stopId = $Mission.stops[1].id
    Invoke-Api -Method Post -Path "/api/v1/driver/missions/$($Mission.id)/stops/$stopId/proof" -Token $script:DriverToken -Body @{
        commandId = "demo-proof-$($Mission.id)"
        recipientName = 'Taylor Receiver'
        signatureName = 'Taylor Receiver'
        deliveredAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        notes = 'SIMULATED DEMO PROOF'
        photos = @(
            @{ mediaAssetId = $asset.assetId; caption = 'Delivery photo' },
            @{ mediaAssetId = $asset.assetId; caption = 'Recipient signature' }
        )
    } | Out-Null
}

function Provision-DemoData {
    Write-Host 'Provisioning demo data through the canonical API...'
    $script:OperatorToken = Login -Email 'operator@northwind.local' -Password 'Operator123!'
    $script:AdminToken = Login -Email 'admin@northwind.local' -Password 'Admin123!'
    $script:DriverToken = Login -Email 'driver@northwind.local' -Password 'Driver123!'
    $organizationId = Get-TokenClaim -Token $script:OperatorToken -Claim 'organization_id'

    $vehicles = Invoke-Api -Method Get -Path '/api/v1/fleet/vehicles' -Token $script:OperatorToken
    $drivers = Invoke-Api -Method Get -Path '/api/v1/fleet/drivers' -Token $script:OperatorToken
    $alex = $drivers | Where-Object { $_.licenseNumber -eq 'NW-DL-001' } | Select-Object -First 1
    if (-not $alex) { throw 'Driver Alex North was not found in the seed.' }

    $now = (Get-Date).ToUniversalTime()
    $agentVehicles = @('NW-113', 'NW-114', 'NW-115', 'NW-116', 'NW-117', 'NW-118')
    $script:AgentConfig = @()
    $index = 0
    foreach ($registration in $agentVehicles) {
        $vehicle = Get-VehicleByRegistration -Vehicles $vehicles -Registration $registration
        $mission = New-AssignedMission -Reference "NW-VIDEO-A$($index + 1)" -Title "Tournee agent virtuel $($index + 1)" `
            -DriverId $alex.id -VehicleId $vehicle.id -StartUtc $now.AddDays(10).AddHours($index * 3)
        $script:AgentConfig += @{
            AgentId = [guid]::NewGuid().ToString()
            OrganizationId = $organizationId
            DriverId = $alex.id
            VehicleId = $vehicle.id
            MissionId = $mission.id
            StopId = $mission.stops[0].id
        }
        $index++
    }

    $driverVehicle1 = Get-VehicleByRegistration -Vehicles $vehicles -Registration 'NW-107'
    $driverVehicle2 = Get-VehicleByRegistration -Vehicles $vehicles -Registration 'NW-108'
    $driverMission1 = New-AssignedMission -Reference 'NW-VIDEO-DRV-1' -Title 'Tournee conducteur filmee' -DriverId $alex.id -VehicleId $driverVehicle1.id -StartUtc $now.AddHours(2)
    $script:DriverMissionId = $driverMission1.id
    New-AssignedMission -Reference 'NW-VIDEO-DRV-2' -Title 'Tournee conducteur de secours' -DriverId $alex.id -VehicleId $driverVehicle2.id -StartUtc $now.AddHours(4) | Out-Null

    $showcaseVehicle = Get-VehicleByRegistration -Vehicles $vehicles -Registration 'NW-100'
    $showcase = New-AssignedMission -Reference 'NW-VIDEO-SHOW' -Title 'Livraison avec preuve signee' -DriverId $alex.id -VehicleId $showcaseVehicle.id -StartUtc $now.AddDays(1)
    Add-DemoProof -Mission $showcase
    $recipient = Invoke-Api -Method Post -Path "/api/v1/dispatch/missions/$($showcase.id)/recipient-status/links" -Token $script:OperatorToken -Body @{
        expiresAtUtc = $now.AddDays(7).ToString('o')
    }
    Set-DemoEnvironment @{ 'DEMO_RECIPIENT_URL' = "$script:WebBaseUrl$($recipient.url)" }
    Write-Host "Recipient status link: $($recipient.url)"

    $delayVehicle = Get-VehicleByRegistration -Vehicles $vehicles -Registration 'NW-109'
    $delayed = New-AssignedMission -Reference 'NW-VIDEO-DELAY' -Title 'Tournee exception retard' -DriverId $alex.id -VehicleId $delayVehicle.id -StartUtc $now.AddDays(3)
    Invoke-Api -Method Post -Path "/api/v1/dispatch/missions/$($delayed.id)/delay-simulation" -Token $script:OperatorToken -Body @{
        delayMinutes = 25; rowVersion = $delayed.rowVersion
    } | Out-Null

    $campaign = Try-Api -Method Post -Path '/api/v1/compliance/campaigns' -Token $script:AdminToken -Body @{
        name = 'Controle pre-depart Alex'
        opensAtUtc = $now.ToString('o')
        closesAtUtc = $now.AddDays(7).ToString('o')
        templateCode = 'vehicle-ready'
        vehicleIds = @($showcaseVehicle.id)
    }
    if ($campaign -and $campaign.id) {
        Invoke-Api -Method Post -Path "/api/v1/compliance/campaigns/$($campaign.id)/activate" -Token $script:AdminToken | Out-Null
    }

    Invoke-Api -Method Post -Path "/api/v1/fleet/vehicles/$($showcaseVehicle.id)/maintenance-plans" -Token $script:AdminToken -Body @{
        title = 'Vidange moteur'
        intervalKilometers = 5000
        intervalDays = 90
        lastCompletedOdometerKm = 0
        lastCompletedAtUtc = $now.AddDays(-100).ToString('o')
    } | Out-Null
    Invoke-Api -Method Post -Path "/api/v1/fleet/vehicles/$($showcaseVehicle.id)/documents" -Token $script:AdminToken -Body @{
        documentType = 'Insurance'
        documentNumber = 'DEMO-POL-1'
        expiresAtUtc = $now.AddDays(10).ToString('o')
        notes = $null
    } | Out-Null
    Invoke-Api -Method Post -Path '/api/v1/alerts/scan' -Token $script:OperatorToken | Out-Null

    Try-Api -Method Post -Path '/api/v1/fleet/devices' -Token $script:AdminToken -Body @{
        serialNumber = 'NW-GPS-900'; displayName = 'Traceur de demonstration'
    } | Out-Null
    Try-Api -Method Post -Path '/api/v1/tracking/geofences' -Token $script:AdminToken -Body @{
        name = 'Depot Northwind'; shape = 'Circle'; centerLatitude = 48.775; centerLongitude = 9.18; radiusMeters = 3000; polygon = $null
    } | Out-Null
    Try-Api -Method Post -Path '/api/v1/admin/integrations/api-keys' -Token $script:AdminToken -Body @{
        name = 'Northwind partner feed'; credentialType = 1; scopes = @('partner.fleet.read')
    } | Out-Null
    Try-Api -Method Post -Path '/api/v1/admin/integrations/webhooks' -Token $script:AdminToken -Body @{
        name = 'Sandbox fleet observer'; eventType = 'fleet.vehicle.created'; targetUrl = $null; signingSecret = 'sandbox-demo-secret'; isSandbox = $true
    } | Out-Null
    Write-Host 'Demo data provisioned.'
}

function Start-AndroidEmulator {
    $sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { Join-Path $env:LOCALAPPDATA 'Android\Sdk' }
    $script:Adb = Join-Path $sdk 'platform-tools\adb.exe'
    $script:EmulatorExe = Join-Path $sdk 'emulator\emulator.exe'
    if (-not (Test-Path $script:Adb) -or -not (Test-Path $script:EmulatorExe)) { throw 'Android SDK tools were not found.' }
    $devices = & $script:Adb devices | Select-Object -Skip 1 | Where-Object { $_ -match "\tdevice$" }
    if ($devices.Count -eq 0) {
        Write-Host 'Starting Android emulator in the background...'
        $process = Start-Process -FilePath $script:EmulatorExe -ArgumentList @(
            '-avd', 'Medium_Phone_API_35', '-no-window', '-no-audio', '-no-boot-anim',
            '-gpu', 'swiftshader_indirect', '-no-snapshot-save'
        ) -WindowStyle Hidden -PassThru
        $processes.Add($process) | Out-Null
    }
}

function Wait-AndroidEmulator {
    for ($bootAttempt = 0; $bootAttempt -lt 2; $bootAttempt++) {
        $deadline = (Get-Date).AddMinutes(9)
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 5
            $boot = (& $script:Adb shell getprop sys.boot_completed 2>$null)
            if ($boot -match '1') {
                & $script:Adb shell settings put global window_animation_scale 0 | Out-Null
                & $script:Adb shell settings put global transition_animation_scale 0 | Out-Null
                & $script:Adb shell settings put global animator_duration_scale 0 | Out-Null
                Write-Host 'Android emulator ready.'
                return
            }
        }
        Write-Host 'Emulator boot timed out; restarting it once...'
        Get-Process | Where-Object { $_.ProcessName -match 'qemu|emulator' } | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 5
        Start-AndroidEmulator
    }
    throw 'The Android emulator did not finish booting.'
}

function Get-UiDump {
    for ($attempt = 0; $attempt -lt 6; $attempt++) {
        & $script:Adb shell uiautomator dump /sdcard/fleetops-video-window.xml | Out-Null
        $xml = (& $script:Adb shell cat /sdcard/fleetops-video-window.xml) -join ''
        if ($xml -match '<hierarchy') {
            try { return [xml]$xml } catch { }
        }
        Start-Sleep -Seconds 1
    }
    throw 'The Android UI hierarchy could not be captured.'
}

function Get-Node {
    param([string]$Text, [switch]$Contains)
    try { $hierarchy = Get-UiDump } catch { return $null }
    foreach ($node in $hierarchy.SelectNodes('//node')) {
        $candidate = if ($node.text) { $node.text } else { $node.'content-desc' }
        if (-not $candidate) { continue }
        if ($Contains) {
            if ($candidate -like "*$Text*") { return $node }
        }
        elseif ($candidate -eq $Text) { return $node }
    }
    return $null
}

function Tap-Node {
    param($Node, [string]$Description)
    if (-not $Node) { throw "Android UI element '$Description' was not found." }
    if ($Node.bounds -notmatch '\[(\d+),(\d+)\]\[(\d+),(\d+)\]') { throw "Android bounds for '$Description' are invalid." }
    $x = [int](([int]$matches[1] + [int]$matches[3]) / 2)
    $y = [int](([int]$matches[2] + [int]$matches[4]) / 2)
    & $script:Adb shell input tap $x $y | Out-Null
    return @{ X = $x; Y = $y; Top = [int]$matches[2]; Bottom = [int]$matches[4] }
}

function Tap-ByText {
    param([string]$Text, [switch]$Contains)
    $node = $null
    $deadline = (Get-Date).AddSeconds(20)
    while (-not $node -and (Get-Date) -lt $deadline) {
        $node = Get-Node -Text $Text -Contains:$Contains
        if (-not $node) { Start-Sleep -Seconds 2 }
    }
    if (-not $node) {
        try {
            $safeName = ($Text -replace '[^A-Za-z0-9-]', '_')
            & $script:Adb shell uiautomator dump "/sdcard/fleetops-video-failure.xml" | Out-Null
            & $script:Adb pull /sdcard/fleetops-video-failure.xml (Join-Path $rawAndroidPath "ui-failure-$safeName.xml") 2>$null | Out-Null
        }
        catch { }
    }
    $result = Tap-Node -Node $node -Description $Text
    Write-Host ("TAP '{0}' at ({1},{2})" -f $Text, $result.X, $result.Y)
    return $result
}

function Scroll-ToText {
    param([string]$Text, [switch]$Contains, [ValidateSet('Forward', 'Backward')][string]$Direction = 'Forward', [int]$MaxSwipes = 14)
    for ($attempt = 0; $attempt -lt $MaxSwipes; $attempt++) {
        $node = Get-Node -Text $Text -Contains:$Contains
        if ($node) { return $node }
        if ($Direction -eq 'Forward') {
            & $script:Adb shell input swipe 360 1350 360 450 350 | Out-Null
        }
        else {
            & $script:Adb shell input swipe 360 450 360 1350 350 | Out-Null
        }
        Start-Sleep -Milliseconds 800
    }
    throw "Could not scroll to '$Text'."
}

function Tap-Scrolled {
    param([string]$Text, [switch]$Contains, [ValidateSet('Forward', 'Backward')][string]$Direction = 'Forward')
    $node = Scroll-ToText -Text $Text -Contains:$Contains -Direction $Direction
    $result = Tap-Node -Node $node -Description $Text
    Write-Host ("TAP '{0}' at ({1},{2})" -f $Text, $result.X, $result.Y)
    return $result
}

function Tap-Checkbox {
    for ($attempt = 0; $attempt -lt 4; $attempt++) {
        $node = $null
        try {
            $hierarchy = Get-UiDump
            $node = $hierarchy.SelectNodes('//node[@checkable="true" and @checked="false"]') | Select-Object -First 1
            if (-not $node) { $node = $hierarchy.SelectNodes('//node[@class="android.widget.CheckBox"]') | Select-Object -First 1 }
        }
        catch { }
        if (-not $node) { throw 'No unchecked checkbox was found on the Android screen.' }
        $result = Tap-Node -Node $node -Description 'checkbox'
        Write-Host ("TAP checkbox at ({0},{1})" -f $result.X, $result.Y)
        Start-Sleep -Seconds 2
        try {
            $after = Get-UiDump
            $button = $after.SelectNodes('//node') | Where-Object { $_.text -eq 'Queue delivery proof' } | Select-Object -First 1
            if ($button -and $button.enabled -eq 'true') {
                Write-Host 'Consent checkbox confirmed; proof submission is enabled.'
                return $result
            }
        }
        catch { }
        Write-Host "Consent checkbox not confirmed yet (attempt $($attempt + 1))."
    }
    throw 'The consent checkbox could not be confirmed.'
}

function Invoke-AndroidSegment {
    param([string]$Name, [scriptblock]$Actions)
    $devicePath = "/sdcard/$Name.mp4"
    $record = Start-Process -FilePath $script:Adb -ArgumentList @(
        'shell', 'screenrecord', '--size', '720x1560', '--bit-rate', '8000000', '--time-limit', '180', $devicePath
    ) -WindowStyle Hidden -PassThru
    try {
        & $Actions
    }
    finally {
        & $script:Adb shell pkill -INT screenrecord | Out-Null
        Start-Sleep -Seconds 3
        if (-not $record.HasExited) { Stop-Process -Id $record.Id -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
    }
    $localPath = Join-Path $rawAndroidPath "$Name.mp4"
    & $script:Adb pull $devicePath $localPath | Out-Null
    if (-not (Test-Path $localPath)) { throw "Android recording $Name was not pulled." }
    Write-Host "Android segment saved: $localPath"
}

function Record-AndroidChapter {
    Write-Host 'Recording the Android driver chapter...'
    if (-not $env:ANDROID_HOME) {
        $defaultAndroidSdk = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
        if (Test-Path $defaultAndroidSdk) {
            Set-DemoEnvironment @{ 'ANDROID_HOME' = $defaultAndroidSdk; 'ANDROID_SDK_ROOT' = $defaultAndroidSdk }
        }
    }
    $androidStudioJbr = 'C:\Program Files\Android\Android Studio\jbr'
    if (Test-Path $androidStudioJbr) {
        Set-DemoEnvironment @{ 'JAVA_HOME' = $androidStudioJbr }
    }
    Wait-AndroidEmulator
    & $script:Adb reverse "tcp:$ApiPort" "tcp:$ApiPort" | Out-Null
    if (-not $SkipBuild) {
        Push-Location (Join-Path $root 'apps/android-driver')
        try {
            Set-DemoEnvironment @{ 'FLEETOPS_API_URL' = "$script:ApiBaseUrl/" }
            & ./gradlew.bat assembleDebug --stacktrace
            if ($LASTEXITCODE -ne 0) { throw 'Android debug build failed.' }
        }
        finally { Pop-Location }
    }
    $apk = Join-Path $root 'apps/android-driver/app/build/outputs/apk/debug/app-debug.apk'
    if (-not (Test-Path $apk)) { throw "Missing Android APK: $apk" }
    & $script:Adb install -r $apk | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Android APK installation failed.' }
    & $script:Adb shell pm clear com.fleetops.driver | Out-Null
    & $script:Adb shell am start -n com.fleetops.driver/.MainActivity | Out-Null
    $loginVisible = $false
    for ($loginAttempt = 0; $loginAttempt -lt 3 -and -not $loginVisible; $loginAttempt++) {
        if ($loginAttempt -gt 0) {
            Write-Host 'Retrying the Android app launch...'
            & $script:Adb shell am start -n com.fleetops.driver/.MainActivity | Out-Null
        }
        $deadline = (Get-Date).AddSeconds(45)
        while (-not $loginVisible -and (Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 3
            try { $loginVisible = $null -ne (Get-Node -Text 'Sign in') } catch { }
        }
    }
    if (-not $loginVisible) {
        & $script:Adb exec-out screencap -p > (Join-Path $rawAndroidPath 'login-failure.png')
        throw 'The Android driver login screen did not become visible.'
    }
    Start-Sleep -Seconds 2

    Invoke-AndroidSegment -Name 'fleetops-driver-1' -Actions {
        Tap-ByText -Text 'Sign in' | Out-Null
        Start-Sleep -Seconds 10
        Tap-ByText -Text 'Assigned missions' | Out-Null
        Start-Sleep -Seconds 8
        Tap-ByText -Text 'NW-VIDEO-DRV-1' -Contains | Out-Null
        Start-Sleep -Seconds 8
        Tap-Scrolled -Text 'Queue ready-to-drive inspection' | Out-Null
        Start-Sleep -Seconds 6
        Tap-Scrolled -Text 'Start mission' -Direction Backward | Out-Null
        Start-Sleep -Seconds 6
        Tap-Scrolled -Text 'Confirm arrival' | Out-Null
        Start-Sleep -Seconds 6
    }

    Invoke-AndroidSegment -Name 'fleetops-driver-2' -Actions {
        Tap-Scrolled -Text 'Capture delivery photo' | Out-Null
        Start-Sleep -Seconds 3
        Tap-ByText -Text 'Use camera' -Contains | Out-Null
        $takePhoto = $null
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            Start-Sleep -Seconds 2
            $permission = Get-Node -Text 'While using the app' -Contains
            if ($permission) {
                Tap-Node -Node $permission -Description 'camera permission' | Out-Null
                Start-Sleep -Seconds 2
                continue
            }
            $takePhoto = Get-Node -Text 'Take photo'
            if ($takePhoto) { break }
        }
        if (-not $takePhoto) { throw 'The camera capture dialog did not become available.' }
        Tap-Node -Node $takePhoto -Description 'Take photo' | Out-Null
        Start-Sleep -Seconds 5
        Tap-Scrolled -Text 'Capture handwritten signature' | Out-Null
        Start-Sleep -Seconds 3
        $instruction = Get-Node -Text 'Ask the recipient to sign inside the box' -Contains
        if ($instruction) {
            $info = Tap-Node -Node $instruction -Description 'signature instructions'
            $x = $info.X
            $y = $info.Bottom + 120
            & $script:Adb shell input swipe ($x - 220) $y ($x + 220) ($y + 180) 600 | Out-Null
            & $script:Adb shell input swipe ($x + 180) ($y + 60) ($x - 160) ($y + 240) 600 | Out-Null
        }
        Start-Sleep -Seconds 2
        Tap-ByText -Text 'Use signature' | Out-Null
        Start-Sleep -Seconds 3
        Tap-Checkbox | Out-Null
        Start-Sleep -Seconds 2
        Tap-Scrolled -Text 'Queue delivery proof' | Out-Null
        Start-Sleep -Seconds 12
        Tap-Scrolled -Text 'Complete mission' -Direction Backward | Out-Null
        Start-Sleep -Seconds 8
        Tap-Scrolled -Text 'Back to mission list' -Direction Backward | Out-Null
        Start-Sleep -Seconds 6
        try {
            Tap-Scrolled -Text 'Refresh from server' | Out-Null
            Start-Sleep -Seconds 10
        }
        catch {
            Write-Host "Refresh button was not reachable: $($_.Exception.Message)"
        }
    }
    if ($script:DriverMissionId) {
        $deadline = (Get-Date).AddSeconds(150)
        $status = $null
        $proofCount = 0
        while ((Get-Date) -lt $deadline) {
            $mission = Invoke-Api -Method Get -Path "/api/v1/dispatch/missions/$($script:DriverMissionId)" -Token $script:OperatorToken
            $status = $mission.status
            $proofCount = @($mission.deliveryProofs).Count
            Write-Host "Android driver mission status after recording: $status (proofs: $proofCount)"
            if (("$status" -eq '6' -or "$status" -eq 'Completed') -and $proofCount -ge 1) { break }
            Start-Sleep -Seconds 6
        }
        if ("$status" -ne '6' -and "$status" -ne 'Completed') {
            throw "The Android driver mission did not reach Completed; last status: $status"
        }
        if ($proofCount -lt 1) {
            throw 'The Android driver mission completed without a delivery proof.'
        }
    }
    Write-Host 'Android chapter recorded.'
}

function Merge-Videos {
    param([string]$OutputFile)
    $webVideos = Get-ChildItem -Path $rawWebPath -Recurse -Filter 'video.webm' -ErrorAction SilentlyContinue |
        Sort-Object { $_.Directory.Name }
    if ($webVideos.Count -eq 0) { throw "No Playwright chapter videos were found in $rawWebPath." }
    $ordered = [System.Collections.Generic.List[string]]::new()
    $intro = $webVideos | Where-Object { $_.Directory.Name -like '11*' } | Select-Object -First 1
    $outro = $webVideos | Where-Object { $_.Directory.Name -like '12*' } | Select-Object -First 1
    foreach ($video in $webVideos) {
        if ($video.Directory.Name -like '11*' -or $video.Directory.Name -like '12*') { continue }
        $ordered.Add($video.FullName)
    }
    if ($intro) { $ordered.Add($intro.FullName) }
    if (-not $SkipAndroid) {
        foreach ($segment in @('fleetops-driver-1.mp4', 'fleetops-driver-2.mp4')) {
            $path = Join-Path $rawAndroidPath $segment
            if (Test-Path $path) { $ordered.Add($path) }
        }
    }
    if ($outro) { $ordered.Add($outro.FullName) }

    $ffmpeg = Get-ChildItem -Path (Join-Path $root '.runtime/tools/ffmpeg') -Recurse -Filter 'ffmpeg.exe' -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $ffmpeg) {
        $candidate = Get-Command ffmpeg -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($candidate) { $ffmpeg = $candidate.Source }
    }
    if (-not $ffmpeg) { throw 'A full ffmpeg binary is required for merging; none was found.' }

    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($file in $ordered) { $arguments.Add('-i'); $arguments.Add($file) }
    $filters = [System.Text.StringBuilder]::new()
    for ($i = 0; $i -lt $ordered.Count; $i++) {
        [void]$filters.Append("[$i`:v]scale=1600:900:force_original_aspect_ratio=decrease,pad=1600:900:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1,fps=25[v$i];")
    }
    $labels = (0..($ordered.Count - 1) | ForEach-Object { "[v$_]" }) -join ''
    [void]$filters.Append("$labels`concat=n=$($ordered.Count):v=1:a=0[out]")
    $arguments.Add('-filter_complex'); $arguments.Add($filters.ToString())
    $arguments.Add('-map'); $arguments.Add('[out]')
    $arguments.Add('-c:v'); $arguments.Add('libx264')
    $arguments.Add('-preset'); $arguments.Add('veryfast')
    $arguments.Add('-crf'); $arguments.Add('24')
    $arguments.Add('-pix_fmt'); $arguments.Add('yuv420p')
    $arguments.Add('-movflags'); $arguments.Add('+faststart')
    $arguments.Add('-y'); $arguments.Add($OutputFile)
    Write-Host "Merging $($ordered.Count) segments into $OutputFile ..."
    & $ffmpeg @arguments
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg merge failed with exit code $LASTEXITCODE." }
}

Push-Location $root
try {
    if ($MergeOnly) {
        $stamp = Get-Date -Format 'yyyyMMdd-HHmm'
        $final = Join-Path $outputPath "FleetOps-demo-$stamp.mp4"
        Merge-Videos -OutputFile $final
        $size = [Math]::Round((Get-Item $final).Length / 1MB, 1)
        Write-Host "FINAL VIDEO: $final ($size MB)"
        return
    }

    if (-not $SkipBuild -and -not $SkipBackendBuild) {
        Write-Host 'Building backend...'
        & dotnet build FleetOps.slnx -c Debug --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }
    }

    Start-DemoApi
    Provision-DemoData

    $workerAEnvironment = @{
        'Testing__UseInMemoryDatabase' = 'true'
        'Testing__DatabaseName' = 'fleetops-demo-video-worker-a'
        'DemoEngine__Enabled' = 'true'
        'DemoEngine__RuntimeMode' = 'Demo'
        'DemoEngine__ApiBaseUrl' = $script:ApiBaseUrl
        'DemoEngine__InternalApiKey' = $script:Key
        'DemoEngine__ApiAccessToken' = $script:OperatorToken
        'DemoEngine__DriverAccessToken' = $script:DriverToken
        'DemoEngine__OrganizationSlug' = 'northwind'
        'DemoEngine__Scenario' = 'LATE_DELIVERY'
        'DemoEngine__Seed' = '3201'
        'DemoEngine__TickSeconds' = '2'
        'DemoEngine__SpeedMultiplier' = '6'
        'DemoEngine__StatePath' = (Join-Path $outputPath 'engine-northwind.json')
        'DemoEngine__SideEffectsSandboxed' = 'true'
        'Alerting__WorkerScanIntervalMinutes' = '1'
    }
    $agentIndex = 0
    foreach ($agent in $script:AgentConfig) {
        $workerAEnvironment["DemoEngine__Agents__$($agentIndex)__AgentId"] = $agent.AgentId
        $workerAEnvironment["DemoEngine__Agents__$($agentIndex)__OrganizationId"] = $agent.OrganizationId
        $workerAEnvironment["DemoEngine__Agents__$($agentIndex)__DriverId"] = $agent.DriverId
        $workerAEnvironment["DemoEngine__Agents__$($agentIndex)__VehicleId"] = $agent.VehicleId
        $workerAEnvironment["DemoEngine__Agents__$($agentIndex)__MissionId"] = $agent.MissionId
        $workerAEnvironment["DemoEngine__Agents__$($agentIndex)__StopId"] = $agent.StopId
        $workerAEnvironment["DemoEngine__Agents__$($agentIndex)__DriverAccessToken"] = $script:DriverToken
        $agentIndex++
    }
    Start-DemoWorker -Name 'worker-northwind-agents' -Environment $workerAEnvironment
    Start-DemoWorker -Name 'worker-public-demo' -Environment @{
        'Testing__UseInMemoryDatabase' = 'true'
        'Testing__DatabaseName' = 'fleetops-demo-video-worker-b'
        'DemoEngine__Enabled' = 'true'
        'DemoEngine__RuntimeMode' = 'Demo'
        'DemoEngine__ApiBaseUrl' = $script:ApiBaseUrl
        'DemoEngine__InternalApiKey' = $script:Key
        'DemoEngine__OrganizationSlug' = 'public-demo'
        'DemoEngine__Scenario' = 'NORMAL_SHIFT'
        'DemoEngine__Seed' = '4201'
        'DemoEngine__TickSeconds' = '2'
        'DemoEngine__SpeedMultiplier' = '4'
        'DemoEngine__StatePath' = (Join-Path $outputPath 'engine-public-demo.json')
        'DemoEngine__SideEffectsSandboxed' = 'true'
    }

    if (-not $SkipAndroid) {
        Start-AndroidEmulator
        Write-Host 'Emulator boot continues in the background while the web chapters record.'
    }

    if (-not $SkipWeb) {
        Write-Host 'Recording the web walkthrough...'
        Set-DemoEnvironment @{
            'DEMO_PACE' = $Pace.ToString([System.Globalization.CultureInfo]::InvariantCulture)
            'DEMO_VIDEO_OUTPUT' = '../../.runtime/demo-video/playwright'
            'PLAYWRIGHT_API_BASE_URL' = $script:ApiBaseUrl
            'PLAYWRIGHT_WEB_BASE_URL' = $script:WebBaseUrl
        }
        Push-Location (Join-Path $root 'apps/web')
        try {
            $npmArguments = @('run', 'demo:video')
            if ($Grep) { $npmArguments += @('--', '--grep', $Grep) }
            & npm @npmArguments
            if ($LASTEXITCODE -ne 0) { throw "The web walkthrough recording failed with exit code $LASTEXITCODE." }
        }
        finally { Pop-Location }
    }

    if (-not $SkipAndroid) { Record-AndroidChapter }

    if (-not $SkipMerge) {
        $stamp = Get-Date -Format 'yyyyMMdd-HHmm'
        $final = Join-Path $outputPath "FleetOps-demo-$stamp.mp4"
        Merge-Videos -OutputFile $final
        $size = [Math]::Round((Get-Item $final).Length / 1MB, 1)
        Write-Host "FINAL VIDEO: $final ($size MB)"
    }
}
finally {
    if (-not $KeepRunning) {
        foreach ($process in $processes) {
            if ($process -and -not $process.HasExited) {
                Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            }
        }
        $listeners = Get-NetTCPConnection -LocalPort $ApiPort -State Listen -ErrorAction SilentlyContinue
        foreach ($listener in $listeners) {
            Stop-Process -Id $listener.OwningProcess -Force -ErrorAction SilentlyContinue
        }
    }
    Clear-DemoEnvironment
    Pop-Location
}
