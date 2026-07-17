# SPRINT-29 — Résilience, observabilité et assurance sécurité

## Objectif

Prouver que FleetOps peut être opéré, diagnostiqué et récupéré en Production avec une petite équipe avant toute disponibilité générale.

## Valeur et constats traités

Ferme les limites d’exploitation de l’audit : alerting non prouvé, worker séquentiel, sécurité dynamique absente et recovery seulement ponctuelle. Il ne s’agit pas de microservices, mais de rendre le monolithe observable et récupérable.

## Tâches principales

- définir SLI/SLO : disponibilité API, fraîcheur tracking, sync, outbox, uploads et latence ;
- créer dashboards, alertes avec seuil/action/runbook et tests synthétiques multi-rôle ;
- séparer les boucles worker si nécessaire dans le même déploiement logique et borner leur concurrence ;
- automatiser backup, test de restauration périodique, rotation de secrets et certificat ;
- exécuter threat model, SAST/DAST, dépendances, scan images et test d’autorisation ciblé ;
- tester pannes SQL, stockage objet, fournisseur, réseau mobile et redémarrage process ;
- formaliser incident, communication, rollback de release et post-mortem sans blâme.

## Composants concernés

API, Worker, SQL, stockage, CI/CD, observabilité, sécurité, runbooks et environnements.

## Dépendances

Sprints 11, 16, 22, 25 et 27.

## Tests et preuves requis

Synthetic E2E, chaos ciblé réversible, charge/soak, restore drill, secret rotation, scan dynamique, tenant/roles et exercice d’incident chronométré.

## Critères d’acceptation

- [ ] chaque alerte de release possède propriétaire, seuil, runbook et test ;
- [ ] restauration périodique respecte RPO/RTO et produit une preuve datée ;
- [ ] panne d’une intégration ne bloque pas les missions ni la synchronisation locale ;
- [ ] rotation de secret/clé se fait sans exposition ni indisponibilité non planifiée ;
- [ ] aucune vulnérabilité critique/élevée non acceptée n’est ouverte ;
- [ ] exercice incident aboutit à détection, mitigation et compte rendu dans les objectifs.

## Livrable démontrable

Un game day coupe successivement fournisseur et stockage, redémarre le worker, restaure SQL et montre dashboards, files, runbooks et récupération sans perte métier.

## Risques et rollback

Les tests de panne peuvent affecter des données. Exécuter dans environnement isolé avec cibles résolues, snapshots et arrêts d’urgence ; aucune expérimentation destructive sur pilote actif.

## Estimation

XL.
