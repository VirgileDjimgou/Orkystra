# Périmètre MVP

## Inclus

1. Identity et multi-tenancy.
2. Fleet registry : véhicules, conducteurs, appareils.
3. Tracking : télémétrie, position actuelle, historique limité.
4. Dispatch : missions, arrêts, affectation et statuts.
5. Driver Android : missions, statut, offline, photo et signature.
6. Inspections et défauts.
7. Alertes de documents, maintenance et anomalies simples.
8. API, webhooks, CSV et audit.
9. Simulateurs et données de démonstration.

## Exclus

- WMS ;
- paie et comptabilité ;
- facturation transport complexe ;
- optimisation d'itinéraire propriétaire ;
- tachygraphe et décision réglementaire ;
- matériel embarqué propriétaire ;
- vidéo télématique ;
- marketplace ;
- application iOS ;
- portail client complet ;
- microservices.

## Critère MVP mature

Un scénario automatisé doit créer une organisation, des véhicules et conducteurs, lancer trois simulateurs GPS, afficher la flotte, créer une mission, l'exécuter depuis Android hors ligne, synchroniser une preuve et déclencher une alerte/notification observable.
