[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PacketDirectory,
    [string]$JsonOutputPath = "",
    [string]$MarkdownOutputPath = ""
)

$ErrorActionPreference = "Stop"

function Read-StructuredJsonFile {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        return $null
    }

    return Get-Content $Path -Raw | ConvertFrom-Json
}

function Read-ArrayLikeJsonFile {
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

function Set-ObjectPropertyValue {
    param(
        [object]$TargetObject,
        [string]$PropertyName,
        [object]$Value
    )

    $TargetObject | Add-Member -NotePropertyName $PropertyName -NotePropertyValue $Value -Force
}

function Get-TriageMetadata {
    param(
        [string]$ValidationStatus,
        [string]$RecommendedAction,
        [string]$EscalationTarget
    )

    if ($ValidationStatus -eq "Rejected") {
        return [ordered]@{
            packetClass = "incomplete-packet"
            triageLane = "operator-regeneration"
            nextOwner = "Operator"
            triageChecks = @(
                "Regenerate the packet until required files and lifecycle sections are present.",
                "Tighten the issue draft headings and evidence before escalation.",
                "Do not hand the packet to a maintainer yet."
            )
        }
    }

    if ($RecommendedAction -eq "Refresh") {
        return [ordered]@{
            packetClass = "evidence-gap-packet"
            triageLane = "operator-refresh"
            nextOwner = "Operator"
            triageChecks = @(
                "Export a fresh support bundle after the next retry or reset.",
                "Make workflow and audit evidence visible in the packet.",
                "Tighten the expected-versus-actual wording before handoff."
            )
        }
    }

    switch ($EscalationTarget) {
        "configuration-or-persistence" {
            return [ordered]@{
                packetClass = "configuration-review-packet"
                triageLane = "self-host-configuration-review"
                nextOwner = "Operator or deploy maintainer"
                triageChecks = @(
                    "Verify deployment mode and persistence posture first.",
                    "Call out the last configuration change that may have shifted behavior.",
                    "Confirm the failure still reproduces after a clean restart."
                )
            }
        }
        "dependency-or-event-backbone" {
            return [ordered]@{
                packetClass = "runtime-dependency-packet"
                triageLane = "runtime-dependency-review"
                nextOwner = "Platform or runtime maintainer"
                triageChecks = @(
                    "Check broker, dependency, or event-flow drift before workflow logic.",
                    "Use the latest delta to describe what changed across retries.",
                    "Attach only the first failing logs needed to prove the posture."
                )
            }
        }
        "product-or-workflow" {
            return [ordered]@{
                packetClass = "product-defect-candidate"
                triageLane = "workflow-maintainer-review"
                nextOwner = "Workflow maintainer"
                triageChecks = @(
                    "Read the issue draft before opening the raw support bundle.",
                    "Use the lifecycle summary to compare only the latest meaningful delta.",
                    "Treat archived packets as context, not as the main entrypoint."
                )
            }
        }
        default {
            return [ordered]@{
                packetClass = "general-support-packet"
                triageLane = "mixed-triage"
                nextOwner = "Operator"
                triageChecks = @(
                    "Clarify the primary troubleshooting lane before escalation.",
                    "Keep the issue draft and support summary aligned.",
                    "Refresh the packet if the latest retry materially changed the failure."
                )
            }
        }
    }
}

function Get-ContextDriftSummary {
    param(
        [object]$LatestAttempt,
        [object]$PreviousAttempt
    )

    if (-not $LatestAttempt) {
        return [ordered]@{
            posture = "no-attempt-history"
            changedFields = @()
            summary = "No readable attempt history exists yet."
        }
    }

    if (-not $PreviousAttempt) {
        return [ordered]@{
            posture = "no-prior-attempt"
            changedFields = @()
            summary = "No earlier attempt exists yet, so there is no context drift to compare."
        }
    }

    $changes = New-Object System.Collections.Generic.List[string]

    $latestRelease = if ($LatestAttempt.PSObject.Properties.Name.Contains("releaseContext")) { $LatestAttempt.releaseContext } else { $null }
    $previousRelease = if ($PreviousAttempt.PSObject.Properties.Name.Contains("releaseContext")) { $PreviousAttempt.releaseContext } else { $null }
    $latestRuntime = if ($LatestAttempt.PSObject.Properties.Name.Contains("runtimeContext")) { $LatestAttempt.runtimeContext } else { $null }
    $previousRuntime = if ($PreviousAttempt.PSObject.Properties.Name.Contains("runtimeContext")) { $PreviousAttempt.runtimeContext } else { $null }

    if ($latestRelease -and $previousRelease) {
        if ([string]$latestRelease.repositoryBranch -ne [string]$previousRelease.repositoryBranch) {
            $changes.Add("repository-branch")
        }

        if ([string]$latestRelease.repositoryCommit -ne [string]$previousRelease.repositoryCommit) {
            $changes.Add("repository-commit")
        }

        if ([string]$latestRelease.releaseVersionHint -ne [string]$previousRelease.releaseVersionHint) {
            $changes.Add("release-version-hint")
        }

        if ([string]$latestRelease.releasePosture -ne [string]$previousRelease.releasePosture) {
            $changes.Add("release-posture")
        }

        if ([string]$latestRelease.repositoryIsDirty -ne [string]$previousRelease.repositoryIsDirty) {
            $changes.Add("dirty-worktree-posture")
        }
    }
    elseif ($latestRelease -or $previousRelease) {
        $changes.Add("release-context-availability")
    }

    if ($latestRuntime -and $previousRuntime) {
        if ([string]$latestRuntime.hostName -ne [string]$previousRuntime.hostName) {
            $changes.Add("host-name")
        }

        if ([string]$latestRuntime.operatingSystem -ne [string]$previousRuntime.operatingSystem) {
            $changes.Add("operating-system")
        }

        if ([string]$latestRuntime.powerShellVersion -ne [string]$previousRuntime.powerShellVersion) {
            $changes.Add("powershell-version")
        }

        if ([string]$latestRuntime.apiBaseUrl -ne [string]$previousRuntime.apiBaseUrl) {
            $changes.Add("api-base-url")
        }

        if ([string]$latestRuntime.bundleSource -ne [string]$previousRuntime.bundleSource) {
            $changes.Add("bundle-source")
        }

        if ([string]$latestRuntime.issueDraftSource -ne [string]$previousRuntime.issueDraftSource) {
            $changes.Add("issue-draft-source")
        }
    }
    elseif ($latestRuntime -or $previousRuntime) {
        $changes.Add("runtime-context-availability")
    }

    if ($changes.Count -eq 0) {
        return [ordered]@{
            posture = "stable-context"
            changedFields = @()
            summary = "The latest attempt reuses the same repository and runtime context as the previous attempt, so the failure drift is more likely product or data driven than environment driven."
        }
    }

    $releaseDriftCount = @($changes | Where-Object { $_ -like "repository-*" -or $_ -like "release-*" -or $_ -eq "dirty-worktree-posture" }).Count
    $runtimeDriftCount = @($changes | Where-Object { $_ -like "host-*" -or $_ -like "operating-*" -or $_ -like "powershell-*" -or $_ -like "api-*" -or $_ -like "bundle-*" -or $_ -like "issue-*" -or $_ -eq "runtime-context-availability" }).Count

    $posture =
        if ($releaseDriftCount -gt 0 -and $runtimeDriftCount -gt 0) { "release-and-runtime-drift" }
        elseif ($releaseDriftCount -gt 0) { "release-drift" }
        else { "runtime-drift" }

    return [ordered]@{
        posture = $posture
        changedFields = @($changes)
        summary = "The latest attempt was captured under a materially different context than the previous attempt. Review the changed fields before treating the newest failure as the same debugging state."
    }
}

$packetDirectory = (Resolve-Path $PacketDirectory).Path

if ([string]::IsNullOrWhiteSpace($JsonOutputPath)) {
    $JsonOutputPath = Join-Path $packetDirectory "SUPPORT_LIFECYCLE.json"
}

if ([string]::IsNullOrWhiteSpace($MarkdownOutputPath)) {
    $MarkdownOutputPath = Join-Path $packetDirectory "SUPPORT_LIFECYCLE.md"
}

$manifest = Read-StructuredJsonFile -Path (Join-Path $packetDirectory "SUPPORT_MANIFEST.json")
$validation = Read-StructuredJsonFile -Path (Join-Path $packetDirectory "SUPPORT_VALIDATION.json")
$attempts = Read-ArrayLikeJsonFile -Path (Join-Path $packetDirectory "SUPPORT_ATTEMPTS.json")
$archives = Read-ArrayLikeJsonFile -Path (Join-Path $packetDirectory "SUPPORT_ARCHIVE_INDEX.json")
$attemptItems = @($attempts)
$archiveItems = @($archives)

if (-not $manifest) {
    throw "SUPPORT_MANIFEST.json is required to summarize a support packet."
}

$activeFiles = @(
    "support-bundle.json",
    "ISSUE_DRAFT.md",
    "SUPPORT_MANIFEST.json",
    "SUPPORT_ATTEMPTS.json",
    "SUPPORT_VALIDATION.json",
    "MAINTAINER_HANDOFF.md",
    "SUPPORT_LIFECYCLE.json",
    "SUPPORT_LIFECYCLE.md"
)

$presentActiveFiles = @(
    $activeFiles | Where-Object { Test-Path (Join-Path $packetDirectory $_) }
)

$latestAttempt = if ($attemptItems.Length -gt 0) { $attemptItems[$attemptItems.Length - 1] } else { $null }
$previousAttempt = if ($attemptItems.Length -gt 1) { $attemptItems[$attemptItems.Length - 2] } else { $null }
$latestArchive = if ($archiveItems.Length -gt 0) { $archiveItems[$archiveItems.Length - 1] } else { $null }
$validationStatus = if ($validation) { [string]$validation.status } else { "Unknown" }
$recommendedAction = if ($validation) { [string]$validation.recommendedAction } else { "Regenerate" }
$escalationTarget = if ($validation) { [string]$validation.escalationTarget } else { "" }
$triageMetadata = Get-TriageMetadata -ValidationStatus $validationStatus -RecommendedAction $recommendedAction -EscalationTarget $escalationTarget
$releaseContext =
    if ($manifest.PSObject.Properties.Name.Contains("releaseContext")) { $manifest.releaseContext }
    else {
        [ordered]@{
            repositoryBranch = ""
            repositoryCommit = ""
            repositoryShortCommit = ""
            repositoryTag = ""
            repositoryIsDirty = $false
            releaseVersionHint = ""
            releasePosture = ""
            releaseGuideFile = ""
            checkpointGuideFile = ""
            checkpointEstimate = ""
        }
    }
$runtimeContext =
    if ($manifest.PSObject.Properties.Name.Contains("runtimeContext")) { $manifest.runtimeContext }
    else {
        [ordered]@{
            capturedAtUtc = ""
            hostName = ""
            userName = ""
            operatingSystem = ""
            powerShellVersion = ""
            apiBaseUrl = ""
            bundleSource = ""
            issueDraftSource = ""
        }
    }
$canonicalPacketState =
    if ($validationStatus -eq "Rejected") { "refresh-or-regenerate-required" }
    elseif ($recommendedAction -eq "Refresh") { "refresh-advised-before-handoff" }
    else { "active-packet-is-canonical" }
$contextDrift = Get-ContextDriftSummary -LatestAttempt $latestAttempt -PreviousAttempt $previousAttempt

$timeline = @()

foreach ($attempt in $attemptItems) {
    $timeline += [ordered]@{
        kind = "attempt"
        sequence = [int]$attempt.attempt
        label = "Attempt $($attempt.attempt)"
        reason = [string]$attempt.reason
        status = [string]$attempt.validationStatus
        occurredAtUtc = [string]$attempt.createdAtUtc
    }
}

foreach ($archive in $archiveItems) {
    $timeline += [ordered]@{
        kind = "archive"
        sequence = [int]$archive.attempt
        label = "Archive from attempt $($archive.attempt)"
        reason = [string]$archive.reason
        status = "Archived snapshot"
        occurredAtUtc = [string]$archive.archivedAtUtc
    }
}

$timeline = @($timeline | Sort-Object occurredAtUtc, kind)

$deltaNarration =
    if ($latestAttempt -and $latestArchive) {
        "The active packet is attempt $($latestAttempt.attempt) with validation $($latestAttempt.validationStatus). The latest archived snapshot was captured from attempt $($latestArchive.attempt) because '$($latestArchive.reason)'."
    }
    elseif ($attemptItems.Length -gt 1 -and $latestAttempt) {
        "The active packet is attempt $($latestAttempt.attempt) with validation $($latestAttempt.validationStatus). Earlier attempts exist, but no archived snapshot was preserved yet, so compare the latest attempt history directly before escalation. $($contextDrift.summary)"
    }
    elseif ($latestAttempt) {
        "The active packet is still the only known attempt, so there is no archived delta to compare yet."
    }
    else {
        "The packet does not yet contain a readable attempt history."
    }

$summary = [ordered]@{
    summarizedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    packetDirectory = $packetDirectory
    tenantId = [string]$manifest.tenantId
    currentAttempt = [int]$manifest.currentAttempt
    refreshCount = [int]$manifest.refreshCount
    archiveCount = [int]$manifest.archiveCount
    validationStatus = $validationStatus
    recommendedAction = $recommendedAction
    canonicalPacketState = $canonicalPacketState
    packetClass = [string]$triageMetadata.packetClass
    triageLane = [string]$triageMetadata.triageLane
    nextOwner = [string]$triageMetadata.nextOwner
    latestRefreshReason = [string]$manifest.lastRefreshReason
    latestArchiveReason = if ($manifest.PSObject.Properties.Name.Contains("lastArchiveReason")) { [string]$manifest.lastArchiveReason } else { "" }
    escalationTarget = $escalationTarget
    posture = if ($validation) { [string]$validation.posture } else { "" }
    releaseContext = $releaseContext
    runtimeContext = $runtimeContext
    activeFiles = @($presentActiveFiles)
    canonicalHandoffFiles = @(
        "ISSUE_DRAFT.md",
        "support-bundle.json",
        "SUPPORT_MANIFEST.json",
        "SUPPORT_VALIDATION.json",
        "MAINTAINER_HANDOFF.md",
        "SUPPORT_LIFECYCLE.json"
    )
    latestAttempt = $latestAttempt
    previousAttempt = $previousAttempt
    latestArchive = $latestArchive
    timeline = @($timeline)
    deltaNarration = $deltaNarration
    triageChecks = @($triageMetadata.triageChecks)
    contextDrift = $contextDrift
}

Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "canonicalPacketState" -Value $canonicalPacketState
Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "packetClass" -Value ([string]$triageMetadata.packetClass)
Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "triageLane" -Value ([string]$triageMetadata.triageLane)
Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "nextOwner" -Value ([string]$triageMetadata.nextOwner)
Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "activeHandoffFiles" -Value @(
    "ISSUE_DRAFT.md",
    "support-bundle.json",
    "SUPPORT_MANIFEST.json",
    "SUPPORT_VALIDATION.json",
    "MAINTAINER_HANDOFF.md",
    "SUPPORT_LIFECYCLE.json"
)
Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "archiveIndexFile" -Value "SUPPORT_ARCHIVE_INDEX.json"
$manifest | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $packetDirectory "SUPPORT_MANIFEST.json") -Encoding UTF8

$summary | ConvertTo-Json -Depth 8 | Set-Content -Path $JsonOutputPath -Encoding UTF8

@"
# Support Lifecycle Summary

- Packet directory: $packetDirectory
- Tenant: $($summary.tenantId)
- Validation status: $($summary.validationStatus)
- Recommended action: $($summary.recommendedAction)
- Canonical packet state: $($summary.canonicalPacketState)
- Packet class: $($summary.packetClass)
- Triage lane: $($summary.triageLane)
- Next owner: $($summary.nextOwner)
- Current attempt: $($summary.currentAttempt)
- Refresh count: $($summary.refreshCount)
- Archive count: $($summary.archiveCount)
- Escalation target: $($summary.escalationTarget)
- Support posture: $($summary.posture)

## Release context

- Branch: $($summary.releaseContext.repositoryBranch)
- Commit: $($summary.releaseContext.repositoryShortCommit)
- Tag or version hint: $($summary.releaseContext.releaseVersionHint)
- Release posture: $($summary.releaseContext.releasePosture)
- Dirty worktree: $($summary.releaseContext.repositoryIsDirty)
- Checkpoint estimate: $($summary.releaseContext.checkpointEstimate)

## Runtime context

- Captured at: $($summary.runtimeContext.capturedAtUtc)
- Host: $($summary.runtimeContext.hostName)
- OS: $($summary.runtimeContext.operatingSystem)
- PowerShell: $($summary.runtimeContext.powerShellVersion)
- API base URL: $($summary.runtimeContext.apiBaseUrl)
- Bundle source: $($summary.runtimeContext.bundleSource)
- Issue draft source: $($summary.runtimeContext.issueDraftSource)

## Context drift

- Posture: $($summary.contextDrift.posture)
- Changed fields: $(if (@($summary.contextDrift.changedFields).Count -gt 0) { ($summary.contextDrift.changedFields -join ", ") } else { "none" })
- Summary: $($summary.contextDrift.summary)

## Active handoff files

$(($presentActiveFiles | ForEach-Object { "- $_" }) -join "`r`n")

## Canonical handoff files

$(($summary.canonicalHandoffFiles | ForEach-Object { "- $_" }) -join "`r`n")

## Latest attempt

- Attempt: $(if ($latestAttempt) { $latestAttempt.attempt } else { "" })
- Reason: $(if ($latestAttempt) { $latestAttempt.reason } else { "" })
- Validation: $(if ($latestAttempt) { $latestAttempt.validationStatus } else { "" })

## Previous attempt

- Attempt: $(if ($previousAttempt) { $previousAttempt.attempt } else { "" })
- Reason: $(if ($previousAttempt) { $previousAttempt.reason } else { "" })
- Validation: $(if ($previousAttempt) { $previousAttempt.validationStatus } else { "" })

## Latest archive

- Archive id: $(if ($latestArchive) { $latestArchive.archiveId } else { "" })
- Reason: $(if ($latestArchive) { $latestArchive.reason } else { "" })
- Folder: $(if ($latestArchive) { $latestArchive.folder } else { "" })

## Triage shortcuts

$(($summary.triageChecks | ForEach-Object { "- $_" }) -join "`r`n")

## Delta narration

$deltaNarration

## Timeline

$(($timeline | ForEach-Object { "- $($_.occurredAtUtc) | $($_.label) | $($_.reason) | $($_.status)" }) -join "`r`n")
"@ | Set-Content -Path $MarkdownOutputPath -Encoding UTF8

Write-Host "Support lifecycle summary written to $JsonOutputPath and $MarkdownOutputPath" -ForegroundColor Green
