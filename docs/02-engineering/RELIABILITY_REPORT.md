# Rapport de fiabilité — Sprint 31

**Preuve de développement simulée — ne constitue pas une preuve pilote ou commerciale.**

Ce rapport documente les mesures obtenues sur un poste de développement isolé, avec des
tenants synthétiques uniquement. Il ne prétend à aucune performance de production.

## Environnement mesuré

| Élément | Valeur |
| --- | --- |
| Date | 2026-09-29 |
| Machine | Windows 10.0.26200, 16 processeurs logiques |
| Runtime | .NET 10.0.12 |
| API | `FleetOps.Api` en `Development`, base EF InMemory, instance unique |
| Worker | `FleetOps.Worker`, moteur Demo déterministe (graine 3101, tick 5 s, vitesse ×1) |
| Client de mesure | `simulators/FleetOpsReliabilityHarness` |
| Tenants | `northwind` (20 véhicules synthétiques), aucun jeu de données réel |

## Budgets et résultats

| Surface | Budget | Mesuré | Statut |
| --- | --- | --- | --- |
| Ingestion télémétrie (20 véhicules, 1 point/s) | p95 < 250 ms | p50 7,9 ms ; p95 45,9 ms ; max 380,8 ms (démarrage à froid) | Conforme |
| Snapshot positions (20 véhicules) | < 1 s (Sprint 21) | p50 7,2 ms ; p95 29,9 ms ; max 45,1 ms | Conforme |
| Catch-up historique (50 points) | < 1 s | p50 8,3 ms ; p95 48,4 ms ; max 65,1 ms | Conforme |
| Diffusion SignalR (mesure client) | p95 < 250 ms | p50 0,1 ms ; p95 2,7 ms | Conforme |
| Erreurs HTTP sous charge | 0 | 0 sur 4 760 requêtes d'ingestion | Conforme |
| Positions courantes manquantes | 0 | 0 sur 20 véhicules, après redémarrage du Worker | Conforme |
| Fuite inter-tenant | 0 | 20 requêtes croisées parallèles → 404 ; 20 écritures croisées → 400 | Conforme |

### Exécution principale — 20 agents, 15 minutes

- identifiant d'exécution : `20260929-222644` ;
- durée réelle : 900,3 s ; 20 véhicules ; 1 tick / 5 s ;
- 3 520 événements acceptés pendant la fenêtre observée (3 540 au total du processus) ;
- 3 212 messages SignalR reçus par l'observateur ;
- 0 doublon, 0 désordre, 308 points rejetés par le contrôle qualité (demi-tour de boucle
  de route déterministe détecté comme saut implausible — comportement attendu et compté
  séparément depuis ce sprint) ;
- redémarrage de fault injection du Worker à t = 301 s : reprise « Restored Demo scenario
  NormalShift at tick 59 », aucun trou ni doublon d'identifiants, séquences strictement
  croissantes pour les 20 véhicules.

### Injection de défauts — charge 20 véhicules, 60 s à 1 point/s

- identifiant d'exécution : `20260929-222517` ;
- 1 210 requêtes émises, 1 204 acceptées, 6 doublons injectés → 6 doublons observés,
  4 points anciens injectés → 4 désordres observés, 0 erreur HTTP ;
- latence d'ingestion p50 7,9 ms / p95 45,9 ms ; SignalR p50 0,1 ms / p95 2,7 ms.

### Redémarrages et reconnexion

| Scénario | Preuve |
| --- | --- |
| Redémarrage API sur SQL Server | `ReliabilitySqlServerIntegrationTests.ApiRestartRestoresCurrentPositionsAndHistory` : 200 positions/10 ticks survivent à un nouvel hôte sur la même base |
| Redémarrage Worker | `DemoScenarioRestartReliabilityTests.WorkerRestartResumesDeterministicallyFromPersistedSnapshot` : reprise au tick 6 avec identifiants identiques au rejeu du snapshot |
| Reconnexion navigateur / SignalR | `SignalRLifecycleReliabilityTests.BrowserDisconnectReconnectRestoresCatchUpAndLiveUpdates` : coupure, ingestion hors ligne, catch-up du dernier point, reprise du direct |
| Concurrence de reset | `ReliabilitySqlServerIntegrationTests.ConcurrentScenarioResetsRemainPredictableAndBounded` : 4 resets parallèles, 100 lignes supprimées exactement une fois, aucun 5xx |

### Sécurité sous charge

| Contrôle | Preuve |
| --- | --- |
| Limite de lancements publics | `PublicDemoSecurityLoadTests.ConcurrentLaunchBurstRespectsPermitLimitWithoutServerErrors` : 10 lancements parallèles, 3 admis, 7 refus 429, aucun autre statut |
| Expiration de session | `PublicDemoSecurityLoadTests.ExpiredSessionIsRejectedUnderConcurrentReads` : 10 lectures parallèles après expiration → 401 |
| Bac à sable Demo | `PublicDemoSecurityLoadTests.DemoSandboxDeniesMutationsWhileReadsRemainAvailable` : 10 écritures parallèles → 403, 10 lectures → 200 |
| Isolation tenant sous charge | `TrackingReliabilityIntegrationTests.TenantIsolationHoldsWhileConcurrentLoadIsApplied` : 20 lectures croisées → 404, 20 écritures croisées → 400 |

## Requêtes et index

- la purge de rétention trie globalement par `RecordedAtUtc` ; l'index composite
  `(OrganizationId, VehicleId, RecordedAtUtc)` ne peut pas servir ce tri ;
- migration `20260929T191130_Sprint31TelemetryRetentionIndex` ajoutant
  `IX_TelemetryPoints_RecordedAtUtc` ;
- `ReliabilitySqlServerIntegrationTests.RetentionPurgeQueryPlanUsesRecordedAtUtcIndex`
  insère 20 000 lignes, actualise les statistiques et vérifie via `SET STATISTICS XML`
  que le plan de la requête de purge référence `IX_TelemetryPoints_RecordedAtUtc`.
- Aucun autre index ajouté : les accès d'ingestion (événement, véhicule, appareil,
  position courante) utilisent déjà des index uniques ou composites existants.

## Observabilité

Compteurs, histogrammes et jauge exposés sous le mètre `FleetOps` et exportés via OTLP
lorsqu'un endpoint est configuré :

- `fleetops.tracking.ingest.duration` / `fleetops.tracking.ingest.events` (tags `result` :
  accepted, duplicate, out_of_order, rejected, error) ;
- `fleetops.tracking.broadcast.duration` / `fleetops.tracking.broadcast.events` ;
- `fleetops.tracking.snapshot.duration` (tag `surface` : positions, history) ;
- `fleetops.tracking.reset.duration` / `fleetops.tracking.reset.events` ;
- `fleetops.tracking.signalr.connections` (jauge observable) ;
- `fleetops.demo.engine.tick.duration`, `fleetops.demo.engine.telemetry.emitted`,
  `fleetops.demo.engine.state.save.duration` ;
- `fleetops.demo.agent.step.duration`, `fleetops.demo.agent.steps` ;
- `fleetops.public_demo.sessions`, `fleetops.public_demo.controls`.

Correction de fiabilité associée : `TrackingMetricsStore` utilise désormais des incréments
atomiques (`Interlocked`) ; une course perte-de-compteur a été détectée par le harnais
(1 196 acceptés au lieu de 1 200) et couverte par
`ObservabilityReliabilityTests.MetricsStoreDoesNotLoseConcurrentCounts`.

## Limites explicites

- 20 véhicules est le maximum du moteur Demo et du scénario interne ; **une charge de
  50 véhicules reste une limite explicite non testée** pour l'offre hébergée actuelle.
- L'exécution principale utilise une base EF InMemory et une API mono-instance ; les
  mesures SQL Server proviennent des tests d'intégration catégorie `SqlServer`.
- Les latences SignalR de l'exécution principale ne sont pas corrélées requête par
  requête (mode observateur) ; elles sont mesurées en mode charge (p95 2,7 ms).
- La mémoire du navigateur n'est pas mesurée automatiquement ; la reconnexion et le
  catch-up sont couverts par tests unitaires Web, test SignalR d'intégration et scénario
  E2E existant.
- Les stores de sessions Demo et de compteurs restent locaux au processus, limitation
  déjà documentée pour le déploiement mono-instance.

## Reproduction

```powershell
# Exécution 20 agents / 15 minutes avec redémarrage Worker injecté
pwsh -ExecutionPolicy Bypass -File scripts/run-reliability-exercise.ps1 `
  -Mode observe -DurationSeconds 900 -Vehicles 20 -TickSeconds 5 `
  -WorkerRestartAfterSeconds 300

# Charge avec injections doublon / désordre
pwsh -ExecutionPolicy Bypass -File scripts/run-reliability-exercise.ps1 `
  -Mode load -DurationSeconds 60 -Vehicles 20 -IntervalMs 1000 `
  -InjectDuplicatesEvery 10 -InjectOutOfOrderEvery 15 -SkipWorker

# Tests de fiabilité rapides
dotnet test tests/backend/FleetOps.UnitTests/FleetOps.UnitTests.csproj `
  --filter "Category=Reliability&Category!=SqlServer"

# Tests SQL Server (redémarrage, reset concurrent, plan d'index)
dotnet test tests/backend/FleetOps.UnitTests/FleetOps.UnitTests.csproj `
  --filter "Category=SqlServer&FullyQualifiedName~ReliabilitySqlServerIntegrationTests"
```
