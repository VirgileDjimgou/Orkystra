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

## Critères d’acceptation

- [ ] trois organisations sont onboardées sans manipulation manuelle de données ;
- [ ] au moins deux utilisent le flux quatre jours par semaine pendant deux semaines ;
- [ ] au moins 95 % des commandes sync et 90 % des missions clôturées ont une preuve complète ;
- [ ] appels de statut ou temps de clôture diminuent de façon mesurée chez au moins un pilote ;
- [ ] un pilote accepte de payer ou signe une lettre d’intention qualifiée ;
- [ ] décision `GO`, `SIMPLIFY`, `PIVOT` ou `STOP` et niche principale sont documentées.

## Livrable démontrable

Une revue alpha associe métriques, incidents, verbatims résumés et décision d’investissement pour les Sprints 21–25.

## Gate de décision et rollback

Sans deux pilotes actifs, ne pas ouvrir les connecteurs télématiques : corriger onboarding/flux ou simplifier. Les données pilotes sont exportables et purgeables selon contrat et rétention.

## Estimation

L.
