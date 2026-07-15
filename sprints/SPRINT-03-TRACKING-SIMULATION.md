# SPRINT-03 — Tracking et simulateurs

## Objectif

Ingestion fiable de télémétrie et carte temps réel multi-véhicules.

## Résultat démontrable

À la fin de ce sprint, un observateur doit pouvoir vérifier le résultat sans lire le code. Préparer un scénario reproductible, des données fictives et les commandes exactes.

## Périmètre obligatoire

- endpoint versionné/idempotent
- persistance points et position courante
- SignalR
- carte Leaflet + liste synchronisée
- simulateur multi-device
- offline/replay/duplicates/out-of-order
- rétention et métriques

## Travaux backend

- concevoir les invariants et contrats avant les endpoints ;
- ajouter migrations, validation, autorisation, audit et erreurs Problem Details ;
- ajouter tests unitaires et d'intégration, dont isolation multi-tenant ;
- instrumenter les opérations importantes.

## Travaux Web

- fournir écrans professionnels avec loading, empty, error et succès ;
- respecter rôles et permissions visibles ;
- ajouter tests composants et parcours critiques ;
- ne pas exposer une action que le serveur refusera normalement.

## Travaux Android

Ne modifier Android que si le périmètre du sprint l'exige. Toute fonction mobile doit traiter offline, retry, idempotence, permission refusée et expiration de session.

## Simulateurs et données

- étendre les scénarios déterministes nécessaires ;
- ne jamais utiliser de données personnelles réelles ;
- permettre la réinitialisation complète du scénario.

## Critères d'acceptation

- [ ] trois véhicules visibles simultanément
- [ ] doublon n'ajoute pas deux points
- [ ] point ancien ne remplace pas la position courante
- [ ] reconnexion SignalR
- [ ] historique paginé

## Tests de robustesse obligatoires

- données invalides et limites ;
- accès interdit et fuite inter-tenant ;
- duplication/rejeu si événement ;
- concurrence et mise à jour obsolète ;
- dépendance externe indisponible ;
- redémarrage du processus ;
- fuseau horaire et UTC si date ;
- UX erreur et reprise.

## Hors périmètre

Tout élément de `docs/00-product/FUTURE_SCOPE.md`, sauf décision explicite et ADR.

## Quality gate de sortie

- build et analyse statique verts ;
- tests concernés verts ;
- scénario démontrable exécuté ;
- revue sécurité/tenant effectuée ;
- documentation, changelog et état agent mis à jour ;
- checkpoint créé.

## Preuves à joindre dans `.agent/QUALITY_REPORT.md`

Commandes exécutées, résultats, captures ou logs synthétiques du scénario, migrations appliquées, limites restantes et dette créée.
