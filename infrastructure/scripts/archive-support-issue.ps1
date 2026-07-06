[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PacketDirectory,
    [string]$Reason = "manual-archive"
)

$ErrorActionPreference = "Stop"

function Set-ObjectPropertyValue {
    param(
        [object]$TargetObject,
        [string]$PropertyName,
        [object]$Value
    )

    $TargetObject | Add-Member -NotePropertyName $PropertyName -NotePropertyValue $Value -Force
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

    return @($raw)
}

function Write-ArchiveIndexFile {
    param(
        [string]$Path,
        [object[]]$ArchiveItems
    )

    $normalizedItems = @(
        $ArchiveItems | ForEach-Object {
            [ordered]@{
                archiveId = [string]$_.archiveId
                attempt = [int]$_.attempt
                archivedAtUtc = [string]$_.archivedAtUtc
                reason = [string]$_.reason
                folder = [string]$_.folder
            }
        }
    )

    ([object[]]$normalizedItems) | ConvertTo-Json -Depth 6 | Set-Content -Path $Path -Encoding UTF8
}

$packetDirectory = (Resolve-Path $PacketDirectory).Path
$manifestPath = Join-Path $packetDirectory "SUPPORT_MANIFEST.json"

if (-not (Test-Path $manifestPath)) {
    throw "SUPPORT_MANIFEST.json is required to archive a support packet."
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$currentAttempt = [int]$manifest.currentAttempt
$archiveRoot = Join-Path $packetDirectory "archive"

if (-not (Test-Path $archiveRoot)) {
    New-Item -ItemType Directory -Path $archiveRoot -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$archiveId = "attempt-$currentAttempt-$timestamp"
$archiveDirectory = Join-Path $archiveRoot $archiveId
New-Item -ItemType Directory -Path $archiveDirectory -Force | Out-Null

$filesToArchive = @(
    "support-bundle.json",
    "ISSUE_DRAFT.md",
    "SUPPORT_MANIFEST.json",
    "SUPPORT_ATTEMPTS.json",
    "SUPPORT_VALIDATION.json",
    "MAINTAINER_HANDOFF.md",
    "SUPPORT_LIFECYCLE.json",
    "SUPPORT_LIFECYCLE.md"
)

foreach ($fileName in $filesToArchive) {
    $sourcePath = Join-Path $packetDirectory $fileName
    if (Test-Path $sourcePath) {
        Copy-Item -Path $sourcePath -Destination (Join-Path $archiveDirectory $fileName) -Force
    }
}

$archiveIndexPath = Join-Path $packetDirectory "SUPPORT_ARCHIVE_INDEX.json"
$archiveIndex = [System.Collections.Generic.List[object]]::new()
foreach ($archiveItem in (Read-ArchiveIndexFile -Path $archiveIndexPath)) {
    $archiveIndex.Add($archiveItem)
}

$archiveIndex.Add([pscustomobject]([ordered]@{
    archiveId = $archiveId
    attempt = $currentAttempt
    archivedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    reason = $Reason
    folder = "archive/$archiveId"
}))

Write-ArchiveIndexFile -Path $archiveIndexPath -ArchiveItems $archiveIndex

Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "archiveCount" -Value $archiveIndex.Count
Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "lastArchivedAtUtc" -Value ((Get-Date).ToUniversalTime().ToString("o"))
Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "lastArchiveReason" -Value $Reason
$manifest | ConvertTo-Json -Depth 8 | Set-Content -Path $manifestPath -Encoding UTF8

$summaryScriptPath = Join-Path $PSScriptRoot "summarize-support-issue.ps1"
& $summaryScriptPath -PacketDirectory $packetDirectory | Out-Null

Write-Host "Support packet archived to $archiveDirectory" -ForegroundColor Green
