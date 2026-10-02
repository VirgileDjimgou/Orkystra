# Handoff — SPRINT-33 terminé (2026-10-01)

## État courant

- SPRINT-33 `DONE`, gate complète verte (`.runtime/sprint33-quality-gate.log`). Prochain sprint éligible : SPRINT-34 `PLANNED` (Realistic Fleet Simulation). Lot 6/1 enregistré ; chaque sprint reste atomique et exige un contexte neuf.
- CI unique : `.github/workflows/release-validation.yml` (backend MinIO/SQL Server réels, web + audit, E2E, Android SDK, compose, gouvernance). `ci.yml` supprimé, aucun contrôle perdu.
- npm : 0 avis (Vitest 5). NuGet : audit complet activé, aucun paquet vulnérable.
- Ajout `scripts/agent/verify_state_consistency.py` au gate et à la CI ; tests du runner 5/5.
- Flake corrigé sans affaiblissement : borne de vivacité du test SignalR portée de 20 s à 45 s après un timeout sous charge.

## Reprise

`Start Next Sprint` sélectionne SPRINT-34 dans un contexte neuf.

# Handoff — Rebaseline phase MVP Consolidation (2026-10-01)

## Nouvelle phase planifiée

- Phase : `MVP Consolidation / Public Autonomous Fleet Demo`, version roadmap `2026.10-mvp-consolidation` (décision D-022).
- `SPRINT-00`–`SPRINT-32` conservés `DONE` ; `SPRINT-33` est `READY` ; `SPRINT-34`–`SPRINT-38` sont `PLANNED`.
- Six contrats créés dans `sprints/` avec les 12 sections obligatoires ; aucun code produit de ces sprints n'a été implémenté.
- État agentique réconcilié : `activeSprint = SPRINT-33`, `execution.status = IDLE`, lot résiduel 1/5 fermé, `lastSuccessfulGate` reciblé sur SPRINT-32, `lastCommit` = `ea529f6`.
- Le runner accepte désormais les statuts de planification `READY` et `PLANNED` (sélection dans l'ordre numérique) ; tests unitaires du runner étendus.
- Incohérences corrigées : `VALIDATION.md` (gelé au Sprint 12) reste à réconcilier par SPRINT-33 ; deux pipelines CI contradictoires identifiés ; 6 avis npm (3 High) documentés ; ajout du human gate ingress/tunnel.

## Prochaine action

`Start Next Sprint` sélectionne SPRINT-33 (Repository Truth & Green CI).

# Handoff — Post-sprint : vidéo de démonstration et correctif Android

## Travaux hors sprint (2026-09-30, après SPRINT-32)

- Pipeline vidéo complet : `scripts/run-demo-video.ps1` (API DemoTesting + 2 Workers moteur, provisioning par les contrats canoniques, parcours Playwright sous-titré `apps/web/demo-video/`, capture Android pilotée par uiautomator/screenrecord, fusion ffmpeg). Vidéo finale : `.runtime/demo-video/FleetOps-demo-20260930-2157.mp4` (23:23, non versionnée).
- Correctif produit découvert par la démo : l'app Android envoyait `action`/`purpose`/`defectSeverity` en texte alors que l'API attend des entiers — inspection, commandes et preuves ne synchronisaient jamais. Corrigé dans `DriverApi.kt`/`DriverRepository.kt` ; tests unitaires Android verts ; fin de parcours vérifiée jusqu'à la mission `Completed` avec 1 preuve.
- Les modifications de ce post-sprint n'ont pas encore été commitées (worktree modifié : scripts vidéo, spec Playwright, correctif Android, docs).

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
