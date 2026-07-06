[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5043",
    [Parameter(Mandatory = $true)]
    [string]$ApiKey,
    [string]$TenantId = "local-demo-tenant",
    [int]$Count = 10,
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputPath = Join-Path (Get-Location) "orkystra-support-bundle-$timestamp.json"
}

$headers = @{
    "X-Api-Key" = $ApiKey
    "X-Tenant-Id" = $TenantId
}

$uri = "$($ApiBaseUrl.TrimEnd('/'))/observability/support-bundle?count=$Count"

Write-Host "Collecting support bundle from $uri" -ForegroundColor Cyan
$bundle = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get

$directory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$bundle | ConvertTo-Json -Depth 12 | Set-Content -Path $OutputPath -Encoding UTF8

Write-Host "Support bundle exported to $OutputPath" -ForegroundColor Green
