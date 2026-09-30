$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location (Join-Path $root 'apps/web')
try {
    npx playwright install chromium
    if ($LASTEXITCODE -ne 0) { throw "Playwright browser installation failed with exit code $LASTEXITCODE." }
    npm run demo:screenshots
    if ($LASTEXITCODE -ne 0) { throw "Demo screenshot capture failed with exit code $LASTEXITCODE." }
    Write-Host "Demo screenshots written to docs/assets/screenshots/demo-*.png."
}
finally {
    Pop-Location
}
