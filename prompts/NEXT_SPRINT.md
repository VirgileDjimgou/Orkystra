# Prompt simple — sprint suivant

Continue le développement de FleetOps.

1. Lis `AGENTS.md`, `.agent/PROJECT_STATE.json`, `.agent/CURRENT_SPRINT.md`, `ROADMAP.md` et le fichier du sprint actif.
2. Audite l'état réel du dépôt : Git, builds, migrations, dépendances, tests, sécurité, multi-tenant et scénario de démonstration.
3. Répare d'abord tout défaut ou sprint précédent incomplet. Ne contourne aucun test.
4. Si le sprint actif n'est pas terminé, implémente uniquement ses éléments manquants. S'il est entièrement terminé et validé, sélectionne le premier sprint `NOT_STARTED`.
5. Pour une tâche complexe, crée et maintiens un ExecPlan conforme à `.agent/PLANS.md`.
6. Implémente par petites étapes cohérentes, avec tests ajoutés en même temps que le code.
7. Exécute la quality gate complète et le scénario de simulation/E2E du sprint.
8. Effectue une revue finale : erreurs, cas limites, autorisations, isolation tenant, concurrence, offline, observabilité, UX et documentation.
9. Mets à jour `.agent/PROJECT_STATE.json`, `.agent/CURRENT_SPRINT.md`, `.agent/HANDOFF.md`, `.agent/QUALITY_REPORT.md`, `CHANGELOG.md` et les décisions si nécessaire.
10. Crée un checkpoint Git uniquement si la quality gate est verte.

À la fin, fournis : ce qui fonctionne, les preuves de tests, les fichiers principaux modifiés, les limites restantes et la prochaine action exacte.
