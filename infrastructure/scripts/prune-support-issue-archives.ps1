[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PacketDirectory,
    [int]$KeepLatest = 3
)

$ErrorActionPreference = "Stop"

if ($KeepLatest -lt 1) {
    throw "KeepLatest must be at least 1."
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

function Set-ObjectPropertyValue {
    param(
        [object]$TargetObject,
        [string]$PropertyName,
        [object]$Value
    )

    $TargetObject | Add-Member -NotePropertyName $PropertyName -NotePropertyValue $Value -Force
}

$packetDirectory = (Resolve-Path $PacketDirectory).Path
$archiveIndexPath = Join-Path $packetDirectory "SUPPORT_ARCHIVE_INDEX.json"
$manifestPath = Join-Path $packetDirectory "SUPPORT_MANIFEST.json"

if (-not (Test-Path $archiveIndexPath)) {
    throw "SUPPORT_ARCHIVE_INDEX.json is required to prune support archives."
}

if (-not (Test-Path $manifestPath)) {
    throw "SUPPORT_MANIFEST.json is required to prune support archives."
}

$archiveItems = Read-ArchiveIndexFile -Path $archiveIndexPath
$orderedItems = @($archiveItems | Sort-Object attempt, archivedAtUtc)

if ($orderedItems.Count -le $KeepLatest) {
    Write-Host "No archive pruning required." -ForegroundColor Cyan
    exit 0
}

$itemsToRemove = @($orderedItems | Select-Object -First ($orderedItems.Count - $KeepLatest))
$itemsToKeep = @($orderedItems | Select-Object -Last $KeepLatest)

foreach ($archiveItem in $itemsToRemove) {
    $archivePath = Join-Path $packetDirectory ([string]$archiveItem.folder -replace '/', '\')
    if (Test-Path $archivePath) {
        Remove-Item -LiteralPath $archivePath -Recurse -Force
    }
}

Write-ArchiveIndexFile -Path $archiveIndexPath -ArchiveItems $itemsToKeep

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "archiveCount" -Value $itemsToKeep.Count
Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "lastPrunedAtUtc" -Value ((Get-Date).ToUniversalTime().ToString("o"))
Set-ObjectPropertyValue -TargetObject $manifest -PropertyName "keepLatestArchiveCount" -Value $KeepLatest
$manifest | ConvertTo-Json -Depth 8 | Set-Content -Path $manifestPath -Encoding UTF8

$summaryScriptPath = Join-Path $PSScriptRoot "summarize-support-issue.ps1"
& $summaryScriptPath -PacketDirectory $packetDirectory | Out-Null

Write-Host "Pruned support packet archives to the latest $KeepLatest snapshot(s)." -ForegroundColor Green
