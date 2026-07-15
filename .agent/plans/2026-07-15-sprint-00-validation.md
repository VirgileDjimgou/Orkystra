## 1. But utilisateur

Rendre le Sprint 00 réellement exécutable sur cette machine Windows : environnement local initialisé, composants principaux validés autant que l'outillage le permet, documentation alignée sur la réalité et quality gate fiable.

## 2. Contexte et contraintes

- Dépôt FleetOps avec renommage commercial progressif vers Zynro.
- Sprint actif unique : `SPRINT-00`.
- Architecture imposée : monolithe modulaire ASP.NET Core, Web Vue, Android natif, SQL Server, SignalR pour le temps réel.
- Aucune avance sur le Sprint 01.
- Quality gate obligatoire via `scripts/quality-gate.ps1` ou `scripts/quality-gate.sh`.
- Environnement observé : Windows, .NET 10, Node 24, Docker présent, Android Studio/JBR 21 et SDK Android local disponibles; `gradle` global absent mais wrapper versionné.

## 3. État initial vérifié

- Dépôt Git déjà initialisé sur `master`.
- Arbre local sale avec `?.vscode/PythonImportHelper-v2-Completion.json`.
- `.env` déjà présent à la racine.
- `package.json` racine absent malgré la consigne du brief joint.
- Docker Compose et les composants applicatifs non encore validés sur cette machine.
- Android initialement incomplètement bootstrapé : wrapper absent et JDK global incompatible, corrigés par wrapper versionné et JBR Android Studio.

## 4. Périmètre

Inclus :
- audit d'environnement et outillage ;
- correction des scripts et configurations Sprint 00 ;
- validation Docker, backend, web, simulateur GPS et Android si possible ;
- mise à jour de la documentation et des fichiers `.agent`.

Exclus :
- démarrage du Sprint 01 ;
- renommage technique massif des namespaces/projets/dossiers ;
- extension fonctionnelle hors Sprint 00.

## 5. Conception

- Corriger en priorité les défauts de reproductibilité et les écarts entre documentation, scripts et dépôt réel.
- Préserver les identifiants techniques existants, ne faire que le renommage commercial visible et sûr demandé pour Sprint 00.
- Documenter explicitement les blocages d'outillage non résolus si l'installation automatique n'est pas fiable ou non disponible.

## 6. Étapes exécutables

1. Terminer l'audit documentaire et technique.
2. Lire les instructions spécifiques backend/web/android avant toute modification dans ces répertoires.
3. Exécuter les validations existantes pour faire émerger les échecs réels.
4. Corriger scripts, configuration, code et tests au minimum nécessaire pour Sprint 00.
5. Rejouer les validations par composant puis la quality gate globale.
6. Mettre à jour documentation, état agent et checkpoint si tout passe.

## 7. Validation

- `docker compose --env-file .env config`
- `docker compose --env-file .env up -d`
- `dotnet restore/build/test FleetOps.slnx`
- validations `npm` dans `apps/web`
- validations Gradle Android si wrapper/outillage disponibles
- `dotnet run --project simulators/GpsSimulator -- --dry-run`
- quality gate PowerShell depuis la racine

## 8. Risques et rollback

- Android peut rester bloqué faute de SDK/outils compatibles.
- Des versions trop récentes de Node ou Java peuvent révéler des incompatibilités de scaffold.
- Docker SQL Server peut échouer selon les contraintes machine/ports/volumes.
- Rollback : conserver des changements ciblés, sans réécriture d'historique, et ne pas toucher aux fichiers utilisateur non liés.

## 9. Progression

- [x] 2026-07-15 Audit initial des consignes et de l'état du dépôt.
- [x] 2026-07-15 Vérification du contexte Git, OS et outillage principal.
- [x] 2026-07-15 Lire les instructions spécifiques backend/web/android.
- [x] 2026-07-15 Exécuter les validations de base par composant.
- [x] 2026-07-15 Corriger les défauts identifiés sur .NET, Web, Docker, EF et branding visible.
- [x] 2026-07-15 Exécuter la quality gate jusqu'au blocage Android résiduel.
- [x] 2026-07-15 Mettre à jour la documentation et les fichiers `.agent`.
- [x] 2026-07-15 Générer et versionner le wrapper Gradle Android.
- [x] 2026-07-15 Valider Android avec `testDebugUnitTest assembleDebug`.
- [x] 2026-07-15 Rejouer la quality gate complète au vert.
- [ ] Créer un checkpoint et un commit si tout est vert.

## 10. Résultat et dette

- Backend .NET, Web, Docker, EF Core, worker, simulateur et Android validés localement.
- Le flux de démonstration `API -> simulateur GPS -> SignalR -> carte` a été vérifié dans le navigateur intégré.
- Dette restante principale: couverture automatisée encore volontairement minimale pour un bootstrap Sprint 00.
- Le checkpoint et le commit restent à exécuter après la mise à jour documentaire finale.
