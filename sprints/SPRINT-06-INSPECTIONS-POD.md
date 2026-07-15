# SPRINT-06 — Inspections, défauts et preuves

## Objectif

Numériser les contrôles et la preuve de livraison.

## Résultat démontrable

À la fin de ce sprint, un observateur doit pouvoir vérifier le résultat sans lire le code. Préparer un scénario reproductible, des données fictives et les commandes exactes.

## Périmètre obligatoire

- modèles de checklist
- inspection pré-départ
- défauts et criticité
- photos et métadonnées
- signature simple
- preuve liée à l'arrêt
- stockage objet avec URL signée
- upload reprise

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

- [ ] défaut critique bloque le départ selon règle
- [ ] photo non publique
- [ ] upload interrompu reprend
- [ ] preuve complète visible opérateur
- [ ] offline Android couvert

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
- documentation, changelog et état du projet mis à jour ;
- checkpoint créé.

## Preuves à joindre dans le rapport de qualité interne

Commandes exécutées, résultats, captures ou logs synthétiques du scénario, migrations appliquées, limites restantes et dette créée.
