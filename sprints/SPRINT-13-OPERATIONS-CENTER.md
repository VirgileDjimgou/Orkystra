# SPRINT-13 — Centre d’opérations actionnable

## Objectif

Permettre à l’opérateur de commencer sa journée par les exceptions prioritaires et de les résoudre sans parcourir plusieurs listes.

## Valeur et constats traités

Traite AUD-009, le dashboard redondant, l’absence de recherche/vues enregistrées et la densité des écrans Alertes/Dispatch. La mesure cible est une exception critique comprise, assignée et traitée en trois actions principales maximum.

## Tâches principales

- créer une boîte d’exceptions unifiant alerte, mission, défaut, retard et synchronisation bloquée ;
- grouper les notifications par événement avec sévérité, âge, propriétaire, SLA et prochaine action ;
- ajouter recherche globale tenant-safe, filtres combinables et vues personnelles/en équipe ;
- proposer assignation, acquittement, résolution, snooze motivé et actions en masse sûres ;
- relier chaque exception à la mission, au véhicule, au conducteur et à sa timeline ;
- extraire les grands composants Vue par flux, avec design tokens et états partagés ;
- limiter SignalR aux changements utiles et réconcilier après reconnexion.

## Composants concernés

Alerts, Dispatch, Tracking, Operations, Web, SignalR, audit et tests.

## Dépendances

Sprints 11–12 pour E2E, sessions et matrice d’autorisations.

## Tests et preuves requis

Priorisation déterministe, déduplication, filtres tenant, concurrence d’assignation, reconnexion temps réel, navigation clavier, tablette/mobile et E2E de résolution.

## Critères d’acceptation

- [ ] aucun doublon par canal n’apparaît dans le résumé d’un événement ;
- [ ] une exception critique est assignée et résolue en trois actions principales maximum ;
- [ ] recherche, filtres et vues enregistrées respectent tenant et rôles ;
- [ ] deux opérateurs ne peuvent pas écraser silencieusement leurs actions ;
- [ ] le centre reste utilisable au clavier et sur tablette ;
- [ ] chaque résolution laisse une timeline et une raison auditées.

## Livrable démontrable

Depuis une seule file, un opérateur traite un retard, un défaut critique et une synchronisation bloquée avec mises à jour temps réel et historique complet.

## Observabilité, sécurité et rollback

Mesurer temps de prise en charge, temps de résolution, âge du backlog et taux de réouverture par tenant. Garder les vues historiques en lecture pendant la migration et activer le centre progressivement.

## Estimation

XL.
