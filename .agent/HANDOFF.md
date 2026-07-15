# Handoff agentique

## Contexte actuel

Le dépôt vient d'être généré. Les fichiers de configuration donnent la structure cible, mais le scaffold doit être restauré et compilé dans l'environnement réel du développeur.

## Priorité immédiate

Terminer SPRINT-00 : vérifier .NET, Node, Java/Android SDK, Docker, restaurer les dépendances, lancer les composants et rendre la CI verte.

## Hypothèses à confirmer

- .NET SDK 10 disponible.
- Node.js 22 ou version LTS compatible disponible.
- JDK 21 et Android SDK installés.
- Docker Desktop ou Docker Engine disponible.
- Les versions Android épinglées sont compatibles dans l'environnement local.

## Interdictions

Ne pas commencer l'authentification, le multi-tenant métier ou la télémétrie persistante avant d'avoir un bootstrap totalement vert.
