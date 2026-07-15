#!/usr/bin/env bash
set -euo pipefail

echo "== Backend =="
dotnet restore FleetOps.slnx
dotnet format FleetOps.slnx --verify-no-changes
dotnet build FleetOps.slnx --no-restore -c Release
dotnet test FleetOps.slnx --no-build -c Release

echo "== Web =="
npm --prefix apps/web ci
npm --prefix apps/web run format:check
npm --prefix apps/web run lint
npm --prefix apps/web run test
npm --prefix apps/web run build

echo "== Android =="
if [[ -x apps/android-driver/gradlew ]]; then
  (cd apps/android-driver && ./gradlew testDebugUnitTest assembleDebug --stacktrace)
else
  echo "AVERTISSEMENT: Gradle wrapper absent; le générer pendant SPRINT-00."
fi

echo "== Docker Compose =="
docker compose --env-file .env.example config --quiet
echo "Quality gate verte."
