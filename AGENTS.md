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
