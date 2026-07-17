# SPRINT-19 — Dispatch productif, modèles et actions en masse

## Objectif

Réduire le temps de préparation quotidien des missions tout en conservant des décisions humaines explicites et auditables.

## Valeur et constats traités

Répond à la densité de Dispatch et au besoin de productivité sans introduire d’optimisation propriétaire. Les opérateurs gagnent du temps grâce aux modèles, validations et actions en masse contrôlées.

## Tâches principales

- créer modèles de mission/arrêts, duplication par date et brouillons réutilisables ;
- importer missions avec prévisualisation, idempotence et rapport d’erreurs ;
- proposer board jour/semaine, filtres enregistrés et vue capacité simple ;
- ajouter affectation et changements de statut en masse avec confirmation d’impact ;
- détecter collisions véhicule/conducteur, immobilisation, conformité et horaires ;
- fournir suggestion déterministe de ressources disponibles, jamais une optimisation opaque ;
- améliorer raccourcis clavier, drag-and-drop accessible et annulation courte côté UI.

## Composants concernés

Dispatch, Fleet, Maintenance, Compliance, Web, API, audit et imports.

## Dépendances

Sprints 13, 17 et 18 pour exceptions et contraintes opérationnelles.

## Tests et preuves requis

Templates, import rejouable, concurrence, conflits, actions partielles/atomiques, rôles, pagination, clavier et E2E création de vingt missions.

## Critères d’acceptation

- [ ] vingt missions récurrentes sont préparées en moins de quinze minutes lors du scénario de référence ;
- [ ] aucun conflit critique n’est masqué par une action en masse ;
- [ ] import rejoué ne duplique ni mission ni arrêt ;
- [ ] suggestion explique les critères utilisés et reste modifiable ;
- [ ] opérations de masse laissent une trace par mission ;
- [ ] board reste utilisable sur écran portable et au clavier.

## Livrable démontrable

Un opérateur importe et affecte vingt missions, corrige deux conflits, publie la journée et suit immédiatement les exceptions résultantes.

## Risques et rollback

Risque d’actions massives destructives. Prévisualiser, valider côté serveur, appliquer idempotence et journaliser ; conserver le flux individuel comme fallback.

## Estimation

XL.
