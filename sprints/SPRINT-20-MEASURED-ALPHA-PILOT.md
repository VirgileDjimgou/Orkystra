# SPRINT-20 — Pilote alpha mesuré et décision de niche

## Objectif

Faire utiliser le flux complet par trois organisations pilotes et décider, sur les données d’usage et entretiens, quel segment mérite la vague télématique.

## Valeur et constats traités

Traite le risque majeur « produit trop large sans niche » et l’absence de valeur mesurée. Ce sprint privilégie apprentissage, support et corrections bloquantes à toute nouvelle verticale.

## Tâches principales

- sélectionner jusqu’à trois pilotes de 5–20 véhicules sur deux segments maximum ;
- établir baseline appels de statut, temps de préparation/clôture et taux de preuve ;
- conduire onboarding, formation courte, support et revue hebdomadaire structurée ;
- instrumenter activation, conducteurs actifs, sync, preuves complètes, exceptions et rétention ;
- classer incidents produit/support, corriger P0/P1 et documenter contournements ;
- tester offre 79/149 € et onboarding payant sans construire de billing automatisé ;
- produire décision de niche et backlog fondé sur fréquence × impact × volonté de payer.

## Composants concernés

Produit complet, analytics respectueux des données, support, runbooks et documentation commerciale.

## Dépendances

Gate Sprint 15 validée et Sprints 16–19 prêts pour le scénario choisi.

## Tests et preuves requis

Dry-run onboarding, restauration périodique, sécurité tenant trois organisations, revue d’incidents, cohérence métriques et consentement/information analytics.

### Dry-run technique validé le 18 juillet 2026

- un simulateur modulaire rejoue 33 preuves produit via les API réelles pour Northwind Logistics, Southridge Transport et Westland Field Services ;
- chaque organisation possède des comptes fictifs Admin, Operator et Driver, ainsi que flotte, appareils, télémétrie, mission, preuve, maintenance, conformité, opérations et métriques agrégées ;
- les rôles non Admin reçoivent `403` sur la revue pilote et les ressources d'un autre tenant restent masquées en `404` ;
- les rapports sont explicitement marqués comme preuves de développement simulées et aucune décision de niche n'est créée ;
- ce dry-run ne coche aucun critère d'adoption ou commercial ci-dessous.

## Critères d’acceptation

La gate a été amendée par la décision `D-016` du 22 juillet 2026. Le Product Owner confirme des essais dans plusieurs entreprises, un intérêt convergent et une demande récurrente pour une carte et des espaces Admin/Operator plus interactifs. Ce signal qualitatif autorise la poursuite vers Sprint 21 sans prétendre que les seuils quantitatifs initiaux ont été atteints.

- [x] plusieurs entreprises ont essayé le produit et exprimé un intérêt positif, confirmation Product Owner ;
- [x] le principal retour produit est consolidé : carte temps réel interactive, diagnostic véhicule/chauffeur/appareil et dashboards plus riches ;
- [x] décision `GO` qualitative documentée dans `D-016`, avec tracking fiable et interactif comme investissement suivant ;
- [x] les références visuelles tierces restent des inspirations de principes UX, sans copie de marque, d’assets ni d’interface propriétaire ;
- [ ] trois organisations onboardées sans manipulation manuelle de données — `NON VÉRIFIÉ`, reporté avant bêta ;
- [ ] usage quatre jours par semaine pendant deux semaines — `NON VÉRIFIÉ`, reporté avant bêta ;
- [ ] 95 % des commandes sync et 90 % des missions avec preuve complète — `NON VÉRIFIÉ`, reporté avant bêta ;
- [ ] amélioration opérationnelle mesurée et volonté de payer/LOI — `NON VÉRIFIÉ`, reporté avant bêta.

## Livrable démontrable

Une revue alpha associe métriques, incidents, verbatims résumés et décision d’investissement pour les Sprints 21–25.

## Gate de décision et rollback

La décision `GO` ouvre uniquement Sprint 21. Les connecteurs télématiques Sprint 22 restent conditionnés à une qualité de tracking démontrée et les preuves commerciales quantitatives restent obligatoires avant la bêta. Les données pilotes sont exportables et purgeables selon contrat et rétention.

## Estimation

L.
