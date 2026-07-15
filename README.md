# Zynro Fleet — Sprint 00 foundation

Zynro Fleet est le nom commercial visible du MVP FleetOps pendant le Sprint 00. Le dépôt reste techniquement nommé `FleetOps` pour éviter un renommage massif risqué avant validation complète.

## Prérequis vérifiés sur cette machine

- Windows PowerShell
- .NET SDK `10.0.201`
- Node.js `v24.18.0`
- npm `11.16.0`
- Python `3.13.2`
- Docker Desktop `28.0.1` avec Compose `v2.33.1`
- Android Studio JBR `21` et Android SDK local avec `android-35`

## État actuel du Sprint 00

- Backend .NET: build Debug/Release et tests verts
- Web Vue: `npm ci`, format, lint, tests et build verts
- Docker local: SQL Server, MinIO, Mosquitto et Mailpit validés
- EF Core: migration initiale créée et appliquée sur SQL Server local
- Démonstrateur: flux `Docker -> API -> simulateur GPS -> SignalR -> carte` validé
- Android: wrapper Gradle versionné, tests unitaires et `assembleDebug` verts

Le Sprint 00 est `DONE` localement.

## Configuration locale

Créer `.env` si nécessaire:

```powershell
Copy-Item .env.example .env
```

Ports locaux FleetOps validés ici:

- API: `http://localhost:5080`
- Web: `http://127.0.0.1:5183`
- SQL Server: `localhost,14333`
- MinIO API: `http://localhost:9010`
- MinIO Console: `http://localhost:9011`
- Mailpit: `http://localhost:8025`
- MQTT: `localhost:1883`

## Démarrage de l'infrastructure

```powershell
./scripts/dev-up.ps1
docker compose ps
```

L'infrastructure FleetOps est configurée avec:

- un volume Docker nommé pour SQL Server, plus stable sous Windows qu'un bind mount;
- MinIO exposé sur `9010/9011` pour éviter les conflits fréquents avec d'autres projets locaux;
- Mailpit sur `8025`;
- Mosquitto sur `1883`.

Pour arrêter l'infrastructure:

```powershell
./scripts/dev-down.ps1
```

## Outils .NET

Restaurer les outils locaux et les dépendances:

```powershell
dotnet tool restore
dotnet restore FleetOps.slnx
```

Créer ou appliquer la base locale:

```powershell
dotnet dotnet-ef database update --project apps/backend/FleetOps.Infrastructure --startup-project apps/backend/FleetOps.Api
```

## Lancer l'API

```powershell
dotnet run --project apps/backend/FleetOps.Api
```

Endpoints vérifiés:

- `GET http://localhost:5080/health`
- `GET http://localhost:5080/api/system/info`
- `GET http://localhost:5080/openapi/v1.json`
- `POST http://localhost:5080/api/simulation/telemetry`
- hub SignalR `http://localhost:5080/hubs/tracking`

## Lancer le front Web

Les commandes doivent être exécutées depuis `apps/web` pour éviter les problèmes PowerShell observés avec `npm --prefix`.

```powershell
Set-Location apps/web
npm ci
npm run dev -- --host 127.0.0.1
```

Validation Web complète:

```powershell
Set-Location apps/web
npm run format:check
npm run lint
npm run test
npm run build
```

## Lancer le simulateur GPS

Mode sec:

```powershell
dotnet run --project simulators/GpsSimulator -- --dry-run
```

Mode connecté à l'API:

```powershell
dotnet run --project simulators/GpsSimulator
```

Le simulateur envoie des points valides en UTC vers `/api/simulation/telemetry`.

## Démonstration validée

1. `./scripts/dev-up.ps1`
2. `dotnet dotnet-ef database update --project apps/backend/FleetOps.Infrastructure --startup-project apps/backend/FleetOps.Api`
3. `dotnet run --project apps/backend/FleetOps.Api`
4. Dans `apps/web`: `npm ci` puis `npm run dev -- --host 127.0.0.1`
5. Ouvrir `http://127.0.0.1:5183/map`
6. `dotnet run --project simulators/GpsSimulator`

Résultat vérifié sur cette machine:

- l'API accepte les points avec `202 Accepted`;
- `/api/tracking/latest` renvoie la position courante;
- la page carte affiche `1` véhicule;
- le panneau latéral se met à jour en temps réel via SignalR pendant que le simulateur continue d'envoyer des points.

## Quality gate

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality-gate.ps1
```

La gate exécute actuellement:

- `git status`
- `dotnet tool restore`
- validation `docker compose`
- restore / format / build / test .NET
- dry-run du simulateur GPS
- `npm ci`, format, lint, tests et build Web
- vérification minimale `/health`
- vérification du wrapper Android et de l'outillage SDK/JDK
- tests unitaires Android et `assembleDebug`

État réel au 2026-07-15:

- la gate complète est verte sur Windows;
- Android est validé via le wrapper Gradle versionné.

## Android

État actuel:

- `apps/android-driver/gradlew` et `gradlew.bat` sont versionnés;
- `gradle` global n'est pas requis;
- la quality gate détecte `%LOCALAPPDATA%\Android\Sdk` si `ANDROID_HOME` n'est pas défini;
- la quality gate force le JBR d'Android Studio local quand il est disponible;
- `testDebugUnitTest` et `assembleDebug` sont verts.

Commande directe:

```powershell
Set-Location apps/android-driver
.\gradlew.bat testDebugUnitTest assembleDebug --stacktrace
```

## Problèmes fréquents

- Si `docker compose up -d` échoue sur MinIO, vérifier qu'aucun autre projet n'occupe `9010/9011`.
- Si SQL Server boucle au démarrage sous Windows, conserver le volume Docker nommé du dépôt et éviter le bind mount direct vers le disque local.
- Si `npm run ...` échoue depuis la racine avec `npm --prefix`, exécuter les commandes directement dans `apps/web`.
- Si `npm ci` échoue avec des URLs internes, vérifier que `apps/web/package-lock.json` pointe bien vers `https://registry.npmjs.org/`.

## Documentation utile

- [ROADMAP.md](/C:/Users/djimg/source/repos/Orkystra%20FleetOps/ROADMAP.md)
- [sprints/SPRINT-00-FOUNDATION.md](/C:/Users/djimg/source/repos/Orkystra%20FleetOps/sprints/SPRINT-00-FOUNDATION.md)
- [VALIDATION.md](/C:/Users/djimg/source/repos/Orkystra%20FleetOps/VALIDATION.md)
- [.agent/CURRENT_SPRINT.md](/C:/Users/djimg/source/repos/Orkystra%20FleetOps/.agent/CURRENT_SPRINT.md)

## Prochaine commande agentique

```text
Préparer SPRINT-01 à partir du Sprint 00 clôturé, sans élargir rétroactivement le périmètre du bootstrap.
```
