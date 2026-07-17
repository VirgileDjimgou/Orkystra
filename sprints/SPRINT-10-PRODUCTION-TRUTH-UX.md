# SPRINT-10 — Vérité Production et socle UX

## Objectif

Supprimer les risques bloquant un pilote réel et rendre les expériences Web et Android plus claires, accessibles et cohérentes sans modifier le modèle métier.

## Problèmes résolus

- clés JWT et média de développement acceptées en Production ;
- comptes de démonstration créés et affichés sans opt-in ;
- login sans rate limit ni lockout ;
- scripts PowerShell de recovery invalides ;
- navigation Web peu responsive et client Android encore démonstratif.

## Tâches principales

- validation fail-fast des secrets et de la connexion en Production ;
- bootstrap séparant migrations, rôles et données de démonstration ;
- rate limiting et lockout Identity sur le login ;
- mode démo Web explicite ;
- parsing des scripts recovery dans la quality gate ;
- shell Web responsive, navigation groupée et accessibilité de base ;
- thème Android, synchronisation visible et packaging sans avertissements simples ;
- tests backend/Web/Android et documentation.

## Composants concernés

API, Infrastructure, Web, Android, scripts, compose et documentation.

## Dépendances

Aucune migration. Les secrets Production deviennent obligatoires.

## Tests requis

Configuration Production, lockout/rate limit, seed opt-in, scripts parsables, tests composants Web, lint/build Android et quality gate complète.

## Critères d’acceptation

- [x] une configuration Production faible refuse de démarrer ;
- [x] aucune donnée de démonstration n’est créée sans opt-in ;
- [x] les échecs répétés de login sont limités et verrouillés ;
- [x] les scripts PowerShell sont parsables ;
- [x] les shells Web et Android sont accessibles et responsive ;
- [x] la quality gate complète est verte.

## Livrable démontrable

Un environnement de développement conserve sa démo explicitement, tandis qu’un lancement Production incomplet échoue avec un message exploitable. Admin, Operator et Driver disposent d’une hiérarchie d’interface plus claire.

## Risques et rollback

Le fail-fast peut révéler des environnements mal configurés ; rollback par variables explicites, jamais par réintroduction de secrets connus. Les styles restent découplés des contrats métier.

## Estimation

L.
