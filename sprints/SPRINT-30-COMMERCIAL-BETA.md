# SPRINT-30 — Bêta commerciale et décision de disponibilité générale

## Objectif

Valider que le produit, l’onboarding, le support et l’offre économique peuvent servir une petite cohorte payante avec une qualité maîtrisée.

## Valeur et constats traités

Transforme les preuves techniques et alpha en décision commerciale. Le sprint ne déclare pas automatiquement FleetOps « production-ready » : il exige une cohorte, une rétention et une volonté de payer mesurées.

## Tâches principales

- sélectionner cinq à dix organisations dans la niche décidée au Sprint 20 ;
- figer offres, limites d’usage, onboarding payant et processus manuel de souscription ;
- publier documentation client, SLA pilote, sécurité, DPA/rétention à validation juridique ;
- instrumenter acquisition→activation→usage→support→renouvellement sans analytics intrusif ;
- opérer support, releases progressives, statut service et revues d’incident ;
- comparer coût infrastructure/support, revenu, marge cible et capacité solo/petite équipe ;
- établir backlog post-bêta et décision `GO`, `SIMPLIFY`, `PIVOT` ou `STOP`.

## Composants concernés

Écosystème complet, opérations commerciales, support, conformité, observabilité et documentation.

## Dépendances

Gate Sprint 25 validée et assurance Production Sprint 29 passée.

## Tests et preuves requis

Release candidate complète, sécurité/tenant, charge, recovery, onboarding de cohorte, facturation manuelle contrôlée, export/purge et exercice de départ client.

## Critères d’acceptation

- [ ] cinq organisations minimum terminent l’onboarding selon le processus publié ;
- [ ] au moins trois sont actives chaque semaine pendant huit semaines ;
- [ ] au moins deux paient au prix testé sans développement spécifique bloquant ;
- [ ] objectifs sync, preuve, disponibilité et support sont tenus ou écarts acceptés ;
- [ ] coût de support et infrastructure est connu par tenant/véhicule ;
- [ ] décision finale, niche, offre et risques résiduels sont signés et documentés.

## Livrable démontrable

Une revue de bêta présente funnel, usage, fiabilité, incidents, revenus/coûts, retours clients et décision de disponibilité générale avec conditions.

## Gate finale et rollback

`GO` exige preuves techniques et commerciales. `SIMPLIFY` réduit le périmètre au flux le plus utilisé ; `PIVOT` conserve les briques validées ; `STOP` déclenche export, rétention contractuelle et fermeture propre des pilotes.

## Estimation

XL.
