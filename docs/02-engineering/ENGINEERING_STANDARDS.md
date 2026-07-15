# Standards d'ingénierie

## Backend

- vertical slices par module ;
- types métier explicites ;
- validation à l'entrée et invariants dans le domaine ;
- annulation via `CancellationToken` ;
- pas de `DateTime.Now`, utiliser UTC/TimeProvider ;
- logs structurés sans données sensibles ;
- migrations et données de seed séparées ;
- pas d'exception utilisée comme flux métier normal.

## Vue

- Composition API et `<script setup lang="ts">` ;
- stores Pinia limités à l'état partagé ;
- appels API centralisés ;
- composants accessibles ;
- états loading/empty/error/success explicites ;
- aucune règle d'autorisation uniquement côté client.

## Android

- Compose et Material 3 ;
- état immutable exposé par `StateFlow` ;
- événements UI vers ViewModel ;
- Repository comme source de vérité ;
- Room pour la file offline ;
- WorkManager pour synchronisation fiable ;
- aucune opération réseau directe dans un Composable.

## Commits

Conventional Commits : `feat:`, `fix:`, `test:`, `docs:`, `refactor:`, `build:`, `chore:`.
