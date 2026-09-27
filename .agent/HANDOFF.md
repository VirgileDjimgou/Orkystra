# Handoff — Sprint 30 terminé

## État courant

Sprint 30 est `DONE` après une quality gate complète verte le 2026-09-27. Le prochain sprint éligible est Sprint 31, qui n'a pas commencé. Deux sprints restent : 31 et 32.

## Livraison Sprint 30

- navigation Web regroupée en `Operate`, `Fleet`, `Manage` et `Administration`, sans déplacer l'autorisation hors du serveur ;
- cockpit map-first conservé comme point de départ avec liens contextuels Overview, Alerts et Fleet map ;
- accès Missions et Daily planning sous Manage, avec alias compatibles `/missions` et `/planning` ;
- limites de présentation testées extraites pour le backlog des missions et les cartes de la file d'exceptions ;
- validation navigateur isolable grâce à `PLAYWRIGHT_WEB_BASE_URL`, sans conflit avec un autre serveur Web local.

## Preuves

Le journal complet est `.runtime/sprint30-quality-gate.log`. La gate a validé format/analyse sur 307 fichiers, build sans avertissement, 170 tests rapides, 1 test MinIO, 3 tests SQL Server, GPS, simulation 33 étapes, Web 32 tests et 8 E2E, santé API et Android. Les E2E couvrent notamment la navigation au clavier, les alias de planification et les bookmarks map.

## Reprise

`Start Next Sprint` doit exécuter le runner atomique et sélectionner uniquement Sprint 31.
