# Handoff — Sprint 27 terminé

## État courant

Sprint 27 est `DONE` après une quality gate complète verte le 2026-09-27. Le prochain sprint éligible est Sprint 28, qui n'a pas commencé. Cinq sprints restent : 28 à 32.

## Livraison Sprint 27

- moteur, horloge et rejeu déterministes avec pause/reprise/reset, accélération et snapshot de redémarrage ;
- catalogue de cinq scénarios, quatre polylines locales et génération bornée de 12 véhicules synthétiques ;
- découverte automatique de la flotte, ingestion Tracking canonique et pont de transition Mission authentifié ;
- activation fail-closed en mode Demo, sandbox obligatoire et rejet tenant croisé testé ;
- service Worker désactivé par défaut ; simulateurs de développement conservés.

## Preuves

Le journal complet est `.runtime/sprint27-quality-gate.log`. La gate a validé format/analyse, build sans avertissement, 155 tests rapides, MinIO, SQL Server, GPS, simulation 33 étapes, Web 28 tests et 6 E2E, santé API et Android. Le test Android connecté est resté non configuré, conformément au périmètre backend.

## Reprise

`Start Next Sprint` doit exécuter le runner atomique et sélectionner uniquement Sprint 28.
