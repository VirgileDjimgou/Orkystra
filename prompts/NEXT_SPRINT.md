# Prompt — sprint suivant atomique

Continue le développement de FleetOps.

1. Exécute `scripts/sprint-runner.ps1 -Action Start -Owner <agent-tool>` et arrête-toi si le verrou, l'état ou une stop condition refuse le démarrage.
2. Lis `AGENTS.md`, `.agent/PROJECT_STATE.json`, `.agent/CURRENT_SPRINT.md`, `ROADMAP.md`, `docs/02-engineering/SPRINT_AUTOPILOT.md` et le contrat du sprint sélectionné.
2. Audite l'état réel du dépôt : Git, builds, migrations, dépendances, tests, sécurité, multi-tenant et scénario de démonstration.
3. Répare d'abord tout défaut ou sprint précédent incomplet. Ne contourne aucun test.
4. Implémente uniquement le sprint sélectionné par le runner ; ne sélectionne jamais un second sprint à la main.
5. Pour une tâche complexe, crée et maintiens un ExecPlan conforme à `.agent/PLANS.md`.
6. Implémente par petites étapes cohérentes, avec tests ajoutés en même temps que le code.
7. Exécute la quality gate complète et le scénario de simulation/E2E du sprint, puis exécute `scripts/sprint-runner.ps1 -Action Validate`.
8. Effectue une revue finale : erreurs, cas limites, autorisations, isolation tenant, concurrence, offline, observabilité, UX et documentation.
9. Mets à jour `.agent/PROJECT_STATE.json`, `.agent/CURRENT_SPRINT.md`, `.agent/HANDOFF.md`, `.agent/QUALITY_REPORT.md`, `CHANGELOG.md` et les décisions si nécessaire.
10. Crée un checkpoint Git uniquement si la quality gate est verte, puis exécute `scripts/sprint-runner.ps1 -Action Complete -Gate <evidence> -Checkpoint <summary>` pour libérer le verrou.

Après un échec, exécute `-Action Fail`; au troisième essai, respecter `HUMAN_REQUIRED.json`. À la fin, fournis : ce qui fonctionne, les preuves de tests, les fichiers principaux modifiés, les limites restantes et la prochaine action exacte. STOP après le sprint.
