# Sprint actif — SPRINT-33 Repository Truth & Green CI

## État

- Phase : `MVP Consolidation / Public Autonomous Fleet Demo` (`2026.10-mvp-consolidation`).
- `SPRINT-00` à `SPRINT-32` restent `DONE` ; aucun historique n'est réécrit.
- `SPRINT-33` est `READY` : c'est le prochain sprint sélectionné par `Start Next Sprint`.
- `SPRINT-34` à `SPRINT-38` sont `PLANNED`.
- Exécution : `IDLE`, aucun verrou actif, lot résiduel fermé.

## Contrat à lire

`sprints/SPRINT-33-REPOSITORY-TRUTH-GREEN-CI.md` — objectif, périmètre, contraintes, tâches, tests, sécurité, Definition of Done, preuves, conditions d'arrêt et dépendances.

Contexte immédiat : deux pipelines CI se contredisent (`ci.yml` et `release-validation.yml`), le test `Minio` s'exécute sans infrastructure, l'Android CI est incomplet, 6 avis npm sont ouverts (3 High) et plusieurs documents d'état sont obsolètes. SPRINT-33 rétablit une vérité unique et un `main` réellement vert sans affaiblir aucun test.

## Démarrage

`Start Next Sprint` exécute le runner atomique, acquiert le verrou et sélectionne SPRINT-33. Chaque sprint reste atomique : ne pas démarrer SPRINT-34 dans le même contexte.

## Limites actives de la phase

- pilotes virtuels hébergés non provisionnés tant que SPRINT-37 n'est pas terminé ;
- simulation candidate aux téléportations de bouclage tant que SPRINT-34 n'est pas terminé ;
- cockpit non publiable comme Control Tower tant que SPRINT-35 n'est pas terminé ;
- contrat C#/TypeScript/Kotlin non couvert tant que SPRINT-36 n'est pas terminé ;
- auto-hébergement public non validé tant que SPRINT-38 n'est pas terminé.

## Human gates ouverts

- nom commercial final ;
- positionnement commercial principal ;
- fournisseur de tuiles cartographiques ;
- fournisseur d'ingress/tunnel public et domaine HTTPS (SPRINT-38) ;
- politique de remédiation npm High si une montée majeure cassante est nécessaire (SPRINT-33).
