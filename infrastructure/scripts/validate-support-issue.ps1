[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PacketDirectory,
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $PacketDirectory)) {
    throw "Support packet directory not found: $PacketDirectory"
}

$packetDirectory = (Resolve-Path $PacketDirectory).Path

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $packetDirectory "SUPPORT_VALIDATION.json"
}

$requiredFiles = @(
    "support-bundle.json",
    "ISSUE_DRAFT.md",
    "SUPPORT_MANIFEST.json",
    "SUPPORT_ATTEMPTS.json",
    "SUPPORT_LIFECYCLE.json",
    "SUPPORT_LIFECYCLE.md"
)

$missingFiles = New-Object System.Collections.Generic.List[string]

foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path (Join-Path $packetDirectory $requiredFile))) {
        $missingFiles.Add($requiredFile)
    }
}

$errors = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]
$notes = New-Object System.Collections.Generic.List[string]
$evidenceGaps = New-Object System.Collections.Generic.List[object]

if ($missingFiles.Count -gt 0) {
    foreach ($missingFile in $missingFiles) {
        $errors.Add("Missing required file: $missingFile")
    }
}

$bundleSummary = $null
$manifest = $null
$recommendedAction = "Regenerate"

function Get-NormalizedIntValue {
    param([object]$Value)

    $candidate = @($Value)
    if ($candidate.Count -eq 0) {
        return 0
    }

    return [int]$candidate[-1]
}

function Read-AttemptHistoryFile {
    param([string]$Path)

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

function Test-ContextFieldSet {
    param(
        [object]$Target,
        [string[]]$RequiredFields
    )

    if (-not $Target) {
        return $false
    }

    foreach ($field in $RequiredFields) {
        if (-not $Target.PSObject.Properties.Name.Contains($field)) {
            return $false
        }
    }

    return $true
}

function Add-EvidenceGap {
    param(
        [System.Collections.Generic.List[object]]$Target,
        [string]$Category,
        [string]$Severity,
        [string]$Summary,
        [string]$NextAction
    )

    $Target.Add([pscustomobject]@{
        category = $Category
        severity = $Severity
        summary = $Summary
        nextAction = $NextAction
    })
}

function Read-ArchiveIndexFile {
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

function Read-StructuredJsonFile {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        return $null
    }

    return Get-Content $Path -Raw | ConvertFrom-Json
}

if ($missingFiles.Count -eq 0) {
    $bundlePath = Join-Path $packetDirectory "support-bundle.json"
    $issueDraftPath = Join-Path $packetDirectory "ISSUE_DRAFT.md"
    $manifestPath = Join-Path $packetDirectory "SUPPORT_MANIFEST.json"
    $attemptsPath = Join-Path $packetDirectory "SUPPORT_ATTEMPTS.json"
    $lifecyclePath = Join-Path $packetDirectory "SUPPORT_LIFECYCLE.json"
    $lifecycleMarkdownPath = Join-Path $packetDirectory "SUPPORT_LIFECYCLE.md"

    $bundle = Get-Content $bundlePath -Raw | ConvertFrom-Json
    $issueDraft = Get-Content $issueDraftPath -Raw
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $attempts = Read-AttemptHistoryFile -Path $attemptsPath
    $attemptItems = @($attempts)
    $lifecycle = Read-StructuredJsonFile -Path $lifecyclePath
    $lifecycleMarkdown = Get-Content $lifecycleMarkdownPath -Raw

    $requiredHeadings = @(
        "## Summary",
        "## Environment",
        "## Steps to reproduce",
        "## Expected result",
        "## Actual result",
        "## Evidence"
    )

    foreach ($requiredHeading in $requiredHeadings) {
        if ($issueDraft -notmatch [Regex]::Escape($requiredHeading)) {
            $errors.Add("Issue draft is missing heading: $requiredHeading")
        }
    }

    if ($issueDraft.Length -lt 280) {
        Add-EvidenceGap -Target $evidenceGaps -Category "operator-narrative" -Severity "weak" -Summary "The issue draft is still thin relative to the expected maintainer handoff shape." -NextAction "Tighten the summary, reproduction steps, and expected-versus-actual wording."
    }

    $requiredManifestFields = @(
        "packetSchemaVersion",
        "createdAtUtc",
        "tenantId",
        "bundleFile",
        "issueDraftFile",
        "packagedFromExistingBundle",
        "packagedFromExistingDraft",
        "refreshCount",
        "currentAttempt",
        "lastRefreshReason",
        "archiveCount",
        "lifecycleSummaryFile",
        "canonicalPacketState",
        "packetClass",
        "triageLane",
        "nextOwner",
        "releaseContext",
        "runtimeContext",
        "activeHandoffFiles",
        "archiveIndexFile"
    )

    foreach ($field in $requiredManifestFields) {
        if (-not $manifest.PSObject.Properties.Name.Contains($field)) {
            $errors.Add("Manifest is missing field: $field")
        }
    }

    if ($attemptItems.Length -eq 0) {
        $warnings.Add("Support packet attempt history is empty.")
    }

    $requiredReleaseContextFields = @(
        "repositoryBranch",
        "repositoryCommit",
        "repositoryShortCommit",
        "repositoryTag",
        "repositoryIsDirty",
        "releaseVersionHint",
        "releasePosture",
        "releaseGuideFile",
        "checkpointGuideFile",
        "checkpointEstimate"
    )

    $requiredRuntimeContextFields = @(
        "capturedAtUtc",
        "hostName",
        "userName",
        "operatingSystem",
        "powerShellVersion",
        "apiBaseUrl",
        "bundleSource",
        "issueDraftSource"
    )

    if ($manifest.PSObject.Properties.Name.Contains("releaseContext")) {
        foreach ($field in $requiredReleaseContextFields) {
            if (-not $manifest.releaseContext.PSObject.Properties.Name.Contains($field)) {
                $errors.Add("Manifest releaseContext is missing field: $field")
            }
        }
    }

    if ($manifest.PSObject.Properties.Name.Contains("runtimeContext")) {
        foreach ($field in $requiredRuntimeContextFields) {
            if (-not $manifest.runtimeContext.PSObject.Properties.Name.Contains($field)) {
                $errors.Add("Manifest runtimeContext is missing field: $field")
            }
        }
    }

    if ($attemptItems.Length -gt 0) {
        $latestAttempt = $attemptItems[$attemptItems.Length - 1]

        if (-not $latestAttempt.PSObject.Properties.Name.Contains("releaseContext")) {
            $warnings.Add("Latest attempt history entry is missing releaseContext.")
        }
        elseif (-not (Test-ContextFieldSet -Target $latestAttempt.releaseContext -RequiredFields $requiredReleaseContextFields)) {
            $warnings.Add("Latest attempt history entry has an incomplete releaseContext snapshot.")
        }

        if (-not $latestAttempt.PSObject.Properties.Name.Contains("runtimeContext")) {
            $warnings.Add("Latest attempt history entry is missing runtimeContext.")
        }
        elseif (-not (Test-ContextFieldSet -Target $latestAttempt.runtimeContext -RequiredFields $requiredRuntimeContextFields)) {
            $warnings.Add("Latest attempt history entry has an incomplete runtimeContext snapshot.")
        }
    }

    if (-not $bundle.summary) {
        $errors.Add("Support bundle is missing the summary block.")
    } else {
        $bundleSummary = $bundle.summary
        $requiredSummaryFields = @(
            "posture",
            "summary",
            "escalationTarget",
            "signals",
            "collectionHints",
            "artifactChecklist"
        )

        foreach ($field in $requiredSummaryFields) {
            if (-not $bundleSummary.PSObject.Properties.Name.Contains($field)) {
                $errors.Add("Support bundle summary is missing field: $field")
            }
        }

        if (
            $bundleSummary.PSObject.Properties.Name.Contains("artifactChecklist") -and
            @($bundleSummary.artifactChecklist).Count -eq 0
        ) {
            $warnings.Add("Support bundle artifact checklist is empty.")
            Add-EvidenceGap -Target $evidenceGaps -Category "attachment-provenance" -Severity "weak" -Summary "The support bundle does not yet describe which artifacts should travel with the handoff." -NextAction "Rebuild the packet with a clearer artifact checklist and reading order."
        }

        if (
            $bundleSummary.PSObject.Properties.Name.Contains("signals") -and
            @($bundleSummary.signals).Count -eq 0
        ) {
            $warnings.Add("Support bundle signals are empty.")
            Add-EvidenceGap -Target $evidenceGaps -Category "runtime-signal-summary" -Severity "weak" -Summary "The support bundle has no summarized runtime signals for the failing state." -NextAction "Capture a fresh support bundle from the failing run."
        }
    }

    if ($bundle.PSObject.Properties.Name.Contains("collections")) {
        if ((Get-NormalizedIntValue $bundle.collections.auditCount) -eq 0) {
            $warnings.Add("Packet has no audit evidence yet.")
            Add-EvidenceGap -Target $evidenceGaps -Category "audit-evidence" -Severity "absent" -Summary "No audit evidence is attached to the packet yet." -NextAction "Capture audit evidence for the same failing run before escalation."
        }

        if ((Get-NormalizedIntValue $bundle.collections.workflowCount) -eq 0) {
            $warnings.Add("Packet has no workflow evidence yet.")
            Add-EvidenceGap -Target $evidenceGaps -Category "workflow-evidence" -Severity "absent" -Summary "No workflow evidence is attached to the packet yet." -NextAction "Capture a workflow trace for the current failure."
        }
    }

    if ($manifest -and $bundle -and $manifest.tenantId -ne $bundle.tenantId) {
        $warnings.Add("Manifest tenantId does not match the support bundle tenantId.")
    }

    if ($manifest -and $attemptItems.Length -gt 0) {
        $latestAttempt = $attemptItems[$attemptItems.Length - 1]
        if ((Get-NormalizedIntValue $manifest.currentAttempt) -ne (Get-NormalizedIntValue $latestAttempt.attempt)) {
            $warnings.Add("Manifest currentAttempt does not match the latest attempt history entry.")
        }
    }

    $archiveIndexPath = Join-Path $packetDirectory "SUPPORT_ARCHIVE_INDEX.json"
    $archiveItems = Read-ArchiveIndexFile -Path $archiveIndexPath
    $archiveEntries = @($archiveItems)

    if ((Get-NormalizedIntValue $manifest.refreshCount) -gt 0 -and $archiveEntries.Length -eq 0) {
        $warnings.Add("Packet has been refreshed, but no archive history is present.")
    }

    if ($archiveEntries.Length -gt 0 -and (Get-NormalizedIntValue $manifest.archiveCount) -ne $archiveEntries.Length) {
        $warnings.Add("Manifest archiveCount does not match the archive index.")
    }

    if ($lifecycle) {
        if ([string]$manifest.canonicalPacketState -ne [string]$lifecycle.canonicalPacketState) {
            $warnings.Add("Manifest canonicalPacketState does not match the lifecycle summary.")
        }

        $requiredLifecycleFields = @(
            "timeline",
            "deltaNarration",
            "canonicalHandoffFiles",
            "currentAttempt",
            "archiveCount",
            "packetClass",
            "triageLane",
            "nextOwner",
            "triageChecks",
            "releaseContext",
            "runtimeContext",
            "contextDrift",
            "previousAttempt",
            "evidenceProvenance",
            "evidenceGapScore"
        )

        foreach ($field in $requiredLifecycleFields) {
            if (-not $lifecycle.PSObject.Properties.Name.Contains($field)) {
                $errors.Add("Lifecycle summary is missing field: $field")
            }
        }

        if ($lifecycleMarkdown -notmatch [Regex]::Escape("## Timeline")) {
            $errors.Add("Lifecycle markdown is missing the timeline section.")
        }

        if ($lifecycleMarkdown -notmatch [Regex]::Escape("## Delta narration")) {
            $errors.Add("Lifecycle markdown is missing the delta narration section.")
        }

        if ($lifecycleMarkdown -notmatch [Regex]::Escape("## Triage shortcuts")) {
            $errors.Add("Lifecycle markdown is missing the triage shortcuts section.")
        }

        if ($lifecycleMarkdown -notmatch [Regex]::Escape("## Release context")) {
            $errors.Add("Lifecycle markdown is missing the release context section.")
        }

        if ($lifecycleMarkdown -notmatch [Regex]::Escape("## Runtime context")) {
            $errors.Add("Lifecycle markdown is missing the runtime context section.")
        }

        if ($lifecycleMarkdown -notmatch [Regex]::Escape("## Context drift")) {
            $errors.Add("Lifecycle markdown is missing the context drift section.")
        }

        if ($lifecycleMarkdown -notmatch [Regex]::Escape("## Previous attempt")) {
            $errors.Add("Lifecycle markdown is missing the previous attempt section.")
        }

        if ($lifecycleMarkdown -notmatch [Regex]::Escape("## Evidence provenance")) {
            $errors.Add("Lifecycle markdown is missing the evidence provenance section.")
        }

        if ($lifecycleMarkdown -notmatch [Regex]::Escape("## Reading order")) {
            $errors.Add("Lifecycle markdown is missing the reading order section.")
        }

        if ($lifecycleMarkdown -notmatch [Regex]::Escape("## Optional comparison context")) {
            $errors.Add("Lifecycle markdown is missing the optional comparison context section.")
        }

        if ($lifecycleMarkdown -notmatch [Regex]::Escape("## Evidence gap score")) {
            $errors.Add("Lifecycle markdown is missing the evidence gap score section.")
        }

        if ($lifecycleMarkdown -notmatch [Regex]::Escape("## Evidence gap categories")) {
            $errors.Add("Lifecycle markdown is missing the evidence gap categories section.")
        }

        if ([string]$manifest.packetClass -ne [string]$lifecycle.packetClass) {
            $warnings.Add("Manifest packetClass does not match the lifecycle summary.")
        }

        if ([string]$manifest.triageLane -ne [string]$lifecycle.triageLane) {
            $warnings.Add("Manifest triageLane does not match the lifecycle summary.")
        }

        if ([string]$manifest.nextOwner -ne [string]$lifecycle.nextOwner) {
            $warnings.Add("Manifest nextOwner does not match the lifecycle summary.")
        }

        if (
            $manifest.PSObject.Properties.Name.Contains("releaseContext") -and
            [string]$manifest.releaseContext.repositoryCommit -ne [string]$lifecycle.releaseContext.repositoryCommit
        ) {
            $warnings.Add("Manifest releaseContext.repositoryCommit does not match the lifecycle summary.")
        }

        if (
            $manifest.PSObject.Properties.Name.Contains("releaseContext") -and
            [string]$manifest.releaseContext.releaseVersionHint -ne [string]$lifecycle.releaseContext.releaseVersionHint
        ) {
            $warnings.Add("Manifest releaseContext.releaseVersionHint does not match the lifecycle summary.")
        }

        if (
            $manifest.PSObject.Properties.Name.Contains("runtimeContext") -and
            [string]$manifest.runtimeContext.bundleSource -ne [string]$lifecycle.runtimeContext.bundleSource
        ) {
            $warnings.Add("Manifest runtimeContext.bundleSource does not match the lifecycle summary.")
        }

        if (-not $lifecycle.contextDrift.PSObject.Properties.Name.Contains("posture")) {
            $errors.Add("Lifecycle contextDrift is missing field: posture")
        }

        if (-not $lifecycle.contextDrift.PSObject.Properties.Name.Contains("changedFields")) {
            $errors.Add("Lifecycle contextDrift is missing field: changedFields")
        }

        if (-not $lifecycle.contextDrift.PSObject.Properties.Name.Contains("summary")) {
            $errors.Add("Lifecycle contextDrift is missing field: summary")
        }

        if (-not $lifecycle.evidenceProvenance.PSObject.Properties.Name.Contains("readingOrder")) {
            $errors.Add("Lifecycle evidenceProvenance is missing field: readingOrder")
        }

        if (-not $lifecycle.evidenceProvenance.PSObject.Properties.Name.Contains("primaryFiles")) {
            $errors.Add("Lifecycle evidenceProvenance is missing field: primaryFiles")
        }

        if (-not $lifecycle.evidenceProvenance.PSObject.Properties.Name.Contains("optionalFiles")) {
            $errors.Add("Lifecycle evidenceProvenance is missing field: optionalFiles")
        }

        if (-not $lifecycle.evidenceProvenance.PSObject.Properties.Name.Contains("comparisonContextSummary")) {
            $errors.Add("Lifecycle evidenceProvenance is missing field: comparisonContextSummary")
        }

        if (-not $lifecycle.evidenceGapScore.PSObject.Properties.Name.Contains("strongCount")) {
            $errors.Add("Lifecycle evidenceGapScore is missing field: strongCount")
        }

        if (-not $lifecycle.evidenceGapScore.PSObject.Properties.Name.Contains("weakCount")) {
            $errors.Add("Lifecycle evidenceGapScore is missing field: weakCount")
        }

        if (-not $lifecycle.evidenceGapScore.PSObject.Properties.Name.Contains("absentCount")) {
            $errors.Add("Lifecycle evidenceGapScore is missing field: absentCount")
        }

        if (-not $lifecycle.evidenceGapScore.PSObject.Properties.Name.Contains("topPriorityCategory")) {
            $errors.Add("Lifecycle evidenceGapScore is missing field: topPriorityCategory")
        }

        if (-not $lifecycle.evidenceGapScore.PSObject.Properties.Name.Contains("topPriorityNextAction")) {
            $errors.Add("Lifecycle evidenceGapScore is missing field: topPriorityNextAction")
        }

        if (-not $lifecycle.evidenceGapScore.PSObject.Properties.Name.Contains("categories")) {
            $errors.Add("Lifecycle evidenceGapScore is missing field: categories")
        }
    }

    $notes.Add("Validation compares packet completeness against the current support packet contract.")
}

if ($errors.Count -eq 0 -and $warnings.Count -eq 0 -and $evidenceGaps.Count -eq 0) {
    Add-EvidenceGap -Target $evidenceGaps -Category "packet-balance" -Severity "strong" -Summary "The packet currently has a balanced narrative, runtime evidence, and attachment structure." -NextAction "Keep the current packet as the canonical handoff unless the failure changes."
}

$priorityScore = @{
    absent = 3
    weak = 2
    strong = 1
}

$orderedEvidenceGaps = @(
    $evidenceGaps | Sort-Object -Property @{ Expression = { $priorityScore[[string]$_.severity] } ; Descending = $true }, category
)

$strongCount = @($orderedEvidenceGaps | Where-Object { $_.severity -eq "strong" }).Count
$weakCount = @($orderedEvidenceGaps | Where-Object { $_.severity -eq "weak" }).Count
$absentCount = @($orderedEvidenceGaps | Where-Object { $_.severity -eq "absent" }).Count
$topEvidenceGap = if ($orderedEvidenceGaps.Count -gt 0) { $orderedEvidenceGaps[0] } else { $null }

$status =
    if ($errors.Count -gt 0) { "Rejected" }
    elseif ($warnings.Count -gt 0) { "AcceptedWithWarnings" }
    else { "Accepted" }

if ($errors.Count -gt 0) {
    $recommendedAction = "Regenerate"
} elseif ($warnings.Count -gt 0) {
    $recommendedAction = "Refresh"
} else {
    $recommendedAction = "Reuse"
}

$report = [ordered]@{
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    packetDirectory = $packetDirectory
    status = $status
    missingFiles = @($missingFiles)
    errorCount = $errors.Count
    warningCount = $warnings.Count
    errors = @($errors)
    warnings = @($warnings)
    notes = @($notes)
    recommendedAction = $recommendedAction
    escalationTarget = if ($bundleSummary) { [string]$bundleSummary.escalationTarget } else { "" }
    posture = if ($bundleSummary) { [string]$bundleSummary.posture } else { "" }
    evidenceGapScore = [ordered]@{
        strongCount = $strongCount
        weakCount = $weakCount
        absentCount = $absentCount
        topPriorityCategory = if ($topEvidenceGap) { [string]$topEvidenceGap.category } else { "" }
        topPriorityNextAction = if ($topEvidenceGap) { [string]$topEvidenceGap.nextAction } else { "" }
        categories = @($orderedEvidenceGaps)
    }
}

$report | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputPath -Encoding UTF8

Write-Host "Support packet validation status: $status" -ForegroundColor Cyan
Write-Host "Validation report written to $OutputPath" -ForegroundColor Green
