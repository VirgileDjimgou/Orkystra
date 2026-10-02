# Sprint actif — SPRINT-34 Realistic Fleet Simulation

## État

- Phase : `MVP Consolidation / Public Autonomous Fleet Demo` (`2026.10-mvp-consolidation`).
- `SPRINT-00` à `SPRINT-33` sont `DONE` ; `SPRINT-33` a clôturé la vérité dépôt et la CI unique (`.runtime/sprint33-quality-gate.log`).
- `SPRINT-34` est `PLANNED` : prochain sprint sélectionné par `Start Next Sprint`.
- `SPRINT-35` à `SPRINT-38` restent `PLANNED`.
- Exécution : `IDLE`, aucun verrou actif. Lot `Start Next Sprints 6` en cours à 1/6 ; chaque sprint exige un contexte neuf.

## Contrat à lire

`sprints/SPRINT-34-REALISTIC-FLEET-SIMULATION.md` — objectif, périmètre, contraintes, tâches, tests, sécurité, Definition of Done, preuves, conditions d'arrêt et dépendances.

Contexte immédiat : le moteur Demo produit encore des téléportations en fin de trajectoire (bouclage des polylignes) et des profils de vitesse/cap peu crédibles, ce qui peut déclencher de faux diagnostics de qualité. SPRINT-34 rend la simulation physiquement cohérente, déterministe et compatible avec le moteur de qualité de tracking.

## Démarrage

`Start Next Sprint` exécute le runner atomique, acquiert le verrou et sélectionne SPRINT-34. Ne pas démarrer SPRINT-35 dans le même contexte.

## Limites actives de la phase

- pilotes virtuels hébergés non provisionnés tant que SPRINT-37 n'est pas terminé ;
- cockpit non publiable comme Control Tower tant que SPRINT-35 n'est pas terminé ;
- contrat C#/TypeScript/Kotlin non couvert tant que SPRINT-36 n'est pas terminé ;
- auto-hébergement public non validé tant que SPRINT-38 n'est pas terminé.

## Human gates ouverts

- nom commercial final ;
- positionnement commercial principal ;
- fournisseur de tuiles cartographiques ;
- fournisseur d'ingress/tunnel public et domaine HTTPS (SPRINT-38).
