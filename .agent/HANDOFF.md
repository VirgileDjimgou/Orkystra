# Handoff — Sprint 32 terminé

## État courant

Sprint 32 est `DONE` après une quality gate complète verte le 2026-09-30 et un smoke hébergé vert. Tous les sprints actifs `SPRINT-00` à `SPRINT-32` sont terminés ; aucun sprint n'est éligible ensuite. Le lot demandé (`Start Next Sprints 5`) reste enregistré à 1/5 complété : `start` atomique fonctionne toujours, mais `batch-start` refuse un nouveau lot tant que l'ancien n'est pas fermé (aucune API de clôture automatisée quand plus aucun sprint ne reste — décision humaine requise si un nouveau lot est nécessaire).

## Livraison Sprint 32

- profil Demo hébergé complet : overlay `docker-compose.demo.yml` (API `Demo`, Worker moteur `public-demo`, Web), images correctes (`dotnet/aspnet:10.0` pour le Worker), redémarrage automatique API/Worker, préflight `.env` ;
- canal de service interne borné (`InternalApi:Key` / `X-FleetOps-Internal-Key`) : endpoints internes accessibles en `Demo`/`DemoTesting` pour le moteur, anonymes refusés, sessions publiques bloquées, `404` en Production ;
- seed `public-demo` : 12 véhicules/appareils, conducteur, mission assignée en retard simulé 15 min (contexte véhicule/mission/exception cohérent) ;
- scripts `demo-up`, `demo-down`, `demo-smoke` (config + exécution complète journalisée), captures `scripts/capture-demo-screenshots.ps1` ;
- parcours Playwright public et opérations, CI GitHub Actions, README recruteur, walkthrough, topologie, release checklist et captures `demo-*.png`.

## Preuves

- Gate complète `PASSED` : `.runtime/sprint32-quality-gate.log` ;
- smoke hébergé `PASSED` (12 véhicules suivis) : `.runtime/sprint32-demo-smoke.log` ;
- limitations honnêtes et rollback : `docs/02-engineering/RELEASE_CHECKLIST.md`.

## Reprise

Aucun sprint restant. Prochaines décisions humaines : nom commercial, fournisseur de tuiles, hébergement/DNS, provisioning éventuel des pilotes virtuels hébergés, et clôture manuelle du lot 1/5 si un nouveau lot doit être lancé.

- mètre applicatif `FleetOps` et instruments ingestion, diffusion SignalR, snapshot, reset, moteur Demo, agents virtuels et Demo public ;
- harnais `simulators/FleetOpsReliabilityHarness` (modes charge et observation, injections doublon/désordre, rapport JSON/Markdown) et script `scripts/run-reliability-exercise.ps1` ;
- exécution 20 agents / 15 minutes avec redémarrage Worker injecté, reprise déterministe au tick suivant ;
- redémarrage API SQL Server, reset concurrent sérialisé par organisation, idempotence concurrente d'ingestion et incréments de compteurs atomiques ;
- migration `IX_TelemetryPoints_RecordedAtUtc` justifiée par un plan de requête mesuré ;
- rapport `docs/02-engineering/RELIABILITY_REPORT.md`, budgets mesurés dans `TRACKING_LOAD_BASELINE.md` et instruments documentés dans `OBSERVABILITY.md` ;
- seed Northwind étendu à 20 véhicules synthétiques pour la charge maximale du moteur Demo.

## Preuves

Le journal complet est `.runtime/sprint31-quality-gate.log`. La gate a validé format/analyse, build sans avertissement, 181 tests rapides, 11 tests `Reliability`, 1 test MinIO, 6 tests SQL Server, GPS, simulation 33 étapes, harnais de fiabilité borné (30 s), Web 32 tests et 8 E2E, santé API et Android. Les mesures détaillées et les limites explicites sont dans `docs/02-engineering/RELIABILITY_REPORT.md`.

## Reprise

`Start Next Sprint` doit exécuter le runner atomique et sélectionner uniquement Sprint 32 (release Demo hébergée et portfolio). La charge 50 véhicules reste une limite explicite à traiter avant toute revendication commerciale.
