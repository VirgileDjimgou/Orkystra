param(
    [string]$Version = "v0.1.0-rc.1",
    [switch]$AllowDirtyWorktree
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $repoRoot

function Write-Check {
    param(
        [string]$Label,
        [bool]$Passed,
        [string]$Detail
    )

    if ($Passed) {
        Write-Host "[OK] $Label - $Detail" -ForegroundColor Green
        return
    }

    Write-Host "[FAIL] $Label - $Detail" -ForegroundColor Red
}

$failed = $false

$requiredFiles = @(
    "README.md",
    "CHANGELOG.md",
    "PROJECT_STATUS.md",
    "IMPLEMENTATION_ROADMAP.md",
    "prompts/CODEX_AUTOPILOT.md",
    "prompts/ORKYSTRA_UNIVERSAL_AGENT_PROMPT_FR.md",
    "docs/operations/release-candidate.md",
    "docs/operations/releases/$Version.md",
    "docs/operations/releases/$Version-manifest.json",
    "docs/operations/releases/$Version-publish-checklist.md",
    "constitution/SMART_LOGISTICS_TWIN_CONSTITUTION_v2.md"
)

Write-Host "== Orkystra release preflight ==" -ForegroundColor Cyan
Write-Host "Repository: $repoRoot"
Write-Host "Version:    $Version"
Write-Host ""

foreach ($path in $requiredFiles) {
    $exists = Test-Path $path
    Write-Check -Label "Required file" -Passed $exists -Detail $path
    if (-not $exists) {
        $failed = $true
    }
}

$gitStatus = git status --short
$isDirty = -not [string]::IsNullOrWhiteSpace(($gitStatus | Out-String))

if ($AllowDirtyWorktree) {
    Write-Check -Label "Git worktree" -Passed $true -Detail "dirty worktree allowed for preflight review"
} else {
    Write-Check -Label "Git worktree" -Passed (-not $isDirty) -Detail "must be clean before cutting the release tag"
    if ($isDirty) {
        $failed = $true
        Write-Host ""
        Write-Host "Dirty files detected:" -ForegroundColor Yellow
        $gitStatus | ForEach-Object { Write-Host "  $_" }
    }
}

$changelogText = Get-Content "CHANGELOG.md" -Raw
$releaseNotesText = Get-Content "docs/operations/releases/$Version.md" -Raw
$statusText = Get-Content "PROJECT_STATUS.md" -Raw
$manifestPath = "docs/operations/releases/$Version-manifest.json"
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

$checks = @(
    @{
        Label = "Changelog entry"
        Passed = $changelogText.Contains(($Version -replace "^v", ""))
        Detail = "CHANGELOG.md mentions $Version"
    },
    @{
        Label = "Release notes entry"
        Passed = $releaseNotesText.Contains($Version)
        Detail = "versioned release notes mention $Version"
    },
    @{
        Label = "Project status release alignment"
        Passed = $statusText.Contains($Version)
        Detail = "PROJECT_STATUS.md still references the active candidate"
    },
    @{
        Label = "Manifest version alignment"
        Passed = ($manifest.tag -eq $Version) -and ($manifest.version -eq ($Version -replace "^v", ""))
        Detail = "manifest version and tag match the requested candidate"
    },
    @{
        Label = "Autopilot prompt alignment"
        Passed = $statusText.Contains("prompts/CODEX_AUTOPILOT.md") -and (Get-Content "prompts/CODEX_AUTOPILOT.md" -Raw).Contains("ORKYSTRA_UNIVERSAL_AGENT_PROMPT_FR.md")
        Detail = "status and autopilot prompt reference the current universal prompt flow"
    }
)

foreach ($check in $checks) {
    Write-Check -Label $check.Label -Passed $check.Passed -Detail $check.Detail
    if (-not $check.Passed) {
        $failed = $true
    }
}

foreach ($artifact in $manifest.artifacts) {
    $artifactExists = Test-Path $artifact
    Write-Check -Label "Manifest artifact" -Passed $artifactExists -Detail $artifact
    if (-not $artifactExists) {
        $failed = $true
    }
}

Write-Host ""
Write-Host "Manifest verification commands:" -ForegroundColor Cyan
$manifest.verificationCommands | ForEach-Object { Write-Host "  - $_" }
Write-Host ""
Write-Host "Recommended manual release sequence:" -ForegroundColor Cyan
Write-Host "  1. Review git diff and close remaining local work."
Write-Host "  2. Run full backend, frontend, and python verification."
Write-Host "  3. Re-run this script without -AllowDirtyWorktree."
Write-Host "  4. Cut the tag: git tag $Version"
Write-Host "  5. Push branch and tag, then publish the release notes."

if ($failed) {
    Write-Host ""
    Write-Host "Release preflight failed." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Release preflight passed." -ForegroundColor Green
