# Roadmap FleetOps

## Principe

La roadmap produit un MVP démontrable, puis commercialisable, en dix sprints maximum. Chaque sprint doit laisser le système exécutable et testable. La durée n'est pas imposée : un sprint se termine uniquement lorsque ses critères de sortie sont satisfaits.

| Sprint | Résultat démontrable | Taille |
|---|---|---:|
| 00 | Dépôt, architecture, CI et environnement local fiables | L |
| 01 | Organisations, utilisateurs, rôles et shell Web professionnel | L |
| 02 | Véhicules, conducteurs et appareils gérés de bout en bout | L |
| 03 | Positions simulées visibles sur une carte interactive | XL |
| 04 | Missions planifiées, affectées et suivies par un opérateur | XL |
| 05 | Application Android conducteur avec synchronisation hors ligne | XL |
| 06 | Inspections, défauts et preuves de livraison numériques | XL |
| 07 | Alertes, maintenance et conformité légère | L |
| 08 | API d'intégration, webhooks, import/export et audit renforcé | XL |
| 09 | Durcissement, déploiement, observabilité et pilote commercial | XL |

## Jalons

### Jalon A — Base technique fiable

Sprints 00 à 02. Le système possède une identité, un tenant, des rôles et les référentiels de flotte.

### Jalon B — Démonstrateur Fleet Management

Sprint 03. Plusieurs véhicules simulés se déplacent sur la carte, avec états et historique.

### Jalon C — Flux métier complet

Sprints 04 à 06. Une mission passe de la planification à la preuve de livraison via l'application Android.

### Jalon D — MVP mature

Sprints 07 et 08. Alertes, intégrations et audit rendent le produit utilisable avec un système tiers.

### Jalon E — MVP commercial

Sprint 09. Le système est déployable, observable, sauvegardé, documenté et prêt pour un pilote payant.

## Règles de passage

- Aucun sprint suivant tant que le précédent n'est pas `DONE` dans `.agent/PROJECT_STATE.json`.
- Toute dette reportée reçoit un identifiant et une échéance.
- Chaque sprint fournit au moins un scénario E2E ou un scénario de simulation reproductible.
- Les tests multi-tenant, autorisation et erreurs réseau sont obligatoires dès que la fonctionnalité les concerne.
- Les fonctionnalités hors périmètre sont refusées ou placées dans `docs/00-product/FUTURE_SCOPE.md`.

## Après le MVP

Les évolutions candidates sont : portail client, maintenance avancée, tarification transport, connecteurs ERP, optimisation achetée à un fournisseur, analytics CO₂ et application manager. Elles ne doivent pas contaminer le MVP initial.
