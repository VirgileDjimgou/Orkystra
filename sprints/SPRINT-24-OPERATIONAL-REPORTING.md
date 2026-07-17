# SPRINT-24 — Rapports opérationnels et indicateurs de valeur

## Objectif

Transformer les événements déjà collectés en décisions quotidiennes et en preuve de ROI compréhensible par un responsable de petite flotte.

## Valeur et constats traités

Répond à la recommandation de rapports simples et aux métriques commerciales non consolidées. Les indicateurs restent explicables et reliés à une action, sans entrepôt de données ni BI complexe.

## Tâches principales

- définir dictionnaire versionné : missions à l’heure, preuves complètes, sync, exceptions, immobilisation et coûts ;
- créer tableaux jour/semaine/mois avec comparaison et drill-down autorisé ;
- calculer temps de préparation, prise en charge, clôture et résolution d’exception ;
- produire rapports maintenance/conformité et disponibilité véhicule ;
- exporter CSV/PDF accessible avec fuseau, filtres, définition et date de fraîcheur ;
- planifier envoi périodique via outbox et rôles autorisés ;
- afficher recommandations déterministes vers les exceptions sous-jacentes.

## Composants concernés

Reporting, Operations, Maintenance, Compliance, Web, Worker, exports et SQL.

## Dépendances

Métriques alpha Sprint 20 et modèles stables Sprints 21–23.

## Tests et preuves requis

Jeux de données connus, fuseaux, intervalles vides, recalcul, tenant/rôles, performance, accessibilité, export et cohérence dashboard→drill-down.

## Critères d’acceptation

- [ ] chaque indicateur possède définition, source, période, fuseau et fraîcheur visibles ;
- [ ] chiffres Web, export et API concordent sur un dataset de référence ;
- [ ] responsable remonte d’un KPI dégradé aux événements concernés ;
- [ ] requêtes respectent budgets de latence sur la volumétrie cible ;
- [ ] rapport programmé n’est envoyé qu’aux destinataires autorisés ;
- [ ] aucun classement de conducteur opaque ou comportemental n’est introduit.

## Livrable démontrable

Un responsable compare deux semaines, identifie la cause d’un recul de preuve complète, ouvre les exceptions sources et exporte un rapport daté.

## Risques et rollback

Risque d’indicateurs trompeurs et requêtes coûteuses. Versionner définitions, tester sur références, pré-calculer seulement si mesuré et feature-flagger les agrégats.

## Estimation

L.
