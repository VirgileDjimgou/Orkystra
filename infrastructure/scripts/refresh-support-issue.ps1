[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PacketDirectory,
    [string]$ApiBaseUrl = "http://127.0.0.1:5043",
    [string]$ApiKey = "",
    [string]$SupportBundlePath = "",
    [string]$IssueDraftPath = "",
    [string]$RefreshReason = "retry-after-reset",
    [switch]$RebootstrapDemo,
    [switch]$ArchiveCurrentStateBeforeRefresh,
    [string]$TenantId = "",
    [int]$Count = 10
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $PacketDirectory)) {
    throw "Support packet directory not found: $PacketDirectory"
}

$packetDirectory = (Resolve-Path $PacketDirectory).Path
$manifestPath = Join-Path $packetDirectory "SUPPORT_MANIFEST.json"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
$checkpointPath = Join-Path $repositoryRoot "docs\\operations\\post-release-candidate-checkpoint.md"
$checkpointEstimate = ""

if (Test-Path $checkpointPath) {
    $checkpointEstimate = Select-String -Path $checkpointPath -Pattern "more consistent sprints" | Select-Object -First 1 | ForEach-Object { $_.Line.Trim() }
}

if (-not (Test-Path $manifestPath)) {
    throw "SUPPORT_MANIFEST.json is required to refresh a support packet."
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

function Set-ObjectPropertyValue {
    param(
        [object]$TargetObject,
        [string]$PropertyName,
        [object]$Value
    )

    $TargetObject | Add-Member -NotePropertyName $PropertyName -NotePropertyValue $Value -Force
}

function Invoke-GitText {
    param(
        [string]$RepositoryRoot,
        [string[]]$Arguments
    )

    try {
        $output = & git -C $RepositoryRoot @Arguments 2>$null
        if ($LASTEXITCODE -ne 0) {
            return ""
        }

        return (($output | Out-String).Trim())
    }
    catch {
        return ""
    }
}

function Get-DocumentedReleaseVersionHint {
    param([string]$RepositoryRoot)

    $releaseDirectory = Join-Path $RepositoryRoot "docs\\operations\\releases"
    if (-not (Test-Path $releaseDirectory)) {
        return ""
    }

    $candidate = Get-ChildItem -Path $releaseDirectory -Filter "*-manifest.json" |
        Sort-Object Name -Descending |
        Select-Object -First 1

    if (-not $candidate) {
        return ""
    }

    return ($candidate.BaseName -replace "-manifest$", "")
}

function Get-ReleaseContext {
    param(
        [string]$RepositoryRoot,
        [string]$CheckpointEstimate
    )

    $branch = Invoke-GitText -RepositoryRoot $RepositoryRoot -Arguments @("rev-parse", "--abbrev-ref", "HEAD")
    $commit = Invoke-GitText -RepositoryRoot $RepositoryRoot -Arguments @("rev-parse", "HEAD")
    $tag = Invoke-GitText -RepositoryRoot $RepositoryRoot -Arguments @("describe", "--tags", "--exact-match")
    $dirtyStatus = Invoke-GitText -RepositoryRoot $RepositoryRoot -Arguments @("status", "--porcelain")

    return [ordered]@{
        repositoryBranch = $branch
        repositoryCommit = $commit
        repositoryShortCommit = if ([string]::IsNullOrWhiteSpace($commit)) { "" } else { $commit.Substring(0, [Math]::Min(8, $commit.Length)) }
        repositoryTag = $tag
        repositoryIsDirty = -not [string]::IsNullOrWhiteSpace($dirtyStatus)
        releaseVersionHint = if ([string]::IsNullOrWhiteSpace($tag)) { Get-DocumentedReleaseVersionHint -RepositoryRoot $RepositoryRoot } else { $tag }
        releasePosture = "release-candidate"
        releaseGuideFile = "docs/operations/release-candidate.md"
        checkpointGuideFile = "docs/operations/post-release-candidate-checkpoint.md"
        checkpointEstimate = $CheckpointEstimate
    }
}

function Get-RuntimeContext {
    param(
        [string]$ApiBaseUrl,
        [string]$SupportBundlePath,
        [string]$IssueDraftPath
    )

    return [ordered]@{
        capturedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        hostName = [System.Environment]::MachineName
        userName = [System.Environment]::UserName
        operatingSystem = [System.Environment]::OSVersion.VersionString
        powerShellVersion = $PSVersionTable.PSVersion.ToString()
        apiBaseUrl = if ([string]::IsNullOrWhiteSpace($SupportBundlePath)) { $ApiBaseUrl } else { "" }
        bundleSource = if ([string]::IsNullOrWhiteSpace($SupportBundlePath)) { "live-api-export" } else { "existing-file" }
        issueDraftSource = if ([string]::IsNullOrWhiteSpace($IssueDraftPath)) { "existing-live-draft" } else { "existing-file" }
    }
}

function Read-AttemptHistoryFile {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        return @()
    }

    $raw = Get-Content $Path -Raw | ConvertFrom-Json

    if ($raw -is [System.Array]) {
        return @($raw)
    }

    if ($raw.PSObject.Properties.Name.Contains("value")) {
        return @($raw.value)
    }

    if ($null -eq $raw) {
        return @()
    }

    return ,$raw
}

function Write-AttemptHistoryFile {
    param(
        [string]$Path,
        [object[]]$AttemptItems
    )

    $normalizedAttempts = @(
        $AttemptItems | ForEach-Object {
            [ordered]@{
                attempt = [int](Get-ObjectMemberValue -TargetObject $_ -MemberName "attempt")
                createdAtUtc = [string](Get-ObjectMemberValue -TargetObject $_ -MemberName "createdAtUtc")
                reason = [string](Get-ObjectMemberValue -TargetObject $_ -MemberName "reason")
                validationStatus = [string](Get-ObjectMemberValue -TargetObject $_ -MemberName "validationStatus")
                releaseContext = Get-ObjectMemberValue -TargetObject $_ -MemberName "releaseContext"
                runtimeContext = Get-ObjectMemberValue -TargetObject $_ -MemberName "runtimeContext"
            }
        }
    )

    ([object[]]$normalizedAttempts) | ConvertTo-Json -Depth 6 | Set-Content -Path $Path -Encoding UTF8
}

function Get-ObjectMemberValue {
    param(
        [object]$TargetObject,
        [string]$MemberName
    )

    if ($null -eq $TargetObject) {
        return $null
    }

    if ($TargetObject -is [System.Collections.IDictionary] -and $TargetObject.Contains($MemberName)) {
        return $TargetObject[$MemberName]
    }

    if ($TargetObject.PSObject.Properties.Name.Contains($MemberName)) {
        return $TargetObject.$MemberName
    }

    return $null
}

if ([string]::IsNullOrWhiteSpace($TenantId)) {
    $TenantId = [string]$manifest.tenantId
}

if ($ArchiveCurrentStateBeforeRefresh) {
    $archiveScriptPath = Join-Path $PSScriptRoot "archive-support-issue.ps1"
    & $archiveScriptPath -PacketDirectory $packetDirectory -Reason $RefreshReason
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
}

if ($RebootstrapDemo) {
    $resetScriptPath = Join-Path $PSScriptRoot "reset-demo-state.ps1"
    $resetApiKey = if ($ApiKey.Length -gt 0) { $ApiKey } else { "orkystra-dev-local-key" }
    & $resetScriptPath -Rebootstrap -TenantId $TenantId -ApiKey $resetApiKey
}

$bundleOutputPath = Join-Path $packetDirectory "support-bundle.json"

if (-not [string]::IsNullOrWhiteSpace($SupportBundlePath)) {
    if (-not (Test-Path $SupportBundlePath)) {
        throw "Support bundle path not found: $SupportBundlePath"
    }

    Copy-Item -Path $SupportBundlePath -Destination $bundleOutputPath -Force
} else {
    if ([string]::IsNullOrWhiteSpace($ApiKey)) {
        throw "Provide -ApiKey or -SupportBundlePath when refreshing the support packet."
    }

    $exportScriptPath = Join-Path $PSScriptRoot "export-support-bundle.ps1"
    & $exportScriptPath `
        -ApiBaseUrl $ApiBaseUrl `
        -ApiKey $ApiKey `
        -TenantId $TenantId `
        -Count $Count `
        -OutputPath $bundleOutputPath
}

$issueDraftOutputPath = Join-Path $packetDirectory "ISSUE_DRAFT.md"

if (-not [string]::IsNullOrWhiteSpace($IssueDraftPath)) {
    if (-not (Test-Path $IssueDraftPath)) {
        throw "Issue draft path not found: $IssueDraftPath"
    }

    Copy-Item -Path $IssueDraftPath -Destination $issueDraftOutputPath -Force
}

Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "refreshCount" -Value ([int]$manifest.refreshCount + 1)
Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "currentAttempt" -Value ([int]$manifest.currentAttempt + 1)
Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "lastRefreshReason" -Value $RefreshReason
Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "lastRefreshedAtUtc" -Value ((Get-Date).ToUniversalTime().ToString("o"))
Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "lifecycleSummaryFile" -Value "SUPPORT_LIFECYCLE.json"
Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "releaseContext" -Value (Get-ReleaseContext -RepositoryRoot $repositoryRoot -CheckpointEstimate $checkpointEstimate)
Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "runtimeContext" -Value (Get-RuntimeContext -ApiBaseUrl $ApiBaseUrl -SupportBundlePath $SupportBundlePath -IssueDraftPath $IssueDraftPath)
$manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $manifestPath -Encoding UTF8

$attemptsPath = Join-Path $packetDirectory "SUPPORT_ATTEMPTS.json"
$attempts = [System.Collections.Generic.List[object]]::new()
foreach ($attemptItem in (Read-AttemptHistoryFile -Path $attemptsPath)) {
    $attempts.Add($attemptItem)
}

$attemptEntry = [ordered]@{
    attempt = [int]$manifest.currentAttempt
    createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    reason = $RefreshReason
    validationStatus = ""
    releaseContext = $manifest.releaseContext
    runtimeContext = $manifest.runtimeContext
}

$attempts.Add([pscustomobject]$attemptEntry)
Write-AttemptHistoryFile -Path $attemptsPath -AttemptItems $attempts

$summaryScriptPath = Join-Path $PSScriptRoot "summarize-support-issue.ps1"
& $summaryScriptPath -PacketDirectory $packetDirectory | Out-Null

$validationScriptPath = Join-Path $PSScriptRoot "validate-support-issue.ps1"
$validationOutputPath = Join-Path $packetDirectory "SUPPORT_VALIDATION.json"
& $validationScriptPath -PacketDirectory $packetDirectory -OutputPath $validationOutputPath

$validationReport = Get-Content $validationOutputPath -Raw | ConvertFrom-Json
$attempts = Read-AttemptHistoryFile -Path $attemptsPath
Set-ObjectPropertyValue -TargetObject $attempts[-1] -PropertyName "validationStatus" -Value $validationReport.status
Write-AttemptHistoryFile -Path $attemptsPath -AttemptItems $attempts

& $summaryScriptPath -PacketDirectory $packetDirectory | Out-Null
$lifecycleReport = Get-Content (Join-Path $packetDirectory "SUPPORT_LIFECYCLE.json") -Raw | ConvertFrom-Json

$handoffPath = Join-Path $packetDirectory "MAINTAINER_HANDOFF.md"

@"
# Maintainer Handoff

- Packet directory: $packetDirectory
- Tenant: $TenantId
- Validation status: $($validationReport.status)
- Escalation target: $($validationReport.escalationTarget)
- Support posture: $($validationReport.posture)
- Packet class: $($lifecycleReport.packetClass)
- Triage lane: $($lifecycleReport.triageLane)
- Next owner: $($lifecycleReport.nextOwner)
- Current attempt: $($manifest.currentAttempt)
- Last refresh reason: $RefreshReason
- Current maturity checkpoint: $checkpointEstimate

## Release context

- Branch: $($lifecycleReport.releaseContext.repositoryBranch)
- Commit: $($lifecycleReport.releaseContext.repositoryShortCommit)
- Tag or version hint: $($lifecycleReport.releaseContext.releaseVersionHint)
- Release posture: $($lifecycleReport.releaseContext.releasePosture)
- Dirty worktree: $($lifecycleReport.releaseContext.repositoryIsDirty)

## Runtime context

- Host: $($lifecycleReport.runtimeContext.hostName)
- OS: $($lifecycleReport.runtimeContext.operatingSystem)
- PowerShell: $($lifecycleReport.runtimeContext.powerShellVersion)
- Bundle source: $($lifecycleReport.runtimeContext.bundleSource)
- Issue draft source: $($lifecycleReport.runtimeContext.issueDraftSource)

## Evidence gap score

- Strong categories: $($lifecycleReport.evidenceGapScore.strongCount)
- Weak categories: $($lifecycleReport.evidenceGapScore.weakCount)
- Absent categories: $($lifecycleReport.evidenceGapScore.absentCount)
- Top priority category: $($lifecycleReport.evidenceGapScore.topPriorityCategory)
- Top priority next action: $($lifecycleReport.evidenceGapScore.topPriorityNextAction)

## Evidence gap categories

$(if (@($lifecycleReport.evidenceGapScore.categories).Count -gt 0) { ($lifecycleReport.evidenceGapScore.categories | ForEach-Object { "- $($_.category) | severity: $($_.severity) | $($_.summary) | next: $($_.nextAction)" }) -join "`r`n" } else { "- No evidence gap categories are currently recorded." })

## Remediation checklist

- Posture: $($lifecycleReport.remediationChecklist.posture)
- Headline: $($lifecycleReport.remediationChecklist.headline)
- Focus category: $($lifecycleReport.remediationChecklist.focusCategory)
- Next owner: $($lifecycleReport.remediationChecklist.nextOwner)
- Handoff ready after pass: $($lifecycleReport.remediationChecklist.handoffReady)

$(if (@($lifecycleReport.remediationChecklist.items).Count -gt 0) { ($lifecycleReport.remediationChecklist.items | ForEach-Object { "- Step $($_.order) | focus: $($_.focus) | owner: $($_.owner) | action: $($_.action)" }) -join "`r`n" } else { "- No remediation steps are currently recorded." })

## Remediation stop conditions

$(if (@($lifecycleReport.remediationChecklist.stopConditions).Count -gt 0) { ($lifecycleReport.remediationChecklist.stopConditions | ForEach-Object { "- $_" }) -join "`r`n" } else { "- No remediation stop conditions are currently recorded." })

## Capture guidance

- Label: $($lifecycleReport.captureGuidance.label)
- Summary: $($lifecycleReport.captureGuidance.summary)
- Preset id: $($lifecycleReport.captureGuidance.presetId)
- Preset label: $($lifecycleReport.captureGuidance.presetLabel)
- Release posture: $($lifecycleReport.captureGuidance.releasePosture)
- Packet class: $($lifecycleReport.captureGuidance.packetClass)
- Triage lane: $($lifecycleReport.captureGuidance.triageLane)
- Next owner: $($lifecycleReport.captureGuidance.nextOwner)

## Capture preset actions

$(if (@($lifecycleReport.captureGuidance.presetActions).Count -gt 0) { ($lifecycleReport.captureGuidance.presetActions | ForEach-Object { "- $_" }) -join "`r`n" } else { "- No capture preset actions are currently recorded." })

## Capture shortcuts

$(if (@($lifecycleReport.captureGuidance.shortcuts).Count -gt 0) { ($lifecycleReport.captureGuidance.shortcuts | ForEach-Object { "- $_" }) -join "`r`n" } else { "- No capture shortcuts are currently recorded." })

## Capture checks

$(if (@($lifecycleReport.captureGuidance.checks).Count -gt 0) { ($lifecycleReport.captureGuidance.checks | ForEach-Object { "- $_" }) -join "`r`n" } else { "- No capture checks are currently recorded." })

## Capture exit criteria

$(if (@($lifecycleReport.captureGuidance.exitCriteria).Count -gt 0) { ($lifecycleReport.captureGuidance.exitCriteria | ForEach-Object { "- $_" }) -join "`r`n" } else { "- No capture exit criteria are currently recorded." })

## Context drift

- Posture: $($lifecycleReport.contextDrift.posture)
- Changed fields: $(if (@($lifecycleReport.contextDrift.changedFields).Count -gt 0) { ($lifecycleReport.contextDrift.changedFields -join ", ") } else { "none" })
- Summary: $($lifecycleReport.contextDrift.summary)

## Reading order

$(($lifecycleReport.evidenceProvenance.readingOrder | ForEach-Object { "- $($_.order). $($_.file) | role: $($_.role) | required: $($_.required) | canonical: $($_.canonical) | present: $($_.present)" }) -join "`r`n")

## Optional comparison context

$(if (@($lifecycleReport.evidenceProvenance.optionalFiles).Count -gt 0) { ($lifecycleReport.evidenceProvenance.optionalFiles | ForEach-Object { "- $($_.file) | $($_.summary)" }) -join "`r`n" } else { "- No optional comparison artifacts are currently present." })

## Included files

- support-bundle.json
- ISSUE_DRAFT.md
- SUPPORT_MANIFEST.json
- SUPPORT_ATTEMPTS.json
- SUPPORT_VALIDATION.json
- SUPPORT_LIFECYCLE.json

## Triage shortcuts

$(($lifecycleReport.triageChecks | ForEach-Object { "- $_" }) -join "`r`n")

## Maintainer action

Use `SUPPORT_ATTEMPTS.json` to see whether this packet was regenerated after a retry or reset. Prefer the latest validation status and the latest issue draft when reviewing the packet.
"@ | Set-Content -Path $handoffPath -Encoding UTF8

& $summaryScriptPath -PacketDirectory $packetDirectory | Out-Null
$lifecycleReport = Get-Content (Join-Path $packetDirectory "SUPPORT_LIFECYCLE.json") -Raw | ConvertFrom-Json

@"
# Maintainer Handoff

- Packet directory: $packetDirectory
- Tenant: $TenantId
- Validation status: $($validationReport.status)
- Escalation target: $($validationReport.escalationTarget)
- Support posture: $($validationReport.posture)
- Packet class: $($lifecycleReport.packetClass)
- Triage lane: $($lifecycleReport.triageLane)
- Next owner: $($lifecycleReport.nextOwner)
- Current attempt: $($manifest.currentAttempt)
- Last refresh reason: $RefreshReason
- Current maturity checkpoint: $checkpointEstimate

## Release context

- Branch: $($lifecycleReport.releaseContext.repositoryBranch)
- Commit: $($lifecycleReport.releaseContext.repositoryShortCommit)
- Tag or version hint: $($lifecycleReport.releaseContext.releaseVersionHint)
- Release posture: $($lifecycleReport.releaseContext.releasePosture)
- Dirty worktree: $($lifecycleReport.releaseContext.repositoryIsDirty)

## Runtime context

- Host: $($lifecycleReport.runtimeContext.hostName)
- OS: $($lifecycleReport.runtimeContext.operatingSystem)
- PowerShell: $($lifecycleReport.runtimeContext.powerShellVersion)
- Bundle source: $($lifecycleReport.runtimeContext.bundleSource)
- Issue draft source: $($lifecycleReport.runtimeContext.issueDraftSource)

## Evidence gap score

- Strong categories: $($lifecycleReport.evidenceGapScore.strongCount)
- Weak categories: $($lifecycleReport.evidenceGapScore.weakCount)
- Absent categories: $($lifecycleReport.evidenceGapScore.absentCount)
- Top priority category: $($lifecycleReport.evidenceGapScore.topPriorityCategory)
- Top priority next action: $($lifecycleReport.evidenceGapScore.topPriorityNextAction)

## Evidence gap categories

$(if (@($lifecycleReport.evidenceGapScore.categories).Count -gt 0) { ($lifecycleReport.evidenceGapScore.categories | ForEach-Object { "- $($_.category) | severity: $($_.severity) | $($_.summary) | next: $($_.nextAction)" }) -join "`r`n" } else { "- No evidence gap categories are currently recorded." })

## Remediation checklist

- Posture: $($lifecycleReport.remediationChecklist.posture)
- Headline: $($lifecycleReport.remediationChecklist.headline)
- Focus category: $($lifecycleReport.remediationChecklist.focusCategory)
- Next owner: $($lifecycleReport.remediationChecklist.nextOwner)
- Handoff ready after pass: $($lifecycleReport.remediationChecklist.handoffReady)

$(if (@($lifecycleReport.remediationChecklist.items).Count -gt 0) { ($lifecycleReport.remediationChecklist.items | ForEach-Object { "- Step $($_.order) | focus: $($_.focus) | owner: $($_.owner) | action: $($_.action)" }) -join "`r`n" } else { "- No remediation steps are currently recorded." })

## Remediation stop conditions

$(if (@($lifecycleReport.remediationChecklist.stopConditions).Count -gt 0) { ($lifecycleReport.remediationChecklist.stopConditions | ForEach-Object { "- $_" }) -join "`r`n" } else { "- No remediation stop conditions are currently recorded." })

## Context drift

- Posture: $($lifecycleReport.contextDrift.posture)
- Changed fields: $(if (@($lifecycleReport.contextDrift.changedFields).Count -gt 0) { ($lifecycleReport.contextDrift.changedFields -join ", ") } else { "none" })
- Summary: $($lifecycleReport.contextDrift.summary)

## Reading order

$(($lifecycleReport.evidenceProvenance.readingOrder | ForEach-Object { "- $($_.order). $($_.file) | role: $($_.role) | required: $($_.required) | canonical: $($_.canonical) | present: $($_.present)" }) -join "`r`n")

## Optional comparison context

$(if (@($lifecycleReport.evidenceProvenance.optionalFiles).Count -gt 0) { ($lifecycleReport.evidenceProvenance.optionalFiles | ForEach-Object { "- $($_.file) | $($_.summary)" }) -join "`r`n" } else { "- No optional comparison artifacts are currently present." })

## Included files

- support-bundle.json
- ISSUE_DRAFT.md
- SUPPORT_MANIFEST.json
- SUPPORT_ATTEMPTS.json
- SUPPORT_VALIDATION.json
- SUPPORT_LIFECYCLE.json

## Triage shortcuts

$(($lifecycleReport.triageChecks | ForEach-Object { "- $_" }) -join "`r`n")

## Maintainer action

Use `SUPPORT_ATTEMPTS.json` to see whether this packet was regenerated after a retry or reset. Prefer the latest validation status and the latest issue draft when reviewing the packet.
"@ | Set-Content -Path $handoffPath -Encoding UTF8

Write-Host "Support packet refreshed in $packetDirectory" -ForegroundColor Green
