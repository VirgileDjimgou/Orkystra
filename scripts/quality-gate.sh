#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SUMMARY=()

run_step() {
  local name="$1"
  shift
  echo "== ${name} =="
  "$@"
  SUMMARY+=("PASSED :: ${name}")
}

cd "$ROOT"

if [[ ! -f .env ]]; then
  echo "Missing .env. Copy .env.example to .env before running the quality gate." >&2
  exit 1
fi

if [[ -z "${ANDROID_HOME:-}" && -d "$HOME/Android/Sdk" ]]; then
  export ANDROID_HOME="$HOME/Android/Sdk"
  export ANDROID_SDK_ROOT="$ANDROID_HOME"
fi

run_step "Git Status" git status --short --branch
run_step "Dotnet Tools" dotnet tool restore
run_step "Docker Compose Config" docker compose --env-file .env config --quiet
run_step "Pilot Compose Config" docker compose --env-file .env -f docker-compose.yml -f docker-compose.pilot.yml config --quiet
run_step "Backend Restore" dotnet restore FleetOps.slnx
run_step "Backend Format" dotnet format FleetOps.slnx --verify-no-changes
run_step "Backend Build" dotnet build FleetOps.slnx --no-restore -c Release
run_step "Backend Test (Fast)" dotnet test FleetOps.slnx --no-build -c Release --filter "Category!=SqlServer"
run_step "Backend Test (SqlServer)" dotnet test tests/backend/FleetOps.UnitTests/FleetOps.UnitTests.csproj --no-build -c Release --filter "Category=SqlServer"
run_step "GPS Dry Run" bash -lc 'mkdir -p .runtime; gps_dll="simulators/GpsSimulator/bin/Release/net10.0/GpsSimulator.dll"; [[ -f "$gps_dll" ]]; tmp_log=".runtime/quality-gps.log"; rm -f "$tmp_log" ".runtime/quality-gps.err"; timeout 5s dotnet exec "$gps_dll" --dry-run >"$tmp_log" 2>".runtime/quality-gps.err" || true; grep -q "\"VehicleId\"" "$tmp_log"'
run_step "Web Install" bash -lc 'cd apps/web && npm ci'
run_step "Web Format" bash -lc 'cd apps/web && npm run format:check'
run_step "Web Lint" bash -lc 'cd apps/web && npm run lint'
run_step "Web Test" bash -lc 'cd apps/web && npm run test'
run_step "Web Build" bash -lc 'cd apps/web && npm run build'
run_step "Web E2E Browser" bash -lc 'cd apps/web && npx playwright install chromium'
run_step "Web E2E" bash -lc 'cd apps/web && npm run e2e'
run_step "API Health Check" bash -lc 'mkdir -p .runtime; api_dll="apps/backend/FleetOps.Api/bin/Release/net10.0/FleetOps.Api.dll"; [[ -f "$api_dll" ]]; tmp_api=".runtime/quality-api.log"; tmp_err=".runtime/quality-api.err"; rm -f "$tmp_api" "$tmp_err"; ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 Testing__UseInMemoryDatabase=true Testing__DatabaseName=quality-gate-api dotnet exec "$api_dll" >"$tmp_api" 2>"$tmp_err" & pid=$!; trap "kill $pid 2>/dev/null || true" EXIT; for _ in $(seq 1 20); do if curl -fsS http://localhost:5080/health >/dev/null && curl -fsS http://localhost:5080/health/ready >/dev/null; then ok=1; break; fi; sleep 1; done; [[ "${ok:-0}" = "1" ]]; kill $pid 2>/dev/null || true; wait $pid 2>/dev/null || true; trap - EXIT'

if [[ ! -x apps/android-driver/gradlew ]]; then
  echo "Gradle wrapper missing in apps/android-driver." >&2
  exit 1
fi
if [[ -z "${ANDROID_HOME:-}" || ! -d "$ANDROID_HOME" ]]; then
  echo "ANDROID_HOME is not set to a valid Android SDK path." >&2
  exit 1
fi

run_step "Android Build" bash -lc 'cd apps/android-driver && ./gradlew lintDebug testDebugUnitTest assembleDebug assembleDebugAndroidTest --stacktrace'

if [[ "${FLEETOPS_ENABLE_ANDROID_CONNECTED:-0}" == "1" ]]; then
  run_step "Android Connected Test" bash -lc '
    cd apps/android-driver
    adb="$ANDROID_HOME/platform-tools/adb"
    verifier_original=""
    restore_verifier() {
      if [[ "${FLEETOPS_MANAGE_ANDROID_ADB_VERIFIER:-0}" == "1" && -n "$verifier_original" ]]; then
        if [[ "$verifier_original" == "null" ]]; then
          "$adb" shell settings delete global verifier_verify_adb_installs >/dev/null
        else
          "$adb" shell settings put global verifier_verify_adb_installs "$verifier_original"
        fi
      fi
    }
    trap restore_verifier EXIT

    if [[ "${FLEETOPS_MANAGE_ANDROID_ADB_VERIFIER:-0}" == "1" ]]; then
      verifier_original="$("$adb" shell settings get global verifier_verify_adb_installs | tr -d "\r")"
      "$adb" shell settings put global verifier_verify_adb_installs 0
      [[ "$("$adb" shell settings get global verifier_verify_adb_installs | tr -d "\r")" == "0" ]]
    fi

    if ./gradlew connectedDebugAndroidTest --stacktrace; then
      exit 0
    fi

    result_root="app/build/outputs/androidTest-results/connected/debug"
    if grep -Rqs "Failed to install split APK(s)" "$result_root" && grep -Rqs "ShellCommandUnresponsiveException" "$result_root"; then
      echo "Android package installation timed out; retrying once after an ADB/package-manager readiness check." >&2
      "$ANDROID_HOME/platform-tools/adb" wait-for-device
      "$ANDROID_HOME/platform-tools/adb" shell pm path android >/dev/null
      sleep 5
      ./gradlew connectedDebugAndroidTest --stacktrace
    else
      exit 1
    fi
  '
else
  SUMMARY+=("SKIPPED :: Android Connected Test (set FLEETOPS_ENABLE_ANDROID_CONNECTED=1 with an emulator or device)")
fi

echo "== Summary =="
printf '%s\n' "${SUMMARY[@]}"
echo "Quality gate passed."
