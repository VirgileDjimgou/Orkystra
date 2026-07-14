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

function Get-RemediationChecklist {
    param(
        [object]$EvidenceGapScore,
        [object]$TriageMetadata,
        [string]$ValidationStatus,
        [string]$RecommendedAction,
        [string]$CanonicalPacketState,
        [object]$ContextDrift
    )

    $items = New-Object System.Collections.Generic.List[object]
    $stopConditions = New-Object System.Collections.Generic.List[string]

    if ($EvidenceGapScore -and [string]$EvidenceGapScore.topPriorityNextAction) {
        $items.Add([ordered]@{
            order = 1
            focus = if ([string]$EvidenceGapScore.topPriorityCategory) { [string]$EvidenceGapScore.topPriorityCategory } else { "primary-evidence-gap" }
            owner = [string]$TriageMetadata.nextOwner
            action = [string]$EvidenceGapScore.topPriorityNextAction
            rationale = "Start with the highest-priority weak or absent proof category before broadening the packet."
        })
    }

    foreach ($triageCheck in @($TriageMetadata.triageChecks | Select-Object -First 2)) {
        $items.Add([ordered]@{
            order = $items.Count + 1
            focus = "triage-lane"
            owner = [string]$TriageMetadata.nextOwner
            action = [string]$triageCheck
            rationale = "Keep the next pass aligned with the current packet class and owner."
        })
    }

    if ($ContextDrift -and [string]$ContextDrift.posture -ne "stable-context" -and [string]$ContextDrift.posture -ne "no-prior-attempt" -and [string]$ContextDrift.posture -ne "no-attempt-history") {
        $items.Add([ordered]@{
            order = $items.Count + 1
            focus = "context-drift"
            owner = "Operator"
            action = "Review the changed context fields before treating the newest failure as the same debugging state."
            rationale = [string]$ContextDrift.summary
        })
    }

    if ($RecommendedAction -eq "Refresh") {
        $items.Add([ordered]@{
            order = $items.Count + 1
            focus = "packet-refresh"
            owner = "Operator"
            action = "Refresh the packet only after the next retry reproduces the same failure with better evidence."
            rationale = "A refresh should improve signal quality, not replace the packet with unrelated noise."
        })
    }
    elseif ($RecommendedAction -eq "Regenerate") {
        $items.Add([ordered]@{
            order = $items.Count + 1
            focus = "packet-regeneration"
            owner = "Operator"
            action = "Regenerate the packet until the required files and lifecycle sections are complete."
            rationale = "The current packet is too incomplete to act as the canonical handoff."
        })
    }

    $stopConditions.Add("Stop once validation is Accepted or AcceptedWithWarnings and the top-priority evidence action is reflected in the packet.")
    $stopConditions.Add("Stop if the failure changed materially; archive the old packet before building the next one.")

    if ($CanonicalPacketState -eq "active-packet-is-canonical") {
        $stopConditions.Add("If the packet already remains canonical after the checklist pass, reuse it instead of creating another duplicate handoff.")
    }

    $checklistPosture =
        if ($ValidationStatus -eq "Rejected") { "regenerate-first" }
        elseif ($RecommendedAction -eq "Refresh") { "focused-refresh" }
        else { "targeted-hardening" }
    $focusCategory = if ($EvidenceGapScore) { [string]$EvidenceGapScore.topPriorityCategory } else { "" }
    $handoffReady =
        ($ValidationStatus -eq "Accepted" -or $ValidationStatus -eq "AcceptedWithWarnings") -and
        ([string]$RecommendedAction -ne "Regenerate")
    $headline =
        if ($ValidationStatus -eq "Rejected") { "Regenerate the packet before deeper debugging." }
        elseif ($RecommendedAction -eq "Refresh") { "Run one focused refresh pass against the weakest evidence category." }
        elseif ([string]$EvidenceGapScore.topPriorityCategory) { "Tighten the packet around '$([string]$EvidenceGapScore.topPriorityCategory)' before broadening scope." }
        else { "Keep the packet aligned and only add evidence that materially sharpens the next handoff." }
    $checklist = [ordered]@{}
    $checklist["posture"] = [string]$checklistPosture
    $checklist["headline"] = [string]$headline
    $checklist["packetClass"] = [string]$TriageMetadata.packetClass
    $checklist["triageLane"] = [string]$TriageMetadata.triageLane
    $checklist["nextOwner"] = [string]$TriageMetadata.nextOwner
    $checklist["focusCategory"] = [string]$focusCategory
    $checklist["handoffReady"] = [bool]$handoffReady
    $checklist["items"] = @($items.ToArray())
    $checklist["stopConditions"] = @($stopConditions.ToArray())

    return $checklist
}

function Get-CaptureGuidance {
    param(
        [object]$BundleSummary,
        [object]$ReleaseContext,
        [object]$TriageMetadata,
        [object]$RemediationChecklist,
        [string]$ValidationStatus,
        [string]$RecommendedAction,
        [string]$CanonicalPacketState,
        [object]$ContextDrift
    )

    $shortcuts = New-Object System.Collections.Generic.List[string]
    $presetActions = New-Object System.Collections.Generic.List[string]
    $checks = New-Object System.Collections.Generic.List[string]
    $exitCriteria = New-Object System.Collections.Generic.List[string]

    $escalationTarget = if ($BundleSummary) { [string]$BundleSummary.escalationTarget } else { "" }
    $packetClass = [string]$TriageMetadata.packetClass
    $triageLane = [string]$TriageMetadata.triageLane
    $nextOwner = [string]$TriageMetadata.nextOwner
    $releasePosture =
        if ($ReleaseContext -and $ReleaseContext.PSObject.Properties.Name.Contains("releasePosture") -and -not [string]::IsNullOrWhiteSpace([string]$ReleaseContext.releasePosture)) {
            [string]$ReleaseContext.releasePosture
        }
        else {
            "release-candidate"
        }

    $packetClassPresetSegment =
        if ([string]::IsNullOrWhiteSpace($packetClass)) {
            "general-support-packet"
        }
        else {
            (($packetClass.ToLowerInvariant() -replace "[^a-z0-9]+", "-").Trim("-"))
        }
    $releasePosturePresetSegment = (($releasePosture.ToLowerInvariant() -replace "[^a-z0-9]+", "-").Trim("-"))
    $presetId = "$packetClassPresetSegment-$releasePosturePresetSegment"
    $presetLabel = "$packetClass / $releasePosture"

    if ($escalationTarget -eq "configuration-or-persistence") {
        $label = "Configuration capture shortcut"
        $summary = "Freeze the active deployment posture first, then capture the exact runtime evidence that changed with the failing retry."
        $presetActions.Add("Capture the active branch, commit, and deployment mode before reproducing the failure.")
        $presetActions.Add("Capture persistence posture and configuration deltas from the same failing run.")
        $presetActions.Add("Export one focused support bundle immediately after the matching failure.")
        $checks.Add("Keep the issue draft and support bundle aligned on the same failure run.")
        $checks.Add("Preserve the current artifact checklist unless the capture path changed materially.")
        $checks.Add("Keep the packet class and release posture tied together in the handoff.")
    }
    elseif ($escalationTarget -eq "dependency-or-event-backbone") {
        $label = "Dependency capture shortcut"
        $summary = "Pin down broker or event-flow evidence around the exact retry that failed, not a broad service dump."
        $presetActions.Add("Capture broker and dependency posture from the same failing attempt.")
        $presetActions.Add("Attach only the first failing trace needed to prove the runtime drift.")
        $presetActions.Add("Re-export the packet only if the same dependency failure still reproduces.")
        $checks.Add("Workflow proof should stay anchored to the same failing run.")
        $checks.Add("Audit proof should stay anchored to the same failing run.")
        $checks.Add("Keep the shortcut aligned with the current packet class and triage lane.")
    }
    elseif ($ValidationStatus -ne "Ready" -and $RecommendedAction -ne "Reuse") {
        $label = "Operator capture shortcut"
        $summary = "Tighten the user story, then capture the evidence that proves the exact first failure."
        $presetActions.Add("Tighten summary, reproduction, expected result, and actual result first.")
        $presetActions.Add("Capture workflow and audit evidence from the same failing run.")
        $presetActions.Add("Refresh only after confirming the failure state is still the same.")
        $checks.Add("Use the remediation checklist as the guardrail for the next pass.")
        $checks.Add("Keep the capture scoped to the same failure state.")
    }
    else {
        $label = "Release-aware capture shortcut"
        $summary = "The packet is already strong, so the next capture should be narrow and delta-focused."
        $presetActions.Add("Freeze branch, commit, and release posture before the next capture.")
        $presetActions.Add("Collect only evidence that explains the latest delta from the canonical packet.")
        $presetActions.Add("Archive older state only when the new packet genuinely supersedes it.")
        $checks.Add("Use the current packet as the canonical handoff unless the failure changed.")
        $checks.Add("Only add evidence that explains a real change.")
    }

    foreach ($presetAction in $presetActions) {
        $shortcuts.Add($presetAction)
    }

    if ($ContextDrift -and [string]$ContextDrift.posture -ne "stable-context" -and [string]$ContextDrift.posture -ne "no-prior-attempt" -and [string]$ContextDrift.posture -ne "no-attempt-history") {
        $checks.Add("Context drift is present, so compare the changed repo and runtime fields before reusing the packet.")
    }

    if ($RecommendedAction -eq "Refresh") {
        $exitCriteria.Add("Stop once the new capture makes the weakest category clearly stronger.")
    }
    elseif ($RecommendedAction -eq "Regenerate") {
        $exitCriteria.Add("Stop only after the packet contract is complete enough to be canonical.")
    }
    else {
        $exitCriteria.Add("Stop when the current packet still explains the same failure and no broader capture is needed.")
    }

    $exitCriteria.Add("Keep the packet aligned with the remediation checklist and the current release posture.")
    $checks.Add("Keep the release posture explicit for the packet and note the exact snapshot that produced it.")

    return [ordered]@{
        label = $label
        summary = $summary
        presetId = $presetId
        presetLabel = $presetLabel
        releasePosture = $releasePosture
        packetClass = $packetClass
        triageLane = $triageLane
        nextOwner = $nextOwner
        presetActions = @($presetActions)
        shortcuts = @($shortcuts)
        checks = @($checks)
        exitCriteria = @($exitCriteria)
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
$remediationChecklist = Get-RemediationChecklist -EvidenceGapScore $evidenceGapScore -TriageMetadata $triageMetadata -ValidationStatus $validationStatus -RecommendedAction $recommendedAction -CanonicalPacketState $canonicalPacketState -ContextDrift $contextDrift
$captureGuidance = Get-CaptureGuidance -BundleSummary $bundleSummary -ReleaseContext $releaseContext -TriageMetadata $triageMetadata -RemediationChecklist $remediationChecklist -ValidationStatus $validationStatus -RecommendedAction $recommendedAction -CanonicalPacketState $canonicalPacketState -ContextDrift $contextDrift

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
    remediationChecklist = $remediationChecklist
    captureGuidance = $captureGuidance
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

## Remediation checklist

- Posture: $($summary.remediationChecklist.posture)
- Headline: $($summary.remediationChecklist.headline)
- Packet class: $($summary.remediationChecklist.packetClass)
- Triage lane: $($summary.remediationChecklist.triageLane)
- Next owner: $($summary.remediationChecklist.nextOwner)
- Focus category: $($summary.remediationChecklist.focusCategory)
- Handoff ready after pass: $($summary.remediationChecklist.handoffReady)

$(if (@($summary.remediationChecklist.items).Count -gt 0) { ($summary.remediationChecklist.items | ForEach-Object { "- Step $($_.order) | focus: $($_.focus) | owner: $($_.owner) | action: $($_.action) | rationale: $($_.rationale)" }) -join "`r`n" } else { "- No remediation steps are currently recorded." })

## Remediation stop conditions

$(if (@($summary.remediationChecklist.stopConditions).Count -gt 0) { ($summary.remediationChecklist.stopConditions | ForEach-Object { "- $_" }) -join "`r`n" } else { "- No stop conditions are currently recorded." })

## Capture guidance

- Label: $($summary.captureGuidance.label)
- Summary: $($summary.captureGuidance.summary)
- Preset id: $($summary.captureGuidance.presetId)
- Preset label: $($summary.captureGuidance.presetLabel)
- Release posture: $($summary.captureGuidance.releasePosture)
- Packet class: $($summary.captureGuidance.packetClass)
- Triage lane: $($summary.captureGuidance.triageLane)
- Next owner: $($summary.captureGuidance.nextOwner)

## Capture preset actions

$(if (@($summary.captureGuidance.presetActions).Count -gt 0) { ($summary.captureGuidance.presetActions | ForEach-Object { "- $_" }) -join "`r`n" } else { "- No capture preset actions are currently recorded." })

## Capture shortcuts

$(if (@($summary.captureGuidance.shortcuts).Count -gt 0) { ($summary.captureGuidance.shortcuts | ForEach-Object { "- $_" }) -join "`r`n" } else { "- No capture shortcuts are currently recorded." })

## Capture checks

$(if (@($summary.captureGuidance.checks).Count -gt 0) { ($summary.captureGuidance.checks | ForEach-Object { "- $_" }) -join "`r`n" } else { "- No capture checks are currently recorded." })

## Capture exit criteria

$(if (@($summary.captureGuidance.exitCriteria).Count -gt 0) { ($summary.captureGuidance.exitCriteria | ForEach-Object { "- $_" }) -join "`r`n" } else { "- No capture exit criteria are currently recorded." })

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
