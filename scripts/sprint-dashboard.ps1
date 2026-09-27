param([switch]$Watch, [int]$IntervalSeconds = 5)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Show-SprintDashboard {
  $state = Get-Content (Join-Path $root '.agent\PROJECT_STATE.json') -Raw | ConvertFrom-Json
  $lockPath = Join-Path $root '.agent\sprint.lock.json'
  $lock = if (Test-Path $lockPath) { Get-Content $lockPath -Raw | ConvertFrom-Json } else { $null }
  $remaining = @($state.sprints.psobject.Properties | Where-Object { $_.Value -ne 'DONE' -and $_.Value -ne 'SUPERSEDED' }).Count
  Write-Host "FleetOps sprint dashboard — $(Get-Date -Format s)" -ForegroundColor Cyan
  Write-Host "Active: $($state.activeSprint) | Execution: $($state.execution.status) | Remaining: $remaining"
  Write-Host "Batch: $($state.execution.batch.completedCount)/$($state.execution.batch.requestedCount)"
  if ($lock) { Write-Host "Lock: $($lock.sprint) by $($lock.owner), expires $($lock.expiresAtUtc)" } else { Write-Host 'Lock: none' }
  Write-Host "Last gate: $($state.lastQualityGate.status) at $($state.lastQualityGate.timestampUtc)"
  if ($state.execution.lastActivity) {
    Write-Host "Last activity: $($state.execution.lastActivity.status) at $($state.execution.lastActivity.timestampUtc)"
    Write-Host "  $($state.execution.lastActivity.summary)"
  }
  if ($state.execution.status -eq 'RUNNING') {
    Write-Host 'Runtime: interactive Codex turn or explicit command only; the lock is not a background worker.' -ForegroundColor Yellow
  } elseif ($state.execution.status -eq 'IDLE' -and $state.sprints.$($state.activeSprint) -eq 'PARTIAL') {
    Write-Host 'Runtime: paused partial sprint; Start Next Sprint will resume it.' -ForegroundColor Yellow
  }
  Write-Host ''
  Write-Host 'Working tree:' -ForegroundColor Cyan
  git -C $root status --short --branch
  Write-Host ''
  $log = Join-Path $root ('.runtime\' + ($state.activeSprint.ToLowerInvariant().Replace('-', '') + '-progress.log'))
  if (Test-Path $log) {
    Write-Host "Latest progress ($log):" -ForegroundColor Cyan
    Get-Content $log -Tail 12
  } else { Write-Host 'No active sprint progress log yet.' }
}

do { Show-SprintDashboard; if ($Watch) { Start-Sleep -Seconds ([Math]::Max(1, $IntervalSeconds)) } } while ($Watch)
