# Handoff agentique

## Contexte actuel

Le dépôt a été restauré et validé localement pour Docker, backend .NET, EF Core, Web, simulateur GPS et Android. Le flux démo jusqu'à la carte a été vérifié sur cette machine.

## Priorité immédiate

Préparer SPRINT-01 à partir d'un SPRINT-00 clôturé et d'une quality gate verte.

## Hypothèses à confirmer

- Le wrapper Gradle Android est versionné dans `apps/android-driver`.
- La quality gate PowerShell force le JBR Android Studio local quand il est disponible.
- Le SDK Android local est détecté depuis `%LOCALAPPDATA%\Android\Sdk`.

## Interdictions

Ne pas commencer l'authentification, le multi-tenant métier ou la télémétrie persistante sans ouvrir explicitement SPRINT-01.
