# ExecPlans

Un ExecPlan est obligatoire pour toute tâche transversale, risquée, supérieure à une session courte ou touchant plusieurs applications.

Créer le fichier `.agent/plans/YYYY-MM-DD-titre.md` avec les sections suivantes et le maintenir jusqu'à la fin :

1. **But utilisateur** — résultat observable.
2. **Contexte et contraintes** — modules, règles et décisions concernées.
3. **État initial vérifié** — builds, tests et défauts existants.
4. **Périmètre** — inclus et explicitement exclu.
5. **Conception** — données, API, sécurité, UX et migrations.
6. **Étapes exécutables** — petites étapes ordonnées.
7. **Validation** — commandes, tests et scénario démontrable.
8. **Risques et rollback** — échecs possibles et retour arrière.
9. **Progression** — checklist datée mise à jour pendant l'exécution.
10. **Résultat et dette** — preuves, limites et suivi.

Le plan doit être autonome : un autre agent doit pouvoir poursuivre uniquement avec le dépôt et ce document.
