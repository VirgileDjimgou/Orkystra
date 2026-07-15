$ErrorActionPreference = "Stop"

Write-Host "== Backend =="
dotnet restore FleetOps.slnx
dotnet format FleetOps.slnx --verify-no-changes
dotnet build FleetOps.slnx --no-restore -c Release
dotnet test FleetOps.slnx --no-build -c Release

Write-Host "== Web =="
npm --prefix apps/web ci
npm --prefix apps/web run format:check
npm --prefix apps/web run lint
npm --prefix apps/web run test
npm --prefix apps/web run build

Write-Host "== Android =="
if (Test-Path apps/android-driver/gradlew.bat) {
  Push-Location apps/android-driver
  ./gradlew.bat testDebugUnitTest assembleDebug --stacktrace
  Pop-Location
} else {
  Write-Warning "Gradle wrapper absent : le générer pendant SPRINT-00."
}

Write-Host "== Docker Compose =="
docker compose --env-file .env.example config --quiet
Write-Host "Quality gate verte."
