param(
  [Parameter(Mandatory = $true)][ValidateRange(1, 10)][int]$Count,
  [string]$Owner = 'powershell-batch-agent',
  [int]$LeaseMinutes = 240
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$orchestrator = Join-Path $root 'scripts\agent\sprint_orchestrator.py'
& python $orchestrator batch-start $Count --owner $Owner --lease-minutes $LeaseMinutes
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Batch intent recorded for $Count sprint(s). Implement only the selected sprint in this context; a fresh context must invoke the next atomic run after completion."
