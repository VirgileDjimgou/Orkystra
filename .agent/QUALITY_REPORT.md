# Rapport de qualité

## Dernière exécution

- Statut : `PARTIAL`
- Date UTC : 2026-07-15
- Sprint : SPRINT-00

## Contrôles

| Contrôle | Statut | Notes |
|---|---|---|
| Build backend | BLOCKED | SDK .NET absent dans l’environnement de génération |
| Tests backend | BLOCKED | Dépend du build backend |
| Installation Web | PASSED | `npm ci`, audit sans vulnérabilité connue |
| Lint Web | PASSED | ESLint sans erreur bloquante |
| Tests Web | PASSED | 1 fichier, 1 test Vitest |
| Build Web | PASSED | `vue-tsc --noEmit` et Vite production |
| Build Android | BLOCKED | Gradle/Android SDK et wrapper absents |
| Docker Compose | BLOCKED | Docker absent |
| Simulateur source | REVIEWED | Exécution .NET à valider sur la machine cible |
| JSON/configuration | PASSED | Validation syntaxique locale |

## Action suivante

Terminer SPRINT-00 sur la machine du développeur et remplacer ce rapport par une quality gate complète.
