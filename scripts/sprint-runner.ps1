param(
  [ValidateSet('Start', 'Validate', 'Complete', 'Fail', 'Pause', 'Stop', 'ResolveGate', 'Status')][string]$Action = 'Status',
  [string]$Owner = 'powershell-agent',
  [int]$LeaseMinutes = 240,
  [string]$Gate,
  [string]$Checkpoint,
  [string]$ObservedFailure,
  [string[]]$AffectedFile = @(),
  [string]$Reason,
  [string]$Note,
  [switch]$CancelBatch
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$orchestrator = Join-Path $root 'scripts\agent\sprint_orchestrator.py'
$arguments = switch ($Action) {
  'Start' { @($orchestrator, 'start', '--owner', $Owner, '--lease-minutes', $LeaseMinutes) }
  'Validate' { @($orchestrator, 'validate') }
  'Complete' { @($orchestrator, 'complete', '--gate', $Gate, '--checkpoint', $Checkpoint) }
  'Fail' { @($orchestrator, 'fail', '--gate', $Gate, '--observed-failure', $ObservedFailure) + ($AffectedFile | ForEach-Object { @('--affected-file', $_) }) }
  'Stop' { @($orchestrator, 'stop', '--reason', $Reason) }
  'Pause' { @($orchestrator, 'pause', '--note', $Note) + $(if ($CancelBatch) { @('--cancel-batch') } else { @() }) }
  'ResolveGate' { @($orchestrator, 'resolve-gate', '--note', $Note) }
  default { @($orchestrator, 'status') }
}
& python @arguments
exit $LASTEXITCODE
