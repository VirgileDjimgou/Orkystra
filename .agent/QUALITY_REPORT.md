# Rapport de qualité

## Dernière exécution

- Statut : `PASSED`
- Date UTC : 2026-07-15T01:30:23Z
- Sprint : SPRINT-00

## Contrôles

| Contrôle | Statut | Notes |
|---|---|---|
| Docker Compose config | PASSED | `docker compose --env-file .env config` |
| Docker services | PASSED | SQL Server, MinIO, Mosquitto et Mailpit démarrés; MinIO déplacé sur `9010/9011`; SQL Server déplacé sur volume Docker nommé |
| Backend restore/build | PASSED | `dotnet restore`, build `Debug` et `Release` |
| Backend tests | PASSED | `dotnet test` en `Debug` et `Release`, 1 test vert |
| Backend vulnerabilities | PASSED | `dotnet list FleetOps.slnx package --vulnerable` sans vulnérabilité après verrouillage `Microsoft.OpenApi` |
| EF Core migrations | PASSED | outil `dotnet-ef` localisé dans `.config/dotnet-tools.json`, migration initiale créée et appliquée |
| API health/system/OpenAPI | PASSED | `/health`, `/api/system/info` et `/openapi/v1.json` répondent |
| Worker | PASSED | démarrage et arrêt contrôlés sans crash |
| Web install | PASSED | `npm ci` reproductible après correction du lockfile |
| Web format | PASSED | `npm run format:check` |
| Web lint | PASSED | `npm run lint` |
| Web tests | PASSED | `npm run test`, 1 test vert |
| Web build | PASSED | `npm run build` |
| npm audit | PASSED | `npm audit` sans vulnérabilité |
| GPS dry-run | PASSED | payloads UTC valides générés |
| GPS vers API | PASSED | simulateur reçoit `202 Accepted` |
| SignalR et carte | PASSED | panneau carte mis à jour en temps réel sur `http://127.0.0.1:5183/map` |
| Quality gate | PASSED | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality-gate.ps1` |
| Android wrapper | PASSED | `apps/android-driver/gradlew` et `gradlew.bat` versionnés |
| Android build/tests | PASSED | `./gradlew.bat testDebugUnitTest assembleDebug --stacktrace`, 2 tests unitaires Android |
| Android SDK/JDK | PASSED | SDK local `Android\Sdk` détecté; `JAVA_HOME` forcé vers le JBR Android Studio quand disponible |

## Action suivante

SPRINT-00 est prêt pour passation. Démarrer SPRINT-01 uniquement après validation explicite du prochain périmètre.
