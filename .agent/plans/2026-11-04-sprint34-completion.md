# Sprint 34 completion

## But utilisateur
Clôturer la simulation réaliste avec preuves, gate et commit, puis préparer Sprint 35.
## Contexte et contraintes
Core/Worker existants, ingestion canonique, vingt véhicules, aucun assouplissement qualité.
## État initial vérifié
Routes fermées mais vitesse indépendante du déplacement, calcul par tick, arrêts à des offsets arbitraires. MinIO refusait l'écriture : propriétaire corrigé vers UID 1001 sans suppression.
## Périmètre
Simulation et preuves uniquement ; pas de routage externe ni provisioning des agents hébergés.
## Conception
Échantillonnage analytique par temps logique, distances géodésiques, profil sinusoïdal par segment, dwell aux sommets, snapshots versionnés. Migration explicite par reset canonique du tracking synthétique.
## Étapes exécutables
1. Corriger cinématique et reprise. 2. Aligner anchors et preuves. 3. Harnais vingt véhicules et gate. 4. Clôturer et committer.
## Validation
Tests continuité, cadence variable, vitesse/distance, cinq scénarios, reprise ; ingestion API réelle et harnais observateur avec restart Worker ; gate canonique.
## Risques et rollback
Anciennes positions incompatibles : reset limité au tenant synthétique via contrat interne, aucun SQL direct. Revenir aux commits antérieurs nécessite un nouveau reset synthétique.
## Progression
- [x] Audit et réparation MinIO.
- [x] Cinématique analytique, snapshots v2 et migration via reset canonique ; arrivée liée aux ancrages.
- [x] Preuves : vingt véhicules/3600 événements/3h logiques, 24h de traces sur cinq scénarios, harnais observateur 90s et 60s avec restart réel et zéro rejet.
- [x] Gate complète verte, preuves et documents de clôture réconciliés.
## Résultat et dette
Code et preuves spécifiques terminés. Gate complète verte le 2026-11-04 : 193 tests rapides, 12 Reliability, MinIO, six SQL Server, simulation, charge, Web 32 tests/10 E2E, API et Android. Correctifs compatibles appliqués (Vue 3.5.44, Vite 7.3.7), audit High vert ; 16 avis Moderate d'outillage restent documentés. Prochain sprint 35, aucune implémentation de ce sprint dans ce contexte.
