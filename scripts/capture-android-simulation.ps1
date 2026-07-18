param(
  [int]$ApiPort = 5090,
  [string]$OutputDirectory = "docs/assets/screenshots"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { Join-Path $env:LOCALAPPDATA "Android\Sdk" }
$adb = Join-Path $sdk "platform-tools\adb.exe"
if (-not (Test-Path $adb)) { throw "ADB was not found at $adb." }
$androidStudioJbr = "C:\Program Files\Android\Android Studio\jbr"
if (Test-Path $androidStudioJbr) { $env:JAVA_HOME = $androidStudioJbr }
if (-not $env:JAVA_HOME -or -not (Test-Path $env:JAVA_HOME)) { throw "A Java 17 JDK is required for the Android build." }
$env:ANDROID_HOME = $sdk
$env:ANDROID_SDK_ROOT = $sdk

$devices = & $adb devices | Select-Object -Skip 1 | Where-Object { $_ -match "\tdevice$" }
if ($devices.Count -ne 1) { throw "Exactly one authorized Android device is required; found $($devices.Count)." }

function Invoke-TapByText {
  param([Parameter(Mandatory = $true)][string]$Text, [switch]$Prefix)
  & $adb shell uiautomator dump /sdcard/fleetops-window.xml | Out-Null
  [xml]$hierarchy = (& $adb shell cat /sdcard/fleetops-window.xml) -join ""
  $node = $hierarchy.SelectNodes("//node") | Where-Object {
    if ($Prefix) { $_.text -like "$Text*" } else { $_.text -eq $Text -or $_.'content-desc' -eq $Text }
  } | Select-Object -First 1
  if (-not $node) { throw "Android UI element '$Text' was not found." }
  if ($node.bounds -notmatch "\[(\d+),(\d+)\]\[(\d+),(\d+)\]") { throw "Android UI bounds for '$Text' are invalid." }
  $x = [int](([int]$matches[1] + [int]$matches[3]) / 2)
  $y = [int](([int]$matches[2] + [int]$matches[4]) / 2)
  & $adb shell input tap $x $y | Out-Null
}

function Save-DeviceScreenshot {
  param([Parameter(Mandatory = $true)][string]$Path)
  $absolute = [System.IO.Path]::GetFullPath($Path)
  New-Item -ItemType Directory -Force (Split-Path -Parent $absolute) | Out-Null
  $capture = Start-Process $adb -ArgumentList @('exec-out', 'screencap', '-p') -RedirectStandardOutput $absolute -NoNewWindow -PassThru
  $capture.WaitForExit()
  $capture.Refresh()
  if (-not (Test-Path $absolute) -or (Get-Item $absolute).Length -eq 0) { throw "Android screenshot failed: $absolute" }
}

Push-Location $root
try {
  & $adb reverse tcp:5080 "tcp:$ApiPort" | Out-Null
  $env:FLEETOPS_API_URL = "http://localhost:5080/"
  Push-Location apps/android-driver
  try { ./gradlew.bat assembleDebug --stacktrace } finally { Pop-Location }
  if ($LASTEXITCODE -ne 0) { throw "Android Debug build failed." }

  $apk = Join-Path $root "apps/android-driver/app/build/outputs/apk/debug/app-debug.apk"
  & $adb install -r $apk | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "Android APK installation failed." }
  & $adb shell pm clear com.fleetops.driver | Out-Null
  & $adb shell am start -n com.fleetops.driver/.MainActivity | Out-Null
  Start-Sleep -Seconds 3
  Invoke-TapByText -Text "Sign in"
  Start-Sleep -Seconds 8
  Save-DeviceScreenshot -Path (Join-Path $OutputDirectory "simulation-android-driver-missions.png")
  Invoke-TapByText -Text "SIM-NW-" -Prefix
  Start-Sleep -Seconds 3
  Save-DeviceScreenshot -Path (Join-Path $OutputDirectory "simulation-android-driver-mission-detail.png")
  Write-Host "Android simulation screenshots captured."
}
finally {
  & $adb reverse --remove tcp:5080 | Out-Null
  Pop-Location
}
