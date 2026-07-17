# SPRINT-18 — Conformité documentaire et campagnes d’inspection

## Objectif

Donner à l’administrateur une vue fiable des échéances et la capacité de demander une inspection ciblée avant qu’un véhicule ou conducteur ne devienne non conforme.

## Valeur et constats traités

Approfondit la conformité légère existante sans prétendre fournir un conseil réglementaire. La valeur tient à l’anticipation, à la preuve et à la réduction des départs non conformes.

## Tâches principales

- définir types de documents configurables, propriétaire, dates et niveau bloquant ;
- gérer dépôt, validation à quatre yeux optionnelle, expiration et remplacement historisé ;
- calculer échéances 30/14/7 jours et créer des exceptions dédupliquées ;
- lancer campagnes d’inspection par groupe de véhicules, modèle et fenêtre ;
- afficher matrice véhicule/conducteur, couverture et éléments manquants ;
- bloquer une affectation seulement selon politique tenant explicite avec override audité ;
- produire export d’audit et modèles d’information/rétention à faire valider juridiquement.

## Composants concernés

Compliance, Inspections, Fleet, Identity, Dispatch, Alerts, Media et Web.

## Dépendances

Sprints 13, 16 et 17.

## Tests et preuves requis

Dates/fuseaux, déduplication, validation/override, tenant/rôles, campagne offline Android, pièces privées, export et accessibilité de la matrice.

## Critères d’acceptation

- [ ] échéances produisent une seule exception au bon horizon ;
- [ ] document remplacé conserve historique sans rester actif ;
- [ ] campagne atteint les conducteurs offline et remonte son statut ;
- [ ] règle bloquante et override sont configurables et audités ;
- [ ] administrateur voit couverture et risques sans parcourir chaque fiche ;
- [ ] interface indique clairement que la configuration relève du client.

## Livrable démontrable

Une campagne pré-saison cible cinq véhicules, remonte deux défauts, bloque une affectation selon la politique et fournit l’export de preuve.

## Risques et rollback

Risque de présenter une règle générique comme obligation légale. Utiliser des modèles configurables, avertissements et validation professionnelle ; rollback par désactivation de politique bloquante.

## Estimation

L.
