# Handoff — Sprint 28 terminé

## État courant

Sprint 28 est `DONE` après une quality gate complète verte le 2026-09-27. Le prochain sprint éligible est Sprint 29, qui n'a pas commencé. Quatre sprints restent : 29 à 32.

## Livraison Sprint 28

- machine d'état explicite, politique déterministe et orchestration concurrente bornée des conducteurs virtuels ;
- outils typés appelant les vrais workflows inspection, mission, preuve privée, retard et incident véhicule ;
- reprise déterministe des fautes offline/delay/issue, retries et clés d'idempotence stables ;
- activités structurées tenant-scoped persistées, auditées et signalées en temps réel ;
- onglet cockpit limité à l'état observable, la politique, l'action et le résultat, sans raisonnement privé.

## Preuves

Le journal complet est `.runtime/sprint28-quality-gate.log`. La gate a validé format/analyse, build sans avertissement, 165 tests rapides, 1 test MinIO, 3 tests SQL Server, GPS, simulation 33 étapes, Web 28 tests et 6 E2E, santé API et Android. Dix-huit tests ciblés couvrent notamment transitions, concurrence, fautes, activité sécurisée et parcours HTTP réel. Le test Android connecté est resté non configuré, conformément au périmètre backend/Web.

## Reprise

`Start Next Sprint` doit exécuter le runner atomique et sélectionner uniquement Sprint 29.
