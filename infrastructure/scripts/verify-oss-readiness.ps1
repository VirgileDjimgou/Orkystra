[CmdletBinding()]
param(
    [switch]$SkipPython
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $repoRoot

function Invoke-Step {
    param(
        [string]$Label,
        [scriptblock]$Action
    )

    Write-Host "== $Label ==" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed."
    }
    Write-Host "[OK] $Label" -ForegroundColor Green
    Write-Host ""
}

Invoke-Step -Label "Backend build" -Action {
    dotnet build backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false
}

Invoke-Step -Label "Backend tests" -Action {
    dotnet test backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false
}

Invoke-Step -Label "Frontend build" -Action {
    Push-Location frontend/web
    try {
        npm run build
    }
    finally {
        Pop-Location
    }
}

if (-not $SkipPython) {
    Invoke-Step -Label "Python tests" -Action {
        Push-Location python-services
        try {
            python -m pytest
        }
        finally {
            Pop-Location
        }
    }

    Invoke-Step -Label "Python bytecode check" -Action {
        python -m compileall python-services
    }
}

Write-Host "OSS readiness verification completed." -ForegroundColor Green
