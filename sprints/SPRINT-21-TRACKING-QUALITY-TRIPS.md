# SPRINT-21 — Qualité du tracking, trajets et zones métier

## Objectif

Faire de la position une donnée de confiance exploitable : fraîcheur visible, anomalies explicables, trajets consultables et zones métier configurables.

## Valeur et constats traités

Répond au besoin d’un écosystème télématique crédible et au manque de tests SignalR/reconnexion/charge. La valeur n’est pas d’accumuler des points, mais de distinguer véhicule réellement arrêté, appareil muet et donnée douteuse.

## Tâches principales

- calculer fraîcheur, précision, source, séquence et score de qualité par appareil ;
- détecter trous, sauts impossibles, doublons et horodatages incohérents sans réécrire la donnée brute ;
- reconstruire trajets/arrêts simples avec version d’algorithme et recalcul borné ;
- créer géofences circulaires/polygones et événements entrée/sortie dédupliqués ;
- afficher diagnostic de tracking, dernière communication et raison d’état ;
- renforcer reconnexion SignalR, snapshot de rattrapage et limitation des fréquences UI ;
- établir baseline de charge sur flotte cible et politique de conservation des points.

## Composants concernés

Tracking, Devices, Alerts, Worker, SignalR, Web Map, SQL et tests de performance.

## Dépendances

Niche validée Sprint 20 et preuves SQL Sprint 11.

## Tests et preuves requis

Horodatage/fuseaux, ordre/duplication, géométrie, tenant, recalcul, reconnexion navigateur, charge 30 puis 100 véhicules et exactitude sur traces connues.

## Critères d’acceptation

- [ ] l’opérateur distingue position fraîche, retardée, invalide et appareil silencieux ;
- [ ] données anormales restent auditables et n’altèrent pas la dernière position fiable ;
- [ ] trajets/arrêts sont reproductibles pour une même version ;
- [ ] événements de zone ne sont pas doublés lors d’une réémission ;
- [ ] reconnexion récupère l’état sans trou visible ni tempête d’événements ;
- [ ] charge cible respecte budgets documentés de latence et ressources.

## Livrable démontrable

Une trace comportant doublons, saut GPS et coupure est ingérée ; l’interface explique la qualité, reconstruit le trajet et déclenche une seule entrée de zone.

## Risques et rollback

Les trajets sont des dérivés, pas une vérité réglementaire. Versionner le calcul, conserver le brut selon rétention et permettre recalcul/disable par tenant.

## Estimation

XL.
