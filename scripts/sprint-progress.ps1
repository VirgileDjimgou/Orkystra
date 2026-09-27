param(
  [Parameter(Mandatory = $true)]
  [ValidateSet('START','DISCOVERY','IMPLEMENTATION','BUILD','TEST','GATE','PAUSED','DONE','BLOCKED')]
  [string]$Status,
  [Parameter(Mandatory = $true)][string]$Message
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$statePath = Join-Path $root '.agent\PROJECT_STATE.json'
$state = Get-Content $statePath -Raw | ConvertFrom-Json
$sprint = $state.execution.currentSprint
if (-not $sprint) { throw 'No current sprint is recorded.' }
$timestamp = [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
$logName = $sprint.ToLowerInvariant().Replace('-', '') + '-progress.log'
$logPath = Join-Path $root ('.runtime\' + $logName)
New-Item -ItemType Directory -Path (Split-Path $logPath) -Force | Out-Null
Add-Content -LiteralPath $logPath -Value "$timestamp | $Status | $Message"
$state.execution | Add-Member -NotePropertyName lastActivity -NotePropertyValue ([pscustomobject]@{
  status = $Status
  timestampUtc = $timestamp
  summary = $Message
  log = ".runtime/$logName"
}) -Force
$state.updatedAtUtc = $timestamp
$state | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $statePath -Encoding utf8
Write-Host "$sprint | $Status | $Message"
