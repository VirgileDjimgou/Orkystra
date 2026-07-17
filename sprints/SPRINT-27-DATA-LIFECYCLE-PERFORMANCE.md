# SPRINT-27 — Cycle de vie des données et performance cible

## Objectif

Maintenir temps de réponse, coûts et obligations de rétention quand les positions, preuves, audits et événements s’accumulent.

## Valeur et constats traités

Traite listes sans pagination, lifecycle chargé en mémoire, rétention télémétrie partielle et risque de base unique. La cible reste une petite flotte, avec marge démontrée jusqu’à 100 véhicules par tenant.

## Tâches principales

- inventorier volumes, croissance, classification et propriétaire de chaque donnée ;
- définir rétention configurable bornée pour télémétrie, audit, médias, logs et exports ;
- implémenter purge batchée, reprise par curseur, dry-run et journal de résultat ;
- ajouter pagination curseur et limites sur toutes les listes volumineuses ;
- profiler index/requêtes N+1, endpoints lourds, worker et agrégats reporting ;
- archiver/exporter selon politique sans casser preuves ni obligations de conservation ;
- exécuter tests de charge/soak, taille base, restauration et objectifs RPO/RTO.

## Composants concernés

SQL Server, Infrastructure, Tracking, Audit, Media, Worker, API, scripts et observabilité.

## Dépendances

Modèles fonctionnels stables Sprints 16–25.

## Tests et preuves requis

Migration, purge/reprise, conservation légale simulée, tenant, curseurs, concurrence, plans SQL, charge 30/100 véhicules, soak et recovery sur volume représentatif.

## Critères d’acceptation

- [ ] aucune purge volumique ne charge l’ensemble d’un tenant en mémoire ;
- [ ] dry-run et exécution produisent comptes vérifiables et reprise sûre ;
- [ ] pagination reste stable sous insertions concurrentes ;
- [ ] budgets p95 API, worker lag, taille et coût sont documentés et tenus ;
- [ ] données sous conservation ne sont pas supprimées ;
- [ ] backup/restore respecte RPO/RTO cible sur le volume de référence.

## Livrable démontrable

Un dataset de plusieurs mois est purgé/archivé par politique, la carte et les rapports restent rapides et la restauration respecte les objectifs publiés.

## Risques et rollback

La suppression est irréversible. Utiliser dry-run, délais, lots, conservation, sauvegarde vérifiée et feature flag ; aucune suppression large sans cible tenant et bornes résolues.

## Estimation

XL.
