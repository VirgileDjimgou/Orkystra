# ExecPlan — Rebaseline Demo Readiness et autopilot de sprints

## But utilisateur

Établir la vérité du dépôt FleetOps, rebaser la roadmap autour d'une démonstration hébergée map-first avec flotte virtuelle autonome, et rendre l'orchestration prête à exécuter exactement un prochain sprint via `Start Next Sprint`, sans implémenter le Sprint 24 pendant cette intervention.

## Contexte et contraintes

- Préserver le monolithe modulaire ASP.NET Core, Vue 3, Android, SQL Server, SignalR, l'isolation tenant, les médias privés, l'audit et les workflows existants.
- Ne pas réécrire le produit, ne pas élargir le périmètre métier et ne pas affaiblir les contrôles Production.
- Conserver l'historique des Sprints 00–22 et déterminer le statut du Sprint 23 à partir du code, des migrations, des tests et des preuves de validation.
- Archiver clairement les anciens plans non implémentés 24–30 comme superseded, puis créer neuf contrats actifs 24–32.
- Réutiliser et renforcer l'autopilot existant au lieu de créer un mécanisme concurrent.

## État initial vérifié

- Branche `main`, en avance d'un commit sur `origin/main`; aucun changement de travail n'était affiché au début de l'audit.
- `.agent/PROJECT_STATE.json`, `.agent/CURRENT_SPRINT.md` et `ROADMAP.md` déclarent le Sprint 23 terminé.
- Le contrat `sprints/SPRINT-23-RECIPIENT-STATUS.md` contient encore tous ses critères d'acceptation décochés : dérive documentaire à résoudre par preuves.
- Les anciens Sprints 24–30 existent comme plans actifs et doivent être audités puis archivés/superseded.
- L'état des scripts d'orchestration, des commandes OpenCode et des risques temps réel reste à vérifier.

## Périmètre

Inclus : audit complet demandé, décision factuelle Sprint 23, roadmap et contrats 24–32, documentation d'architecture, état machine, verrouillage et commandes d'autopilot, validation syntaxique et cohérence documentaire.

Exclus : toute correction produit prévue au Sprint 24, refonte cockpit, moteur de démo, agents virtuels, déploiement, push ou merge distant.

## Conception

- Une roadmap humaine canonique complétée par un état JSON machine-readable.
- Un runner atomique autoritaire qui sélectionne le plus petit sprint éligible non `DONE`/`SUPERSEDED`, acquiert un verrou local, applique les stop conditions et s'arrête après un seul sprint.
- Un batch runner borné qui répète le runner atomique avec un contexte distinct par itération.
- Des documents d'architecture concis reliés depuis la roadmap et le README, sans duplication.
- Aucun changement de schéma, API ou comportement produit dans ce rebaseline.

## Étapes exécutables

- [ ] Inventorier le dépôt, les instructions, l'historique Git et les preuves de Sprint 23.
- [ ] Auditer docs, sprints, CI/quality gate, compose, Web/map/tracking, backend ingestion, simulation, migrations, tests et orchestration.
- [ ] Documenter la dérive et décider du statut Sprint 23.
- [ ] Archiver les anciens plans 24–30 comme `SUPERSEDED — NOT IMPLEMENTED`.
- [ ] Rédiger ROADMAP et contrats détaillés 24–32.
- [ ] Créer/mettre à jour les documents d'architecture et le README.
- [ ] Implémenter ou renforcer l'autopilot canonique, son état persistant, son verrou, ses commandes et ses règles agent.
- [ ] Mettre à jour CURRENT_SPRINT, HANDOFF, PROJECT_STATE, QUALITY_REPORT et DECISIONS/CHANGELOG si nécessaire.
- [ ] Valider références, numéros uniques, syntaxe, tests légers et diff final.

## Validation

- Analyse automatique des liens/références locales et unicité des sprints actifs.
- Validation JSON et parsing PowerShell/Bash des scripts modifiés.
- Tests légers dédiés à la sélection, au verrou et aux états d'arrêt de l'orchestrateur.
- Formatage/analyse documentaire applicable et inspection `git diff --check`/`git diff`.
- Aucun test produit ne sera revendiqué s'il n'est pas exécuté.

## Risques et rollback

- Risque principal : déclarer Sprint 23 terminé sans preuve suffisante. Retour : le laisser `IMPLEMENTED_NOT_VALIDATED` et le rendre prioritaire.
- Risque de double source de vérité : toute logique de sélection sera centralisée et les commandes ne feront que l'invoquer.
- Risque de déplacer des plans encore utiles : archivage Git traçable, sans suppression, avec table de correspondance.
- Rollback : restaurer les fichiers documentaires/orchestration du commit précédent; aucun état métier ni migration n'est modifié.

## Progression

- [x] 2026-09-26 — fichier d'instructions joint lu intégralement.
- [x] 2026-09-26 — documents obligatoires initiaux et contrat Sprint 23 lus.
- [x] 2026-09-26 — audit factuel complet.
- [x] 2026-09-26 — rebaseline et orchestration mis à jour.
- [x] 2026-09-26 — validation ciblée et rapport final.

## Résultat et dette

Sprint 23 est `DONE` sur la base du commit, migrations, tests et gate du 2026-09-25; la dérive était documentaire. Le nouveau contrat et les neuf sprints sont cohérents, et les scripts ont une compilation/tests/parse ciblés verts. La full quality gate a démarré mais n'a pas rendu de résultat final exploitable après `dotnet format`; elle doit être relancée avant checkpoint. Aucun comportement produit n'a été modifié.
