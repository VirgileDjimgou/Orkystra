# Instructions permanentes pour les agents

## Mission

Construire FleetOps, un MVP commercial de gestion de flotte pour petites entreprises, sans copier une marque ni reproduire un produit propriétaire. Le dépôt doit rester compréhensible et maintenable par un développeur unique assisté d'agents IA.

## Documents à lire avant toute modification

1. `.agent/PROJECT_STATE.json`
2. `.agent/CURRENT_SPRINT.md`
3. `ROADMAP.md`
4. le fichier du sprint actif dans `sprints/`
5. les instructions spécifiques au répertoire modifié

Ne chargez les autres documents que lorsqu'ils sont nécessaires à la tâche.

## Architecture non négociable

- Monolithe modulaire ASP.NET Core ; pas de microservices sans ADR approuvé.
- Une application Web Vue pour Administrateur et Opérateur, différenciée par rôles.
- Une application Android native dédiée au Conducteur.
- SQL Server comme source transactionnelle principale.
- Stockage objet pour photos et documents.
- SignalR uniquement pour positions, états et alertes réellement temps réel.
- MQTT uniquement pour communication directe avec appareils ou simulateurs.
- Pas de repository générique au-dessus d'EF Core.
- Pas de MediatR/CQRS cérémoniel. Utiliser des vertical slices et services applicatifs simples.
- Les entités tenant-aware portent `OrganizationId`; l'organisation est déterminée depuis l'identité authentifiée, jamais depuis une valeur libre du client.

## Discipline d'implémentation

- Corriger d'abord les régressions existantes.
- Implémenter un seul sprint à la fois.
- Ne pas élargir le périmètre sans enregistrer la décision dans `.agent/DECISIONS.md`.
- Utiliser des migrations de base de données ; ne jamais modifier une base de production manuellement.
- Ne jamais masquer un test défaillant, supprimer une assertion utile ou réduire la couverture pour obtenir du vert.
- Ne jamais committer de secret, token, mot de passe ou donnée personnelle réelle.
- Le code, les noms et les messages techniques sont en anglais ; la documentation produit peut être en français.
- Toute API publique doit être versionnée ou explicitement déclarée interne.
- Toute opération sensible doit être autorisée côté serveur.

## Quality gate obligatoire

Avant de terminer une tâche :

1. formatage et analyse statique ;
2. compilation de tous les composants modifiés ;
3. tests unitaires ;
4. tests d'intégration concernés ;
5. tests Web ou Android concernés ;
6. scénario de démonstration du sprint ;
7. vérification sécurité et multi-tenant ;
8. mise à jour de la documentation et de `.agent/PROJECT_STATE.json`.

Utiliser `scripts/quality-gate.ps1` ou `scripts/quality-gate.sh`.

## Plans d'exécution

Pour une fonctionnalité complexe, une migration, un refactoring transversal ou une tâche dépassant une session courte, créer un ExecPlan conforme à `.agent/PLANS.md` dans `.agent/plans/` et le maintenir pendant l'exécution.

## Fin de session

Mettre à jour :

- `.agent/CURRENT_SPRINT.md` ;
- `.agent/HANDOFF.md` ;
- `.agent/PROJECT_STATE.json` ;
- `.agent/QUALITY_REPORT.md` ;
- `CHANGELOG.md` si une capacité utilisateur a changé.

Créer un checkpoint avec `python scripts/agent/checkpoint.py --summary "..."` après une quality gate verte.

## Commandes naturelles de sprint

Quand l'utilisateur écrit `Start Next Sprint`, appliquer le contrat atomique documenté dans `docs/02-engineering/SPRINT_AUTOPILOT.md` : exécuter `scripts/sprint-runner.ps1 -Action Start`, prendre le verrou, sélectionner exactement le plus petit sprint éligible, lire son contrat complet, n'implémenter que lui, valider, mettre à jour les preuves et libérer le verrou. Arrêter après ce sprint ; ne jamais commencer le suivant dans le même contexte.

Le démarrage doit être visible et factuel : afficher immédiatement le sprint sélectionné, le nombre de sprints restants et le chemin du journal `.runtime/<sprint>-progress.log`; enregistrer chaque jalon avec `scripts/sprint-progress.ps1`; fournir des mises à jour utilisateur pendant l'exécution. Un verrou n'est jamais une preuve qu'un agent travaille en arrière-plan. Si la session s'arrête avant la Definition of Done, exécuter `scripts/sprint-runner.ps1 -Action Pause -Note "..."`, marquer le sprint `PARTIAL/IDLE` et libérer le verrou. Le prochain `Start Next Sprint` reprend ce sprint partiel.

Quand l'utilisateur écrit `Start Next Sprints N`, accepter seulement `1 <= N <= 10`, lancer `scripts/sprint-batch-runner.ps1 -Count N`, puis traiter chaque sprint comme une itération atomique et un contexte agent distinct. Le runner atomique reste l'unique autorité de sélection. S'arrêter dès qu'une gate échoue ou qu'une intervention humaine est nécessaire.

Ne jamais présenter un batch enregistré comme une exécution autonome : sans worker externe réellement lancé et observable, les itérations restent séquentielles et interactives.

L'état machine canonique est `.agent/PROJECT_STATE.json`. Ne pas contourner `.agent/sprint.lock.json`, `STOP`, `.agent/STOP` ou `.agent/HUMAN_REQUIRED.json`. Après trois échecs de réparation du même gate, enregistrer le blocage structuré et demander la décision requise. Ne jamais voler un verrou expiré sans résolution humaine explicite.

## Règles renforcées Demo Readiness

- préserver l'architecture modulaire existante et les contrats tenant-aware ;
- ne pas faire de refactorisation non liée, ni ajouter une capacité spéculative ;
- ne jamais contourner les workflows métier par modification directe de la base ;
- ne jamais affaiblir l'isolation tenant, l'autorisation serveur ou la validation Production pour le mode Demo ;
- ne jamais désactiver/supprimer un test, modifier silencieusement une acceptation, pousser, déployer ou merger automatiquement ;
- préserver les routes utiles et les contrats existants avec une compatibilité documentée ;
- enregistrer les décisions d'architecture dans `.agent/DECISIONS.md` ;
- s'arrêter lorsqu'un jugement humain, une dépendance externe, des credentials ou une action destructive sortent du périmètre autorisé.
