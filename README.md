# FleetOps — Kit de démarrage agentique

FleetOps est un MVP de gestion de flotte conçu pour être développé par une seule personne avec des agents IA dans VS Code, Codex ou OpenCode.

Le dépôt privilégie un **monolithe modulaire**, une interface Web unique fondée sur les rôles, une application Android conducteur et des simulateurs permettant de démontrer l'écosystème sans camions ni boîtiers réels.

> Nom provisoire : remplacez `FleetOps` avant toute commercialisation après vérification juridique et disponibilité du domaine.

## Produit MVP

Le MVP couvre :

- organisations et utilisateurs multi-tenant ;
- rôles Administrateur, Opérateur et Conducteur ;
- véhicules, conducteurs et appareils GPS ;
- ingestion de télémétrie HTTP, puis MQTT si nécessaire ;
- carte interactive et historique simplifié ;
- missions, étapes et états d'exécution ;
- application Android conducteur, utilisable hors ligne ;
- inspections, défauts, photos, signature et preuve de livraison ;
- alertes d'échéance et maintenance légère ;
- API REST, SignalR, webhooks, import/export CSV ;
- simulateur GPS et scénarios de flotte.

Le MVP n'inclut pas un WMS complet, l'optimisation avancée, un tachygraphe propriétaire, la paie, la comptabilité, le matériel GPS propriétaire ou une architecture microservices.

## Stack

- Backend : .NET 10 LTS, C# 14, ASP.NET Core, EF Core, SQL Server.
- Web : Vue 3, TypeScript, Vite, Pinia, Bootstrap 5, Leaflet.
- Android : Kotlin, Jetpack Compose, Material 3, Navigation Compose, Room et WorkManager à partir du sprint mobile.
- Infrastructure locale : Docker Compose, SQL Server, MinIO, Mosquitto, Mailpit.
- Tests : xUnit, tests d'intégration, Vitest, Playwright, tests Compose.

## Démarrage agentique

1. Ouvrir la racine du dépôt dans VS Code, Codex ou OpenCode.
2. Lire `AGENTS.md`, `ROADMAP.md` et `.agent/PROJECT_STATE.json`.
3. Exécuter le prompt `prompts/NEXT_SPRINT.md` ou la commande OpenCode `/next-sprint`.
4. Ne jamais démarrer un sprint tant que la quality gate du sprint précédent n'est pas verte.
5. À la fin d'une session, mettre à jour les fichiers `.agent/*` et créer un checkpoint Git.

## Commandes initiales

### Windows PowerShell

```powershell
Copy-Item .env.example .env
./scripts/dev-up.ps1
./scripts/quality-gate.ps1
```

### Linux/macOS/WSL

```bash
cp .env.example .env
./scripts/dev-up.sh
./scripts/quality-gate.sh
```

## Lancement du démonstrateur minimal

Après installation de .NET 10 et Node.js :

```bash
dotnet run --project apps/backend/FleetOps.Api
npm --prefix apps/web install
npm --prefix apps/web run dev

dotnet run --project simulators/GpsSimulator -- --dry-run
```

Retirez `--dry-run` lorsque l'API fonctionne. Le simulateur enverra des positions à l'endpoint de développement.

## Documentation principale

- `ROADMAP.md` : séquence complète des sprints.
- `sprints/` : cahier des charges de chaque sprint.
- `docs/00-product/` : vision, périmètre et parcours.
- `docs/01-architecture/` : architecture, données et intégrations.
- `docs/02-engineering/` : sécurité, tests, UX et simulation.
- `.agent/` : état courant, handoff, décisions et quality gate.
- `prompts/` : prompts réutilisables.

## Règle fondamentale

Chaque fonctionnalité doit être démontrable par un scénario automatisé ou simulé. Aucune fonctionnalité n'est considérée terminée uniquement parce que le code compile.
