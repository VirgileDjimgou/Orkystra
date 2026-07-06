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

function Get-EvidenceProvenance {
    param(
        [string[]]$PresentActiveFiles,
        [string[]]$CanonicalHandoffFiles,
        [int]$ArchiveCount
    )

    $entries = @(
        [ordered]@{
            order = 1
            file = "ISSUE_DRAFT.md"
            role = "primary-narrative"
            required = $true
            summary = "Operator-written narrative that defines the bug report shape and reproduction steps."
        }
        [ordered]@{
            order = 2
            file = "support-bundle.json"
            role = "primary-runtime-evidence"
            required = $true
            summary = "Canonical runtime evidence bundle for the active packet."
        }
        [ordered]@{
            order = 3
            file = "SUPPORT_VALIDATION.json"
            role = "packet-gate"
            required = $true
            summary = "Validation verdict that says whether the packet should be reused, refreshed, or regenerated."
        }
        [ordered]@{
            order = 4
            file = "SUPPORT_LIFECYCLE.json"
            role = "packet-interpretation"
            required = $true
            summary = "Machine-readable lifecycle and drift interpretation for the active handoff."
        }
        [ordered]@{
            order = 5
            file = "MAINTAINER_HANDOFF.md"
            role = "maintainer-entrypoint"
            required = $true
            summary = "Maintainer-facing reading order and quick decision surface."
        }
        [ordered]@{
            order = 6
            file = "SUPPORT_ATTEMPTS.json"
            role = "comparison-context"
            required = $false
            summary = "Historical attempt record used when comparing retries or drift."
        }
        [ordered]@{
            order = 7
            file = "SUPPORT_ARCHIVE_INDEX.json"
            role = "archive-context"
            required = $false
            summary = "Index of archived packet snapshots for deeper comparison only."
        }
        [ordered]@{
            order = 8
            file = "SUPPORT_LIFECYCLE.md"
            role = "human-readable-summary"
            required = $false
            summary = "Human-readable companion to the lifecycle JSON."
        }
        [ordered]@{
            order = 9
            file = "SUPPORT_MANIFEST.json"
            role = "packet-contract"
            required = $false
            summary = "Packet contract and metadata backing the generated summaries."
        }
    )

    $annotated = @(
        $entries | ForEach-Object {
            [pscustomobject]@{
                order = [int]$_.order
                file = [string]$_.file
                role = [string]$_.role
                required = [bool]$_.required
                present = $PresentActiveFiles -contains [string]$_.file
                canonical = $CanonicalHandoffFiles -contains [string]$_.file
                summary = [string]$_.summary
            }
        }
    )

    return [ordered]@{
        readingOrder = @($annotated | Sort-Object -Property @{ Expression = { [int]$_.order } })
        primaryFiles = @($annotated | Where-Object { $_.required })
        optionalFiles = @($annotated | Where-Object { -not $_.required -and $_.present })
        comparisonContextSummary =
            if ($ArchiveCount -gt 0) {
                "Archived packet snapshots exist, so comparison evidence should stay secondary to the active issue draft and support bundle."
            }
            else {
                "No archived packet snapshots exist yet, so the active issue draft and support bundle remain the only canonical evidence pair."
            }
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
$evidenceGapScore =
    if ($validation -and $validation.PSObject.Properties.Name.Contains("evidenceGapScore")) { $validation.evidenceGapScore }
    else {
        [ordered]@{
            strongCount = 0
            weakCount = 0
            absentCount = 0
            topPriorityCategory = ""
            topPriorityNextAction = ""
            categories = @()
        }
    }
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
    evidenceGapScore = $evidenceGapScore
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
$evidenceProvenance = Get-EvidenceProvenance -PresentActiveFiles $presentActiveFiles -CanonicalHandoffFiles @($summary.canonicalHandoffFiles) -ArchiveCount $summary.archiveCount
$summary.evidenceProvenance = $evidenceProvenance

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

## Evidence gap score

- Strong categories: $($summary.evidenceGapScore.strongCount)
- Weak categories: $($summary.evidenceGapScore.weakCount)
- Absent categories: $($summary.evidenceGapScore.absentCount)
- Top priority category: $($summary.evidenceGapScore.topPriorityCategory)
- Top priority next action: $($summary.evidenceGapScore.topPriorityNextAction)

## Evidence gap categories

$(if (@($summary.evidenceGapScore.categories).Count -gt 0) { ($summary.evidenceGapScore.categories | ForEach-Object { "- $($_.category) | severity: $($_.severity) | $($_.summary) | next: $($_.nextAction)" }) -join "`r`n" } else { "- No evidence gap categories are currently recorded." })

## Context drift

- Posture: $($summary.contextDrift.posture)
- Changed fields: $(if (@($summary.contextDrift.changedFields).Count -gt 0) { ($summary.contextDrift.changedFields -join ", ") } else { "none" })
- Summary: $($summary.contextDrift.summary)

## Evidence provenance

- Comparison posture: $($summary.evidenceProvenance.comparisonContextSummary)

## Reading order

$(($summary.evidenceProvenance.readingOrder | ForEach-Object { "- $($_.order). $($_.file) | role: $($_.role) | required: $($_.required) | canonical: $($_.canonical) | present: $($_.present)" }) -join "`r`n")

## Optional comparison context

$(if (@($summary.evidenceProvenance.optionalFiles).Count -gt 0) { ($summary.evidenceProvenance.optionalFiles | ForEach-Object { "- $($_.file) | $($_.summary)" }) -join "`r`n" } else { "- No optional comparison artifacts are currently present." })

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
