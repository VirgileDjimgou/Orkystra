# Aucun sprint actif

## État

`SPRINT-32` est terminé et sa quality gate est verte le 2026-09-30 (`PASSED`, `.runtime/sprint32-quality-gate.log`). Tous les sprints actifs `SPRINT-00` à `SPRINT-32` sont `DONE`.

## Lots

Le lot `Start Next Sprints 5` demandé pendant la session est enregistré à 1/5 complété dans `.agent/PROJECT_STATE.json` (`execution.batch`). Comme il ne reste aucun sprint éligible, le lot ne peut pas se compléter : un prochain `Start Next Sprints N` sera refusé par `batch-start` ("A batch is already recorded"). La clôture de ce lot résiduel est une décision humaine (aucune commande du runner ne ferme un lot sans sprint verrouillé).

## Reprise

Aucun sprint à reprendre. Avant toute nouvelle capacité :

- choisir le nom commercial final ;
- décider du fournisseur de tuiles cartographiques pour un hébergement public ;
- autoriser explicitement tout déploiement/publication ;
- si un nouveau lot est nécessaire, fermer d'abord le lot 1/5 résiduel.

## Limites actives

- pilotes virtuels non provisionnés dans le profil Demo hébergé (activité prouvée par la simulation de développement et le parcours Playwright) ;
- charge 50 véhicules non testée (moteur Demo borné à 20) ;
- stores de session Demo et de compteurs process-locaux (mono-instance) ;
- mesures de fiabilité sur environnement de développement isolé uniquement (`docs/02-engineering/RELIABILITY_REPORT.md`).
